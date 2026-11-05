using DJMaxEditor.Controls.Editor.Renderers.Events;
using DJMaxEditor.DJMax;
using System;
using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers
{
    internal class TracksRenderer
    {
        public const int VirtualTrackheight = 120;

        /// <summary>Milliseconds spent on the row fills and grid lines.</summary>
        public double DebugFillMs { get; private set; }

        /// <summary>Milliseconds spent on everything left around the events:
        /// track names and zones.</summary>
        public double DebugChromeMs { get; private set; }

        public TracksRenderer(EventsRenderer eventsRenderer, ZonesRenderer zonesRenderer)
        {
            m_eventsRenderer = eventsRenderer;
            m_zonesRenderer = zonesRenderer;
            m_trackRectangle = new Rectangle();
            m_oddTrackBrush = new SolidBrush(ColorScheme.oddTrackColor);
            m_evenTrackBrush = new SolidBrush(ColorScheme.evenTrackColor);
            m_gridColor = new Pen(Color.FromArgb(
                112,
                UI.StudioDesignSystem.Border));
            m_gridColorBeat = new Pen(Color.FromArgb(
                176,
                UI.StudioDesignSystem.PulseCyan));
            m_infoFont = UI.StudioDesignSystem.DisplayFont(16f);
            m_infoBrush = new SolidBrush(UI.StudioDesignSystem.Frost);
        }

        public void RenderTracskList(GraphicsWrapper g, TracksList tracksList, Rectangle bounds, int beatSize, int blockSize, int virtualMaxTick, Rectangle drawableZone)
        {
            var phaseWatch = System.Diagnostics.Stopwatch.StartNew();

            // Default color used for even tracks
            g.FillRectangle(m_evenTrackBrush, bounds);

            var trackRectangle = m_trackRectangle;
            var virtualTrackheight = VirtualTrackheight;

            foreach (var track in tracksList)
            {
                int trackIndex = (int)track.Idx;
                int trackX = trackRectangle.X = drawableZone.X;
                int trackY = trackRectangle.Y = trackIndex * virtualTrackheight;
                int trackWidth = trackRectangle.Width = drawableZone.Width;
                int trackHeight = trackRectangle.Height = virtualTrackheight;

                var viewableTrackRectangle = Rectangle.Intersect(trackRectangle, bounds);
                if (viewableTrackRectangle.IsEmpty)
                {
                    continue;
                }

                var isOddTrack = (trackIndex & 1) == 1;
                if (isOddTrack)
                {
                    g.FillRectangle(
                        m_oddTrackBrush,
                        viewableTrackRectangle
                    );
                }
            }

            int boundsX = bounds.X;
            var boundsY = bounds.Y;
            var boundsWidth = bounds.Width;
            var boundsHeight = bounds.Height;

            var blocksCount = boundsWidth / blockSize;
            var blockFrom = ((boundsX / blockSize) + 1) * blockSize;
            var blockTo = blockFrom + boundsWidth;
            var blockColor = m_gridColor;
            for (int i = blockFrom; i < blockTo; i += blockSize)
            {                
                g.DrawRectangle(blockColor, i, boundsY, 1, boundsHeight);
            }

            var beatsCount = boundsWidth / beatSize;
            var beatFrom = ((boundsX / beatSize) + 1) * beatSize;
            var beatTo = beatFrom + boundsWidth;
            var beatColor = m_gridColorBeat;
            for (int i = beatFrom; i < beatTo; i += beatSize)
            {
                g.DrawRectangle(beatColor, i, boundsY, 1, boundsHeight);
            }

            phaseWatch.Stop();
            DebugFillMs += phaseWatch.Elapsed.TotalMilliseconds;
            phaseWatch.Restart();

            foreach (var track in tracksList)
            {
                int trackIndex = (int)track.Idx;
                int trackX = trackRectangle.X = drawableZone.X;
                int trackY = trackRectangle.Y = trackIndex * virtualTrackheight;
                int trackWidth = trackRectangle.Width = drawableZone.Width;
                int trackHeight = trackRectangle.Height = virtualTrackheight;

                var viewableTrackRectangle = Rectangle.Intersect(trackRectangle, bounds);
                if (viewableTrackRectangle.IsEmpty)
                {
                    continue;
                }

                // Events are drawn separately, in device space (see
                // RenderTrackEvents) - keeping the per-note blits on GDI+'s fast
                // path is what keeps dense-chart playback at frame rate.
                int trackNamePosX = viewableTrackRectangle.X;
                int trackNamePosY = trackY;

                if (trackNamePosX < boundsX)
                {
                    trackNamePosX = boundsX;
                }

                if (trackNamePosY < boundsY)
                {
                    trackNamePosY = boundsY;
                }

                g.DrawString(
                    track.DisplayedTrackName,
                    m_infoFont,
                    m_infoBrush,
                    (float)trackNamePosX + 10,
                    (float)trackNamePosY + 3);

                m_zonesRenderer.DrawZones(g, trackIndex, trackX, trackY, trackWidth, trackHeight, viewableTrackRectangle);
            }

            phaseWatch.Stop();
            DebugChromeMs += phaseWatch.Elapsed.TotalMilliseconds;
        }

        /// <summary>
        /// Draws the events of every visible track. The caller puts the wrapper in
        /// device space first (identity world transform): the events arrive
        /// ordered by virtual tick (TrackData.Events), so the visible window is
        /// culled with a scan - skip notes ending left of the clip, stop at the
        /// first note starting right of it - and each surviving note is issued as
        /// a device-space blit. Boundary cases still go through RenderEventData's
        /// own IntersectsWith.
        /// </summary>
        public void RenderTrackEvents(GraphicsWrapper g, TracksList tracksList, Rectangle bounds, Rectangle drawableZone)
        {
            var trackRectangle = m_trackRectangle;
            var virtualTrackheight = VirtualTrackheight;
            const int halfNote = EventsRenderer.VirtualNoteWidth / 2;

            // Repeat (per-lane) and Press/chain (cross-lane, tick order) under notes.
            var technikaTheme = m_eventsRenderer.Theme as TechnikaThemeRenderer;
            if (technikaTheme != null)
            {
                technikaTheme.RenderSeriesLinks(g, tracksList, bounds);
            }

            foreach (var track in tracksList)
            {
                int trackIndex = (int)track.Idx;
                trackRectangle.X = drawableZone.X;
                int trackY = trackRectangle.Y = trackIndex * virtualTrackheight;
                trackRectangle.Width = drawableZone.Width;
                trackRectangle.Height = virtualTrackheight;

                var viewableTrackRectangle = Rectangle.Intersect(trackRectangle, bounds);
                if (viewableTrackRectangle.IsEmpty)
                {
                    continue;
                }

                int clipLeft = viewableTrackRectangle.Left;
                int clipRight = viewableTrackRectangle.Right;

                foreach (var eventData in track.Events)
                {
                    int eventX = eventData.VirtualTick;
                    // Duration > 6 is the editor's long-note gate (same as GetEventRectangle):
                    // a hold's body must keep the note in the cull window after the head
                    // has scrolled off the left edge.
                    int span = eventData.Duration > 6 ? eventData.VirtualDuration : 0;
                    if (eventX - halfNote > clipRight)
                    {
                        break;
                    }
                    if (eventX + span + halfNote < clipLeft)
                    {
                        continue;
                    }
                    DebugNoteDraws++;
                    m_eventsRenderer.RenderEventData(g, eventData, viewableTrackRectangle, trackY);
                }
            }
        }

        /// <summary>Called by the owning surface before a frame: per-frame phase
        /// accumulators must not leak into the next paint's diagnostics.</summary>
        public void BeginFrameDiagnostics()
        {
            DebugFillMs = 0;
            DebugChromeMs = 0;
            DebugNoteDraws = 0;
        }

        /// <summary>How many event visuals the last frame issued - the hosted
        /// benchmark prints this to separate call count from per-call cost.</summary>
        public int DebugNoteDraws { get; private set; }

        private readonly ZonesRenderer m_zonesRenderer;

        private readonly EventsRenderer m_eventsRenderer;

        private readonly Brush m_oddTrackBrush;

        private readonly Brush m_evenTrackBrush;

        private readonly Pen m_gridColor;

        private readonly Pen m_gridColorBeat;

        private readonly Font m_infoFont;

        private readonly Brush m_infoBrush;

        private Rectangle m_trackRectangle;

        private int RoundUp(int num, int factor)
        {
            return num + factor - 1 - (num - 1) % factor;
        }
    }
}
