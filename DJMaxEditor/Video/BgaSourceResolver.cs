using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DJMaxEditor.Video
{
    /// <summary>
    /// Turns files Media Foundation cannot decode (notably Technika Bink previews) into
    /// cached H.264 MP4 via an optional FFmpeg child process.
    /// </summary>
    internal static class BgaSourceResolver
    {
        private static readonly HashSet<string> AlwaysTranscode =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bik", ".bik2", ".bk2", ".smk" };

        private static readonly string CacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DJMaxEditor", "bga-cache");

        private static readonly TimeSpan TranscodeTimeout = TimeSpan.FromMinutes(5);

        private static readonly string[] DiscoveryExtensions =
            { ".mp4", ".bik", ".mkv", ".wmv", ".avi", ".mov" };

        private static string _ffmpegProbed;
        private static bool _ffmpegProbeDone;
        private static string _ffmpegOverride;

        public static string ExplicitFfmpegPath
        {
            get { return _ffmpegOverride; }
            set
            {
                string cleaned = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (string.Equals(cleaned, _ffmpegOverride, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                _ffmpegOverride = cleaned;
                _ffmpegProbeDone = false;
                _ffmpegProbed = null;
            }
        }

        internal static bool RequiresTranscode(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                return AlwaysTranscode.Contains(Path.GetExtension(path));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Looks for a BGA beside a chart. Prefers &lt;folder&gt;_pre / &lt;stem&gt;_pre names,
        /// then a lone video file in the folder.
        /// </summary>
        public static string FindForChart(string chartPath)
        {
            if (string.IsNullOrEmpty(chartPath))
            {
                return null;
            }

            string folder;
            string chartStem;
            string songKey;
            try
            {
                folder = Path.GetDirectoryName(chartPath);
                chartStem = Path.GetFileNameWithoutExtension(chartPath);
                songKey = string.IsNullOrEmpty(folder) ? null : Path.GetFileName(folder);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            List<string> stems = new List<string>(5);
            if (!string.IsNullOrEmpty(songKey))
            {
                stems.Add(songKey + "_pre");
                stems.Add(songKey);
            }
            if (!string.IsNullOrEmpty(chartStem))
            {
                stems.Add(chartStem + "_pre");
                stems.Add(chartStem);
            }
            stems.Add("bga");

            foreach (string stem in stems)
            {
                foreach (string extension in DiscoveryExtensions)
                {
                    string candidate = Path.Combine(folder, stem + extension);
                    try
                    {
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }

            try
            {
                string lone = null;
                foreach (string file in Directory.EnumerateFiles(folder))
                {
                    string extension = Path.GetExtension(file);
                    bool supported = false;
                    for (int i = 0; i < DiscoveryExtensions.Length; i++)
                    {
                        if (string.Equals(extension, DiscoveryExtensions[i], StringComparison.OrdinalIgnoreCase))
                        {
                            supported = true;
                            break;
                        }
                    }

                    if (!supported)
                    {
                        continue;
                    }

                    if (lone != null)
                    {
                        return null;
                    }

                    lone = file;
                }

                return lone;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns a path Media Foundation can open. Blocking — call off the UI thread when
        /// a Bink conversion is required.
        /// </summary>
        internal static bool TryResolve(string path, out string playablePath, out string error)
        {
            playablePath = path;
            error = null;

            if (!RequiresTranscode(path))
            {
                return true;
            }

            if (!File.Exists(path))
            {
                error = "file not found";
                return false;
            }

            string ffmpeg = FindFfmpeg();
            if (ffmpeg == null)
            {
                error = Path.GetExtension(path).TrimStart('.').ToUpperInvariant() +
                    " video needs FFmpeg, " +
                    (string.IsNullOrEmpty(_ffmpegOverride)
                        ? "which was not found on PATH (place ffmpeg.exe beside the editor or on PATH)"
                        : "and there is no file at the configured path");
                return false;
            }

            string cached;
            try
            {
                cached = CachePathFor(path);
            }
            catch (Exception ex)
            {
                error = "cache path failed: " + ex.Message;
                return false;
            }

            if (File.Exists(cached) && new FileInfo(cached).Length > 0L)
            {
                playablePath = cached;
                return true;
            }

            return Transcode(ffmpeg, path, cached, out playablePath, out error);
        }

        private static bool Transcode(string ffmpeg, string source, string destination,
            out string playablePath, out string error)
        {
            playablePath = source;
            error = null;

            string partial = destination + ".part";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));

                // net48 has no ArgumentList — build a quoted command line.
                string arguments =
                    "-nostdin -hide_banner -loglevel error -y" +
                    " -i " + Quote(source) +
                    " -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p" +
                    " -movflags +faststart -f mp4 " + Quote(partial);

                var startInfo = new ProcessStartInfo(ffmpeg, arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        error = "FFmpeg did not start";
                        return false;
                    }

                    string stderr = process.StandardError.ReadToEnd();
                    process.StandardOutput.ReadToEnd();

                    if (!process.WaitForExit((int)TranscodeTimeout.TotalMilliseconds))
                    {
                        TryKill(process);
                        error = "FFmpeg timed out after " +
                            TranscodeTimeout.TotalMinutes.ToString(CultureInfo.InvariantCulture) + " minutes";
                        return false;
                    }

                    if (process.ExitCode != 0)
                    {
                        error = "FFmpeg failed: " + FirstLine(stderr);
                        return false;
                    }
                }

                if (!File.Exists(partial) || new FileInfo(partial).Length == 0L)
                {
                    error = "FFmpeg produced no output";
                    return false;
                }

                if (File.Exists(destination))
                {
                    File.Delete(destination);
                }
                File.Move(partial, destination);

                playablePath = destination;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
            finally
            {
                TryDelete(partial);
            }
        }

        private static string Quote(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "\"\"";
            }
            return "\"" + path.Replace("\"", "\\\"") + "\"";
        }

        private static string CachePathFor(string path)
        {
            FileInfo info = new FileInfo(path);
            string identity = info.FullName.ToUpperInvariant() + "|" +
                info.Length.ToString(CultureInfo.InvariantCulture) + "|" +
                info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);

            byte[] hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
            }

            StringBuilder name = new StringBuilder(24);
            for (int i = 0; i < 10; i++)
            {
                name.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
            }

            string stem = SanitiseStem(Path.GetFileNameWithoutExtension(path));
            string leaf = stem.Length == 0 ? name.ToString() : stem + "-" + name;
            return Path.Combine(CacheRoot, leaf + ".mp4");
        }

        private static string SanitiseStem(string stem)
        {
            if (string.IsNullOrEmpty(stem))
            {
                return string.Empty;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder text = new StringBuilder(stem.Length);
            foreach (char character in stem)
            {
                text.Append(Array.IndexOf(invalid, character) >= 0 ? '_' : character);
            }

            return text.ToString().Trim().TrimEnd('.');
        }

        internal static string FindFfmpeg()
        {
            if (_ffmpegProbeDone)
            {
                return _ffmpegProbed;
            }

            _ffmpegProbeDone = true;
            _ffmpegProbed = ProbeFfmpeg();
            return _ffmpegProbed;
        }

        private static string ProbeFfmpeg()
        {
            string explicitPath = _ffmpegOverride;
            if (!string.IsNullOrEmpty(explicitPath))
            {
                try
                {
                    return File.Exists(explicitPath) ? explicitPath : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            List<string> candidates = new List<string>();

            string pathVariable = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVariable))
            {
                foreach (string directory in pathVariable.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        candidates.Add(Path.Combine(directory.Trim(), "ffmpeg.exe"));
                    }
                }
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles))
            {
                candidates.Add(Path.Combine(programFiles, "ffmpeg", "bin", "ffmpeg.exe"));
            }
            candidates.Add(@"C:\ffmpeg\bin\ffmpeg.exe");

            string ourFolder = Path.GetDirectoryName(typeof(BgaSourceResolver).Assembly.Location);
            if (!string.IsNullOrEmpty(ourFolder))
            {
                candidates.Add(Path.Combine(ourFolder, "ffmpeg.exe"));
            }

            foreach (string candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (Exception)
                {
                }
            }

            return null;
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "no diagnostic output";
            }

            string[] lines = text.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    return trimmed.Length > 200 ? trimmed.Substring(0, 200) : trimmed;
                }
            }

            return "no diagnostic output";
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill();
            }
            catch (Exception)
            {
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
