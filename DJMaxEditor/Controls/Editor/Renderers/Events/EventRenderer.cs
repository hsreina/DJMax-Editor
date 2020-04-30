using DJMaxEditor.DJMax;
using System.Collections.Generic;
using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Events
{
    public enum DisplayMode
    {
        DisplayModeHorizontal,
        DisplayModeVertical
    }

    public interface IEventRenderer 
    {
        string GetName();

        void RenderNote(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY);

        void RenderEventData(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY);

        void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds);

        IEnumerable<KeyValuePair<string, EventData>> GetTemplates();
    }

    internal abstract class EventRenderer : Renderer, IEventRenderer
    {

        public abstract string GetName();

        public abstract void RenderNote(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY);

        public abstract void RenderEventData(GraphicsWrapper g, EventData eventData, Rectangle eventRectangle, int centerX, int centerY);

        public abstract void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds);

        public abstract IEnumerable<KeyValuePair<string, EventData>> GetTemplates();

        public EventRenderer()
        {
            this.m_displayMode = DisplayMode.DisplayModeVertical;
        }

        protected void DrawZone(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds, Brush brush, string text, int from, int to)
        {
            int lineHeight = 4;
            int vx = bounds.X;
            int vy = bounds.Y;

            if (trackIndex == from)
            {
                Rectangle topLine = new Rectangle(trackX, trackY, width, lineHeight);
                topLine.Intersect(bounds);
                FillRectangle(
                    g,
                    brush,
                    topLine
                );
            }
            if (trackIndex == to)
            {
                Rectangle bottomLine = new Rectangle(trackX, trackY + height - lineHeight, width, lineHeight);
                bottomLine.Intersect(bounds);
                FillRectangle(
                    g,
                    brush,
                    bottomLine
                );

                int num2x = trackX;

                if (num2x < vx)
                {
                    num2x = vx;
                }

                DrawString(g, text, m_infoFont2, brush, num2x + 40, trackY + height - 50);
            }
        }

        protected KeyValuePair<string, EventData> CreateNoteFromEventData(string name, EventData eventData)
        {
            return new KeyValuePair<string, EventData>(name, eventData);
        }

        private Font m_infoFont2 = new Font("Tahoma", 20, FontStyle.Bold);

        public DisplayMode m_displayMode { get; protected set; }

        public EventDisplayMode EventDisplayMode { get; set; }
    }
}
