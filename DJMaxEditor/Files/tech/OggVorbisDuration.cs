using System;
using System.IO;

namespace DJMaxEditor.Files.Tech
{
    /// <summary>
    /// Reads an Ogg Vorbis file's playback length from its pages — no decoder required.
    /// Used when a .tech chart's BGM stem outlives the last packed note.
    /// </summary>
    internal static class OggVorbisDuration
    {
        /// <summary>
        /// Returns the stream length in seconds, or 0 when the file is missing / not Vorbis.
        /// </summary>
        public static double TryGetSeconds(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return 0.0;
            }

            try
            {
                using (var stream = File.OpenRead(path))
                {
                    return ReadSeconds(stream);
                }
            }
            catch (IOException)
            {
                return 0.0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0.0;
            }
        }

        private static double ReadSeconds(Stream stream)
        {
            int sampleRate = 0;
            long lastGranule = -1;
            var header = new byte[27];
            var segmentTable = new byte[255];

            while (true)
            {
                if (stream.Read(header, 0, 27) < 27)
                {
                    break;
                }

                if (header[0] != (byte)'O' || header[1] != (byte)'g' ||
                    header[2] != (byte)'g' || header[3] != (byte)'S')
                {
                    break;
                }

                long granule = BitConverter.ToInt64(header, 6);
                int pageSegments = header[26];
                if (pageSegments <= 0 ||
                    stream.Read(segmentTable, 0, pageSegments) < pageSegments)
                {
                    break;
                }

                int payload = 0;
                for (int i = 0; i < pageSegments; i++)
                {
                    payload += segmentTable[i];
                }

                if (sampleRate == 0 && payload >= 16)
                {
                    var body = new byte[payload];
                    if (stream.Read(body, 0, payload) < payload)
                    {
                        break;
                    }

                    // Vorbis identification header: 0x01 + "vorbis" + version + channels + rate.
                    if (body.Length >= 16 &&
                        body[0] == 1 &&
                        body[1] == (byte)'v' &&
                        body[2] == (byte)'o' &&
                        body[3] == (byte)'r' &&
                        body[4] == (byte)'b' &&
                        body[5] == (byte)'i' &&
                        body[6] == (byte)'s')
                    {
                        sampleRate = BitConverter.ToInt32(body, 12);
                    }
                }
                else
                {
                    stream.Seek(payload, SeekOrigin.Current);
                }

                if (granule >= 0)
                {
                    lastGranule = granule;
                }
            }

            if (sampleRate <= 0 || lastGranule <= 0)
            {
                return 0.0;
            }

            return lastGranule / (double)sampleRate;
        }
    }
}
