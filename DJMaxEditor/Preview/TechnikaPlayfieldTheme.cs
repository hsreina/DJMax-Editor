using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using DJMaxEditor.UI;

namespace DJMaxEditor.Preview
{
    /// <summary>
    /// Every brush, pen and glow the TECHNIKA playfield paints with, allocated once.
    /// A GDI+ port of the reference project's theme: the note colours are the arcade's,
    /// sampled from the game's own sprites (taps magenta, chains green, holds blue,
    /// repeats purple), and the scanline wash is the client's own line_star.png column
    /// profile transcribed stop by stop. The chrome stays in the editor's own palette,
    /// the same split the reference made.
    /// </summary>
    internal sealed class TechnikaPlayfieldTheme
    {
        private static TechnikaPlayfieldTheme _default;

        private readonly Dictionary<GameplayPreviewNoteKind, TechnikaNoteBrushes> _notes =
            new Dictionary<GameplayPreviewNoteKind, TechnikaNoteBrushes>();

        private TechnikaPlayfieldTheme()
        {
            Backdrop = Color.FromArgb(0xFF, 0x10, 0x11, 0x13);
            FieldUpper = Color.FromArgb(0xFF, 0x0B, 0x0C, 0x0E);
            FieldLower = Color.FromArgb(0xFF, 0x0E, 0x10, 0x13);

            // The arcade fills this area with the BGA video; with no video loaded it is near
            // black. The plate keeps the editor shell's raised-panel values, as the reference
            // theme did (StudioPalette.Raised / StudioPalette.Edge).
            HeaderFill = Color.FromArgb(0xFF, 0x1F, 0x21, 0x24);
            HeaderEdge = PenFor(Color.FromArgb(0xFF, 0x34, 0x37, 0x3C), 1f);

            DividerCore = Color.FromArgb(0xFF, 0x24, 0x24, 0x24);
            DividerEdge = PenFor(Color.FromArgb(0xCC, 0x00, 0x00, 0x00), 1f);

            LaneRule = PenFor(Color.FromArgb(0xFF, 0x1E, 0x21, 0x24), 1f);
            LaneCenterRule = PenFor(Color.FromArgb(0xFF, 0x26, 0x2A, 0x2F), 1f);
            MarginRule = PenFor(Color.FromArgb(0x66,
                StudioDesignSystem.PulseCyan.R, StudioDesignSystem.PulseCyan.G,
                StudioDesignSystem.PulseCyan.B), 1f);

            SidebarFill = Color.FromArgb(0xFF, 0x16, 0x18, 0x1B);

            ScanlineForward = BuildScanlineStrip(false);
            ScanlineReverse = BuildScanlineStrip(true);
            ScanlineCore = PenFor(Color.FromArgb(0xFF, 0xFC, 0xFD, 0xFD), 1f);

            ApproachRing = PenFor(Color.FromArgb(0x7F, 0xBF, 0xE8, 0xFF), 1.5f);

            ChainRunLine = Color.FromArgb(0xFF, 0xFF, 0xD3, 0x00);

            BuildNoteBrushes();
        }

        public static TechnikaPlayfieldTheme Default
        {
            get
            {
                if (_default == null)
                {
                    _default = new TechnikaPlayfieldTheme();
                }
                return _default;
            }
        }

        public Color Backdrop { get; private set; }
        public Color FieldUpper { get; private set; }
        public Color FieldLower { get; private set; }

        public Color HeaderFill { get; private set; }
        public Pen HeaderEdge { get; private set; }

        public Color DividerCore { get; private set; }
        public Pen DividerEdge { get; private set; }

        public Pen LaneRule { get; private set; }
        public Pen LaneCenterRule { get; private set; }
        public Pen MarginRule { get; private set; }

        public Color SidebarFill { get; private set; }

        /// <summary>Scanline wash for a left-to-right sweep (the upper half).</summary>
        public Image ScanlineForward { get; private set; }

        /// <summary>Scanline wash for a right-to-left sweep (the lower half), mirrored.</summary>
        public Image ScanlineReverse { get; private set; }

