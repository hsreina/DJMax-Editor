using System;
using System.Windows.Forms;

namespace DJMaxEditor.Controls.Editor
{
    class RenderTimer
    {
        public event EventHandler Tick;

        public int FrameRate
        {
            get { return frameRate; }
            set
            {
                frameRate = value;
                Setup();
            }
        }

        public RenderTimer()
        {
            renderTimer.Tick += renderTimer_Tick;
            Setup();
        }

        private readonly Timer renderTimer = new Timer();

        private int frameRate = 60;

        private void Setup()
        {
            if (frameRate < 0)
            {
                frameRate = 0;
            }                

            if (frameRate == 0 && renderTimer.Enabled)
            {
                renderTimer.Enabled = false;
                return;
            }

            renderTimer.Interval = (int)(1000.0 / FrameRate);

            if (renderTimer.Enabled == false)
            {
                renderTimer.Enabled = true;
            }
        }

        private void renderTimer_Tick(object sender, EventArgs e)
        {
            Tick?.Invoke(this, e);
        }
    }
}
