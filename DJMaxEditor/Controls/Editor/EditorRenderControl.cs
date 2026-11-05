using System;
using System.Windows.Forms;

namespace DJMaxEditor.Controls.Editor
{
    /// <summary>
    /// Paint surface for the legacy editor timeline. The editor repaints the
    /// whole client area on every frame, so OptimizedDoubleBuffer rasterizes
    /// off-screen and presents complete frames instead of drawing straight to
    /// the visible DC (which both tears and blocks the UI thread longer).
    /// </summary>
    public partial class EditorRenderControl : Control
    {
        public EditorRenderControl()
        {
            InitializeComponent();
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        }
    }
}
