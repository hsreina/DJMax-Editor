using System;
using System.Globalization;
using System.Runtime.InteropServices;
using SharpDX;
using SharpDX.MediaFoundation;

namespace DJMaxEditor.Video
{
    /// <summary>
    /// Reference-counted MFStartup / MFShutdown for the process.
    /// </summary>
    internal static class MediaFoundationRuntime
    {
        internal const string MissingPlatformHint =
            "Media Foundation is not available on this Windows installation. " +
            "Install the Media Feature Pack (Settings > Apps > Optional features) to enable BGA " +
            "video preview.";

        internal const string MissingCodecHint =
            "If this is an ordinary MP4, WMV or AVI, this Windows edition may be missing the " +
            "Media Feature Pack (Settings > Apps > Optional features).";

        private static readonly object Gate = new object();
        private static int _refCount;
        private static string _failure;

        public static bool TryAcquire(out string error)
        {
            lock (Gate)
            {
                if (_failure != null)
                {
                    error = _failure;
                    return false;
                }

                if (_refCount > 0)
                {
                    _refCount++;
                    error = null;
                    return true;
                }

                try
                {
                    MediaManager.Startup(false);
                }
                catch (Exception ex)
                {
                    _failure = MissingPlatformHint + " (" + ex.GetType().Name + ": " + ex.Message + ")";
                    Logs.Write("BGA: MFStartup threw {0}: {1}", ex.GetType().Name, ex.Message);
                    error = _failure;
                    return false;
                }

                _refCount = 1;
                error = null;
                return true;
            }
        }