        /// <summary>The hard leading edge, drawn as a line on top of the wash.</summary>
        public Pen ScanlineCore { get; private set; }

        public Pen ApproachRing { get; private set; }

        /// <summary>
        /// The yellow bar joining chain members: the arcade's line matches the yellow node
        /// rings, so the fallback must not reuse the chain head's green.
        /// </summary>
        public Color ChainRunLine { get; private set; }

        public TechnikaNoteBrushes NoteFor(GameplayPreviewNoteKind kind)
        {
            TechnikaNoteBrushes brushes;
            if (_notes.TryGetValue(kind, out brushes))
            {
                return brushes;
            }
            return _notes[GameplayPreviewNoteKind.Basic];
        }

        public Color FieldFor(bool isTopHalf)
        {
            return isTopHalf ? FieldUpper : FieldLower;
        }

        private void BuildNoteBrushes()
        {
            // Sampled from the arcade's note sprites: colour tracks type, not lane.
            TechnikaNoteBrushes tap = new TechnikaNoteBrushes(
                Color.FromArgb(0xFF, 0xD3, 0x00, 0x4F),
                Color.FromArgb(0xFF, 0xFF, 0x6E, 0x9E));
            TechnikaNoteBrushes chain = new TechnikaNoteBrushes(
                Color.FromArgb(0xFF, 0x63, 0xA8, 0x02),
                Color.FromArgb(0xFF, 0xB6, 0xF0, 0x6A));
            TechnikaNoteBrushes hold = new TechnikaNoteBrushes(
                Color.FromArgb(0xFF, 0x0B, 0x3E, 0x98),
                Color.FromArgb(0xFF, 0x6F, 0xA8, 0xFF));
            TechnikaNoteBrushes repeat = new TechnikaNoteBrushes(
                Color.FromArgb(0xFF, 0xC2, 0x03, 0xBE),
                Color.FromArgb(0xFF, 0xF5, 0x8C, 0xF2));

            _notes[GameplayPreviewNoteKind.Basic] = tap;
            _notes[GameplayPreviewNoteKind.Generic] = tap;
            // A drag is a tap with a duration - same magenta head, the trail tells them apart.
            _notes[GameplayPreviewNoteKind.Drag] = tap;
            _notes[GameplayPreviewNoteKind.ChainHead] = chain;
            _notes[GameplayPreviewNoteKind.ChainNode] = chain;
            _notes[GameplayPreviewNoteKind.Hold] = hold;
            _notes[GameplayPreviewNoteKind.RepeatHead] = repeat;
            _notes[GameplayPreviewNoteKind.RepeatHeadHold] = repeat;
            _notes[GameplayPreviewNoteKind.Repeat] = repeat;
            _notes[GameplayPreviewNoteKind.RepeatHold] = repeat;
        }

        /// <summary>
        /// The scanline wash as a horizontal gradient strip, transcribed stop by stop from
        /// the client's own line_star.png: a long alpha ramp to a flat blue plateau, then
        /// the hard edge - near-white - and a collapse in eight px. GDI+ has no multi-stop
        /// LinearGradientBrush, so the strip is baked into a bitmap and drawn stretched.
        /// </summary>
        private static Image BuildScanlineStrip(bool mirrored)
        {
            var stops = new[]
            {
                new GradientStop(0.000, 0x00, 0x00, 0x1A, 0xA8),
                new GradientStop(0.043, 0x00, 0x00, 0x1A, 0xA8),
                new GradientStop(0.422, 0x80, 0x00, 0x1A, 0xA8),
                new GradientStop(0.594, 0x80, 0x00, 0x1A, 0xA8),
                new GradientStop(0.645, 0xBD, 0x00, 0x45, 0xE0),
                new GradientStop(0.660, 0xFF, 0x14, 0xB2, 0xFF),
                new GradientStop(0.684, 0xFF, 0xC2, 0xEA, 0xFD),
                new GradientStop(0.707, 0xFF, 0xFC, 0xFD, 0xFD),
                new GradientStop(0.746, 0xFF, 0x35, 0xBE, 0xFE),
                new GradientStop(0.762, 0x46, 0x00, 0x54, 0xFF),
                new GradientStop(0.777, 0x00, 0x00, 0x54, 0xFF),
                new GradientStop(1.000, 0x00, 0x00, 0x54, 0xFF),
            };

            const int width = 256;
            var strip = new Bitmap(width, 1, PixelFormat.Format32bppArgb);
            for (int x = 0; x < width; x++)
            {
                double t = mirrored
                    ? 1.0 - (x / (double)(width - 1))
                    : x / (double)(width - 1);
                strip.SetPixel(x, 0, Sample(stops, t));
            }
            return strip;
        }

