using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;
using DJMaxEditor.Files.Tech;
using DJMaxEditor.UI;
using DJMaxEditor.Video;

namespace DJMaxEditor.Preview
{
    public sealed class GameplayPreviewControl : Control
    {
        private EditorDocumentContext _document;
        private GameplayPreviewProjection _projection;
        private GameplayPreviewFrame _frame;
        private GameplayPreviewProfile _profile = GameplayPreviewProfile.Generic;
        private TechnikaScrollDirection _scrollDirection = TechnikaScrollDirection.Clockwise;
        private float _noteZoom = 1.0f;
        private readonly TechnikaPlayfieldRenderer _technikaRenderer =
            new TechnikaPlayfieldRenderer();
        private readonly BgaPlayer _bga = new BgaPlayer();
        private readonly BgaClockMap _bgaClock = new BgaClockMap();
        private bool _bgaEnabled = true;
        private int _bgaAttachGeneration;
        private string _bgaPath;

        public GameplayPreviewControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            BackColor = StudioDesignSystem.Void;
            Dock = DockStyle.Fill;
            MinimumSize = new Size(320, 220);
            TabStop = true;

            // The persisted sprite style applies from construction so the preview draws the
            // owner's chosen set before any document is bound; the form's dropdown re-sends
            // the same id harmlessly when it initialises.
            SpriteStyleId = FeatureFlags.PreviewSpriteStyleId;
        }

        public EditorDocumentContext Document
        {
            get { return _document; }
        }

        public GameplayPreviewProfile Profile
        {
            get { return _profile; }
        }

        /// <summary>
        /// The TECHNIKA scroll-direction effector. Changing it re-projects the chart -
        /// note positions, scanlines, hold bodies and the approach glow all read the one
        /// direction - so it rebuilds the projection like a profile change does.
        /// </summary>
        public TechnikaScrollDirection ScrollDirection
        {
            get { return _scrollDirection; }
            set
            {
                if (_scrollDirection == value) return;
                _scrollDirection = value;
                RebuildProjection();
            }
        }

        /// <summary>The note-series effector (fade in / fade out), applied per paint from
        /// each note's distance to the sweep.</summary>
        internal TechnikaNoteFader NoteFader
        {
            get { return _technikaRenderer.NoteFader; }
            set
            {
                if (_technikaRenderer.NoteFader == value) return;
                _technikaRenderer.NoteFader = value;
                Invalidate();
            }
        }

        /// <summary>The timeline-series effector (blink / blind), applied per paint from
        /// the frame's musical phase.</summary>
        internal TechnikaLineEffector LineEffector
        {
            get { return _technikaRenderer.LineEffector; }
            set
            {
                if (_technikaRenderer.LineEffector == value) return;
                _technikaRenderer.LineEffector = value;
                Invalidate();
            }
        }

        /// <summary>"ARCADE SPRITES" or "PACKAGED GLYPHS" - which note set the TECHNIKA
        /// playfield resolved, for the panel header. A named style reports its own name
        /// (<c>T3 STAR #03</c>) instead.</summary>
        internal string SpriteSourceLabel
        {
            get { return _technikaRenderer.SpriteSourceLabel; }
        }

        /// <summary>
        /// The SPRITE SET choice, as a catalog style id. Resolving goes through the catalog
        /// rather than straight to the renderer so a persisted id that no longer matches
        /// anything on disk falls back to the same answer AUTO would give, instead of
        /// failing or rendering nothing.
        /// </summary>
        internal string SpriteStyleId
        {
            set
            {
                TechnikaSpriteStyle style = TechnikaSpriteCatalog.Resolve(value);
                if (_technikaRenderer.SetSpriteStyle(style))
                {
                    Invalidate();
                }
            }
        }

        /// <summary>The SPRITE SET choice when the caller already holds the resolved catalog
        /// entry (startup selection, catalog rebuild). Skips the persisted-id re-probe the
        /// setter above performs; sheets load lazily at the next paint.</summary>
        internal void ApplySpriteStyle(TechnikaSpriteStyle style)
        {
            if (_technikaRenderer.SetSpriteStyle(style))
            {
                Invalidate();
            }
        }

        /// <summary>The dropdown pick: the chosen set was decoded and pre-warmed on a loader
        /// thread, so this is the cheap swap. A no-op when the id is unchanged.</summary>
        internal void ApplySpriteStyle(TechnikaSpriteStyle style, TechnikaNoteSprites sprites)
        {
            if (_technikaRenderer.SetSpriteStyle(style, sprites))
            {
                Invalidate();
            }
        }

