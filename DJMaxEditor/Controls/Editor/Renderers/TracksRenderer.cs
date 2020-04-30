using DJMaxEditor.DJMax;
using System;
using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers
{
    internal class TracksRenderer
    {
        public const int VirtualTrackheight = 120;

        public TracksRenderer(EventsRenderer eventsRenderer, ZonesRenderer zonesRenderer)
        {
            m_eventsRenderer = eventsRenderer;
            m_zonesRenderer = zonesRenderer;
            m_trackRectangle = new Rectangle();
            m_oddTrackBrush = new SolidBrush(ColorScheme.oddTrackColor);
            m_evenTrackBrush = new SolidBrush(ColorScheme.evenTrackColor);
            m_gridColor = new Pen(Color.FromArgb(200, 107, 117, 130));
            m_gridColorBeat = new Pen(Color.FromArgb(200, 254, 222, 42));
            m_infoFont = new Font("Tahoma", 16);
        }

        public void RenderTracskList(GraphicsWrapper g, TracksList tracksList, Rectangle bounds, int beatSize, int blockSize, int virtualMaxTick, Rectangle drawableZone)
        {
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

                foreach (var eventData in track.Events)
                {
                    m_eventsRenderer.RenderEventData(g, eventData, viewableTrackRectangle, trackY);
                }

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

                g.DrawString(track.DisplayedTrackName, m_infoFont, Brushes.White, (float)trackNamePosX + 10, (float)trackNamePosY + 3);

                m_zonesRenderer.DrawZones(g, trackIndex, trackX, trackY, trackWidth, trackHeight, viewableTrackRectangle);
            }
        }

        private readonly ZonesRenderer m_zonesRenderer;

        private readonly EventsRenderer m_eventsRenderer;

        private readonly Brush m_oddTrackBrush;

        private readonly Brush m_evenTrackBrush;

        private readonly Pen m_gridColor;

        private readonly Pen m_gridColorBeat;

        private readonly Font m_infoFont;

        private Rectangle m_trackRectangle;

        private int RoundUp(int num, int factor)
        {
            return num + factor - 1 - (num - 1) % factor;
        }
    }
}
