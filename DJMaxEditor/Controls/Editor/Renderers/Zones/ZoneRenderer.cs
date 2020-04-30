using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Zones
{
    internal interface IZoneRenderer
    {
        void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds);

        string GetName();
    }

    internal abstract class ZoneRenderer : Renderer, IZoneRenderer
    {
        public abstract void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds);

        public abstract string GetName();

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

                DrawString(g, text, InfoFont2, brush, num2x + 40, trackY + height - 50);
            }
        }
    }
}