        /// <summary>The playback position the current frame was built at, or 0 before a
        /// document is bound. Surfaced in the header status line, which is where the
        /// floating overlay used to draw it.</summary>
        internal int CurrentTick
        {
            get { return _frame == null ? 0 : _frame.CurrentTick; }
        }

        public float NoteZoom
        {
            get { return _noteZoom; }
            set
            {
                _noteZoom = Math.Max(0.75f, Math.Min(2.5f, value));
                Invalidate();
            }
        }

        /// <summary>
        /// The chart's scroll speed as a SWEEP-rate multiplier: notes never move
        /// from their authored scan positions; the scanline crosses the field at
        /// this many times the musical rate (BYTES attribute 1 = half, 2 = double).
        /// Hit states, the hit flash and the approach glow measure from the sweep,
        /// so they follow the faster line.
        /// </summary>
        public double ScrollSpeed
        {
            get { return _scrollSpeed; }
            set
            {
                double clamped = Math.Max(0.25, Math.Min(4.0, value));
                if (Math.Abs(_scrollSpeed - clamped) < 1e-9) return;
                _scrollSpeed = clamped;
                RefreshPlayback();
            }
        }
        private double _scrollSpeed = 1.0;

        public string ProjectionStatus
        {
            get
            {
                return _projection == null
                    ? "NO DOCUMENT"
                    : _projection.StatusLabel;
            }
        }

        public int DiagnosticCount
        {
            get { return _projection == null ? 0 : _projection.Diagnostics.Count; }
        }

        /// <summary>When true and a video is loaded, the BGA paints under the playfield notes.</summary>
        public bool BgaEnabled
        {
            get { return _bgaEnabled; }
            set
            {
                if (_bgaEnabled == value)
                {
                    return;
                }
                _bgaEnabled = value;
                Invalidate();
            }
        }

        public bool BgaIsOpen
        {
            get { return _bga.IsOpen; }
        }

        public string BgaStatusText
        {
            get { return _bga.StatusText; }
        }

        public string BgaPath
        {
            get { return _bgaPath; }
        }

        public event EventHandler BgaChanged;

        public void Bind(EditorDocumentContext document)
        {
            if (_document != null)
            {
                _document.Model.Tracks.EventAdded -= ChartTopologyChanged;
                _document.Model.Tracks.EventRemoved -= ChartTopologyChanged;
                _document.UndoManager.OnUndoRedo -= DocumentUndoRedo;
            }

            _document = document;
            if (_document != null)
            {
                _document.Model.Tracks.EventAdded += ChartTopologyChanged;
                _document.Model.Tracks.EventRemoved += ChartTopologyChanged;
                _document.UndoManager.OnUndoRedo += DocumentUndoRedo;
            }
            RebuildProjection();
            AttachBgaForDocument(document);
        }

        public void SetProfile(GameplayPreviewProfile profile)
        {
            if (_profile == profile) return;
            _profile = profile;
            RebuildProjection();
        }

        public void RefreshTopology()
        {
            RebuildProjection();
        }

        public void RefreshPlayback()
        {
            if (_projection == null || _document == null)
            {
                _frame = null;
            }
            else
            {
                _frame = _projection.CreateRenderableFrame(
                    _document.Model.CurrentTick, _scrollSpeed);
            }
            SyncBga();
            Invalidate();
        }

        /// <summary>Opens a BGA file for the backdrop. Returns false when the decoder rejects it.</summary>
        public async Task<bool> LoadBgaAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            _bgaPath = path;
            int generation = ++_bgaAttachGeneration;
            string playable = path;

            if (BgaSourceResolver.RequiresTranscode(path))
            {
                string resolved = null;
                string failure = null;
                await Task.Run(() =>
                {
                    if (!BgaSourceResolver.TryResolve(path, out resolved, out failure))
                    {
                        resolved = null;
                    }
                }).ConfigureAwait(true);

                if (generation != _bgaAttachGeneration)
                {
                    return false;
                }

                if (resolved == null)
                {
                    Logs.Write("BGA: transcode failed: {0} for {1}", failure, path);
                    RaiseBgaChanged();
                    return false;
                }

                playable = resolved;
            }

            if (generation != _bgaAttachGeneration)
            {
                return false;
            }

