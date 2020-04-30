using System.Drawing;
using System.Drawing.Imaging;

namespace DJMaxEditor.Controls.Editor
{
    public class GraphicsWrapper
    {
        private Graphics m_graphics;

        public void UpdateGraphics(Graphics graphics)
        {
            m_graphics = graphics;
        }

        public void FillRectangle(Brush brush, RectangleF rect)
        {
            m_graphics?.FillRectangle(brush, rect);
        }

        public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, ImageAttributes imageAttrs)
        {
            m_graphics?.DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs);
        }

        public void DrawString(string s, Font font, Brush brush, float x, float y)
        {
            m_graphics?.DrawString(s, font, brush, x, y);
        }

        public void DrawRectangle(Pen pen, int x, int y, int width, int height)
        {
            m_graphics?.DrawRectangle(pen, x, y, width, height);
        }

        public void DrawRectangle(Pen pen, Rectangle rect)
        {
            m_graphics?.DrawRectangle(pen, rect);
        }

        public void FillRectangle(Brush brush, Rectangle rect)
        {
            m_graphics?.FillRectangle(brush, rect);
        }

        public void FillRectangle(Brush brush, int x, int y, int width, int height)
        {
            m_graphics?.FillRectangle(brush, x, y, width, height);
        }

        public void DrawString(string s, Font font, Brush brush, float x, float y, StringFormat format)
        {
            m_graphics?.DrawString(s, font, brush, x, y, format);
        }
    }
}