        public static void Release()
        {
            lock (Gate)
            {
                if (_refCount <= 0)
                {
                    return;
                }

                _refCount--;
                if (_refCount > 0)
                {
                    return;
                }

                try
                {
                    MediaManager.Shutdown();
                }
                catch (Exception ex)
                {
                    Logs.Write("BGA: MFShutdown threw {0}", ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// Frame-accurate BGA decoding via Media Foundation's source reader (SharpDX bindings).
    /// The sequencer owns time; this class only maps a position to RGB32 pixels.
    /// </summary>
    public sealed class MediaFoundationBgaSource : IBgaSource
    {
        private const long NoTimestamp = long.MinValue;
        private const long ForwardSeekToleranceTicks = TimeSpan.TicksPerSecond;
        private const int MaxFramesToSkip = 300;
        private const long FallbackFrameSpanTicks = TimeSpan.TicksPerSecond / 25;
        private const int SeekBackOffAttempts = 12;

        private SourceReader _reader;
        private bool _platformHeld;
        private bool _disposed;

        private int _width;
        private int _height;
        private int _defaultStride;
        private double _frameRate;
        private TimeSpan _duration;
        private string _description = string.Empty;

        private long _readerTicks = NoTimestamp;
        private long _heldTicks = NoTimestamp;
        private long _heldSpanTicks;
        private bool _endOfStream;

        public bool IsOpen
        {
            get { return _reader != null; }
        }

        public int PixelWidth
        {
            get { return _width; }
        }

        public int PixelHeight
        {
            get { return _height; }
        }

        public TimeSpan Duration
        {
            get { return _duration; }
        }

        public double FrameRate
        {
            get { return _frameRate; }
        }

        public string Description
        {
            get { return _description; }
        }

        public bool Open(string path, out string error)
        {
            Close();

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "No video file specified.";
                return false;
            }

            if (!System.IO.File.Exists(path))
            {
                error = "File not found: " + path;
                return false;
            }

            if (!MediaFoundationRuntime.TryAcquire(out error))
            {
                return false;
            }

            _platformHeld = true;

            try
            {
                using (var attributes = new MediaAttributes(1))
                {
                    attributes.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, true);
                    _reader = new SourceReader(path, attributes);
                }

                ConfigureStreams();
                ReadFormat();
                ReadDuration();
                ReadDescription();

                if (_width <= 0 || _height <= 0)
                {
                    error = "The file has no readable video stream.";
                    Close();
                    return false;
                }

                _readerTicks = 0;
                error = null;
                Logs.Write(
                    "BGA: opened {0} - {1}x{2} stride {3} {4:0.###} fps {5}",
                    path, _width, _height, _defaultStride, _frameRate, _duration);
                return true;
            }
            catch (SharpDXException ex)
            {
                error = DescribeHResult(ex.ResultCode, ex.Message);
                Logs.Write("BGA: open failed 0x{0:X8} for {1}", ex.ResultCode.Code, path);
                Close();
                return false;
            }
            catch (Exception ex)
            {
                error = "Could not open the video: " + ex.Message;
                Logs.Write("BGA: open threw {0}: {1}", ex.GetType().Name, ex.Message);
                Close();
                return false;
            }
        }

        public void Close()
        {
            if (_reader != null)
            {
                try
                {
                    _reader.Dispose();
                }
                catch (Exception ex)
                {
                    Logs.Write("BGA: reader dispose threw {0}", ex.Message);
                }
                _reader = null;
            }

            _width = 0;
            _height = 0;
            _defaultStride = 0;
            _frameRate = 0.0;
            _duration = TimeSpan.Zero;
            _description = string.Empty;
            _readerTicks = NoTimestamp;
            _heldTicks = NoTimestamp;
            _heldSpanTicks = 0;
            _endOfStream = false;

            if (_platformHeld)
            {
                _platformHeld = false;
                MediaFoundationRuntime.Release();
            }
        }

        public bool TryGetFrame(TimeSpan position, BgaFrameBuffer destination)
        {
            if (destination == null || _reader == null)
            {
                return false;
            }

            try
            {
                return ReadInto(position, destination);
            }
            catch (Exception ex)
            {
                Logs.Write(
                    "BGA: decode failed at {0} - {1}: {2}", position, ex.GetType().Name, ex.Message);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Close();
        }

        private void ConfigureStreams()
        {
            _reader.SetStreamSelection(SourceReaderIndex.AllStreams, false);
            _reader.SetStreamSelection(SourceReaderIndex.FirstVideoStream, true);

            using (var target = new MediaType())
            {
                target.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                target.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.Rgb32);
                _reader.SetCurrentMediaType(SourceReaderIndex.FirstVideoStream, target);
            }
        }

        private void ReadFormat()
        {
            using (MediaType current = _reader.GetCurrentMediaType(SourceReaderIndex.FirstVideoStream))
            {
                try
                {
                    long packed = current.Get(MediaTypeAttributeKeys.FrameSize);
                    _width = (int)(packed >> 32);
                    _height = (int)(packed & 0xFFFFFFFFL);
                }
                catch (SharpDXException)
                {
                }

                try
                {
                    long rate = current.Get(MediaTypeAttributeKeys.FrameRate);
                    uint numerator = (uint)(rate >> 32);
                    uint denominator = (uint)(rate & 0xFFFFFFFFL);
                    _frameRate = denominator == 0 ? 0.0 : numerator / (double)denominator;
                }
                catch (SharpDXException)
                {
                    _frameRate = 0.0;
                }

                try
                {
                    _defaultStride = current.Get(MediaTypeAttributeKeys.DefaultStride);
                }
                catch (SharpDXException)
                {
                    _defaultStride = 0;
                }

                if (_defaultStride == 0 && _width > 0)
                {
                    _defaultStride = _width * 4;
                }
            }
        }

        private void ReadDuration()
        {
            try
            {
                long ticks = _reader.GetPresentationAttribute(
                    SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration);
                if (ticks > 0)
                {
                    _duration = TimeSpan.FromTicks(ticks);
                }
            }
            catch (Exception)
            {
                _duration = TimeSpan.Zero;
            }
        }

        private void ReadDescription()
        {
            string codec = null;
            try
            {
                using (MediaType native =
                    _reader.GetNativeMediaType(SourceReaderIndex.FirstVideoStream, 0))
                {
                    Guid subtype = native.Get(MediaTypeAttributeKeys.Subtype);
                    codec = SubtypeName(subtype);
                }
            }
            catch (Exception)
            {
                codec = null;
            }

            string container = null;
            try
            {
                container = _reader.GetPresentationAttribute(
                    SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.MimeType);
            }
            catch (Exception)
            {
                container = null;
            }

            if (!string.IsNullOrEmpty(codec) && !string.IsNullOrEmpty(container))
            {
                _description = codec + " (" + container + ")";
            }
            else
            {
                _description = codec ?? container ?? string.Empty;
            }
        }

        private bool ReadInto(TimeSpan position, BgaFrameBuffer destination)
        {
            long target = position.Ticks;
            if (target < 0)
            {
                target = 0;
            }
            if (_duration > TimeSpan.Zero && target >= _duration.Ticks)
            {
                target = Math.Max(0, _duration.Ticks - NominalFrameSpanTicks);
            }

            bool wantsHeldFrame = HoldsFrameFor(target);
            if (wantsHeldFrame && destination.Timestamp.Ticks == _heldTicks &&
                destination.Width == _width && destination.Height == _height)
            {
                return true;
            }

            bool seek = _readerTicks == NoTimestamp
                || target < _readerTicks
                || target - _readerTicks > ForwardSeekToleranceTicks
                || wantsHeldFrame;

            if (seek)
            {
                SeekTo(target);
            }

            long lastSeen;
            if (DecodeTo(target, destination, out lastSeen))
            {
                return true;
            }

            if (lastSeen != NoTimestamp && lastSeen < target)
            {
                SeekTo(lastSeen);
                long ignored;
                if (DecodeTo(lastSeen, destination, out ignored))
                {
                    _endOfStream = true;
                    return true;
                }
            }

            return _heldTicks != NoTimestamp && destination.Timestamp.Ticks == _heldTicks;
        }

        private void SeekTo(long ticks)
        {
            if (TrySetPosition(ticks))
            {
                return;
            }

            long low = _heldTicks != NoTimestamp ? _heldTicks : 0;
            long high = ticks;
            long best = NoTimestamp;
            long precision = NominalFrameSpanTicks;

            for (int i = 0; i < SeekBackOffAttempts && high - low > precision; i++)
            {
                long middle = low + ((high - low) / 2);
                if (middle <= low)
                {
                    break;
                }

                if (TrySetPosition(middle))
                {
                    best = middle;
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            if (best != NoTimestamp)
            {
                if (_readerTicks != best)
                {
                    TrySetPosition(best);
                }
                return;
            }

            Logs.Write(
                "BGA: seek to {0} refused, reading on from {1}",
                TimeSpan.FromTicks(ticks),
                _readerTicks == NoTimestamp ? TimeSpan.Zero : TimeSpan.FromTicks(_readerTicks));
        }

        private bool TrySetPosition(long ticks)
        {
            try
            {
                _reader.SetCurrentPosition(ticks);
                _readerTicks = ticks;
                _endOfStream = false;
                return true;
            }
            catch (SharpDXException ex)
            {
                if (ex.ResultCode == ResultCode.InvalidPosition)
                {
                    return false;
                }
                throw;
            }
        }

        private bool DecodeTo(long target, BgaFrameBuffer destination, out long lastSeen)
        {
            lastSeen = NoTimestamp;

            for (int skipped = 0; skipped < MaxFramesToSkip && !_endOfStream; skipped++)
            {
                int actualStream;
                SourceReaderFlags flags;
                long timestamp;
                Sample sample = _reader.ReadSample(
                    SourceReaderIndex.FirstVideoStream, SourceReaderControlFlags.None,
                    out actualStream, out flags, out timestamp);

                try
                {
                    if ((flags & SourceReaderFlags.Currentmediatypechanged) != 0)
                    {
                        ReadFormat();
                    }

                    if ((flags & SourceReaderFlags.Endofstream) != 0)
                    {
                        _endOfStream = true;
                        break;
                    }

                    if (sample == null)
                    {
                        continue;
                    }

                    _readerTicks = timestamp;
                    lastSeen = timestamp;
                    long span = FrameSpanTicks(sample);

                    bool stillEarly = timestamp + span <= target;
                    if (stillEarly && skipped + 1 < MaxFramesToSkip)
                    {
                        continue;
                    }

                    if (!CopyFrame(sample, destination))
                    {
                        return false;
                    }

                    destination.Timestamp = TimeSpan.FromTicks(timestamp);
                    _heldTicks = timestamp;
                    _heldSpanTicks = span;
                    return true;
                }
                finally
                {
                    if (sample != null)
                    {
                        sample.Dispose();
                    }
                }
            }

            return false;
        }

        private bool HoldsFrameFor(long target)
        {
            if (_heldTicks == NoTimestamp || target < _heldTicks)
            {
                return false;
            }
            if (_endOfStream)
            {
                return true;
            }
            long span = _heldSpanTicks > 0 ? _heldSpanTicks : NominalFrameSpanTicks;
            return target < _heldTicks + span;
        }

        private long NominalFrameSpanTicks
        {
            get
            {
                return _frameRate > 0.0
                    ? (long)Math.Round(TimeSpan.TicksPerSecond / _frameRate)
                    : FallbackFrameSpanTicks;
            }
        }

        private long FrameSpanTicks(Sample sample)
        {
            try
            {
                long duration = sample.SampleDuration;
                if (duration > 0)
                {
                    return duration;
                }
            }
            catch (Exception)
            {
            }

            if (_frameRate > 0.0)
            {
                return NominalFrameSpanTicks;
            }
            return FallbackFrameSpanTicks;
        }

        private bool CopyFrame(Sample sample, BgaFrameBuffer destination)
        {
            if (_width <= 0 || _height <= 0)
            {
                return false;
            }

            destination.Resize(_width, _height);

            MediaBuffer buffer = sample.BufferCount == 1
                ? sample.GetBufferByIndex(0)
                : sample.ConvertToContiguousBuffer();

            try
            {
                int maxLength;
                int currentLength;
                IntPtr data = buffer.Lock(out maxLength, out currentLength);
                try
                {
                    CopyRowsFromMemoryOrder(data, _defaultStride, destination);
                    return true;
                }
                finally
                {
                    buffer.Unlock();
                }
            }
            finally
            {
                buffer.Dispose();
            }
        }

        private static void CopyRowsFromMemoryOrder(IntPtr data, int stride, BgaFrameBuffer destination)
        {
            // IMFMediaBuffer.Lock after the RGB32 video-processor path hands back a linear
            // top-down buffer. MF_MT_DEFAULT_STRIDE can still be negative (bottom-up media
            // type); using that sign to reverse rows flips a picture that was already upright
            // (closer.mp4 logged "stride -5120" and rendered upside-down). Pitch is |stride|.
            int sourceStride = Math.Abs(stride);
            if (sourceStride == 0)
            {
                sourceStride = destination.Stride;
            }

            if (sourceStride == destination.Stride)
            {
                Marshal.Copy(data, destination.Pixels, 0, destination.Stride * destination.Height);
                return;
            }

            int rowBytes = Math.Min(sourceStride, destination.Stride);
            for (int y = 0; y < destination.Height; y++)
            {
                Marshal.Copy(
                    IntPtr.Add(data, y * sourceStride),
                    destination.Pixels,
                    y * destination.Stride,
                    rowBytes);
            }
        }

        private static string DescribeHResult(Result result, string fallback)
        {
            if (result == ResultCode.UnsupportedByteStreamType ||
                result == ResultCode.InvalidFileFormat)
            {
                return "Media Foundation does not recognise this file as a media container. " +
                    MediaFoundationRuntime.MissingCodecHint;
            }
            if (result == ResultCode.InvalidMediaType)
            {
                return "No decoder is installed for this video's codec. " +
                    MediaFoundationRuntime.MissingCodecHint;
            }
            if (result == ResultCode.UnsupportedScheme)
            {
                return "Media Foundation cannot read from this location.";
            }

            return "Could not open the video (0x" +
                result.Code.ToString("X8", CultureInfo.InvariantCulture) + "): " + fallback;
        }

        private static string SubtypeName(Guid subtype)
        {
            byte[] bytes = subtype.ToByteArray();
            bool fourCc =
                bytes[4] == 0x00 && bytes[5] == 0x00 && bytes[6] == 0x10 && bytes[7] == 0x00 &&
                bytes[8] == 0x80 && bytes[9] == 0x00 && bytes[10] == 0x00 && bytes[11] == 0xAA &&
                bytes[12] == 0x00 && bytes[13] == 0x38 && bytes[14] == 0x9B && bytes[15] == 0x71;

            if (fourCc)
            {
                char[] chars = new char[4];
                for (int i = 0; i < 4; i++)
                {
                    byte b = bytes[i];
                    if (b < 0x20 || b > 0x7E)
                    {
                        return subtype.ToString("D");
                    }
                    chars[i] = (char)b;
                }
                return new string(chars).Trim();
            }

            return subtype.ToString("D");
        }
    }
}
