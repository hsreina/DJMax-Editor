using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DJMaxEditor.Video
{
    /// <summary>
    /// Non-UI BGA decoder session: open a file, seek by chart time, expose a reusable Bitmap.
    /// Shared by the standalone BGA panel and the gameplay-preview backdrop.
    /// </summary>
    public sealed class BgaPlayer : IDisposable
    {
        private readonly BgaFrameBuffer _frame = new BgaFrameBuffer();
        private IBgaSource _source;
        private Bitmap _bitmap;
        private TimeSpan _onScreen = TimeSpan.MinValue;
        private string _error;
        private bool _disposed;

        public IBgaSource Source
        {
            get { return _source; }
        }

        public bool IsOpen
        {
            get { return _source != null && _source.IsOpen; }
        }

        /// <summary>The frame currently decoded, or null when nothing is on screen.</summary>
        public Bitmap CurrentBitmap
        {
            get { return _bitmap; }
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
                return false;
            }

            _frame.Resize(_source.PixelWidth, _source.PixelHeight);
            RebuildBitmap(_source.PixelWidth, _source.PixelHeight);
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
        }

        public void Clear()
        {
            if (_source != null)
            {
                _source.Close();
            }
            Forget();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Clear();
            if (_source != null)
            {
                _source.Dispose();
                _source = null;
            }
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
