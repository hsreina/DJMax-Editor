using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using DJMaxEditor.Diagnostics;

namespace DJMaxEditor.Preview
{
    /// <summary>
    /// One note glyph: a single image, or the horizontal strip of frames the arcade animates it
    /// from. A GDI+ port of the reference project's TechnikaNoteSprite: every animated piece in
    /// the TECHNIKA note set is a ten frame loop, cropped once and handed out by position in the
    /// loop.
    ///
    /// <para>Every file in the set is the same ten frames; what differs is how wide one frame is.
    /// A head is square - ninety, a hundred and sixteen - a cap is narrower than it is tall, and
    /// a body is a single column of pixels, the cross-section the run is drawn out of. See
    /// <see cref="FrameWidthOf"/>: the frame count is the constant and the width is derived from
    /// it, which is the way round the TechMania skin manifest for this set states it too
    /// (<c>"columns": 10</c> on every entry, whatever the file's dimensions).</para>
    /// </summary>
    internal sealed class TechnikaNoteSprite
    {
        /// <summary>Frames in an arcade note strip. Every animated piece in the TECHNIKA note set
        /// is a ten frame loop; the packaged sheets this renderer draws are all ten.</summary>
        private const int StripFrames = 10;

        /// <summary>
        /// How much of a cap frame's width is the body cross-section, as a fraction. Two pixels of
        /// a thirty pixel frame, which is what the legacy renderer takes from <c>longnoteline</c>.
        /// Only the drag curve needs this: the other two hold families are authored with a body
        /// sheet of their own and take their cross-section from that instead.
        /// </summary>
        private const double StemFraction = 0.08;

        private readonly Image[] _frames;

        private Image[] _stems;

        private double[] _sweepOffsets;

        private int _peakFrame = -1;

        private TechnikaNoteSprite(Image[] frames, double frameSize, double frameWidth)
        {
            _frames = frames;
            FrameSize = frameSize;
            FrameWidth = frameWidth;
        }

        /// <summary>Frames in the loop. One for a still image.</summary>
        public int FrameCount
        {
            get { return _frames.Length; }
        }

        /// <summary>
        /// Edge of one frame in the sprite's own pixels. Exposed so one sprite can be scaled
        /// against another - the arcade authors its ring larger than the head it surrounds -
        /// rather than against a constant that would be wrong for a different note set.
        /// </summary>
        public double FrameSize { get; private set; }

        /// <summary>
        /// One frame's width in the sprite's own pixels - the same as <see cref="FrameSize"/> for
        /// a strip of square frames, and the whole image for art that is not one. Exposed so a
        /// piece meant to be repeated along a run, like the dotted bar between repeat notes, can
        /// be tiled at its authored aspect instead of stretched to whatever length the run happens
        /// to be.
        /// </summary>
        public double FrameWidth { get; private set; }

        /// <summary>
        /// The frame at a position in a repeating loop. <paramref name="phase"/> is wrapped, so a
        /// caller can hand over a musical position that keeps counting up and never has to do the
        /// modulo itself.
        /// </summary>
        public Image Frame(double phase)
        {
            return _frames[IndexOf(phase)];
        }

        /// <summary>
        /// The leftmost columns of the frame at <paramref name="phase"/>, to be stretched along a
        /// hold's body while the frame itself closes the far end. The left edge is the cut through
        /// the tube: the flat side that abuts the note, as against the rounded outer edge on the
        /// right. Stretching from the other end would smear the outline down the whole body.
        /// </summary>
        public Image Stem(double phase)
        {
            int index = IndexOf(phase);

            if (_stems == null)
            {
                _stems = new Image[_frames.Length];
            }
            if (_stems[index] != null)
            {
                return _stems[index];
            }

            Image stem = _frames[index];
            var bitmap = stem as Bitmap;
            if (bitmap != null && bitmap.Width > 1)
            {
                int columns = (int)Math.Round(bitmap.Width * StemFraction);
                if (columns < 1) { columns = 1; }
                if (columns > bitmap.Width) { columns = bitmap.Width; }
                try
                {
                    stem = bitmap.Clone(
                        new Rectangle(0, 0, columns, bitmap.Height),
                        PixelFormat.Format32bppArgb);
                }
                catch (ArgumentException)
                {
                    // A frame that will not crop is not worth losing the body over - stretching
                    // the whole frame is wrong but visible, and the cap still draws.
                }
                catch (OutOfMemoryException)
                {
                    // GDI+ reports an invalid crop rectangle as OutOfMemory: same answer.
                }
            }

            _stems[index] = stem;
            return stem;
        }

        /// <summary>
        /// The frame that puts the most light on screen, measured from the pixels the first
        /// time it is asked for. The arcade's approach glow does not build to its end:
        /// <c>note_circle</c> opens from a sliver to a full ring across its first half and
        /// closes back across its second, so a one-shot approach has to finish on the peak
        /// in the middle, not on the last frame. Measured rather than assumed to be the
        /// midpoint, because where a strip peaks is a property of whichever note set is
        /// loaded.
        /// </summary>
        public int PeakFrame
        {
            get
            {
                if (_peakFrame < 0)
                {
                    _peakFrame = FindPeakFrame();
                }
                return _peakFrame;
            }
        }

        /// <summary>
        /// The frame at a one-shot progress across the whole sequence: 0 gives the first
        /// frame and 1 the last. Clamped, not wrapped - an effect played once must not
        /// restart on its final pixel. This is what the CoolBomb burst indexes by.
        /// </summary>
        public Image Shot(double progress)
        {
            if (_frames.Length == 1 || double.IsNaN(progress) || progress <= 0.0)
            {
                return _frames[0];
            }
            if (progress >= 1.0)
            {
                return _frames[_frames.Length - 1];
            }
            int index = (int)(progress * _frames.Length);
            if (index < 0) { index = 0; }
            if (index >= _frames.Length) { index = _frames.Length - 1; }
            return _frames[index];
        }

