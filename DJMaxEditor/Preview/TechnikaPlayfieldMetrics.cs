using System;

namespace DJMaxEditor.Preview
{
    /// <summary>
    /// The DJMAX TECHNIKA 2 arcade playfield, in its own pixels - a GDI+ port of the
    /// reference project's measured layout table. The cabinet is fixed at 1280x768 with
    /// no responsive layout, so a constant table plus a letterbox fit is the whole model.
    /// Values here are the ones the paint pass needs; every number is inherited from the
    /// reference measurement, not re-derived.
    /// </summary>
    internal static class TechnikaPlayfieldMetrics
    {
        // ---- the frame -----------------------------------------------------------------------

        /// <summary>Backbuffer and viewport width. Fixed; the arcade is exclusive fullscreen.</summary>
        public const double NativeWidth = 1280.0;

        /// <summary>Backbuffer and viewport height.</summary>
        public const double NativeHeight = 768.0;

        /// <summary>Header band height, from the capture: a 72 px plate across the top.</summary>
        public const double HeaderHeight = 72.0;

        /// <summary>Top of the playable area: immediately below the header.</summary>
        public const double PlayfieldTop = HeaderHeight;

        /// <summary>Bottom of the playable area: the bottom of the screen.</summary>
        public const double PlayfieldBottom = NativeHeight;

        // ---- the two halves ------------------------------------------------------------------

        /// <summary>
        /// Where the upper half ends and the lower half begins. The halves are not equal:
        /// the divider sprite's 5 px core centres on y416, giving 344 above and 352 below.
        /// </summary>
        public const double HalfBoundary = 416.0;

        public const double UpperFieldTop = PlayfieldTop;
        public const double UpperFieldBottom = HalfBoundary;
        public const double LowerFieldTop = HalfBoundary;
        public const double LowerFieldBottom = PlayfieldBottom;

        /// <summary>Height of the upper half: 344 px.</summary>
        public const double UpperFieldHeight = UpperFieldBottom - UpperFieldTop;

        /// <summary>Height of the lower half: 352 px.</summary>
        public const double LowerFieldHeight = LowerFieldBottom - LowerFieldTop;

        // ---- centre divider ------------------------------------------------------------------

        /// <summary>Top of the divider's opaque 5 px core, once the source is stretched.</summary>
        public const double DividerCoreTop = 413.5;

        /// <summary>Height of the divider's opaque core.</summary>
        public const double DividerCoreHeight = 5.0;

        // ---- scanline ------------------------------------------------------------------------

        /// <summary>Width of the scanline quad, from the capture.</summary>
        public const double ScanlineWidth = 250.0;

        /// <summary>Height of the scanline quad - taller than either half, so it overhangs.</summary>
        public const double ScanlineHeight = 355.0;

        /// <summary>Top of the upper-half scanline quad (y 63.5 to 418.5).</summary>
        public const double ScanlineUpperTop = 63.5;

        /// <summary>Top of the lower-half scanline quad (y 417.5 to 772.5).</summary>
        public const double ScanlineLowerTop = 417.5;

        /// <summary>
        /// Where the bright leading edge sits inside the scanline quad, measured from the
        /// quad's leading side: 180/256 x 250 = 175.8 px, off the client's own
        /// panel\line_star.png column profile.
        /// </summary>
        public const double ScanlineLeadingEdgeOffset = 175.8;

        // ---- note field ----------------------------------------------------------------------

        /// <summary>
        /// Left margin of the note field as a fraction of the width. The projector places
        /// notes with these same margins, and the scanline has to arrive at a note exactly
        /// when the note is due, so this is one constant shared through
        /// <see cref="GameplayPreviewProjector.TechnikaFieldLeft"/> rather than restated.
        /// </summary>
        public const double NoteMarginLeft = GameplayPreviewProjector.TechnikaFieldLeft;

        /// <summary>Right margin of the note field. See <see cref="NoteMarginLeft"/>.</summary>
        public const double NoteMarginRight = GameplayPreviewProjector.TechnikaFieldRight;

