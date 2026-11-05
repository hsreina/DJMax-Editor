using System;

namespace DJMaxEditor.Video
{
    /// <summary>
    /// A decoder the BGA preview can pull single frames out of, by absolute time.
    /// The editor's sequencer owns time; a source is a pure function from a position to a frame.
    /// </summary>
    public interface IBgaSource : IDisposable
    {
        bool IsOpen { get; }

        int PixelWidth { get; }

        int PixelHeight { get; }

        /// <summary><see cref="TimeSpan.Zero"/> when the container does not declare a duration.</summary>
        TimeSpan Duration { get; }

        /// <summary>Frames per second, or 0 when unknown.</summary>
        double FrameRate { get; }

        /// <summary>Codec / container summary for the status line.</summary>
        string Description { get; }

        /// <summary>
        /// Opens <paramref name="path"/>. Must never throw: failure returns false with a
        /// human-readable <paramref name="error"/>.
        /// </summary>
        bool Open(string path, out string error);

        void Close();

        /// <summary>
        /// Decodes the frame at or just before <paramref name="position"/> into
        /// <paramref name="destination"/> as BGRA32. Returns false if no frame is available.
        /// </summary>
        bool TryGetFrame(TimeSpan position, BgaFrameBuffer destination);
    }

    /// <summary>
    /// One decoded frame in Bgr32 layout, reused across frames to avoid LOH traffic while scrubbing.
    /// </summary>
    public sealed class BgaFrameBuffer
    {
        public BgaFrameBuffer()
        {
            // MinValue cannot collide with a real presentation time (frame 0 is TimeSpan.Zero).
            Timestamp = TimeSpan.MinValue;
        }

        public byte[] Pixels { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        public int Stride { get; private set; }

        public TimeSpan Timestamp { get; set; }

        public bool HasFrame
        {
            get { return Pixels != null && Timestamp != TimeSpan.MinValue; }
        }

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (Pixels != null && Width == width && Height == height)
            {
                return;
            }

            Width = width;
            Height = height;
            Stride = width * 4;
            Pixels = new byte[Stride * height];
            Timestamp = TimeSpan.MinValue;
        }

        public void Clear()
        {
            Timestamp = TimeSpan.MinValue;
        }
    }
}
