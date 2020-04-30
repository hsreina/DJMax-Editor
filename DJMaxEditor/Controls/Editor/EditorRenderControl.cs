using System;
using System.Windows.Forms;

namespace DJMaxEditor.Controls.Editor
{
    public partial class EditorRenderControl : UserControl
    {
        public EditorRenderControl()
        {
            InitializeComponent();
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

        /*
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            Invalidate();
        }

        private void renderTimer_Tick(object sender, EventArgs e)
        {
            // Invalidate();
        }
        */
    }
}
