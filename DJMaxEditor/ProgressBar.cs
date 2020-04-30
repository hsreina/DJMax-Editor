using DJMaxEditor.Controls.Editor;
using System.Drawing;

namespace DJMaxEditor 
{
    internal class ProgressBar 
    {
        /// <summary>
        /// Current position
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// The width
        /// </summary>
        public int Width { get; set; }

        public ProgressBar()
        {
            m_brush1 = new SolidBrush(Color.FromArgb(255, 255, 38, 38));
            m__rect = new Rectangle(0, 0, 0, 0);
            Position = 0;
            Width = 3;
        }

        /// <summary>
        /// Render the Progressbar in Graphics/viewablePixels
        /// </summary>
        /// <param name="g"></param>
        /// <param name="viewablePixels"></param>
        public void Render(GraphicsWrapper g, Rectangle viewablePixels)
        {
            g.FillRectangle(m_brush1, Position - (Width / 2), viewablePixels.Y, Width, viewablePixels.Height);
        }

        /// <summary>
        /// The brush to track the ProgressBar
        /// </summary>
        private readonly Brush m_brush1;

        /// <summary>
        /// Rectangle of the ProgressBar
        /// </summary>
        private readonly Rectangle m__rect;
    }
}
