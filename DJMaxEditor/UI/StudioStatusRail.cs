using System.Drawing;
using System.Windows.Forms;

namespace DJMaxEditor.UI
{
    public sealed class StudioStatusRail : Control
    {
        private string _leftText = "READY";
        private string _centerText = "SNAP 1/8";
        private string _rightText = "NO DOCUMENT";
        private string _performanceText = "idle";

        public StudioStatusRail()
        {
            BackColor = StudioDesignSystem.Void;
            Dock = DockStyle.Bottom;
            DoubleBuffered = true;
            Font = StudioDesignSystem.UtilityFont(8f);
            ForeColor = StudioDesignSystem.Muted;
            Height = 28;
            MinimumSize = new Size(0, 28);
        }

        public void SetStatus(string left, string center, string right)
        {
            _leftText = string.IsNullOrWhiteSpace(left) ? "READY" : left;
            _centerText = string.IsNullOrWhiteSpace(center) ? "SNAP --" : center;
            _rightText = string.IsNullOrWhiteSpace(right) ? "NO DOCUMENT" : right;
            Invalidate();
        }

        /// <summary>
        /// The playback pump readout ("16.7 ms  60 fps"), shown in the rightmost slot
        /// while the timeline plays. Empty clears the slot; while playback is idle the
        /// last reading stays, matching the reference project's perf bar.
        /// </summary>
        public void SetPerformance(string performance)
        {
            _performanceText = performance ?? string.Empty;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle bounds = ClientRectangle;

            using (var line = new Pen(StudioDesignSystem.PulseCyan))
            using (var beat = new Pen(StudioDesignSystem.BeatViolet))
            using (var text = new SolidBrush(ForeColor))
            using (var bright = new SolidBrush(StudioDesignSystem.Frost))
            {
                e.Graphics.DrawLine(line, 0, 0, bounds.Width, 0);
                int pulseWidth = System.Math.Min(bounds.Width / 5, 280);
                e.Graphics.DrawLine(beat, 0, 1, pulseWidth, 1);

                var format = new StringFormat
                {
                    Alignment = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                e.Graphics.DrawString(_leftText, Font, bright,
                    new RectangleF(12, 3, bounds.Width * 0.4f, bounds.Height - 3), format);

                format.Alignment = StringAlignment.Center;
                e.Graphics.DrawString(_centerText, Font, text,
                    new RectangleF(bounds.Width * 0.38f, 3, bounds.Width * 0.24f, bounds.Height - 3), format);

                format.Alignment = StringAlignment.Far;
                bool hasPerformance = _performanceText.Length > 0;
                float rightLeft = hasPerformance ? bounds.Width * 0.60f : bounds.Width * 0.62f;
                float rightWidth = (hasPerformance ? bounds.Width * 0.20f : bounds.Width * 0.38f) - 12;
                e.Graphics.DrawString(_rightText, Font, text,
                    new RectangleF(rightLeft, 3, rightWidth, bounds.Height - 3), format);

                if (hasPerformance)
                {
                    using (var perfBrush = new SolidBrush(StudioDesignSystem.PulseCyan))
                    {
                        e.Graphics.DrawString(_performanceText, Font, perfBrush,
                            new RectangleF(bounds.Width * 0.80f, 3,
                                bounds.Width * 0.20f - 12, bounds.Height - 3),
                            format);
                    }
                }
            }
        }
    }
}
