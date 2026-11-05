using System;

namespace DJMaxEditor.Preview
{
    /// <summary>
    /// One moment of the arcade's tap hit burst: how big it is and how bright. Pure
    /// arithmetic over the measured cool.vce envelope, ported from the reference project
    /// so the draw path carries no numbers of its own. The burst snaps in, pinches early,
    /// then blows outward as it fades.
    /// </summary>
    internal struct TechnikaHitFlash
    {
        private TechnikaHitFlash(
            double width,
            double height,
            double opacity,
            double frameProgress)
        {
            Width = width;
            Height = height;
            Opacity = opacity;
            FrameProgress = frameProgress;
        }

        /// <summary>Width of the burst, in arcade pixels.</summary>
        public double Width { get; }

        /// <summary>Height of the burst, in arcade pixels.</summary>
        public double Height { get; }

        /// <summary>Opacity of the burst, 1 at the strike and 0 when it is spent.</summary>
        public double Opacity { get; }

        /// <summary>
        /// The same moment as a one-shot progress across the CoolBomb frame sequence, 0 at
        /// the strike and 1 when the burst ends - what an arcade frame sequence indexes by.
        /// </summary>
        public double FrameProgress { get; }

        public bool IsVisible
        {
            get { return Opacity > 0.0 && Width > 0.0 && Height > 0.0; }
        }

        /// <summary>
        /// The burst <paramref name="progress"/> of the way through, where 0 is the frame
        /// the note was struck on and 1 is the end of
        /// <see cref="TechnikaPlayfieldMetrics.CoolBombSeconds"/>. Outside 0..1 the answer
        /// is an invisible flash, so a caller that draws what it is handed needs no window
        /// test of its own.
        /// </summary>
        public static TechnikaHitFlash At(double progress)
        {
            if (double.IsNaN(progress) || progress < 0.0 || progress > 1.0)
            {
                return new TechnikaHitFlash(0.0, 0.0, 0.0, 0.0);
            }

            double pinch = TechnikaPlayfieldMetrics.CoolBombPinchProgress;
            if (progress <= pinch)
            {
                double t = pinch <= 0.0 ? 1.0 : progress / pinch;
                return new TechnikaHitFlash(
                    Lerp(TechnikaPlayfieldMetrics.CoolBombWidth,
                        TechnikaPlayfieldMetrics.CoolBombPinchWidth, t),
                    Lerp(TechnikaPlayfieldMetrics.CoolBombHeight,
                        TechnikaPlayfieldMetrics.CoolBombPinchHeight, t),
                    Lerp(1.0, TechnikaPlayfieldMetrics.CoolBombPinchAlpha, t),
                    progress);
            }

            double u = pinch >= 1.0 ? 1.0 : (progress - pinch) / (1.0 - pinch);
            return new TechnikaHitFlash(
                Lerp(TechnikaPlayfieldMetrics.CoolBombPinchWidth,
                    TechnikaPlayfieldMetrics.CoolBombEndWidth, u),
                Lerp(TechnikaPlayfieldMetrics.CoolBombPinchHeight,
                    TechnikaPlayfieldMetrics.CoolBombEndHeight, u),
                Lerp(TechnikaPlayfieldMetrics.CoolBombPinchAlpha, 0.0, u),
                progress);
        }

        /// <summary>The same moment taken <paramref name="scale"/> as large and
        /// <paramref name="opacity"/> as bright, for the per-kind profile and the
        /// preview's smaller field. The frame progress passes through untouched.</summary>
        public TechnikaHitFlash Scaled(double scale, double opacity)
        {
            if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0.0 ||
                double.IsNaN(opacity) || double.IsInfinity(opacity) || opacity <= 0.0)
            {
                return new TechnikaHitFlash(0.0, 0.0, 0.0, 0.0);
            }
            return new TechnikaHitFlash(
                Width * scale, Height * scale, Opacity * opacity, FrameProgress);
        }

        /// <summary>
        /// How much of a scan the burst lasts on a chart whose scans are
        /// <paramref name="scanSeconds"/> long, or 0 when that is not known - which the
        /// caller reads as "no burst", better than a burst of invented length.
        /// </summary>
        public static double ScansFor(double scanSeconds)
        {
            if (double.IsNaN(scanSeconds) || double.IsInfinity(scanSeconds) || scanSeconds <= 0.0)
            {
                return 0.0;
            }
            return TechnikaPlayfieldMetrics.CoolBombSeconds / scanSeconds;
        }

        private static double Lerp(double from, double to, double t)
        {
            return from + ((to - from) * t);
        }
    }
}