            string error;
            bool ok = _bga.TryOpen(playable, out error);
            if (!ok)
            {
                Logs.Write("BGA: open failed: {0} for {1}", error, playable);
            }
            SyncBga();
            Invalidate();
            RaiseBgaChanged();
            return ok;
        }

        public void ClearBga()
        {
            _bgaAttachGeneration++;
            _bgaPath = null;
            _bga.Clear();
            _bgaClock.Clear();
            Invalidate();
            RaiseBgaChanged();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_document != null)
                {
                    _document.Model.Tracks.EventAdded -= ChartTopologyChanged;
                    _document.Model.Tracks.EventRemoved -= ChartTopologyChanged;
                    _document.UndoManager.OnUndoRedo -= DocumentUndoRedo;
                }
                _bga.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            NoteZoom += e.Delta > 0 ? 0.1f : -0.1f;
            base.OnMouseWheel(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(StudioDesignSystem.Void);

            bool showBga = _bgaEnabled && _bga.IsOpen && _bga.CurrentBitmap != null;

            Rectangle viewport = ClientRectangle;
            viewport.Inflate(-12, -12);
            if (viewport.Width <= 0 || viewport.Height <= 0) return;

            if (_projection == null || _frame == null)
            {
                DrawEmptyState(graphics, viewport);
                return;
            }

            if (_projection.Profile == GameplayPreviewProfile.Technika)
            {
                _technikaRenderer.Paint(
                    graphics, viewport, _projection, _frame, _noteZoom,
                    showBga ? _bga.CurrentBitmap : null,
                    showBga);
            }
            else
            {
                if (showBga)
                {
                    DrawGenericBga(graphics, viewport, _bga.CurrentBitmap);
                }
                DrawGenericFrame(graphics, viewport);
            }
        }

        private void AttachBgaForDocument(EditorDocumentContext document)
        {
            _bgaAttachGeneration++;
            _bga.Clear();
            _bgaPath = null;
            _bgaClock.Clear();

            if (document == null || document.Model == null)
            {
                RaiseBgaChanged();
                return;
            }

            ReloadBgaClock(document.Model);

            string discovered = DiscoverBgaPath(document);
            if (!string.IsNullOrEmpty(discovered))
            {
                _ = LoadBgaAsync(discovered);
            }
            else
            {
                RaiseBgaChanged();
            }
        }

        private void SyncBga()
        {
            if (!_bgaEnabled || !_bga.IsOpen || _document == null || _document.Model == null)
            {
                return;
            }

            // Prefer the document playhead: VirtualCurrentTick is the same source the
            // timeline and VideoStart marker share. Frame.CurrentTick is a rounded native
            // copy of that value and must not drift the BGA relative to the cue.
            int virtualTick = _document.Model.VirtualCurrentTick;
            _bga.Seek(_bgaClock.TimeForVirtualTick(virtualTick));
        }

        private void ReloadBgaClock(PlayerData model)
        {
            _bgaClock.Clear();
            if (model == null)
            {
                return;
            }

            _bgaClock.Load(model);
            _bgaClock.Offset = ResolveBgaOffset(model, _bgaClock);
        }

        private void RaiseBgaChanged()
        {
            EventHandler handler = BgaChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static string DiscoverBgaPath(EditorDocumentContext document)
        {
            if (document == null)
            {
                return null;
            }

            PlayerData model = document.Model;
            TechMetadata tech = model != null ? model.TechMetadata : null;
            if (tech != null && !string.IsNullOrEmpty(tech.Bga) &&
                !string.IsNullOrEmpty(document.SourcePath))
            {
                try
                {
                    string folder = Path.GetDirectoryName(document.SourcePath);
                    string candidate = Path.Combine(folder ?? string.Empty, tech.Bga);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (Exception)
                {
                }
            }

            return BgaSourceResolver.FindForChart(document.SourcePath);
        }

        /// <summary>
        /// TECHMANIA <c>bgaOffset</c> delays (+) / advances (-) the video. When the chart
        /// already carries an Attr-100 VideoStart (including the one tech import drops on
        /// track 17 at bgaOffset ticks), that marker alone owns sync — applying Offset on
        /// top double-shifts the picture.
        /// </summary>
        private static TimeSpan ResolveBgaOffset(PlayerData model, BgaClockMap clock)
        {
            if (clock != null && clock.HasVideoStartMarker)
            {
                return TimeSpan.Zero;
            }

            if (model == null || model.TechMetadata == null)
            {
                return TimeSpan.Zero;
            }

            double seconds = model.TechMetadata.BgaOffset;
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds == 0.0)
            {
                return TimeSpan.Zero;
            }

            // TimeForVirtualTick adds Offset; positive bgaOffset must delay → negate.
            return TimeSpan.FromSeconds(-seconds);
        }

        private static void DrawGenericBga(Graphics graphics, Rectangle viewport, Image frame)
        {
            if (frame == null)
            {
                return;
            }

            RectangleF dest = Cover(viewport, frame.Width, frame.Height);
            if (dest.Width < 1f || dest.Height < 1f)
            {
                return;
            }

            var previous = graphics.InterpolationMode;
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            GraphicsState clip = graphics.Save();
            graphics.SetClip(viewport);
            graphics.DrawImage(frame, dest);
            graphics.Restore(clip);
            using (var dim = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
            {
                graphics.FillRectangle(dim, viewport);
            }
            graphics.InterpolationMode = previous;
        }

        private static RectangleF Cover(Rectangle bounds, int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return RectangleF.Empty;
            }

            float scale = Math.Max(
                (float)bounds.Width / pixelWidth,
                (float)bounds.Height / pixelHeight);
            float width = pixelWidth * scale;
            float height = pixelHeight * scale;
            return new RectangleF(
                bounds.X + ((bounds.Width - width) / 2f),
                bounds.Y + ((bounds.Height - height) / 2f),
                width,
                height);
        }

        private void RebuildProjection()
        {
            // Re-probe for sprites an owner may have dropped in since startup; a changed set
            // is worth a repaint even when the projection itself is unchanged.
            if (_technikaRenderer.ReloadSprites())
            {
                Invalidate();
            }
            _projection = _document == null
                ? null
                : GameplayPreviewProjector.Project(_document.Model, _profile, _scrollDirection);
            RefreshPlayback();
        }

        private void ChartTopologyChanged(object sender, EventArgs e)
        {
            RebuildProjection();
            if (_document != null)
            {
                ReloadBgaClock(_document.Model);
                SyncBga();
            }
        }

        private void DocumentUndoRedo(object sender, UndoManager.Action action)
        {
            RebuildProjection();
            if (_document != null)
            {
                ReloadBgaClock(_document.Model);
                SyncBga();
            }
        }

        private void DrawEmptyState(Graphics graphics, Rectangle viewport)
        {
            using (var title = StudioDesignSystem.DisplayFont(14f))
            using (var body = StudioDesignSystem.BodyFont(9f))
            using (var primary = new SolidBrush(StudioDesignSystem.Frost))
            using (var muted = new SolidBrush(StudioDesignSystem.Muted))
            {
                graphics.DrawString("GAMEPLAY PREVIEW", title, primary,
                    viewport.Left + 18, viewport.Top + 18);
                graphics.DrawString(
                    "Open a chart to visualize the shared playback position.",
                    body,
                    muted,
                    viewport.Left + 18,
                    viewport.Top + 54);
            }
        }

        private void DrawGenericFrame(Graphics graphics, Rectangle viewport)
        {
            using (var deck = new SolidBrush(StudioDesignSystem.Deck))
            using (var border = new Pen(StudioDesignSystem.Border))
            using (var lanePen = new Pen(Color.FromArgb(100, StudioDesignSystem.Border)))
            {
                graphics.FillRectangle(deck, viewport);
                graphics.DrawRectangle(border, viewport);
                DrawLaneGrid(graphics, viewport, Math.Min(32, _projection.LaneCount), lanePen);
            }

            int playhead = viewport.Left + viewport.Width / 2;
            using (var glow = new Pen(Color.FromArgb(58, StudioDesignSystem.BeatViolet), 8f))
            using (var line = new Pen(StudioDesignSystem.BeatViolet, 2f))
            {
                graphics.DrawLine(glow, playhead, viewport.Top, playhead, viewport.Bottom);
                graphics.DrawLine(line, playhead, viewport.Top, playhead, viewport.Bottom);
            }

            foreach (ProjectedGameplayNote note in _frame.Notes)
            {
                if (note.X <= 0.04 || note.X >= 0.96) continue;
                int x = viewport.Left + (int)Math.Round(note.X * viewport.Width);
                int y = viewport.Top + (int)Math.Round(note.Y * viewport.Height);
                int size = Math.Max(6, (int)Math.Round(12 * _noteZoom));
                Color color = note.State == GameplayPreviewNoteState.Active
                    ? StudioDesignSystem.PulseCyan
                    : StudioDesignSystem.Muted;
                using (var brush = new SolidBrush(color))
                {
                    graphics.FillEllipse(brush, x - size / 2, y - size / 2, size, size);
                }
            }
        }

        private void DrawLaneGrid(
            Graphics graphics,
            Rectangle rectangle,
            int lanes,
            Pen pen)
        {
            lanes = Math.Max(1, lanes);
            for (int lane = 1; lane < lanes; lane++)
            {
                int y = rectangle.Top + (rectangle.Height * lane / lanes);
                graphics.DrawLine(pen, rectangle.Left, y, rectangle.Right, y);
            }
        }
    }
}