        /// <summary>
        /// The frame at a one-shot progress that finishes on <see cref="PeakFrame"/>: 0
        /// gives the first frame and 1 the brightest. Clamped rather than wrapped, so a
        /// glow played once over an approach cannot snap back to a sliver on its last pixel.
        /// </summary>
        public Image Swell(double progress)
        {
            if (_frames.Length == 1 || double.IsNaN(progress) || progress <= 0.0)
            {
                return _frames[0];
            }
            int peak = PeakFrame;
            if (progress >= 1.0)
            {
                return _frames[peak];
            }
            int index = (int)Math.Round(progress * peak);
            if (index < 0) { index = 0; }
            if (index > peak) { index = peak; }
            return _frames[index];
        }

        /// <summary>
        /// The frame whose light sits <paramref name="offsetInFrames"/> of a frame width from
        /// the note's centre, or null when the strip's light never reaches that far - so a
        /// caller draws nothing rather than the nearest thing it has. <c>note_circle</c> is
        /// not a ring that opens on the spot: measured across its twenty frames the light
        /// starts at 12% of the frame width and finishes at 87%, always vertically centred -
        /// a crescent the sweep drags through the note, left to right, fattening as it
        /// crosses. Asked for by where the sweep actually is, it is the piece of light it
        /// was drawn as, and it appears only while the sweep is genuinely over the note.
        /// Offsets are measured off the pixels, once, because where a strip's light sits is
        /// a property of whichever note set is loaded.
        /// </summary>
        public Image Sweep(double offsetInFrames)
        {
            if (_frames.Length == 1)
            {
                return _frames[0];
            }

            double[] offsets = SweepOffsets();
            int best = -1;
            double nearest = 0.0;
            for (int i = 0; i < offsets.Length; i++)
            {
                double gap = Math.Abs(offsets[i] - offsetInFrames);
                if (best < 0 || gap < nearest)
                {
                    best = i;
                    nearest = gap;
                }
            }

            // Outside the strip's own travel by more than the step between two of its
            // frames, the honest answer is that this strip has no picture of the sweep
            // being there.
            double step = Math.Abs(offsets[offsets.Length - 1] - offsets[0]) /
                Math.Max(1, offsets.Length - 1);
            if (best < 0 || nearest > Math.Max(step, 1e-6))
            {
                return null;
            }
            return _frames[best];
        }

        /// <summary>
        /// Where each frame's light sits, as a signed fraction of the frame width from its
        /// centre. Alpha-weighted, so a wide faint arc and a narrow bright one are placed by
        /// where the light actually is rather than by the extent of the pixels that are not
        /// quite transparent.
        /// </summary>
        private double[] SweepOffsets()
        {
            if (_sweepOffsets == null)
            {
                var offsets = new double[_frames.Length];
                for (int i = 0; i < _frames.Length; i++)
                {
                    offsets[i] = Centroid(_frames[i]);
                }
                _sweepOffsets = offsets;
            }
            return _sweepOffsets;
        }

        /// <summary>
        /// The alpha-weighted horizontal centre of a frame's light, as a signed fraction of
        /// its width from the middle. Zero when nothing can be measured, which places an
        /// unreadable frame on the note rather than off the side of it.
        /// </summary>
        private static double Centroid(Image frame)
        {
            var bitmap = frame as Bitmap;
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return 0.0;
            }