        private static Color Sample(GradientStop[] stops, double t)
        {
            GradientStop lo = stops[0];
            GradientStop hi = stops[stops.Length - 1];
            for (int i = 0; i < stops.Length - 1; i++)
            {
                if (t >= stops[i].Position && t <= stops[i + 1].Position)
                {
                    lo = stops[i];
                    hi = stops[i + 1];
                    break;
                }
            }
            double span = hi.Position - lo.Position;
            double u = span > 0.0 ? (t - lo.Position) / span : 0.0;
            return Color.FromArgb(
                (int)(lo.A + ((hi.A - lo.A) * u)),
                (int)(lo.R + ((hi.R - lo.R) * u)),
                (int)(lo.G + ((hi.G - lo.G) * u)),
                (int)(lo.B + ((hi.B - lo.B) * u)));
        }

        /// <summary>
        /// A radial glow in the note's edge colour: opaque enough at the rim to read as a
        /// halo, gone by the outer edge. The arcade draws glows additively; GDI+ has no
        /// additive mode, so the closest honest approximation is this radial falloff,
        /// baked once per note family and drawn with a per-call alpha.
        /// </summary>
        internal static Image BuildGlowBitmap(Color edgeColor)
        {
            const int size = 96;
            var stops = new[]
            {
                new GradientStop(0.00, 0x59, edgeColor.R, edgeColor.G, edgeColor.B),
                new GradientStop(0.55, 0x3D, edgeColor.R, edgeColor.G, edgeColor.B),
                new GradientStop(0.80, 0x1C, edgeColor.R, edgeColor.G, edgeColor.B),
                new GradientStop(1.00, 0x00, edgeColor.R, edgeColor.G, edgeColor.B),
            };

            var glow = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            double centre = (size - 1) / 2.0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = (x - centre) / centre;
                    double dy = (y - centre) / centre;
                    double t = Math.Sqrt((dx * dx) + (dy * dy));
                    if (t > 1.0) t = 1.0;
                    glow.SetPixel(x, y, Sample(stops, t));
                }
            }
            return glow;
        }

        private static Pen PenFor(Color color, float width)
        {
            return new Pen(color, width);
        }

        private struct GradientStop
        {
            public GradientStop(double position, byte a, byte r, byte g, byte b)
            {
                Position = position;
                A = a;
                R = r;
                G = g;
                B = b;
            }

            public readonly double Position;
            public readonly byte A;
            public readonly byte R;
            public readonly byte G;
            public readonly byte B;
        }
    }

    /// <summary>A note family's fill, outline, trail and baked radial glow. All shared, none disposed per frame.</summary>
    internal sealed class TechnikaNoteBrushes
    {
        public TechnikaNoteBrushes(Color fill, Color edge)
        {
            Fill = fill;
            Edge = Pen(edge, 1f);
            Trail = Solid(Color.FromArgb(140, fill.R, fill.G, fill.B));
            Glow = TechnikaPlayfieldTheme.BuildGlowBitmap(edge);
        }

        public Color Fill { get; private set; }
        public Pen Edge { get; private set; }
        public SolidBrush Trail { get; private set; }
        public Image Glow { get; private set; }

        private static Pen Pen(Color color, float width)
        {
            return new Pen(color, width);
        }

        private static SolidBrush Solid(Color color)
        {
            return new SolidBrush(color);
        }
    }
}
