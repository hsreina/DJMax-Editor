using System;

namespace DJMaxEditor.Video
{
    /// <summary>
    /// Placeholder when Media Foundation is unavailable (e.g. Windows N without the Feature Pack).
    /// </summary>
    public sealed class NullBgaSource : IBgaSource
    {
        public bool IsOpen
        {
            get { return false; }
        }

        public int PixelWidth
        {
            get { return 0; }
        }

        public int PixelHeight
        {
            get { return 0; }
        }

        public TimeSpan Duration
        {
            get { return TimeSpan.Zero; }
        }

        public double FrameRate
        {
            get { return 0.0; }
        }

        public string Description
        {
            get { return string.Empty; }
        }

        public bool Open(string path, out string error)
        {
            error = "Video preview unavailable.";
            return false;
        }

        public void Close()
        {
        }

        public bool TryGetFrame(TimeSpan position, BgaFrameBuffer destination)
        {
            return false;
        }

        public void Dispose()
        {
        }
    }
}