        /// <summary>
        /// Vertical inset of the lane stack inside a half, as a fraction of the half's height.
        /// The projector's lane placement runs 0.05 to 0.95, so the lanes occupy the middle 90%.
        /// </summary>
        public const double LaneInset = 0.05;

        /// <summary>Side rail width. The arcade's rails are 40 px strips at each edge of each half.</summary>
        public const double SidebarWidth = 40.0;

        /// <summary>Note frame size for a 3-line chart - Star Mixing - in arcade pixels.</summary>
        public const double NoteFrameThreeLine = 116.0;

        /// <summary>Note frame size for a 4-line chart - Pop Mixing.</summary>
        public const double NoteFrameFourLine = 90.0;

        /// <summary>The mean of the two halves, which is the height one lane pitch is measured on.</summary>
        public const double MeanHalfHeight = (UpperFieldHeight + LowerFieldHeight) / 2.0;

        /// <summary>
        /// Diameter of the hold hit effect, in arcade pixels. Measured from the client's own VCE
        /// quads: <c>CoolBomb\*\hold\note\note.vce</c> draws it note-local and centred at
        /// -175..175, and all six skin sets agree. It is a fixed size, not a multiple of the note
        /// or the lane: 350 px is a half's own height, so the effect covers the half it fires in
        /// whichever mode is playing. The cool flash beside it is 394x385 from
        /// <c>CoolBomb\*\cool\cool.vce</c>.
        /// </summary>
        public const double MeasuredHitEffectSize = 350.0;

        // ---- hit burst -----------------------------------------------------------------------

        /// <summary>Width of the tap hit burst's first key, from the client's own cool.vce.</summary>
        public const double CoolBombWidth = 394.0;

        /// <summary>Height of the burst's first key.</summary>
        public const double CoolBombHeight = 385.0;

        /// <summary>Width of the burst's last key, where it has blown outward and gone.</summary>
        public const double CoolBombEndWidth = 602.0;

        /// <summary>Height of the burst's last key.</summary>
        public const double CoolBombEndHeight = 347.0;

        /// <summary>Where the middle (pinch) key sits in the effect, as a fraction: tick 5 of 35.</summary>
        public const double CoolBombPinchProgress = 5.0 / 35.0;

        /// <summary>Width at the pinch.</summary>
        public const double CoolBombPinchWidth = 306.6;

        /// <summary>Height at the pinch.</summary>
        public const double CoolBombPinchHeight = 369.7;

        /// <summary>Alpha at the pinch, as a fraction: 218.6 of 255.</summary>
        public const double CoolBombPinchAlpha = 218.6 / 255.0;

        /// <summary>How long the burst runs: 35 ticks of a 60 fps animation.</summary>
        public const double CoolBombSeconds = 35.0 / 60.0;

        // ---- fitting --------------------------------------------------------------------------

        /// <summary>Top of one half, in arcade pixels.</summary>
        public static double HalfTop(bool isTopHalf)
        {
            return isTopHalf ? UpperFieldTop : LowerFieldTop;
        }

        /// <summary>Height of one half, in arcade pixels. 344 above, 352 below.</summary>
        public static double HalfHeight(bool isTopHalf)
        {
            return isTopHalf ? UpperFieldHeight : LowerFieldHeight;
        }

        /// <summary>
        /// The size the arcade draws a note frame at, in arcade pixels, for a chart with
        /// <paramref name="laneCount"/> lines. 3 and 4 are measured; anything else is one
        /// lane pitch of <see cref="MeanHalfHeight"/>, extrapolated.
        /// </summary>
        public static double NoteFrameSize(int laneCount)
        {
            if (laneCount == 3)
            {
                return NoteFrameThreeLine;
            }
            if (laneCount == 4)
            {
                return NoteFrameFourLine;
            }
            return MeanHalfHeight / (laneCount < 1 ? 1 : laneCount);
        }