            try
            {
                Bitmap converted = null;
                try
                {
                    converted = new Bitmap(
                        bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
                    using (var graphics = Graphics.FromImage(converted))
                    {
                        graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                    }

                    BitmapData data = converted.LockBits(
                        new Rectangle(0, 0, converted.Width, converted.Height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);
                    try
                    {
                        double weight = 0.0;
                        double moment = 0.0;
                        int width = converted.Width;
                        int height = converted.Height;
                        var rowBytes = new byte[Math.Abs(data.Stride)];
                        for (int y = 0; y < height; y++)
                        {
                            Marshal.Copy(
                                new IntPtr(data.Scan0.ToInt64() + ((long)y * data.Stride)),
                                rowBytes, 0, rowBytes.Length);
                            int rowOffset = 0;
                            for (int x = 0; x < width; x++)
                            {
                                double alpha = rowBytes[rowOffset + 3];
                                weight += alpha;
                                moment += alpha * x;
                                rowOffset += 4;
                            }
                        }
                        if (weight <= 0.0)
                        {
                            return 0.0;
                        }
                        return ((moment / weight) - ((width - 1) / 2.0)) / width;
                    }
                    finally
                    {
                        converted.UnlockBits(data);
                    }
                }
                finally
                {
                    if (converted != null)
                    {
                        converted.Dispose();
                    }
                }
            }
            catch (ArgumentException)
            {
                return 0.0;
            }
            catch (OutOfMemoryException)
            {
                return 0.0;
            }
        }

        /// <summary>
        /// A sprite whose frames were authored as separate numbered files rather than as one
        /// strip - which is how the arcade keeps every effect under <c>CoolBomb</c>,
        /// twenty-three files for the tap burst alone. The frames are taken in the order
        /// given; the first one's dimensions are the frame size, since a sequence's files
        /// are one canvas throughout.
        /// </summary>
        internal static TechnikaNoteSprite Sequence(IList<Image> frames)
        {
            if (frames == null || frames.Count == 0)
            {
                return null;
            }

            var copy = new Image[frames.Count];
            for (int i = 0; i < frames.Count; i++)
            {
                copy[i] = frames[i];
            }

            double height = copy[0].Height;
            double width = copy[0].Width;
            return new TechnikaNoteSprite(copy, height, width);
        }

        /// <summary>The brightest frame's index, or the midpoint when the measurement cannot
        /// single one out. Never frame 0 for a strip: a peak at the very start would collapse
        /// <see cref="Swell"/> onto one image, and a strip whose frames all measure the same
        /// is one this cannot speak about - the midpoint is where every arcade strip peaks
        /// anyway.</summary>
        private int FindPeakFrame()
        {
            int best = 0;
            long brightest = -1;
            for (int i = 0; i < _frames.Length; i++)
            {
                long light = Light(_frames[i]);
                if (light > brightest)
                {
                    brightest = light;
                    best = i;
                }
            }
            return best > 0 ? best : _frames.Length / 2;
        }

        /// <summary>Alpha weighted by colour: how much light a frame actually puts on screen.
        /// Transparency alone would not separate a wide faint ring from a narrow bright one,
        /// and colour alone would count pixels that are not drawn at all.</summary>
        private static long Light(Image frame)
        {
            var bitmap = frame as Bitmap;
            if (bitmap == null)
            {
                return 0;
            }

            try
            {
                Bitmap converted = null;
                try
                {
                    converted = new Bitmap(
                        bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb);
                    using (var graphics = Graphics.FromImage(converted))
                    {
                        graphics.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
                    }

                    BitmapData data = converted.LockBits(
                        new Rectangle(0, 0, converted.Width, converted.Height),
                        ImageLockMode.ReadOnly,
                        PixelFormat.Format32bppArgb);
                    try
                    {
                        long total = 0;
                        var rowBytes = new byte[Math.Abs(data.Stride)];
                        for (int y = 0; y < converted.Height; y++)
                        {
                            Marshal.Copy(
                                new IntPtr(data.Scan0.ToInt64() + ((long)y * data.Stride)),
                                rowBytes, 0, rowBytes.Length);
                            for (int i = 0; i + 3 < rowBytes.Length; i += 4)
                            {
                                total += rowBytes[i + 3] *
                                    ((long)rowBytes[i] + rowBytes[i + 1] + rowBytes[i + 2]);
                            }
                        }
                        return total;
                    }
                    finally
                    {
                        converted.UnlockBits(data);
                    }
                }
                finally
                {
                    if (converted != null)
                    {
                        converted.Dispose();
                    }
                }
            }
            catch (ArgumentException)
            {
                return 0;
            }
            catch (OutOfMemoryException)
            {
                return 0;
            }
        }

        private int IndexOf(double phase)
        {
            if (_frames.Length == 1 || double.IsNaN(phase) || double.IsInfinity(phase))
            {
                return 0;
            }

            double wrapped = phase - Math.Floor(phase);
            int index = (int)(wrapped * _frames.Length);
            if (index < 0) { index = 0; }
            if (index >= _frames.Length) { index = _frames.Length - 1; }
            return index;
        }

        /// <summary>
        /// Slices a horizontal strip into its frames, or keeps the image whole when it is not a
        /// strip. <see cref="FrameWidthOf"/> decides which it is; a single glyph comes back as a
        /// strip of one, so a strip and a still image drop in through the same path. Frames are
        /// cloned out of the source bitmap, so the shared resource image is never drawn against
        /// and never disposed.
        /// </summary>
        internal static TechnikaNoteSprite Slice(Image source)
        {
            int width = source.Width;
            int height = source.Height;
            if (width <= 0 || height <= 0)
            {
                return new TechnikaNoteSprite(new Image[] { source }, height, width);
            }

            int frameWidth = FrameWidthOf(width, height);
            if (frameWidth <= 0)
            {
                return new TechnikaNoteSprite(new Image[] { source }, height, width);
            }

            var bitmap = source as Bitmap;
            if (bitmap == null)
            {
                return new TechnikaNoteSprite(new Image[] { source }, height, width);
            }

            int count = width / frameWidth;
            var frames = new Image[count];
            for (int i = 0; i < count; i++)
            {
                frames[i] = bitmap.Clone(
                    new Rectangle(i * frameWidth, 0, frameWidth, height),
                    PixelFormat.Format32bppArgb);
            }
            return new TechnikaNoteSprite(frames, height, frameWidth);
        }

        /// <summary>
        /// One frame's width in a horizontal strip, or 0 for art that is a single image.
        ///
        /// <para>Ten frames first, because that is what the whole note set is: <c>Note_Basic</c>
        /// is ten 90x90 in a 900x90 sheet, <c>longnoteline</c> ten 30x76, <c>long_note_end</c>
        /// ten 25x90, and <c>long_note_line</c> ten 1x90 in a sheet ten pixels wide. Squareness is
        /// not the test - it never was; it is a coincidence that holds for the heads.</para>
        ///
        /// <para>Reading those ten pixel sheets as one image instead is what the playfield's worst
        /// artefact was. They are the bodies a hold or a run is drawn out of, and each column is a
        /// different frame of the same pulse: kept whole and stretched along a note's duration, all
        /// ten columns paint at once - a banded ten-shade ramp down the length of every hold,
        /// frozen there - instead of one shade animating.</para>
        ///
        /// <para>A frame wider than the sheet is tall means those ten were not frames, and a sheet
        /// that is square is one glyph and nothing else: 90x90 divides by ten as readily as
        /// 900x90 does, and reading a lone <c>videoStart</c> as ten nine-pixel frames is the same
        /// mistake in the other direction.</para>
        /// </summary>
        private static int FrameWidthOf(int width, int height)
        {
            if (width == height)
            {
                return 0;
            }
            if (width % StripFrames == 0 && width / StripFrames <= height)
            {
                return width / StripFrames;
            }
            if (width % height == 0)
            {
                return height;
            }
            return 0;
        }
    }

