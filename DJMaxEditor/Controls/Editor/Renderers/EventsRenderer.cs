using DJMaxEditor.Controls.Editor.Renderers.Events;
using DJMaxEditor.DJMax;
using System.Collections.Generic;
using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers
{
    internal class CustomBrushes
    {
        private static SolidBrush m_player1Visible;

        private static SolidBrush m_player2Visible;

        private static SolidBrush m_noteBackground;

        public static Brush NoteBackground
        {
            get
            {
                if (null == m_noteBackground)
                {
                    m_noteBackground = new SolidBrush(FromRGB(0x8BC2FF));
                }
                return m_noteBackground;
            }
        }

        public static Brush Player1Visible
        {
            get
            {
                if (null == m_player1Visible)
                {
                    m_player1Visible = new SolidBrush(FromRGB(0xFF9F1C));
                }
                return m_player1Visible;
            }
        }

        public static Brush Player2Visible
        {
            get
            {
                if (null == m_player2Visible)
                {
                    m_player2Visible = new SolidBrush(FromRGB(0xCBF3F0));
                }
                return m_player2Visible;
            }
        }

        private static Color FromRGB(int rgb)
        {
            return Color.FromArgb((int)(0xFF000000 + rgb));
        }

    }

    internal class EventsRenderer
    {
        public const int VirtualTrackheight = 120;

        public IEventRenderer Theme
        {
            get
            {
                return m_theme;
            }
            set
            {
                if (null == value)
                {
                    return;
                }
                m_theme = value;
                (value as EventRenderer).EventDisplayMode = EventDisplayMode;
            }
        }

        public EventDisplayMode EventDisplayMode
        {
            get
            {
                return (m_theme as EventRenderer).EventDisplayMode;
            }
            set
            {
                (m_theme as EventRenderer).EventDisplayMode = value;
            }
        }

        public readonly IList<EventRenderer> Themes;

        public EventsRenderer()
        {
            Themes = new List<EventRenderer>();
            var nullTheme = new NullThemeRenderer();
            Themes.Add(nullTheme);
            Themes.Add(new TechnikaThemeRenderer());
            Themes.Add(new TrilogyThemeRenderer());
            Themes.Add(new CyclonThemeRenderer());
            Theme = nullTheme;
        }

        public Rectangle GetTrackRectangle(TrackData trackData, int virtualMaxTick)
        {
            int trackIndex = (int)trackData.Idx;

            int trackX = 0;
            int trackY = trackIndex * VirtualTrackheight;
            int width = (int)virtualMaxTick;
            int height = 120;

            return new Rectangle(trackX, trackY, width, height);
        }

        private Point GetEventPosition(EventData eventData, int? trackY = null)
        {
            int yPos = trackY.HasValue ? trackY.Value : (int)eventData.TrackId * EventsRenderer.VirtualTrackheight;

            int x = (int)(eventData.VirtualTick);
            int y = yPos + (VirtualTrackheight / 2);

            return new Point(x, y);
        }

        public Rectangle GetEventRectangle(EventData eventData, int? trackY = null)
        {
            Point eventPosition = GetEventPosition(eventData, trackY);

            int x = eventPosition.X;
            int y = eventPosition.Y;

            int noteWidth = virtualNoteWidth;
            int noteHeight = virtualNoteHeight;

            int virtualDuration = eventData.Duration > 6 ? eventData.VirtualDuration : 0;

            return new Rectangle(
                x - noteWidth / 2,
                y - noteHeight / 2,
                noteWidth + virtualDuration,
                noteHeight
            );
        }

        public void RenderEventDataAtInRect(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY)
        {            
            m_theme.RenderEventData(g, eventData, eventRectangle, centerX, centerY);
        }

        public void RenderEventData(GraphicsWrapper g, EventData eventData, Rectangle bounds, int? trackY = null)
        {
            if (null == eventData || null == bounds)
            {
                return;
            }

            Rectangle eventRectangle = GetEventRectangle(eventData, trackY);

            if (!eventRectangle.IntersectsWith(bounds))
            {
                return;
            }

            Point eventPosition = GetEventPosition(eventData);

            this.RenderEventDataAtInRect(g, eventData, eventRectangle, eventPosition.X, eventPosition.Y);
        }

        private const int virtualNoteWidth = 120;

        private const int virtualNoteHeight = 120;

        private int RoundUp(int num, int factor)
        {
            return num + factor - 1 - (num - 1) % factor;
        }

        private IEventRenderer m_theme;
    }
}
