using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DJMaxEditor.Controls.TimelineV2;
using DJMaxEditor.Controls.TimelineV2.Renderers;
using DJMaxEditor.UI;

namespace DJMaxEditor.Preview
{
    /// <summary>
    /// The TECHNIKA playfield, drawn at the arcade's own proportions in GDI+. A port of
    /// the reference project's WPF TechnikaPlayfieldView: the screen is split into two
    /// halves (344 px above, 352 below), a scan sweeps the upper half left to right and
    /// the next the lower half right to left, and a note's horizontal position is its
    /// position in time within the current scan.
    ///
    /// <para>The reference splits chrome and field into two retained layers because WPF
    /// redraws whatever a layer holds; the GDI+ equivalent is a cached chrome bitmap - the
    /// backdrop, halves, rails, lane rules, divider and header plate change only with the
    /// size or the lane count, so painting them sixty times a second to move one scanline
    /// is cost the port does not pay either.</para>
    /// </summary>
    internal sealed class TechnikaPlayfieldRenderer
    {
        /// <summary>Opacity of a note that belongs to the next scan and has not been handed over yet.</summary>
        private const double PrepareOpacity = 0.6;

        /// <summary>Phase at which the next scan's notes go active and its scanline appears.</summary>
        private const double HandoverPhase = 0.875;

        private const int NoFamily = 0;
        private const int ChainFamily = 1;
        private const int RepeatFamily = 2;

        private const int NoRun = -1;
        private const int AnyLane = -1;

        /// <summary>Height of a countdown digit as a share of one half of the playfield.</summary>
        private const double CountdownHeightShare = 0.42;

        /// <summary>
        /// The arcade's chain-head art points the opposite way to its chain: <c>notepressstart</c>
        /// is a left-pointing arrow, so a head aimed along the run has the frame turned through
        /// half a turn before it is drawn. Measured off the sprite rather than chosen - see the
        /// reference project's TechnikaPlayfieldView.
        /// </summary>
        private const double HeadArtDegrees = 180.0;

        private readonly TechnikaPlayfieldTheme _theme = TechnikaPlayfieldTheme.Default;
        private TechnikaNoteSprites _sprites = TechnikaNoteSprites.Load();

        /// <summary>The id of the style currently driving <see cref="_sprites"/>, so a
        /// re-applied style is recognised and skipped instead of reloading every sheet.
        /// Null until the first <see cref="SetSpriteStyle"/>: the field initializer above
        /// already loaded the AUTO probe, which is what an unresolved style resolves to.</summary>
        private string _spriteStyleId;

        private Bitmap _chrome;
        private int _chromeWidth;
        private int _chromeHeight;
        private int _chromeLaneCount = -1;
        private bool _chromeShowBga;

        private double[] _headAngles = new double[0];
        private int[] _runOf = new int[0];
        private OpenRun[] _openRuns = new OpenRun[8];
        private int _openRunCount;
        private int _runCount;

        private Font _countdownFont;
        private double _countdownEm = -1.0;

        private TechnikaNoteFader _noteFader = TechnikaNoteFader.Off;
        private TechnikaLineEffector _lineEffector = TechnikaLineEffector.On;

        private GameplayPreviewProjection _projection;
        private GameplayPreviewFrame _frame;
        private TechnikaPlayfieldFit _fit;

        /// <summary>
        /// The note-series effector (fade in / fade out). Renderer state only: the arcade
        /// computes visibility from where the sweep is, which this renderer knows per note,
        /// so this never rebuilds the projection the way the scroll direction does.
        /// </summary>
        public TechnikaNoteFader NoteFader
        {
            get { return _noteFader; }
            set { _noteFader = value; }
        }

        /// <summary>
        /// The timeline-series effector (blink / blind). Flashing is driven from the frame's
        /// phase, so changing this only changes how the next paint draws the scanlines.
        /// </summary>
        public TechnikaLineEffector LineEffector
        {
            get { return _lineEffector; }
            set { _lineEffector = value; }
        }

        /// <summary>The style's name for the panel header (<c>T3 STAR #03</c>), or "ARCADE
        /// SPRITES" / "PACKAGED GLYPHS" for the probe-driven and packaged sources.</summary>
        public string SpriteSourceLabel
        {
            get { return _sprites.SourceLabel; }
        }

        /// <summary>
        /// Switches the note set to a catalog style. Rebuilds the sprite cache when the style
        /// differs from the current one and reports whether it did, so the caller knows to
        /// repaint and refresh its source label. The replaced instance is dropped without
        /// disposal on purpose: its clone frames are owned per instance and collected with
        /// it, and the packaged sheets are shared cached bitmaps that must never be disposed.
        /// </summary>
        public bool SetSpriteStyle(TechnikaSpriteStyle style)
        {
            return SetSpriteStyle(style, TechnikaNoteSprites.ForStyle(style));
        }