    /// <summary>
    /// Note glyphs for the TECHNIKA playfield, resolved once per kind and cached. A GDI+ port of
    /// the reference project's TechnikaNoteSprites, with one source instead of two: the arcade
    /// sheets already compiled into this assembly's resource table, sliced the same way a local
    /// extraction would be. The run lines, actively-held trail variants and hold caps are all in
    /// the table too, so a fresh build gets real note art for every kind with no setup at all.
    /// Only the approach ring (<c>note_circle</c>) and the CoolBomb hit sequence are absent and
    /// keep their existing vector fallbacks.
    ///
    /// <para>Which folder (or resource table) the sheets come from is a
    /// <see cref="TechnikaSpriteStyle"/>: <see cref="ForStyle"/> pins an instance to one style,
    /// while <see cref="Load"/> keeps the pre-style behaviour of probing for a local root on
    /// its own. Every loading, slicing and caching rule below is identical for both - a style
    /// only decides where the files are read from.</para>
    ///
    /// <para>One process-wide instance, like the theme's baked bitmaps: the underlying sheets are
    /// themselves cached resource bitmaps, and the clones each sprite hands out are owned by this
    /// cache for the life of the process, matching the timeline art's scaled-image cache.</para>
    /// </summary>
    internal sealed class TechnikaNoteSprites
    {
        /// <summary>
        /// Packaged glyph per note kind, keyed by the arcade's own file name: the same sheets the
        /// legacy editor timeline draws, reached through the resource table. That matters most for
        /// <see cref="GameplayPreviewNoteKind.Drag"/>: the old six-glyph TechMania set had no
        /// slide note at all and handed it the blue hold head, which made the preview report
        /// every drag as a hold. The strips are sliced exactly as a local extraction would be.
        /// </summary>
        private static readonly Dictionary<GameplayPreviewNoteKind, string> PackagedGlyphs =
            new Dictionary<GameplayPreviewNoteKind, string>
            {
                { GameplayPreviewNoteKind.Basic, "Note_Basic" },
                { GameplayPreviewNoteKind.Drag, "longnote" },
                { GameplayPreviewNoteKind.Generic, "Note_Basic" },
                { GameplayPreviewNoteKind.ChainHead, "notepressstart" },
                { GameplayPreviewNoteKind.ChainNode, "notepressnote" },
                { GameplayPreviewNoteKind.Hold, "longnotehold" },
                { GameplayPreviewNoteKind.RepeatHead, "noterepeat" },
                { GameplayPreviewNoteKind.RepeatHeadHold, "noterepeat" },
                { GameplayPreviewNoteKind.Repeat, "repeattail" },
                { GameplayPreviewNoteKind.RepeatHold, "repeattail" },
            };

        /// <summary>
        /// The cap that closes a held note's body at the far end, per kind. One file per long
        /// family: attribute 0's drag curve is <c>longnoteline</c>, attribute 12's hold closes
        /// with <c>line_nor_end</c>, and a repeat carrying a duration closes with
        /// <c>long_note_end</c>.
        /// </summary>
        private static readonly Dictionary<GameplayPreviewNoteKind, string> ArcadeTrailCaps =
            new Dictionary<GameplayPreviewNoteKind, string>
            {
                { GameplayPreviewNoteKind.Drag, "longnoteline" },
                { GameplayPreviewNoteKind.Hold, "line_nor_end" },
                { GameplayPreviewNoteKind.RepeatHeadHold, "long_note_end" },
                { GameplayPreviewNoteKind.RepeatHold, "long_note_end" },
            };

        /// <summary>
        /// The body a held note runs between its head and its cap, per kind: the arcade's own
        /// cross-section sheet, ten frames one pixel wide. No entry for the drag curve: the
        /// arcade authors that one as a cap alone, so its body stays a cut through it. The body
        /// carries a per-frame colour ramp no cut through the cap can reproduce.
        /// </summary>
        private static readonly Dictionary<GameplayPreviewNoteKind, string> ArcadeTrailBodies =
            new Dictionary<GameplayPreviewNoteKind, string>
            {
                { GameplayPreviewNoteKind.Hold, "longholdgauge" },
                { GameplayPreviewNoteKind.RepeatHeadHold, "long_note_line" },
                { GameplayPreviewNoteKind.RepeatHold, "long_note_line" },
            };

        /// <summary>What a hold's body becomes while it is actually being held - the same shape
        /// lit. Kinds absent from this table keep their resting body throughout, which is all the
        /// arcade gives them.</summary>
        private static readonly Dictionary<GameplayPreviewNoteKind, string> ArcadeHeldTrailBodies =
            new Dictionary<GameplayPreviewNoteKind, string>
            {
                { GameplayPreviewNoteKind.Hold, "longholdgaugein" },
            };

        /// <summary>The cap that goes with <see cref="ArcadeHeldTrailBodies"/>: <c>line_in_end</c>
        /// is <c>line_nor_end</c>'s "in" to <c>longholdgaugein</c>'s.</summary>
        private static readonly Dictionary<GameplayPreviewNoteKind, string> ArcadeHeldTrailCaps =
            new Dictionary<GameplayPreviewNoteKind, string>
            {
                { GameplayPreviewNoteKind.Hold, "line_in_end" },
            };

        /// <summary>
        /// The line the arcade runs between the members of a group. A chain and a repeat run are
        /// several notes joined by one bar, not one note with a duration, so the line is art in
        /// its own right rather than a stretched head.
        /// </summary>
        private static readonly Dictionary<GameplayPreviewNoteKind, string> ArcadeLines =
            new Dictionary<GameplayPreviewNoteKind, string>
            {
                { GameplayPreviewNoteKind.ChainHead, "notepressline" },
                { GameplayPreviewNoteKind.ChainNode, "notepressline" },
                { GameplayPreviewNoteKind.RepeatHead, "noterepeatline" },
                { GameplayPreviewNoteKind.RepeatHeadHold, "noterepeatline" },
                { GameplayPreviewNoteKind.Repeat, "noterepeatline" },
                { GameplayPreviewNoteKind.RepeatHold, "noterepeatline" },
            };

        /// <summary>Environment override for the local sprite folder, for owners who keep it
        /// elsewhere.</summary>
        private const string PathVariable = "DJMAX_EDITOR_TECHNIKA_ASSETS";

        /// <summary>
        /// The glow that opens around an approaching note. Arcade only - the packaged set has
        /// no equivalent, and the renderer tweens a vector ring when it is absent.
        /// </summary>
        private const string ArcadeRing = "note_circle";

        /// <summary>
        /// The arcade's folder of hit effects, and the one this draws:
        /// <c>CoolBomb\&lt;set&gt;\cool</c>, twenty-three files named <c>cool_0000.png</c>
        /// upward. The tap burst only: the set also carries good/max and a hold's separate
        /// animations, and a preview has no judgement to report, so the burst a note is
        /// struck with is the whole of what can honestly be drawn here.
        /// </summary>
        private const string ArcadeEffectFolder = "CoolBomb";
        private const string ArcadeHitEffect = "cool";

        /// <summary>
        /// How many folders to try when looking for the effect tree, counting the note folder
        /// itself. Five means four levels above it, which reaches <c>MainGame</c> from
        /// <c>MainGame\note\pop\0</c> - the furthest the arcade's own layout puts between
        /// them. Stopping there keeps a mistaken environment variable from walking a whole
        /// drive.
        /// </summary>
        private const int EffectSearchDepth = 5;

