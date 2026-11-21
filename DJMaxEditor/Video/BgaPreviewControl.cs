using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DJMaxEditor.Video
{
    /// <summary>
    /// WinForms BGA viewport: one Bitmap, drawn letterboxed and centred.
    /// </summary>
    public sealed class BgaPreviewControl : Control
    {
        private readonly BgaFrameBuffer _frame = new BgaFrameBuffer();
        private IBgaSource _source;
        private Bitmap _bitmap;
        private TimeSpan _onScreen = TimeSpan.MinValue;
        private string _error;

        public BgaPreviewControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.Black;
            Dock = DockStyle.Fill;
        }

        public IBgaSource Source
        {
            get { return _source; }
            set
            {
                if (ReferenceEquals(_source, value))
                {
                    return;
                }
                _source = value;
                Forget();
                Invalidate();
            }
        }

        public string StatusText
        {
            get
            {
                if (!string.IsNullOrEmpty(_error))
                {
                    return _error;
                }

                IBgaSource source = _source;
                if (source == null || !source.IsOpen)
                {
                    return string.Empty;
                }

                string text = source.PixelWidth.ToString(CultureInfo.InvariantCulture) + "x" +
                    source.PixelHeight.ToString(CultureInfo.InvariantCulture);

                if (!string.IsNullOrEmpty(source.Description))
                {
                    text = source.Description + " - " + text;
                }

                if (source.FrameRate > 0.0)
                {
                    text += " - " + source.FrameRate.ToString("0.##", CultureInfo.InvariantCulture) + " fps";
                }

                string duration = FormatDuration(source.Duration);
                if (duration != null)
                {
                    text += " - " + duration;
                }

                return text;
            }
        }

        public bool TryOpen(string path, out string error)
        {
            Forget();

            if (_source == null)
            {
                _source = new MediaFoundationBgaSource();
            }

            if (!_source.Open(path, out error))
            {
                _error = error;
                Invalidate();
                return false;
            }

            _frame.Resize(_source.PixelWidth, _source.PixelHeight);
            RebuildBitmap(_source.PixelWidth, _source.PixelHeight);
            Invalidate();
            return true;
        }

        public void Seek(TimeSpan position)
        {
            IBgaSource source = _source;
            if (source == null || !source.IsOpen)
            {
                return;
            }

            if (position < TimeSpan.Zero)
            {
                BlankFrame();
                return;
            }

            if (CurrentFrameStillCovers(position, source))
            {
                return;
            }

            if (!source.TryGetFrame(position, _frame))
            {
                return;
            }

            if (_frame.Timestamp == _onScreen || !_frame.HasFrame)
            {
                return;
            }

            if (_bitmap == null ||
                _bitmap.Width != _frame.Width ||
                _bitmap.Height != _frame.Height)
            {
                RebuildBitmap(_frame.Width, _frame.Height);
            }

            WriteFrameToBitmap();
            _onScreen = _frame.Timestamp;
            Invalidate();
        }

        public void Clear()
        {
            if (_source != null)
            {
                _source.Close();
            }
            Forget();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            if (_bitmap == null)
            {
                return;
            }

            RectangleF target = Letterbox(
                new RectangleF(0, 0, ClientSize.Width, ClientSize.Height),
                _bitmap.Width, _bitmap.Height);
            if (target.Width < 1f || target.Height < 1f)
            {
                return;
            }

            g.DrawImage(_bitmap, target);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_bitmap != null)
                {
                    _bitmap.Dispose();
                    _bitmap = null;
                }
                if (_source != null)
                {
                    _source.Dispose();
                    _source = null;
                }
            }
            base.Dispose(disposing);
        }

        public static RectangleF Letterbox(RectangleF bounds, int pixelWidth, int pixelHeight)
        {
            if (pixelWidth <= 0 || pixelHeight <= 0 || bounds.Width <= 0f || bounds.Height <= 0f)
            {
                return RectangleF.Empty;
            }

            float scale = Math.Min(bounds.Width / pixelWidth, bounds.Height / pixelHeight);
            float width = pixelWidth * scale;
            float height = pixelHeight * scale;

            return new RectangleF(
                bounds.X + ((bounds.Width - width) / 2f),
                bounds.Y + ((bounds.Height - height) / 2f),
                width,
                height);
        }

        private bool CurrentFrameStillCovers(TimeSpan position, IBgaSource source)
        {
            if (_bitmap == null || _onScreen == TimeSpan.MinValue || source == null ||
                source.FrameRate <= 0.0 || position < _onScreen)
            {
                return false;
            }

            double span = TimeSpan.TicksPerSecond / source.FrameRate;
            if (span <= 1.0)
            {
                return false;
            }

            long frameEnd = _onScreen.Ticks + (long)Math.Round(span);
            return position.Ticks < frameEnd;
        }

        private void RebuildBitmap(int width, int height)
        {
            if (_bitmap != null)
            {
                _bitmap.Dispose();
                _bitmap = null;
            }
            if (width <= 0 || height <= 0)
            {
                return;
            }
            _bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
        }

        private void WriteFrameToBitmap()
        {
            if (_bitmap == null || !_frame.HasFrame)
            {
                return;
            }

            BitmapData data = _bitmap.LockBits(
                new Rectangle(0, 0, _frame.Width, _frame.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppRgb);
            try
            {
                // GDI+ Format32bppRgb is still 4 bytes/pixel; MF gave us BGRA.
                if (data.Stride == _frame.Stride)
                {
                    Marshal.Copy(_frame.Pixels, 0, data.Scan0, _frame.Stride * _frame.Height);
                }
                else
                {
                    for (int y = 0; y < _frame.Height; y++)
                    {
                        Marshal.Copy(
                            _frame.Pixels,
                            y * _frame.Stride,
                            IntPtr.Add(data.Scan0, y * data.Stride),
                            Math.Min(_frame.Stride, Math.Abs(data.Stride)));
                    }
                }
            }
            finally
            {
                _bitmap.UnlockBits(data);
            }
        }

        private void Forget()
        {
            BlankFrame();
            _error = null;
        }

        private void BlankFrame()
        {
            if (_bitmap == null && _onScreen == TimeSpan.MinValue)
            {
                return;
            }

            if (_bitmap != null)
            {
                _bitmap.Dispose();
                _bitmap = null;
            }
            _frame.Clear();
            _onScreen = TimeSpan.MinValue;
            Invalidate();
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return null;
            }

            if (duration.TotalHours >= 1.0)
            {
                return ((int)duration.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" +
                    duration.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                    duration.Seconds.ToString("00", CultureInfo.InvariantCulture);
            }

            return ((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture) + ":" +
                duration.Seconds.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
