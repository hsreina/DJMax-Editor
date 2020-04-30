using DJMaxEditor.Controls.Editor.Renderers.Events;
using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers
{
    internal abstract class Renderer
    {
        protected Rectangle Rectangle;

        protected Renderer()
        {
            DisplayMode = DisplayMode.DisplayModeVertical;
        }

        protected void DrawRectangle(GraphicsWrapper g, Pen pen, int x, int y, int width, int height)
        {
            g.DrawRectangle(pen, x, y, width, height);
        }

        protected void DrawString(GraphicsWrapper g, string s, Font font, Brush brush, float x, float y)
        {
            g.DrawString(s, font, brush, x, y);
        }

        protected void FillRectangle(GraphicsWrapper g, Brush brush, Rectangle rect)
        {
            g.FillRectangle(brush, rect);
        }

        protected void FillRectangle(GraphicsWrapper g, Brush brush, int x, int y, int width, int height)
        {
            g.FillRectangle(brush, x, y, width, height);
        }

        protected void DrawImage(GraphicsWrapper g, Image image, int x, int y, int width, int height)
        {
            Rectangle.X = x;
            Rectangle.Y = y;
            Rectangle.Width = width;
            Rectangle.Height = height;

            g.DrawImage(image,
                Rectangle,
                0,
                0,
                width,
                height,
                GraphicsUnit.Pixel,
                null
            );
        }

        protected readonly Font InfoFont2 = new Font("Tahoma", 20, FontStyle.Bold);

        public DisplayMode DisplayMode { get; protected set; }
    }
}