        private readonly Dictionary<GameplayPreviewNoteKind, TechnikaNoteSprite> _cache =
            new Dictionary<GameplayPreviewNoteKind, TechnikaNoteSprite>();

        private readonly Dictionary<GameplayPreviewNoteKind, TechnikaNoteSprite> _lines =
            new Dictionary<GameplayPreviewNoteKind, TechnikaNoteSprite>();

        private readonly Dictionary<TrailKey, TechnikaNoteSprite> _trailCaps =
            new Dictionary<TrailKey, TechnikaNoteSprite>();

        private readonly Dictionary<TrailKey, TechnikaNoteSprite> _trailBodies =
            new Dictionary<TrailKey, TechnikaNoteSprite>();

        /// <summary>
        /// Packaged sheets keyed by file name, so the trail caps/bodies and run lines resolve
        /// through one cache rather than reloading a strip for every kind that shares it. Nulls
        /// are cached too: a sheet the set does not carry would otherwise be rebuilt for every
        /// note of every frame.
        /// </summary>
        private readonly Dictionary<string, TechnikaNoteSprite> _packagedSheets =
            new Dictionary<string, TechnikaNoteSprite>(StringComparer.OrdinalIgnoreCase);

        private string _localRoot;

        /// <summary>The style's display name, reported through <see cref="SourceLabel"/> in
        /// place of the generic "ARCADE SPRITES" when a specific style drives this instance.
        /// Null for the classic probes, which resolve a root without a style.</summary>
        private readonly string _styleLabel;

        /// <summary>True when the root is pinned by an explicit style and must not move:
        /// <see cref="RefreshLocalRoot"/> becomes a no-op so a chart rebind cannot silently
        /// swap an owner-chosen skin for whatever the probe finds this time.</summary>
        private readonly bool _fixedRoot;

        private TechnikaNoteSprite _ring;
        private bool _ringResolved;

        private TechnikaNoteSprite _coolBomb;
        private bool _coolBombResolved;

        private TechnikaNoteSprites(string localRoot, string styleLabel, bool fixedRoot)
        {
            _localRoot = localRoot;
            _styleLabel = styleLabel;
            _fixedRoot = fixedRoot;
        }

        /// <summary>The local folder in use, or null when only packaged glyphs are
        /// available.</summary>
        public string LocalRoot
        {
            get { return _localRoot; }
        }

        /// <summary>True when the owner's own arcade sprites are being drawn.</summary>
        public bool UsesLocalAssets
        {
            get { return _localRoot != null; }
        }

        /// <summary>"ARCADE SPRITES" or "PACKAGED GLYPHS", for the panel header - or the
        /// style's own name when one drives this instance, so the header can say
        /// <c>T3 STAR #03</c> instead of a category.</summary>
        public string SourceLabel
        {
            get
            {
                if (_styleLabel != null)
                {
                    return _styleLabel;
                }
                return UsesLocalAssets ? "ARCADE SPRITES" : "PACKAGED GLYPHS";
            }
        }

        /// <summary>
        /// The glow strip drawn around an approaching note, or null when the local folder is
        /// absent or does not carry one. Null is a normal answer: the renderer tweens a
        /// vector ring instead, which is the same read at lower fidelity.
        /// </summary>
        public TechnikaNoteSprite Ring
        {
            get
            {
                if (!_ringResolved)
                {
                    _ringResolved = true;
                    _ring = LoadLocal(ArcadeRing);
                }
                return _ring;
            }
        }

        /// <summary>
        /// The burst the arcade fires where a note is struck, as its twenty-three frames, or
        /// null when the local folder is absent or carries no <c>CoolBomb</c> tree. Null is a
        /// normal answer: the renderer draws a fading ring instead, which says a note was hit
        /// here without claiming to be the arcade's art.
        /// </summary>
        public TechnikaNoteSprite CoolBomb
        {
            get
            {
                if (!_coolBombResolved)
                {
                    _coolBombResolved = true;
                    _coolBomb = LoadLocalSequence(ArcadeEffectFolder, ArcadeHitEffect);
                }
                return _coolBomb;
            }
        }

        /// <summary>
        /// The frame size every other glyph is measured against: the tap's, because the arcade
        /// draws its tap exactly one lane tall and authors everything else around it. Zero when
        /// nothing can be resolved, which a caller reads as "no normalisation available".
        /// </summary>
        public double ReferenceFrameSize
        {
            get
            {
                TechnikaNoteSprite basic = For(GameplayPreviewNoteKind.Basic);
                return basic == null ? 0.0 : basic.FrameSize;
            }
        }

        /// <summary>Eagerly loads every sheet the renderer can ask for - every note kind, run
        /// line, trail cap and body in both states, the approach ring and the hit-effect
        /// sequence - so a style switch pays the decode once on a loader thread instead of
        /// stalling the first paint. The caches above make the later real reads instant.</summary>
        internal void Warm()
        {
            var kinds = (GameplayPreviewNoteKind[])Enum.GetValues(typeof(GameplayPreviewNoteKind));
            for (int i = 0; i < kinds.Length; i++)
            {
                For(kinds[i]);
                Line(kinds[i]);
                TrailCap(kinds[i], false);
                TrailCap(kinds[i], true);
                TrailBody(kinds[i], false);
                TrailBody(kinds[i], true);
            }
            _ = Ring;
            _ = CoolBomb;
            _ = ReferenceFrameSize;
        }

        public static TechnikaNoteSprites Load()
        {
            return new TechnikaNoteSprites(FindLocalRoot(), null, false);
        }

        /// <summary>
        /// The sprites for one catalog style. The packaged style pins the instance to the
        /// resource table (localRoot null, the classic fallback path); a named local style pins
        /// it to that style's folder and freezes the root; AUTO delegates to <see cref="Load"/>,
        /// which re-probes the filesystem exactly as it did before styles existed. Every other
        /// load goes through the same code after this choice - the style is purely where the
        /// files come from.
        /// </summary>
        public static TechnikaNoteSprites ForStyle(TechnikaSpriteStyle style)
        {
            if (style == null || style.IsAuto)
            {
                return Load();
            }
            if (style.IsPackaged)
            {
                return new TechnikaNoteSprites(null, null, true);
            }
            return new TechnikaNoteSprites(style.NoteRoot, style.DisplayName, true);
        }