        /// <summary>Installs an already-built (and pre-warmed) sprite set, e.g. one decoded on
        /// a loader thread so the UI thread never pays the decode. The unchanged-id guard is
        /// the same as the style-only overload: re-selecting the current set is a no-op.</summary>
        public bool SetSpriteStyle(TechnikaSpriteStyle style, TechnikaNoteSprites sprites)
        {
            if (style == null || sprites == null ||
                (_spriteStyleId != null &&
                    string.Equals(_spriteStyleId, style.Id, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
            _spriteStyleId = style.Id;
            _sprites = sprites;
            return true;
        }

        /// <summary>
        /// Re-probes for a local sprite folder (an owner may have dropped one in after the
        /// editor started). Returns true when the resolved set changed, so the caller knows
        /// to repaint and refresh its source label. Only probe-driven instances refresh: a
        /// style with a pinned root reports no change, because its root is the point.
        /// </summary>
        public bool ReloadSprites()
        {
            return _sprites.RefreshLocalRoot();
        }

        private double PulsesPerScan
        {
            get { return 240.0 * (_projection == null ? 4.0 : _projection.BeatsPerScan); }
        }

        /// <summary>
        /// Shine loops per scan - one per beat, so this equals the chart's beats per scan (4 on
        /// standard charts, 2 on half-scan charts). Frame animation runs off this musical phase,
        /// never a wall clock, so a stopped transport freezes the animation too.
        /// </summary>
        private double ShineLoopsPerScan
        {
            get { return _projection.BeatsPerScan; }
        }

        public bool HasPlayfield
        {
            get
            {
                return _projection != null &&
                    _projection.Profile == GameplayPreviewProfile.Technika;
            }
        }

        /// <summary>
        /// Paints one frame: cached chrome, then the per-tick field (scanlines, run links,
        /// notes, hit bursts, count-in) in that order, so a burst reads as light in front
        /// of the field and the count sits over everything.
        /// </summary>
        public void Paint(
            Graphics graphics,
            RectangleF viewport,
            GameplayPreviewProjection projection,
            GameplayPreviewFrame frame,
            double noteZoom)
        {
            Paint(graphics, viewport, projection, frame, noteZoom, null, false);
        }

        /// <param name="bgaFrame">Decoded BGA frame, drawn under notes when <paramref name="showBga"/> is set.</param>
        public void Paint(
            Graphics graphics,
            RectangleF viewport,
            GameplayPreviewProjection projection,
            GameplayPreviewFrame frame,
            double noteZoom,
            Image bgaFrame,
            bool showBga)
        {
            _projection = projection;
            _frame = frame;
            _fit = TechnikaPlayfieldMetrics.Fit(viewport.Width, viewport.Height);

            if (!_fit.IsUsable)
            {
                return;
            }

            bool paintBga = showBga && bgaFrame != null;
            EnsureChrome(viewport, paintBga);

            // _fit is relative to the viewport's top-left. Chrome is painted in local
            // bitmap space; notes/scanlines share that space after TranslateTransform so
            // lane rules and note centres land on the same pixels.
            GraphicsState world = graphics.Save();
            try
            {
                graphics.TranslateTransform(viewport.X, viewport.Y);

                // BGA under the chrome: playfield halves in the chrome bitmap stay
                // transparent when a video is showing, so rails / rules / header sit on top.
                if (paintBga)
                {
                    DrawBgaBackdrop(graphics, bgaFrame);
                }
                graphics.DrawImage(
                    _chrome,
                    new RectangleF(0, 0, viewport.Width, viewport.Height));

                if (projection == null || frame == null || !HasPlayfield)
                {
                    return;
                }

                DrawScanlines(graphics);

                IReadOnlyList<ProjectedGameplayNote> notes = frame.Notes;

                // Links first, and not only so the lines pass under the heads: the walk that
                // finds the runs is also what tells a chain head which way its arrow points.
                DrawGroupLinks(graphics, notes);
                for (int i = 0; i < notes.Count; i++)
                {
                    DrawNote(graphics, notes[i], _headAngles[i], noteZoom);
                }

                DrawHitEffects(graphics, notes);
                DrawCountdown(graphics);
            }
            finally
            {
                graphics.Restore(world);
            }
        }

        // -----------------------------------------------------------------------------------
        // Chrome
        // -----------------------------------------------------------------------------------

        private void EnsureChrome(RectangleF viewport, bool showBga)
        {
            int laneCount = _projection == null ? 0 : _projection.LaneCount;
            int width = Math.Max(1, (int)Math.Ceiling(viewport.Width));
            int height = Math.Max(1, (int)Math.Ceiling(viewport.Height));

            if (_chrome != null &&
                _chromeWidth == width && _chromeHeight == height &&
                _chromeLaneCount == laneCount &&
                _chromeShowBga == showBga)
            {
                return;
            }

            DisposeChrome();
            _chrome = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            _chromeWidth = width;
            _chromeHeight = height;
            _chromeLaneCount = laneCount;
            _chromeShowBga = showBga;

            using (Graphics g = Graphics.FromImage(_chrome))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                // Local bitmap space: (0,0) is the viewport's top-left after TranslateTransform.
                g.Clear(Color.Transparent);
                var local = new RectangleF(0, 0, width, height);
                using (var backdrop = new SolidBrush(_theme.Backdrop))
                {
                    g.FillRectangle(backdrop, local);
                }

                if (!HasPlayfield)
                {
                    return;
                }

                if (showBga)
                {
                    // Punch a transparent hole over the note field so the BGA layer
                    // underneath shows through; rails still draw on top.
                    RectangleF hole = NoteFieldBounds();
                    if (hole.Width >= 1f && hole.Height >= 1f)
                    {
                        var prev = g.CompositingMode;
                        g.CompositingMode = CompositingMode.SourceCopy;
                        using (var clear = new SolidBrush(Color.Transparent))
                        {
                            g.FillRectangle(clear, hole);
                        }
                        g.CompositingMode = prev;
                    }
                }

                DrawHalfChrome(g, true, laneCount, showBga);
                DrawHalfChrome(g, false, laneCount, showBga);
                // Arcade dual-scan cabinet: thin centre divider between the two halves.
                DrawDividerChrome(g);
            }
        }

        private RectangleF NoteFieldBounds()
        {
            float left = (float)_fit.X(TechnikaPlayfieldMetrics.SidebarWidth);
            float top = (float)_fit.Y(TechnikaPlayfieldMetrics.PlayfieldTop);
            float width = (float)_fit.Length(
                TechnikaPlayfieldMetrics.NativeWidth -
                (2.0 * TechnikaPlayfieldMetrics.SidebarWidth));
            float height = (float)_fit.Length(
                TechnikaPlayfieldMetrics.PlayfieldBottom -
                TechnikaPlayfieldMetrics.PlayfieldTop);
            if (width < 1f || height < 1f)
            {
                return RectangleF.Empty;
            }
            return new RectangleF(left, top, width, height);
        }

        private void DrawBgaBackdrop(Graphics g, Image frame)
        {
            if (frame == null || !HasPlayfield)
            {
                return;
            }

            // Only the note field between the side rails — not the whole preview panel.
            RectangleF bounds = NoteFieldBounds();
            if (bounds.Width < 1f || bounds.Height < 1f)
            {
                return;
            }

            RectangleF dest = Cover(bounds, frame.Width, frame.Height);
            if (dest.Width < 1f || dest.Height < 1f)
            {
                return;
            }

            var previous = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            GraphicsState state = g.Save();
            g.SetClip(bounds);
            g.DrawImage(frame, dest);
            g.Restore(state);
            g.InterpolationMode = previous;
        }

        /// <summary>Uniform scale that fills <paramref name="bounds"/>, cropping overflow.</summary>
        private static RectangleF Cover(RectangleF bounds, int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0 || bounds.Width <= 0f || bounds.Height <= 0f)
            {
                return RectangleF.Empty;
            }

            float scale = Math.Max(bounds.Width / pixelWidth, bounds.Height / pixelHeight);
            float width = pixelWidth * scale;
            float height = pixelHeight * scale;
            return new RectangleF(
                bounds.X + ((bounds.Width - width) / 2f),
                bounds.Y + ((bounds.Height - height) / 2f),
                width,
                height);
        }

        private void DrawHalfChrome(Graphics g, bool isTopHalf, int laneCount, bool showBga)
        {
            double top = _fit.Y(TechnikaPlayfieldMetrics.HalfTop(isTopHalf));
            double height = _fit.Length(TechnikaPlayfieldMetrics.HalfHeight(isTopHalf));
            double left = _fit.X(0);
            double width = _fit.Length(TechnikaPlayfieldMetrics.NativeWidth);

            if (!showBga)
            {
                using (var field = new SolidBrush(_theme.FieldFor(isTopHalf)))
                {
                    g.FillRectangle(field,
                        (float)left, (float)top, (float)width, (float)height);
                }
            }
            // When BGA is on the note field stays transparent so the video shows through;
            // side rails still frame the tracks.

            // Side rails: the frame the touch strip sits in.
            double rail = _fit.Length(TechnikaPlayfieldMetrics.SidebarWidth);
            if (rail >= 1)
            {
                using (var sidebar = new SolidBrush(_theme.SidebarFill))
                {
                    g.FillRectangle(sidebar,
                        (float)left, (float)top, (float)rail, (float)height);
                    g.FillRectangle(sidebar,
                        (float)(left + width - rail), (float)top, (float)rail, (float)height);
                }
            }

            if (laneCount < 1 || showBga)
            {
                // With BGA on, skip lane rules so they do not score the video.
                return;
            }

            // Lane separators. The stack occupies the middle 90% of the half, which is the
            // space the projector's lane centre runs over.
            double inset = TechnikaPlayfieldMetrics.LaneInset;
            for (int lane = 0; lane <= laneCount; lane++)
            {
                double localY = inset + ((1.0 - (2.0 * inset)) * lane / laneCount);
                double y = Snap(_fit.Y(TechnikaPlayfieldMetrics.HalfTop(isTopHalf) +
                    (localY * TechnikaPlayfieldMetrics.HalfHeight(isTopHalf))));
                g.DrawLine(_theme.LaneRule,
                    (float)left, (float)y, (float)(left + width), (float)y);
            }
        }

        private void DrawDividerChrome(Graphics g)
        {
            // Match the arcade: a thin black bar across the note field only (not the rails).
            double coreTop = _fit.Y(TechnikaPlayfieldMetrics.DividerCoreTop);
            double coreHeight = Math.Max(1.0,
                _fit.Length(TechnikaPlayfieldMetrics.DividerCoreHeight));
            double left = _fit.X(TechnikaPlayfieldMetrics.SidebarWidth);
            double width = _fit.Length(
                TechnikaPlayfieldMetrics.NativeWidth -
                (2.0 * TechnikaPlayfieldMetrics.SidebarWidth));

            using (var core = new SolidBrush(Color.Black))
            {
                g.FillRectangle(core,
                    (float)left, (float)coreTop, (float)width, (float)coreHeight);
            }
        }

        private void DrawHeaderChrome(Graphics g)
        {
            double top = _fit.Y(0);
            double height = _fit.Length(TechnikaPlayfieldMetrics.HeaderHeight);
            double left = _fit.X(0);
            double width = _fit.Length(TechnikaPlayfieldMetrics.NativeWidth);

            using (var header = new SolidBrush(_theme.HeaderFill))
            {
                g.FillRectangle(header,
                    (float)left, (float)top, (float)width, (float)height);
            }
            g.DrawLine(_theme.HeaderEdge,
                (float)left, (float)Snap(top + height),
                (float)(left + width), (float)Snap(top + height));
        }

        // -----------------------------------------------------------------------------------
        // Scanlines
        // -----------------------------------------------------------------------------------

        private void DrawScanlines(Graphics g)
        {
            // Blind draws no sweep at all; Blink gates it on the musical phase. Both lines in
            // a handover share that phase, so they flash together rather than independently.
            if (_lineEffector == TechnikaLineEffector.Blind)
            {
                return;
            }
            bool visible = LineVisibleAt(_frame.CurrentPhase);
            if (!visible && _frame.CurrentPhase < HandoverPhase)
            {
                return;
            }

            bool currentIsTop = (_frame.CurrentIntScan & 1) == 1;
            if (visible)
            {
                DrawScanline(g, currentIsTop, _frame.CurrentPhase);
            }

            // During the handover the arcade shows both: the outgoing scan finishing its
            // sweep and the incoming one already parked at its start.
            if (_frame.CurrentPhase >= HandoverPhase &&
                LineVisibleAt(0.0))
            {
                DrawScanline(g, !currentIsTop, 0.0);
            }
        }

        private void DrawScanline(Graphics g, bool isTopHalf, double phase)
        {
            bool rightward = SweepRightward(isTopHalf);
            double left = TechnikaPlayfieldMetrics.NoteMarginLeft;
            double right = TechnikaPlayfieldMetrics.NoteMarginRight;
            double travel = (right - left) * phase;
            double normalizedX = rightward ? left + travel : right - travel;
            double edgeX = normalizedX * TechnikaPlayfieldMetrics.NativeWidth;

            // Place the quad so its bright edge lands on the hit point; the wash trails
            // behind, which is why the offset flips with the sweep direction.
            double quadLeft = rightward
                ? edgeX - TechnikaPlayfieldMetrics.ScanlineLeadingEdgeOffset
                : edgeX - (TechnikaPlayfieldMetrics.ScanlineWidth -
                    TechnikaPlayfieldMetrics.ScanlineLeadingEdgeOffset);

            var quad = new RectangleF(
                (float)_fit.X(quadLeft),
                (float)_fit.Y(isTopHalf
                    ? TechnikaPlayfieldMetrics.ScanlineUpperTop
                    : TechnikaPlayfieldMetrics.ScanlineLowerTop),
                (float)_fit.Length(TechnikaPlayfieldMetrics.ScanlineWidth),
                (float)_fit.Length(TechnikaPlayfieldMetrics.ScanlineHeight));

            double halfTop = _fit.Y(TechnikaPlayfieldMetrics.HalfTop(isTopHalf));
            double halfHeight = _fit.Length(TechnikaPlayfieldMetrics.HalfHeight(isTopHalf));
            var half = new RectangleF(
                (float)_fit.X(0), (float)halfTop,
                (float)_fit.Length(TechnikaPlayfieldMetrics.NativeWidth), (float)halfHeight);

            // The quad is taller than its half and deliberately overhangs; clip it so the
            // upper sweep does not bleed across the divider into the lower field.
            GraphicsState state = g.Save();
            try
            {
                g.SetClip(half);
                Image wash = rightward ? _theme.ScanlineForward : _theme.ScanlineReverse;
                g.DrawImage(wash, quad);
                double edge = _fit.X(edgeX);
                g.DrawLine(_theme.ScanlineCore,
                    (float)edge, (float)halfTop,
                    (float)edge, (float)(halfTop + halfHeight));
            }
            finally
            {
                g.Restore(state);
            }
        }

        // -----------------------------------------------------------------------------------
        // Notes
        // -----------------------------------------------------------------------------------

        private void DrawNote(
            Graphics g,
            ProjectedGameplayNote note,
            double angleDegrees,
            double noteZoom)
        {
            if (note.State == GameplayPreviewNoteState.Inactive ||
                note.State == GameplayPreviewNoteState.Resolved)
            {
                return;
            }

            int laneCount = Math.Max(1, _projection.LaneCount);
            double size = Math.Max(4.0,
                _fit.Length(TechnikaPlayfieldMetrics.NoteFrameSize(laneCount)) * noteZoom);
            double centerX = _fit.NoteX(note.X);
            double centerY = _fit.NoteY(note.Y, note.IsTopHalf);
            TechnikaNoteBrushes brushes = _theme.NoteFor(note.Kind);

            // The lane box is what a whole note frame fills, at either mode's pitch; a glyph
            // authored on a smaller canvas than that is smaller on purpose - see
            // TechnikaNoteSprites.ScaleOf. Reading a 3-line canvas in a 4-line set as "authored
            // larger" would draw a chain head 1.29x the note it leads.
            double headSize = Math.Max(4.0, size * _sprites.ScaleFor(note.Kind));

            bool prepare = note.State == GameplayPreviewNoteState.Prepare;
            // Active holds/repeat heads stay fully opaque: the arcade does not fade them
            // out while the gesture is still being held.
            bool keepSolid = note.State == GameplayPreviewNoteState.Active &&
                (GameplayPreviewNoteKinds.HasHoldTrail(note.Kind) ||
                    note.Kind == GameplayPreviewNoteKind.RepeatHead ||
                    note.Kind == GameplayPreviewNoteKind.RepeatHeadHold);
            double opacity = keepSolid
                ? 1.0
                : (prepare ? PrepareOpacity : 1.0) * FaderOpacityFor(note);
            if (opacity <= 0.01)
            {
                // Fully faded by the note effector: no head, trail or approach - the
                // arcade's Fade Out reads as empty field at the line, not as a stack of
                // zero-alpha art.
                return;
            }

            DrawTrail(g, note, size, brushes, opacity);

            // The head draws while its own scan is on screen. Holds and RepeatHeads stay
            // drawn for the whole Active window (tail / series end) so the head does not
            // vanish mid-gesture and leave only a washed-out trail.
            bool headVisible = note.ScanIndex == _frame.CurrentIntScan ||
                note.ScanIndex == _frame.CurrentIntScan + 1;
            if (!headVisible && keepSolid)
            {
                headVisible = true;
            }
            if (!headVisible)
            {
                return;
            }

            if (note.ApproachVisible)
            {
                DrawApproach(g, note, centerX, centerY, size);
            }

            TechnikaNoteSprite sprite = _sprites.For(note.Kind);
            if (sprite != null)
            {
                // A chain head is an arrow, so it is the one glyph whose orientation carries
                // meaning: it says where the chain goes next, and a chain crosses lanes.
                // angleDegrees already carries the -180 of HeadArtDegrees - the art's own
                // authored direction, subtracted where the run walk stored it. The arcade
                // glyphs carry their glow inside their frame, so unlike the vector fallback
                // nothing is layered under them: a second glow reads as a smudge around the
                // note rather than as light coming off it.
                GraphicsState state = g.Save();
                try
                {
                    g.TranslateTransform((float)centerX, (float)centerY);
                    if (Math.Abs(angleDegrees) > 0.01)
                    {
                        g.RotateTransform((float)angleDegrees);
                    }
                    var head = new RectangleF(
                        (float)(-headSize / 2.0), (float)(-headSize / 2.0),
                        (float)headSize, (float)headSize);
                    DrawImage(g, sprite.Frame(_frame.CurrentScan * ShineLoopsPerScan),
                        head, opacity);
                }
                finally
                {
                    g.Restore(state);
                }
                return;
            }

            // The static-art fallback draws the timeline's Techmania glyphs, whose chain head
            // is authored pointing along the run rather than against it: hand back the raw
            // bearing by undoing the art-direction offset the sprite path stores. Only a head
            // the run walk actually turned carries a nonzero angle - see DrawChainLink.
            double fallbackAngle = Math.Abs(angleDegrees) > 0.01
                ? angleDegrees + HeadArtDegrees
                : 0.0;
            if (!TechnikaNoteArt.TryDrawPlayfieldHead(
                g,
                ToTechnikaKind(note.Kind),
                centerX,
                centerY,
                size,
                opacity,
                fallbackAngle))
            {
                // Vector chrome stands in for art that is missing: a filled ellipse still
                // says where the note is and what kind it is.
                DrawGlow(g, brushes, centerX, centerY, size * 1.44, opacity * 0.9);
                using (var body = new SolidBrush(WithAlpha(brushes.Fill, opacity)))
                {
                    g.FillEllipse(body,
                        (float)(centerX - (size / 2.0)), (float)(centerY - (size / 2.0)),
                        (float)size, (float)size);
                }
                using (var edge = new Pen(WithAlpha(brushes.Edge.Color, opacity), 1f))
                {
                    g.DrawEllipse(edge,
                        (float)(centerX - (size / 2.0)), (float)(centerY - (size / 2.0)),
                        (float)size, (float)size);
                }
            }
        }

        /// <summary>
        /// The arcade's approach glow. With the local ring strip present it is drawn as the
        /// crescent the sweep drags through the note, asked for by where the sweep actually
        /// is: played as a swell it would only ever show its first frames, parking stray
        /// arcs a third of a lane left of every approaching note. Without the strip a
        /// converging circle says the same thing at lower fidelity.
        /// </summary>
        private void DrawApproach(
            Graphics g,
            ProjectedGameplayNote note,
            double centerX,
            double centerY,
            double size)
        {
            TechnikaNoteSprite ring = _sprites.Ring;
            if (ring == null)
            {
                double opacity = 0.30 + (0.70 * note.ApproachProgress);
                double radius = size * (0.5 + (0.45 * (1.0 - note.ApproachProgress)));
                using (var pen = new Pen(WithAlpha(_theme.ApproachRing.Color, opacity),
                    _theme.ApproachRing.Width))
                {
                    g.DrawEllipse(pen,
                        (float)(centerX - radius), (float)(centerY - radius),
                        (float)(radius * 2.0), (float)(radius * 2.0));
                }
                return;
            }

            double scale = _sprites.ScaleOf(ring);
            double edge = size * scale;
            if (edge <= 0.0)
            {
                return;
            }

            // Native pixels rather than screen: the ratio is what matters and the arcade's
            // own numbers are the ones the sweep width was measured in.
            double scanTravel = (TechnikaPlayfieldMetrics.NoteMarginRight -
                TechnikaPlayfieldMetrics.NoteMarginLeft) *
                TechnikaPlayfieldMetrics.NativeWidth;
            double glowNative = TechnikaPlayfieldMetrics.NoteFrameSize(
                Math.Max(1, _projection.LaneCount)) * scale;
            if (glowNative <= 0.0 || scanTravel <= 0.0)
            {
                return;
            }

            double offset = note.ApproachScanDistance * scanTravel / glowNative;
            // The light enters from the edge the sweep comes from, so its sign follows the
            // sweep's direction rather than the half - an ACW/LL/RR field otherwise drags
            // the glow in from the wrong side.
            bool rightward = SweepRightward(note.IsTopHalf);
            Image frame = ring.Sweep(rightward ? offset : -offset);
            if (frame == null)
            {
                return;
            }

            var destination = new RectangleF(
                (float)(centerX - (edge / 2.0)), (float)(centerY - (edge / 2.0)),
                (float)edge, (float)edge);
            if (rightward)
            {
                g.DrawImage(frame, destination);
                return;
            }
            // Mirrored on the lower half, where the sweep runs the other way and the light
            // has to arrive from the other side. Flipped in the transform rather than a
            // copied bitmap: the destination is symmetric about the note's own x, so the
            // mirror maps it onto itself.
            GraphicsState mirrorState = g.Save();
            try
            {
                g.TranslateTransform((float)centerX, 0f);
                g.ScaleTransform(-1f, 1f);
                g.TranslateTransform((float)-centerX, 0f);
                g.DrawImage(frame, destination);
            }
            finally
            {
                g.Restore(mirrorState);
            }
        }

        /// <summary>
        /// The body a held note stretches behind its head, drawn per visible scan: a hold
        /// longer than a scan crosses the divider into the half where the sweep runs the
        /// other way, so each of the two drawn scans gets the intersection of the hold's
        /// span with that scan.
        ///
        /// <para>With the arcade sheets present each segment is a body run out of the family's
        /// cross-section sheet and, on the segment holding the tail, a cap closing the far end
        /// - laid out along +x from the segment's start and mirrored on the lower half, where
        /// the sweep runs the other way: the cap has a flat side and a round one, so drawing it
        /// unmirrored down there would point it back into the note. The animation phase is the
        /// musical clock, one loop per beat. While the sweep is inside a segment's span that
        /// segment is the one being held and it draws the lit "in" variants of its art.</para>
        /// </summary>
        private void DrawTrail(
            Graphics g,
            ProjectedGameplayNote note,
            double size,
            TechnikaNoteBrushes brushes,
            double opacity)
        {
            if (note.DurationPulse <= 0 ||
                !GameplayPreviewNoteKinds.HasHoldTrail(note.Kind))
            {
                return;
            }

            double pulsesPerScan = PulsesPerScan;
            double headFloatScan = note.Pulse / pulsesPerScan;
            double tailFloatScan = (note.Pulse + note.DurationPulse) / pulsesPerScan;
            double phase = _frame.CurrentScan * ShineLoopsPerScan;

            for (int scan = _frame.CurrentIntScan; scan <= _frame.CurrentIntScan + 1; scan++)
            {
                double start = Math.Max(headFloatScan, scan);
                double end = Math.Min(tailFloatScan, scan + 1);
                if (start >= end)
                {
                    continue;
                }
                DrawTrailSegment(g, note, size, brushes, opacity, phase, scan, start, end,
                    tailFloatScan <= scan + 1);
            }
        }

        /// <summary>
        /// One scan's worth of a hold's body: the span [<paramref name="start"/>,
        /// <paramref name="end"/>] in scan units, drawn in <paramref name="scan"/>'s half and
        /// lane. <paramref name="withCap"/> is true only for the segment holding the tail; a
        /// continuation segment runs body to the scan's edge and stops, because the cap belongs
        /// to the hold's end, not to the divider it crosses. The lit "in" art follows the sweep
        /// per segment rather than per note: while the line is inside this segment's span the
        /// player is holding this part of the body, and the segment in the other half waits its
        /// turn at rest.
        /// </summary>
        private void DrawTrailSegment(
            Graphics g,
            ProjectedGameplayNote note,
            double size,
            TechnikaNoteBrushes brushes,
            double opacity,
            double phase,
            int scan,
            double start,
            double end,
            bool withCap)
        {
            bool top = (scan & 1) == 1;
            bool rightward = SweepRightward(top);
            double left = TechnikaPlayfieldMetrics.NoteMarginLeft;
            double right = TechnikaPlayfieldMetrics.NoteMarginRight;
            double fromFraction = start - scan;
            double toFraction = end - scan;
            double fromX = rightward
                ? left + ((right - left) * fromFraction)
                : right - ((right - left) * fromFraction);
            double toX = rightward
                ? left + ((right - left) * toFraction)
                : right - ((right - left) * toFraction);

            int laneCount = Math.Max(1, _projection.LaneCount);
            double inset = TechnikaPlayfieldMetrics.LaneInset;
            double localY = inset + ((1.0 - (2.0 * inset)) * (note.Lane + 0.5) / laneCount);
            double normalizedY = top ? localY / 2.0 : 0.5 + (localY / 2.0);

            double fromPointX = _fit.NoteX(fromX);
            double toPointX = _fit.NoteX(toX);
            double pointY = _fit.NoteY(normalizedY, top);
            double length = Math.Abs(toPointX - fromPointX);
            if (length < 1)
            {
                return;
            }

            bool ongoing = note.State == GameplayPreviewNoteState.Active &&
                _frame.CurrentScan >= start && _frame.CurrentScan <= end;

            // LongHoldNote: arcade longholdgauge body + line_nor_end cap (neon tube, opaque
            // along length; soft alpha only on the authored top/bottom glow and end flare).
            TechnikaNoteSprite cap = _sprites.TrailCap(note.Kind, ongoing);
            if (cap == null)
            {
                // No cap in the packaged sheets - only the actively-held trail variants ship
                // nowhere - so a themed bar spans the segment at lower fidelity, which is what
                // the trail drew for every kind before. Active segments stay solid; waiting
                // halves stay a lighter preview so they do not read as a fade-out of the hold.
                double flat = size * 0.42;
                double trailAlpha = ongoing ? opacity : (0.55 * opacity);
                using (var trail = new SolidBrush(WithAlpha(brushes.Fill, trailAlpha)))
                {
                    g.FillRectangle(trail,
                        (float)Math.Min(fromPointX, toPointX),
                        (float)(pointY - (flat / 2.0)),
                        (float)length,
                        (float)flat);
                }
                return;
            }

            // Height against the tap the whole set is authored around, exactly as a head is: the
            // arcade draws attribute 0's body 76 px tall inside a 90 px lane box and attribute
            // 12's at the full 90, and that difference is visible.
            double height = size * _sprites.ScaleOf(cap);
            double aspect = cap.FrameSize > 0 ? cap.FrameWidth / cap.FrameSize : 1.0;
            // Drag (BasicNote) tips extend past the hold end — Trilogy draws longnoteline's
            // rounded nose at x+duration. Hold/repeat caps still close flush at the end.
            bool tipPastEnd = note.Kind == GameplayPreviewNoteKind.Drag;
            double capWidth = withCap
                ? (tipPastEnd ? height * aspect : Math.Min(length, height * aspect))
                : 0.0;

            // Laid out along +x from the segment's start and mirrored when the sweep runs the
            // other way: the cap has a flat side and a round one, so drawing it unmirrored on
            // the lower half would point it back into the note.
            //
            // Hold/repeat: body through the end, cap overlaid so the authored soft flare sits
            // on solid tube rather than an open cut. Drag: body only to the hold end, tip
            // starting there and extending past — longnoteline's right-edge alpha is the
            // rounded nose; parking that flare on the end made the tip look hollow.
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform((float)fromPointX, (float)pointY);
                if (toPointX < fromPointX)
                {
                    g.ScaleTransform(-1f, 1f);
                }
                if (length > 0)
                {
                    // Stretched, not tiled: a body frame is one column of pixels, so every tile
                    // of it would be the same and the pitch has nothing to say. The drag curve
                    // has no body sheet - the arcade authors it as a cap alone - so its body is
                    // a cut through the cap's own frame.
                    TechnikaNoteSprite sheet = _sprites.TrailBody(note.Kind, ongoing);
                    Image run = sheet == null ? cap.Stem(phase) : sheet.Frame(phase);
                    DrawImage(g, run,
                        new RectangleF(0f, (float)(-height / 2.0), (float)length, (float)height),
                        opacity);
                }
                if (withCap && capWidth > 0)
                {
                    float tipX = tipPastEnd
                        ? (float)length
                        : (float)(length - capWidth);
                    DrawImage(g, cap.Frame(phase),
                        new RectangleF(
                            tipX, (float)(-height / 2.0),
                            (float)capWidth, (float)height),
                        opacity);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        // -----------------------------------------------------------------------------------
        // Hit bursts
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// The burst the sweep leaves behind it on every note it has just passed. Nothing
        /// here reads the note's state - the clock is
        /// <see cref="ProjectedGameplayNote.ApproachScanDistance"/>, so the burst outlives
        /// the note, and silent when the chart has no tempo.
        /// </summary>
        private void DrawHitEffects(Graphics g, IReadOnlyList<ProjectedGameplayNote> notes)
        {
            double effectScans = TechnikaHitFlash.ScansFor(_projection.ScanSeconds);
            double previewScale = TechnikaHitEffectProfile.PreviewScale(
                Math.Max(1, _projection.LaneCount));
            if (effectScans <= 0.0 || previewScale <= 0.0)
            {
                return;
            }

            TechnikaNoteSprite bomb = _sprites.CoolBomb;
            for (int i = 0; i < notes.Count; i++)
            {
                DrawHitEffect(g, notes[i], bomb, effectScans, previewScale);
            }
        }

        private void DrawHitEffect(
            Graphics g,
            ProjectedGameplayNote note,
            TechnikaNoteSprite bomb,
            double effectScans,
            double previewScale)
        {
            TechnikaHitEffectProfile profile = TechnikaHitEffectProfile.For(note.Kind);
            double life = effectScans * profile.Life;
            if (life <= 0.0)
            {
                return;
            }

            TechnikaHitFlash flash = TechnikaHitFlash.At(note.ApproachScanDistance / life)
                .Scaled(previewScale * profile.Scale, profile.Opacity);
            if (!flash.IsVisible)
            {
                return;
            }

            double width = _fit.Length(flash.Width);
            double height = _fit.Length(flash.Height);
            if (width <= 0.0 || height <= 0.0)
            {
                return;
            }

            double centerX = _fit.NoteX(note.X);
            double centerY = _fit.NoteY(note.Y, note.IsTopHalf);
            TechnikaNoteBrushes brushes = _theme.NoteFor(note.Kind);

            // Kept to its own half: at the arcade's authored size the burst is taller than
            // the half it fires in, and a lane at the edge of a half still can be.
            double halfTop = _fit.Y(TechnikaPlayfieldMetrics.HalfTop(note.IsTopHalf));
            double halfHeight = _fit.Length(
                TechnikaPlayfieldMetrics.HalfHeight(note.IsTopHalf));
            var half = new RectangleF(
                (float)_fit.X(0), (float)halfTop,
                (float)_fit.Length(TechnikaPlayfieldMetrics.NativeWidth),
                (float)halfHeight);

            GraphicsState state = g.Save();
            try
            {
                g.SetClip(half);
                Image frame = bomb == null ? null : bomb.Shot(flash.FrameProgress);
                if (frame == null)
                {
                    // No arcade art: a soft disc in the note's own colour, sized and faded by
                    // the same envelope. It says "struck here, just now" without pretending
                    // to be the art.
                    DrawGlow(g, brushes, centerX, centerY, Math.Max(width, height),
                        flash.Opacity);
                }
                else
                {
                    // The burst the arcade fires where the note was struck, run once across
                    // its frames by the same envelope that sizes and fades it.
                    var quad = new RectangleF(
                        (float)(centerX - (width / 2.0)), (float)(centerY - (height / 2.0)),
                        (float)width, (float)height);
                    DrawImage(g, frame, quad, flash.Opacity);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        // -----------------------------------------------------------------------------------
        // Run links
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// The bar the arcade runs through a chain or a repeat run: several notes joined
        /// by one line, read off the run rather than any single note. Drawn before the
        /// heads, so the line passes under them as it does in the arcade.
        /// </summary>
        private void DrawGroupLinks(Graphics g, IReadOnlyList<ProjectedGameplayNote> notes)
        {
            PrepareHeadAngles(notes.Count);
            GroupRuns(notes);

            for (int run = 0; run < _runCount; run++)
            {
                DrawGroupLink(g, notes, run);
            }
        }

        private void GroupRuns(IReadOnlyList<ProjectedGameplayNote> notes)
        {
            if (_runOf.Length < notes.Count)
            {
                _runOf = new int[Math.Max(notes.Count, 64)];
            }
            _openRunCount = 0;
            _runCount = 0;

            for (int i = 0; i < notes.Count; i++)
            {
                _runOf[i] = NoRun;

                ProjectedGameplayNote note = notes[i];
                int family = LinkFamily(note.Kind);
                if (family == NoFamily)
                {
                    continue;
                }

                int lane = family == RepeatFamily ? note.Lane : AnyLane;
                int slot = IndexOfOpenRun(family, lane, note.IsTopHalf, note.ScanIndex);
                if (slot < 0 || IsRunHead(note.Kind))
                {
                    slot = StartRun(family, lane, note.IsTopHalf, note.ScanIndex);
                }

                _runOf[i] = _openRuns[slot].Run;
            }
        }

        private int StartRun(int family, int lane, bool isTopHalf, int scanIndex)
        {
            int slot = IndexOfOpenRun(family, lane, isTopHalf, scanIndex);
            if (slot < 0)
            {
                if (_openRunCount == _openRuns.Length)
                {
                    Array.Resize(ref _openRuns, _openRuns.Length * 2);
                }
                slot = _openRunCount++;
            }

            _openRuns[slot] = new OpenRun(family, lane, isTopHalf, scanIndex, _runCount++);
            return slot;
        }

        private int IndexOfOpenRun(int family, int lane, bool isTopHalf, int scanIndex)
        {
            for (int i = 0; i < _openRunCount; i++)
            {
                if (_openRuns[i].Matches(family, lane, isTopHalf, scanIndex))
                {
                    return i;
                }
            }
            return -1;
        }

        private struct OpenRun
        {
            public OpenRun(int family, int lane, bool isTopHalf, int scanIndex, int run)
            {
                _family = family;
                _lane = lane;
                _isTopHalf = isTopHalf;
                _scanIndex = scanIndex;
                Run = run;
            }

            private readonly int _family;
            private readonly int _lane;
            private readonly bool _isTopHalf;
            private readonly int _scanIndex;

            public readonly int Run;

            public bool Matches(int family, int lane, bool isTopHalf, int scanIndex)
            {
                return _family == family && _lane == lane &&
                    _isTopHalf == isTopHalf && _scanIndex == scanIndex;
            }
        }

        private void PrepareHeadAngles(int count)
        {
            if (_headAngles.Length < count)
            {
                _headAngles = new double[Math.Max(count, 64)];
            }
            for (int i = 0; i < count; i++)
            {
                _headAngles[i] = 0.0;
            }
        }

        private void DrawGroupLink(
            Graphics g,
            IReadOnlyList<ProjectedGameplayNote> notes,
            int run)
        {
            int first = -1;
            int last = -1;
            for (int i = 0; i < notes.Count; i++)
            {
                if (_runOf[i] != run || !IsDrawn(notes[i]))
                {
                    continue;
                }
                if (first < 0) { first = i; }
                last = i;
            }
            if (first < 0 || last == first)
            {
                return;
            }

            ProjectedGameplayNote from = notes[first];
            bool prepare = from.State == GameplayPreviewNoteState.Prepare;
            double opacity = (prepare ? PrepareOpacity : 1.0) * FaderOpacityFor(from);
            if (opacity <= 0.01)
            {
                return;
            }

            int laneCount = Math.Max(1, _projection.LaneCount);
            double size = Math.Max(4.0, _fit.Length(
                TechnikaPlayfieldMetrics.NoteFrameSize(laneCount)));

            if (LinkFamily(from.Kind) == ChainFamily)
            {
                DrawChainLink(g, notes, run, first, size, opacity);
            }
            else
            {
                // A repeat series lives in one lane, so its members are collinear and one
                // bar from the first to the last is the whole line.
                double ax = _fit.NoteX(from.X);
                double ay = _fit.NoteY(from.Y, from.IsTopHalf);
                double bx = _fit.NoteX(notes[last].X);
                double by = _fit.NoteY(notes[last].Y, notes[last].IsTopHalf);
                DrawRunBar(g, from.Kind, Math.Min(ax, bx), ay,
                    Math.Abs(bx - ax), 0.0, size, opacity);
            }
        }

        /// <summary>
        /// Whether a run line may join to this note: drawn, with its head's scan on
        /// screen, so a line never hangs off a head that has already been played.
        /// Still-Active hold / Repeat heads stay joinable off their home scan so the
        /// bar remains anchored until the gesture ends.
        /// </summary>
        private bool IsDrawn(ProjectedGameplayNote note)
        {
            if (note.State == GameplayPreviewNoteState.Inactive ||
                note.State == GameplayPreviewNoteState.Resolved)
            {
                return false;
            }
            if (note.ScanIndex == _frame.CurrentIntScan ||
                note.ScanIndex == _frame.CurrentIntScan + 1)
            {
                return true;
            }
            return note.State == GameplayPreviewNoteState.Active &&
                (GameplayPreviewNoteKinds.HasHoldTrail(note.Kind) ||
                    note.Kind == GameplayPreviewNoteKind.RepeatHead ||
                    note.Kind == GameplayPreviewNoteKind.RepeatHeadHold);
        }

        /// <summary>
        /// A chain, joined member to member - the projector's chain pass absorbs whatever
        /// taps fall inside the span, whichever lane they are in, so a chain is a path
        /// across lanes and its members are collinear only by accident - and its head
        /// turned to face the second member.
        /// </summary>
        private void DrawChainLink(
            Graphics g,
            IReadOnlyList<ProjectedGameplayNote> notes,
            int run,
            int head,
            double size,
            double opacity)
        {
            // Path follows chart time: head first, then members by pulse (then lane).
            // Sorting by InsNum groups reused keysounds (eg-13/14/15…) and tangles the
            // zigzag — STOP Pop EX @22080 is the textbook case; DJMAX STUDIO is pulse order.
            var members = new List<int>(8);
            for (int i = 0; i < notes.Count; i++)
            {
                if (_runOf[i] == run && IsDrawn(notes[i]))
                {
                    members.Add(i);
                }
            }
            if (members.Count < 2)
            {
                return;
            }

            members.Sort(delegate(int a, int b)
            {
                bool aHead = IsRunHead(notes[a].Kind);
                bool bHead = IsRunHead(notes[b].Kind);
                if (aHead != bHead)
                {
                    return aHead ? -1 : 1;
                }
                int pulse = notes[a].Pulse.CompareTo(notes[b].Pulse);
                if (pulse != 0)
                {
                    return pulse;
                }
                return notes[a].Lane.CompareTo(notes[b].Lane);
            });

            for (int m = 1; m < members.Count; m++)
            {
                int previous = members[m - 1];
                int next = members[m];
                ProjectedGameplayNote from = notes[previous];
                ProjectedGameplayNote to = notes[next];
                double ax = _fit.NoteX(from.X);
                double ay = _fit.NoteY(from.Y, from.IsTopHalf);
                double bx = _fit.NoteX(to.X);
                double by = _fit.NoteY(to.Y, to.IsTopHalf);
                double length = Distance(ax, ay, bx, by);
                double degrees = Bearing(ax, ay, bx, by);
                DrawRunBar(g, from.Kind, ax, ay, length, degrees, size, opacity);

                // The arrow answers "where does this chain go", so it is aimed at the
                // member the head is actually joined to. The -180 is the art's own
                // authored direction - see HeadArtDegrees: notepressstart points left.
                if (previous == head && IsRunHead(notes[head].Kind))
                {
                    _headAngles[head] = degrees - HeadArtDegrees;
                }
            }
        }

        /// <summary>
        /// One straight piece of a run's line: the art stretched from <paramref name="originX"/>
        /// along <paramref name="length"/>, turned by <paramref name="degrees"/> and centred on
        /// the line it joins. Both families draw through this - a repeat run passes its whole
        /// horizontal span, a chain passes each of its segments.
        ///
        /// <para>With the arcade sheets present the line is one column of the strip's current
        /// frame stretched down the whole run: both line strips are built as a cap and not as a
        /// tile, so the flat near end is the cross-section and the soft far end is where the art
        /// stops - tiling drew a run as a row of fading blobs. The strip's cut animates on the
        /// musical clock like every other piece. Without art a themed bar joins the same two
        /// points at lower fidelity: yellow for a chain, the family's 55% trail colour for a
        /// repeat.</para>
        /// </summary>
        private void DrawRunBar(
            Graphics g,
            GameplayPreviewNoteKind kind,
            double originX,
            double originY,
            double length,
            double degrees,
            double size,
            double opacity)
        {
            if (length < 1)
            {
                return;
            }

            TechnikaNoteSprite line = _sprites.Line(kind);

            // The line is authored against the tap the whole set is measured from - 76 px of
            // art for a 90 px note - so it is scaled the same way a head and a hold's body are.
            // Against the head it joins instead, which is what this did, a chain's line came out
            // 22% short: its head is a 116 px frame, so 76/116 rather than 76/90.
            double height = line == null ? size * 0.18 : size * _sprites.ScaleOf(line);
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform((float)originX, (float)originY);
                if (Math.Abs(degrees) > 0.01)
                {
                    g.RotateTransform((float)degrees);
                }
                if (line == null)
                {
                    // Chain connectors are yellow even though chain heads are green: the
                    // arcade's line matches the yellow node rings. Repeat connectors reuse the
                    // purple trail at its own 55% alpha.
                    Color color = LinkFamily(kind) == ChainFamily
                        ? _theme.ChainRunLine
                        : _theme.NoteFor(kind).Trail.Color;
                    using (var bar = new SolidBrush(WithAlpha(color, opacity)))
                    {
                        g.FillRectangle(bar, 0f, (float)(-height / 2.0), (float)length, (float)height);
                    }
                }
                else
                {
                    // Stem's cut is 2 of noterepeatline's 30 columns and lands inside the flat
                    // core; on the chain's 1x76 columns it is the whole frame and this is a
                    // plain stretch.
                    DrawImage(g, line.Stem(_frame.CurrentScan * ShineLoopsPerScan),
                        new RectangleF(0f, (float)(-height / 2.0), (float)length, (float)height),
                        opacity);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private static int LinkFamily(GameplayPreviewNoteKind kind)
        {
            switch (kind)
            {
                case GameplayPreviewNoteKind.ChainHead:
                case GameplayPreviewNoteKind.ChainNode:
                    return ChainFamily;
                case GameplayPreviewNoteKind.RepeatHead:
                case GameplayPreviewNoteKind.RepeatHeadHold:
                case GameplayPreviewNoteKind.Repeat:
                case GameplayPreviewNoteKind.RepeatHold:
                    return RepeatFamily;
                default:
                    return NoFamily;
            }
        }

        private static bool IsRunHead(GameplayPreviewNoteKind kind)
        {
            return kind == GameplayPreviewNoteKind.ChainHead ||
                kind == GameplayPreviewNoteKind.RepeatHead ||
                kind == GameplayPreviewNoteKind.RepeatHeadHold;
        }

        private static double Bearing(double fromX, double fromY, double toX, double toY)
        {
            return Math.Atan2(toY - fromY, toX - fromX) * 180.0 / Math.PI;
        }

        private static double Distance(double fromX, double fromY, double toX, double toY)
        {
            double dx = toX - fromX;
            double dy = toY - fromY;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        // -----------------------------------------------------------------------------------
        // Count-in
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// The count-in before the chart starts: 3, 2, 1, one to a beat, centred on the
        /// boundary between the halves. Measured in beats off the musical clock rather
        /// than seconds off a wall clock, so scrubbing back into the count-in shows it
        /// again, and silent from the first note onwards.
        /// </summary>
        private void DrawCountdown(Graphics g)
        {
            double firstScan;
            if (!FirstNoteScan(out firstScan))
            {
                return;
            }

            double beatsPerScan = _projection.BeatsPerScan;
            TechnikaCountIn count = TechnikaCountIn.At(
                (firstScan - _frame.CurrentScan) * beatsPerScan);
            if (!count.IsVisible)
            {
                return;
            }

            double em = _fit.Length(
                TechnikaPlayfieldMetrics.MeanHalfHeight * CountdownHeightShare);
            EnsureCountdownFont(em);

            double centerX = _fit.X(TechnikaPlayfieldMetrics.NativeWidth / 2.0);
            double centerY = _fit.Y(TechnikaPlayfieldMetrics.HalfBoundary);

            GraphicsState state = g.Save();
            try
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                using (var brush = new SolidBrush(
                    WithAlpha(Color.FromArgb(0xFF, 0xED, 0xEE, 0xF0), count.Opacity)))
                {
                    var format = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(count.Number.ToString(), _countdownFont, brush,
                        new RectangleF(
                            (float)(centerX - _fit.Length(200)),
                            (float)(centerY - em),
                            (float)_fit.Length(400),
                            (float)(em * 2)),
                        format);
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        private bool FirstNoteScan(out double scan)
        {
            scan = 0.0;
            IReadOnlyList<ProjectedGameplayNote> all = _projection.Notes;
            if (all == null || all.Count == 0)
            {
                return false;
            }
            scan = all[0].Pulse / PulsesPerScan;
            return true;
        }

        private void EnsureCountdownFont(double em)
        {
            if (_countdownFont != null && Math.Abs(em - _countdownEm) < 0.5)
            {
                return;
            }
            if (_countdownFont != null)
            {
                _countdownFont.Dispose();
            }
            _countdownEm = em;
            _countdownFont = new Font("Segoe UI", (float)Math.Max(1.0, em),
                GraphicsUnit.Pixel);
        }

        // -----------------------------------------------------------------------------------
        // Effectors
        // -----------------------------------------------------------------------------------

        /// <summary>Which way the sweep travels over the named half under the bound
        /// projection's scroll-direction effector.</summary>
        private bool SweepRightward(bool isTopHalf)
        {
            TechnikaScrollDirection direction = _projection == null
                ? TechnikaScrollDirection.Clockwise
                : _projection.ScrollDirection;
            return GameplayPreviewProjector.TechnikaSweepRightward(isTopHalf, direction);
        }

        /// <summary>
        /// The fader's opacity for one note, read off how many scans ahead of the sweep its
        /// head sits (<see cref="ProjectedGameplayNote.ApproachScanDistance"/> negated).
        /// Fade In is invisible far ahead and solid at the line; Fade Out is the inverse.
        /// The level-1 variants complete the fade across most of a scan (0.9), level 2
        /// across about half one (0.45). Notes already behind the sweep (a hold's played
        /// body) are held solid under Fade In and gone under Fade Out, which is what
        /// applying the same ramp past its ends naturally answers.
        /// </summary>
        private double FaderOpacityFor(ProjectedGameplayNote note)
        {
            if (_noteFader == TechnikaNoteFader.Off)
            {
                return 1.0;
            }

            double ahead = -note.ApproachScanDistance;
            double window = _noteFader == TechnikaNoteFader.FadeIn2 ||
                _noteFader == TechnikaNoteFader.FadeOut2
                ? 0.45
                : 0.9;

            double alpha;
            if (_noteFader == TechnikaNoteFader.FadeIn ||
                _noteFader == TechnikaNoteFader.FadeIn2)
            {
                alpha = (window - ahead) / window;
            }
            else
            {
                alpha = ahead / window;
            }
            return Math.Max(0.0, Math.Min(1.0, alpha));
        }

        /// <summary>
        /// Whether the sweep is visible at a scan phase under Blink / Blind. A half-scan
        /// blink period gives Blink a 50% duty and Blink2 a 25% duty, matching the arcade
        /// timings; the incoming sweep during a handover shares the phase, so the two lines
        /// flash together.
        /// </summary>
        private bool LineVisibleAt(double phase)
        {
            if (_lineEffector == TechnikaLineEffector.Blind)
            {
                return false;
            }
            if (_lineEffector == TechnikaLineEffector.On)
            {
                return true;
            }

            const double Period = 0.5;
            double duty = _lineEffector == TechnikaLineEffector.Blink2 ? 0.125 : 0.25;
            double within = phase - Math.Floor(phase / Period) * Period;
            return within < duty;
        }

        // -----------------------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// A sprite frame drawn into a destination rectangle, at full opacity or faded through a
        /// colour matrix - the same construction the theme's glow drawing uses, so a note in the
        /// next scan reads at 60% whether it is a bitmap or a vector fallback.
        /// </summary>
        private static void DrawImage(
            Graphics g,
            Image image,
            RectangleF destination,
            double opacity)
        {
            if (opacity >= 0.999)
            {
                g.DrawImage(image, destination);
                return;
            }
            using (var attributes = new ImageAttributes())
            {
                var matrix = new ColorMatrix { Matrix33 = (float)opacity };
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.DrawImage(image, Rectangle.Round(destination),
                    0f, 0f, (float)image.Width, (float)image.Height,
                    GraphicsUnit.Pixel, attributes);
            }
        }

        private void DrawGlow(
            Graphics g,
            TechnikaNoteBrushes brushes,
            double centerX,
            double centerY,
            double size,
            double opacity)
        {
            if (size <= 0.0 || opacity <= 0.01)
            {
                return;
            }
            var destination = new RectangleF(
                (float)(centerX - (size / 2.0)), (float)(centerY - (size / 2.0)),
                (float)size, (float)size);
            if (opacity >= 0.999)
            {
                g.DrawImage(brushes.Glow, destination);
                return;
            }
            using (var attributes = new ImageAttributes())
            {
                var matrix = new ColorMatrix { Matrix33 = (float)opacity };
                attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.DrawImage(brushes.Glow, Rectangle.Round(destination),
                    0f, 0f, (float)brushes.Glow.Width,
                    (float)brushes.Glow.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        private static Color WithAlpha(Color color, double opacity)
        {
            int alpha = (int)(color.A * Math.Max(0.0, Math.Min(1.0, opacity)));
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static double Snap(double value)
        {
            return Math.Round(value) + 0.5;
        }

        private static TechnikaNoteKind ToTechnikaKind(GameplayPreviewNoteKind kind)
        {
            switch (kind)
            {
                case GameplayPreviewNoteKind.Basic:
                    return TechnikaNoteKind.Basic;
                case GameplayPreviewNoteKind.Drag:
                    return TechnikaNoteKind.Drag;
                case GameplayPreviewNoteKind.ChainHead:
                    return TechnikaNoteKind.ChainHead;
                case GameplayPreviewNoteKind.ChainNode:
                    return TechnikaNoteKind.ChainNode;
                case GameplayPreviewNoteKind.RepeatHead:
                    return TechnikaNoteKind.RepeatHead;
                case GameplayPreviewNoteKind.RepeatHeadHold:
                    return TechnikaNoteKind.RepeatHeadHold;
                case GameplayPreviewNoteKind.Repeat:
                    return TechnikaNoteKind.Repeat;
                case GameplayPreviewNoteKind.RepeatHold:
                    return TechnikaNoteKind.RepeatHold;
                case GameplayPreviewNoteKind.Hold:
                    return TechnikaNoteKind.Hold;
                default:
                    return TechnikaNoteKind.Unknown;
            }
        }

        private void DisposeChrome()
        {
            if (_chrome != null)
            {
                _chrome.Dispose();
                _chrome = null;
            }
        }
    }
}
