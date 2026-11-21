using System;

namespace DJMaxEditor.Preview
{
    /// <summary>
    /// How one kind of note's hit burst differs from the tap's: how large, how long, how
    /// bright. The arcade carries a sequence per gesture but only the tap's is available,
    /// so the one envelope stands in for the others, shaped per kind - a chain node's tick
    /// is a smaller, quicker flash than a tap's; a hold's is the wide one sized off the
    /// half itself. Pure arithmetic, ported from the reference project.
    /// </summary>
    internal struct TechnikaHitEffectProfile
    {
        /// <summary>
        /// The hold burst against the cool burst, both from their own VCE quads: 350 against 394.
        /// The one number here that is measured rather than chosen.
        /// </summary>
        public const double HoldScale =
            TechnikaPlayfieldMetrics.MeasuredHitEffectSize / TechnikaPlayfieldMetrics.CoolBombWidth;

        /// <summary>
        /// How many note frames wide the tap's burst is drawn in the preview. The arcade's
        /// own burst is four and a half notes wide, which would hide several lanes of chart
        /// in an editor; two note frames keeps it a burst and the pattern legible.
        /// </summary>
        public const double PreviewNoteFrames = 2.0;

        private TechnikaHitEffectProfile(double scale, double life, double opacity)
        {
            Scale = scale;
            Life = life;
            Opacity = opacity;
        }

        /// <summary>Size of this kind's burst against the tap's.</summary>
        public double Scale { get; }

        /// <summary>How long it lasts against the tap's, so a quick tick can be a quick tick.</summary>
        public double Life { get; }

        /// <summary>Brightness against the tap's.</summary>
        public double Opacity { get; }

        public static TechnikaHitEffectProfile For(GameplayPreviewNoteKind kind)
        {
            switch (kind)
            {
                // A chain's members are ticks along a gesture rather than hits: half size,
                // half length, dimmer - a dozen of these can land inside one scan.
                case GameplayPreviewNoteKind.ChainHead:
                case GameplayPreviewNoteKind.ChainNode:
                    return new TechnikaHitEffectProfile(0.5, 0.5, 0.75);

                // The hold family fires the wide sequence; it runs full length because a
                // hold's burst marks a note still being held.
                case GameplayPreviewNoteKind.Hold:
                case GameplayPreviewNoteKind.Drag:
                case GameplayPreviewNoteKind.RepeatHeadHold:
                case GameplayPreviewNoteKind.RepeatHold:
                    return new TechnikaHitEffectProfile(HoldScale, 1.0, 1.0);

                // A repeat series fires once per member; smaller, shorter and dimmer so the
                // members stay countable instead of reading as one glare.
                case GameplayPreviewNoteKind.RepeatHead:
                case GameplayPreviewNoteKind.Repeat:
                    return new TechnikaHitEffectProfile(0.55, 0.7, 0.7);

                default:
                    return new TechnikaHitEffectProfile(1.0, 1.0, 1.0);
            }
        }

        /// <summary>
        /// The factor that takes the burst from the size the arcade authored it at to the
        /// size the preview draws it at, on a chart of <paramref name="laneCount"/> lines:
        /// <see cref="PreviewNoteFrames"/> note frames expressed as a share of the authored
        /// quad. Zero when there is no measurement to scale against.
        /// </summary>
        public static double PreviewScale(int laneCount)
        {
            double note = TechnikaPlayfieldMetrics.NoteFrameSize(laneCount);
            double authored = TechnikaPlayfieldMetrics.CoolBombWidth;
            if (note <= 0.0 || authored <= 0.0)
            {
                return 0.0;
            }
            return note * PreviewNoteFrames / authored;
        }
    }
}