        /// <summary>
        /// How much larger than the lane box one strip's glyph is authored, so the renderer can
        /// draw it at the size it was drawn at. 1.0 when either side cannot be measured.
        ///
        /// <para>A whole note frame draws at the lane box exactly, whichever of the two mode
        /// pitches it was authored at, because that is what a note frame means - the reference is
        /// only needed to place art that is <em>not</em> a note frame, and the two are told apart
        /// by <see cref="TechnikaPlayfieldMetrics.IsNoteFrameSize"/>. Without that the ratio
        /// reads a 3-line glyph in a 4-line set as authored a third larger and draws a chain head
        /// over the note it leads.</para>
        /// </summary>
        public double ScaleOf(TechnikaNoteSprite sprite)
        {
            double reference = ReferenceFrameSize;
            if (sprite == null || sprite.FrameSize <= 0 || reference <= 0)
            {
                return 1.0;
            }
            if (TechnikaPlayfieldMetrics.IsNoteFrameSize(sprite.FrameSize))
            {
                return 1.0;
            }
            return sprite.FrameSize / reference;
        }

        /// <summary><see cref="ScaleOf"/> for the glyph a kind is drawn with. 1.0 when the set has
        /// no art for the kind.</summary>
        public double ScaleFor(GameplayPreviewNoteKind kind)
        {
            return ScaleOf(For(kind));
        }

        /// <summary>
        /// The glyph for a note kind, or null if the sheet is absent. Null is a normal answer,
        /// not a failure: the renderer falls back to the timeline's static art and then to vector
        /// shapes, which is what keeps a missing image from blanking the preview.
        /// </summary>
        public TechnikaNoteSprite For(GameplayPreviewNoteKind kind)
        {
            TechnikaNoteSprite cached;
            if (_cache.TryGetValue(kind, out cached))
            {
                return cached;
            }

            string name;
            if (!PackagedGlyphs.TryGetValue(kind, out name))
            {
                _cache[kind] = null;
                return null;
            }

            // The owner's own extraction wins: same file names, so a copied arcade tree
            // drops in without renaming. The packaged sheet is the fallback that keeps a
            // fresh build on real art.
            TechnikaNoteSprite resolved = LoadLocal(name);
            if (resolved == null)
            {
                resolved = LoadPackagedSheet(name);
            }
            _cache[kind] = resolved;
            return resolved;
        }

        /// <summary>
        /// The cap that closes a held note's body, or null when the kind is not a hold or the
        /// sheet is absent. <paramref name="ongoing"/> asks for the actively-held variant, and
        /// falls back to the resting one where the set has no separate art for it.
        /// </summary>
        public TechnikaNoteSprite TrailCap(GameplayPreviewNoteKind kind, bool ongoing)
        {
            return Resolve(_trailCaps, ArcadeTrailCaps, ArcadeHeldTrailCaps, kind, ongoing);
        }

        /// <summary>
        /// The cross-section a held note's body is run out of, or null when the kind has no body
        /// sheet - the drag curve, whose body is a cut through its cap. <paramref name="ongoing"/>
        /// asks for the actively-held variant.
        /// </summary>
        public TechnikaNoteSprite TrailBody(GameplayPreviewNoteKind kind, bool ongoing)
        {
            return Resolve(_trailBodies, ArcadeTrailBodies, ArcadeHeldTrailBodies, kind, ongoing);
        }

        /// <summary>
        /// The line joining the members of a chain or repeat run, or null when the kind has no
        /// line. Null is a normal answer: the renderer falls back to a themed bar, which joins
        /// the same two points at lower fidelity.
        /// </summary>
        public TechnikaNoteSprite Line(GameplayPreviewNoteKind kind)
        {
            TechnikaNoteSprite cached;
            if (_lines.TryGetValue(kind, out cached))
            {
                return cached;
            }

            string name;
            if (!ArcadeLines.TryGetValue(kind, out name))
            {
                _lines[kind] = null;
                return null;
            }

            TechnikaNoteSprite resolved = LoadLocal(name);
            if (resolved == null)
            {
                resolved = LoadPackagedSheet(name);
            }
            _lines[kind] = resolved;
            return resolved;
        }

        /// <summary>
        /// One entry of a two-state art table, cached per kind and state. The held table is
        /// consulted first and the resting one is the fallback, so a set that has no "in" variant
        /// of a piece simply keeps drawing the one it has rather than losing the piece.
        /// </summary>
        private TechnikaNoteSprite Resolve(
            Dictionary<TrailKey, TechnikaNoteSprite> cache,
            Dictionary<GameplayPreviewNoteKind, string> resting,
            Dictionary<GameplayPreviewNoteKind, string> held,
            GameplayPreviewNoteKind kind,
            bool ongoing)
        {
            var key = new TrailKey(kind, ongoing);
            TechnikaNoteSprite cached;
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            string name;
            TechnikaNoteSprite resolved = null;
            if (ongoing && held.TryGetValue(kind, out name))
            {
                resolved = LoadSheet(name);
            }
            if (resolved == null && resting.TryGetValue(kind, out name))
            {
                resolved = LoadSheet(name);
            }
            cache[key] = resolved;
            return resolved;
        }

        /// <summary>The local extraction of one sheet by the arcade's own file name, or null
        /// when there is no local folder or it carries no file of that name.</summary>
        private TechnikaNoteSprite LoadSheet(string name)
        {
            return LoadLocal(name) ?? LoadPackagedSheet(name);
        }

        private Bitmap LoadLocalBitmap(string name)
        {
            if (_localRoot == null || string.IsNullOrEmpty(name))
            {
                return null;
            }
            string path = Path.Combine(_localRoot, name + ".png");
            return File.Exists(path) ? LoadBitmapFile(path) : null;
        }