        /// <summary>
        /// Whether a canvas of <paramref name="size"/> arcade pixels is a whole note frame, i.e.
        /// one of the two sizes the client authors a full-lane glyph at.
        ///
        /// <para>The question this answers is which of two things a difference in canvas size
        /// means. The set carries both mixing modes' art mixed together - <c>notepressstart</c>
        /// and <c>longnote</c> are 116 px cells while the tap beside them is 90 - and 116 against
        /// 90 is not one glyph authored larger than another. It is the same glyph, one lane pitch
        /// tall, in the two modes. Read as an authored size difference it draws a chain head
        /// 116/90 = 1.29x the note it leads, which is the oversized head the reference playtest
        /// reported.</para>
        ///
        /// <para>A canvas that is neither is a genuine authored size, and stays one: the chain
        /// node's 76 px cell is no mode's pitch, and the 76 px line strips really are thinner
        /// than a lane - those proportions are the art and must survive. Half a pixel of slack,
        /// because the sizes are read back off a PNG.</para>
        /// </summary>
        public static bool IsNoteFrameSize(double size)
        {
            return Math.Abs(size - NoteFrameThreeLine) < 0.5 ||
                Math.Abs(size - NoteFrameFourLine) < 0.5;
        }

        /// <summary>
        /// Letterboxes the playable area (below the 72px title plate) into the viewport.
        /// The header band is cropped out so the preview has no empty black strip on top.
        /// </summary>
        public static TechnikaPlayfieldFit Fit(double viewportWidth, double viewportHeight)
        {
            if (viewportWidth <= 0 || viewportHeight <= 0 ||
                double.IsNaN(viewportWidth) || double.IsNaN(viewportHeight))
            {
                return default(TechnikaPlayfieldFit);
            }

            double contentHeight = NativeHeight - PlayfieldTop;
            double scale = Math.Min(viewportWidth / NativeWidth, viewportHeight / contentHeight);
            if (scale <= 0 || double.IsInfinity(scale))
            {
                return default(TechnikaPlayfieldFit);
            }

            // Shift Y so arcade PlayfieldTop lands at the top of the letterboxed content.
            double offsetY = ((viewportHeight - (contentHeight * scale)) / 2.0) -
                (PlayfieldTop * scale);

            return new TechnikaPlayfieldFit(
                scale,
                (viewportWidth - (NativeWidth * scale)) / 2.0,
                offsetY);
        }
    }

    /// <summary>
    /// A uniform scale plus a centring offset: the one transform between arcade pixels and
    /// the control's own coordinates. A struct, so a per-frame draw pass costs no allocation.
    /// </summary>
    internal struct TechnikaPlayfieldFit
    {
        public TechnikaPlayfieldFit(double scale, double offsetX, double offsetY)
        {
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        /// <summary>Control units per arcade pixel. Zero when the viewport is unusable.</summary>
        public double Scale { get; private set; }

        /// <summary>Left edge of the letterboxed frame.</summary>
        public double OffsetX { get; private set; }

        /// <summary>Top edge of the letterboxed frame.</summary>
        public double OffsetY { get; private set; }

        /// <summary>False when there is not enough room to draw anything.</summary>
        public bool IsUsable
        {
            get { return Scale > 0; }
        }

        public double X(double nativeX)
        {
            return OffsetX + (nativeX * Scale);
        }

        public double Y(double nativeY)
        {
            return OffsetY + (nativeY * Scale);
        }

        public double Length(double nativeLength)
        {
            return nativeLength * Scale;
        }

        /// <summary>
        /// Places a projected note. X is the projector's normalised X across the full width; Y is
        /// resolved through the measured 344/352 half split rather than by halving, so a note
        /// lands on its real arcade lane centre.
        /// </summary>
        public double NoteX(double normalizedX)
        {
            return X(normalizedX * TechnikaPlayfieldMetrics.NativeWidth);
        }

        public double NoteY(double normalizedY, bool isTopHalf)
        {
            double localY = isTopHalf
                ? normalizedY * 2.0
                : (normalizedY - 0.5) * 2.0;
            return Y(TechnikaPlayfieldMetrics.HalfTop(isTopHalf) +
                (localY * TechnikaPlayfieldMetrics.HalfHeight(isTopHalf)));
        }
    }
}
