using System;

namespace DJMaxEditor.Preview
{
    /// <summary>
    /// One moment of the count-in before a chart's first note: which number is showing and
    /// how brightly. Beats are counted off the musical clock, not a wall clock, so a
    /// stopped transport scrubbed back into the count-in shows the count again and the
    /// digits land on the beats an author hears. Pure arithmetic, ported from the
    /// reference project.
    /// </summary>
    internal struct TechnikaCountIn
    {
        /// <summary>Beats of count-in drawn before the first note: 3, 2, 1 and then play.</summary>
        public const int Beats = 3;

        /// <summary>
        /// How much of its own beat a digit spends at full strength before it starts to
        /// fade, so the count reads as a pulse rather than three numbers cross-dissolving.
        /// </summary>
        public const double FadeShare = 0.55;

        private TechnikaCountIn(int number, double opacity)
        {
            Number = number;
            Opacity = opacity;
        }

        /// <summary>The number showing, or 0 when the count-in is not running.</summary>
        public int Number { get; }

        /// <summary>How brightly to draw it, 1 on the beat and falling to 0 by the next one.</summary>
        public double Opacity { get; }

        public bool IsVisible
        {
            get { return Number > 0 && Opacity > 0.0; }
        }

        /// <summary>
        /// The count-in <paramref name="beatsAway"/> beats before the first note. Nothing
        /// at or past the note itself, and nothing more than <see cref="Beats"/> beats
        /// ahead of it; outside the window the answer is an invisible count, so a caller
        /// that draws what it is handed cannot leave a "1" parked over a chart already
        /// playing.
        /// </summary>
        public static TechnikaCountIn At(double beatsAway)
        {
            if (double.IsNaN(beatsAway) || double.IsInfinity(beatsAway) ||
                beatsAway <= 0.0 || beatsAway > Beats)
            {
                return new TechnikaCountIn(0, 0.0);
            }

            // Ceiling, so the whole beat before the first note is "1" and the beat before
            // that is "2": the number showing is how many beats are still to come.
            int number = (int)Math.Ceiling(beatsAway);
            double intoBeat = number - beatsAway;
            double opacity = intoBeat <= FadeShare
                ? 1.0
                : 1.0 - ((intoBeat - FadeShare) / (1.0 - FadeShare));
            return new TechnikaCountIn(number, opacity);
        }
    }
}