        /// <summary>A private, fully decoded copy of one image file. Reading through a
        /// stream and cloning matters: a Bitmap created straight from a path keeps the file
        /// locked for its whole lifetime, which would hold every sprite file open against
        /// the next extraction replacing it.</summary>
        private static Bitmap LoadBitmapFile(string path)
        {
            try
            {
                using (var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (Image raw = Image.FromStream(stream))
                {
                    return new Bitmap(raw);
                }
            }
            catch (IOException ex)
            {
                DiagnosticLog.Exception("technika.sprite", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticLog.Exception("technika.sprite", ex);
            }
            catch (NotSupportedException ex)
            {
                // A file that is not actually a decodable image. Fall through to the
                // packaged glyph rather than losing the note.
                DiagnosticLog.Exception("technika.sprite", ex);
            }
            catch (ArgumentException ex)
            {
                DiagnosticLog.Exception("technika.sprite", ex);
            }
            return null;
        }

        /// <summary>One local sheet by the arcade's own file name, sliced and cached like the
        /// packaged ones.</summary>
        private TechnikaNoteSprite LoadLocal(string name)
        {
            Bitmap bitmap = LoadLocalBitmap(name);
            return bitmap == null ? null : TechnikaNoteSprite.Slice(bitmap);
        }

        /// <summary>
        /// One effect's frames, read from <paramref name="folder"/> beside the note art as
        /// <c>&lt;prefix&gt;_0000.png</c> upward, or null when that folder is not there. The
        /// arcade keeps its effects in their own tree next to the notes, so this looks for
        /// <paramref name="folder"/> at the note root and then in each parent above it: an
        /// owner who copies one folder of PNGs across and an owner who copies the whole
        /// <c>MainGame</c> tree both end up with a working burst.
        /// </summary>
        private TechnikaNoteSprite LoadLocalSequence(string folder, string prefix)
        {
            string directory = FindEffectFolder(folder, prefix);
            if (directory == null)
            {
                return null;
            }

            try
            {
                string[] files = Directory.GetFiles(directory, prefix + "_*.png");
                // The arcade zero-pads its frame numbers to four digits, so name order is
                // frame order and there is no number to parse.
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                var frames = new List<Image>(files.Length);
                for (int i = 0; i < files.Length; i++)
                {
                    // The resolved folder can sit above the note root (the tree walk climbs
                    // parents), so load by the full path, not through the note root.
                    Bitmap frame = LoadBitmapFile(files[i]);
                    if (frame != null)
                    {
                        frames.Add(frame);
                    }
                }
                return TechnikaNoteSprite.Sequence(frames);
            }
            catch (IOException ex)
            {
                DiagnosticLog.Exception("technika.sprite", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                DiagnosticLog.Exception("technika.sprite", ex);
            }
            return null;
        }

        private string FindEffectFolder(string folder, string prefix)
        {
            if (_localRoot == null)
            {
                return null;
            }

            // The note root's own leaf is the set the glyphs came from - "0" in an arcade
            // tree - and the burst should come from the same one, so the picture stays of
            // one piece.
            string preferredSet = Path.GetFileName(_localRoot);
            string current = _localRoot;
            for (int depth = 0; depth < EffectSearchDepth && current != null; depth++)
            {
                string resolved = ResolveEffectFolder(
                    Path.Combine(current, folder), prefix, preferredSet);
                if (resolved != null)
                {
                    return resolved;
                }

                // An unwrapped copy: the arcade keeps its frames under a folder named like
                // the effect (CoolBomb\0\cool), but a copy of one set has cool\ hanging
                // straight off the set root. The wrapped shape above keeps precedence.
                resolved = ResolveEffectFolder(current, prefix, preferredSet);
                if (resolved != null)
                {
                    return resolved;
                }
                current = Path.GetDirectoryName(current);
            }
            return null;
        }

        private static string ResolveEffectFolder(
            string candidate, string prefix, string preferredSet)
        {
            try
            {
                if (!Directory.Exists(candidate))
                {
                    return null;
                }

                // A flat copy of one effect: CoolBomb\cool\cool_0000.png.
                string direct = Path.Combine(candidate, prefix);
                if (HasFrames(direct, prefix))
                {
                    return Path.GetFullPath(direct);
                }

                // The arcade's own shape, one numbered set per folder: CoolBomb\0\cool\.
                if (!string.IsNullOrWhiteSpace(preferredSet))
                {
                    string preferred = Path.Combine(candidate, preferredSet, prefix);
                    if (HasFrames(preferred, prefix))
                    {
                        return Path.GetFullPath(preferred);
                    }
                }

                string[] children = Directory.GetDirectories(candidate);
                Array.Sort(children, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < children.Length; i++)
                {
                    string nested = Path.Combine(children[i], prefix);
                    if (HasFrames(nested, prefix))
                    {
                        return Path.GetFullPath(nested);
                    }
                }
            }
            catch (IOException)
            {
                // An unreadable candidate is simply not the folder we are looking for.
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
                // A set name that is not a legal path fragment.
            }
            return null;
        }

        private static bool HasFrames(string directory, string prefix)
        {
            try
            {
                return Directory.Exists(directory) &&
                    Directory.GetFiles(directory, prefix + "_*.png").Length > 0;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static string FindLocalRoot()
        {
            string root = ProbeLocalRoot();
            if (root == null)
            {
                ReportMissingRoot();
            }
            return root;
        }

        /// <summary>
        /// The local sprite root the classic probe resolves, without the missing-root
        /// diagnostic: the sprite catalog lists the legacy source as a style and must be
        /// able to ask "is there one?" quietly on every dropdown open.
        /// </summary>
        internal static string ProbeLocalRoot()
        {
            foreach (string candidate in CandidatePaths())
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }
                string resolved = ResolveRoot(candidate);
                if (resolved != null)
                {
                    return resolved;
                }
            }
            return null;
        }

        private static bool _missingRootReported;

        /// <summary>
        /// One line in the diagnostic log naming the folders that were looked in, so "I
        /// dropped the sprites and nothing changed" has an answer besides the source label.
        /// </summary>
        private static void ReportMissingRoot()
        {
            if (_missingRootReported)
            {
                return;
            }
            _missingRootReported = true;
            DiagnosticLog.Write(
                "technika.sprite",
                "no local sprite root; checked: " +
                string.Join(" | ", CandidatePaths()));
        }

        /// <summary>
        /// Re-probes the filesystem for a local sprite root. Called when a chart is (re)bound
        /// so sprites dropped in while the editor is already running are picked up without a
        /// restart; cheap when nothing changed (a couple of directory checks per candidate).
        /// A no-op when an explicit style pinned the root: the owner chose this set, and a
        /// rebind must not trade it for whatever the probe would find today.
        /// </summary>
        public bool RefreshLocalRoot()
        {
            if (_fixedRoot)
            {
                return false;
            }

            string root = FindLocalRoot();
            if (string.Equals(root, _localRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            _localRoot = root;
            _cache.Clear();
            _lines.Clear();
            _trailCaps.Clear();
            _trailBodies.Clear();
            _packagedSheets.Clear();
            _ring = null;
            _ringResolved = false;
            _coolBomb = null;
            _coolBombResolved = false;
            return true;
        }

        /// <summary>
        /// A folder holding sprites, or its first subfolder that does. The arcade keeps six
        /// complete note sets side by side in numbered folders, so an owner who copies that
        /// tree across lands one level above the files; descending once means the copy works
        /// as-is. Effects live a level deeper still (<c>0\cool\cool_0000.png</c>), and a copy
        /// of just that tree declares no glyphs at all: it is accepted as a root too - glyph
        /// lookups come back null and the renderer draws the packaged set, while the effect
        /// resolver finds the bursts by its own tree walk.
        /// </summary>
        private static string ResolveRoot(string candidate)
        {
            try
            {
                if (!Directory.Exists(candidate))
                {
                    return null;
                }
                if (Directory.GetFiles(candidate, "*.png").Length > 0)
                {
                    return Path.GetFullPath(candidate);
                }

                string[] children = Directory.GetDirectories(candidate);
                Array.Sort(children, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < children.Length; i++)
                {
                    if (Directory.GetFiles(children[i], "*.png").Length > 0)
                    {
                        return Path.GetFullPath(children[i]);
                    }
                }

                if (LooksLikeSpriteTree(candidate, children))
                {
                    return Path.GetFullPath(candidate);
                }
            }
            catch (IOException)
            {
                // An unreadable candidate is simply not the folder we are looking for.
            }
            catch (UnauthorizedAccessException)
            {
            }
            return null;
        }

        /// <summary>
        /// Whether any frame PNGs hang off the candidate within two levels - the depth a
        /// copied effect tree (<c>cool\cool_0000.png</c>, or one numbered set above it) puts
        /// them at. Two levels and no more, so "some random folder" is still not a sprite
        /// root.
        /// </summary>
        private static bool LooksLikeSpriteTree(string candidate, string[] children)
        {
            try
            {
                for (int i = 0; i < children.Length; i++)
                {
                    string[] grandchildren = Directory.GetDirectories(children[i]);
                    for (int j = 0; j < grandchildren.Length; j++)
                    {
                        if (Directory.GetFiles(grandchildren[j], "*.png").Length > 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return false;
        }

        private static IEnumerable<string> CandidatePaths()
        {
            yield return Environment.GetEnvironmentVariable(PathVariable);

            string directory = Path.GetDirectoryName(
                typeof(TechnikaNoteSprites).Assembly.Location);
            if (string.IsNullOrWhiteSpace(directory))
            {
                directory = AppDomain.CurrentDomain.BaseDirectory;
            }
            if (!string.IsNullOrWhiteSpace(directory))
            {
                yield return Path.Combine(directory, "LocalAssets", "Technika2");
            }
        }

        /// <summary>
        /// One packaged sheet by the arcade's own file name, from this assembly's resource table -
        /// the glossy arcade strips, sliced once and cached by name so a strip loads once no
        /// matter how many kinds share it. Returns null for a sheet the resource table does not
        /// carry, which leaves the caller on its themed fallback.
        /// </summary>
        private TechnikaNoteSprite LoadPackagedSheet(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            TechnikaNoteSprite cached;
            if (_packagedSheets.TryGetValue(name, out cached))
            {
                return cached;
            }

            TechnikaNoteSprite sprite = null;
            Bitmap sheet = LoadBitmap(name);
            if (sheet != null)
            {
                sprite = TechnikaNoteSprite.Slice(sheet);
            }

            // Cache nulls too: a kind without a sheet would otherwise reslice for every note of
            // every frame.
            _packagedSheets[name] = sprite;
            return sprite;
        }

        private static Bitmap LoadBitmap(string name)
        {
            // The resource getters return the cached shared bitmap; Slice clones its frames out,
            // so the shared image is never mutated and never disposed.
            switch (name)
            {
                case "Note_Basic": return Resources.Note_Basic;
                case "longnote": return Resources.longnote;
                case "longnotehold": return Resources.longnotehold;
                case "notepressstart": return Resources.notepressstart;
                case "notepressnote": return Resources.notepressnote;
                case "noterepeat": return Resources.noterepeat;
                case "repeattail": return Resources.repeattail;
                case "longnoteline": return Resources.longnoteline;
                case "line_nor_end": return Resources.line_nor_end;
                case "line_in_end": return Resources.line_in_end;
                case "long_note_end": return Resources.long_note_end;
                case "longholdgauge": return Resources.longholdgauge;
                case "longholdgaugein": return Resources.longholdgaugein;
                case "long_note_line": return Resources.long_note_line;
                case "notepressline": return Resources.notepressline;
                case "noterepeatline": return Resources.noterepeatline;
                default: return null;
            }
        }

        private struct TrailKey : IEquatable<TrailKey>
        {
            public TrailKey(GameplayPreviewNoteKind kind, bool ongoing)
            {
                Kind = kind;
                Ongoing = ongoing;
            }

            public GameplayPreviewNoteKind Kind { get; private set; }
            public bool Ongoing { get; private set; }

            public bool Equals(TrailKey other)
            {
                return Kind == other.Kind && Ongoing == other.Ongoing;
            }

            public override bool Equals(object obj)
            {
                return obj is TrailKey && Equals((TrailKey)obj);
            }

            public override int GetHashCode()
            {
                return ((int)Kind * 397) ^ (Ongoing ? 1 : 0);
            }
        }
    }
}
