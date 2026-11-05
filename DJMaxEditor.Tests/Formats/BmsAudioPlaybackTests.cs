using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace DJMaxEditor.Tests
{
    internal static partial class Program
    {
        private static void RunBmsAudioTests()
        {
            Test("Bms_AudioPauseFreezesEveryOverlappingLongSample", () =>
            {
                string fmodDirectory = FindReleaseFmodDirectory();
                AssertTrue(fmodDirectory != null,
                    "FMOD test runtime was not built (build the solution in Release)");
                SetDllDirectory(fmodDirectory);
                string previousDirectory = Environment.CurrentDirectory;
                Environment.CurrentDirectory = Path.GetFullPath(Path.Combine(fmodDirectory, @"..\.."));

                string wavePath = Path.Combine(Path.GetTempPath(),
                    "djmax-editor-pause-" + Guid.NewGuid().ToString("N") + ".wav");
                WriteSilentWave(wavePath, 44100 * 2);

                var audio = new AudioPlayerFmodEx();
                try
                {
                    AssertTrue(audio.LoadSound(1, wavePath), "silent test sound did not load");
                    AssertTrue(audio.PlaySound(0, 1, 1.0f, 64), "first long sample did not start");
                    Thread.Sleep(40);
                    FMODEX.Channel first = GetActiveChannel(audio, 0);
                    uint firstMovingBefore = GetPcmPosition(first);
                    Thread.Sleep(80);
                    UpdateFmod(audio);
                    uint firstMovingAfter = GetPcmPosition(first);
                    AssertTrue(firstMovingAfter - firstMovingBefore > 1024,
                        "FMOD test sample was not advancing before pause");

                    AssertTrue(audio.PlaySound(0, 1, 1.0f, 64), "overlapping long sample did not start");
                    FMODEX.Channel second = GetActiveChannel(audio, 0);
                    Thread.Sleep(80);
                    UpdateFmod(audio);

                    audio.PauseAllSounds();
                    uint firstBefore = GetPcmPosition(first);
                    uint secondBefore = GetPcmPosition(second);
                    Thread.Sleep(120);
                    UpdateFmod(audio);
                    uint firstAfter = GetPcmPosition(first);
                    uint secondAfter = GetPcmPosition(second);

                    AssertTrue(firstAfter - firstBefore < 1024,
                        "the older overlapping sample advanced while paused");
                    AssertTrue(secondAfter - secondBefore < 1024,
                        "the newest overlapping sample advanced while paused");

                    audio.StopAllSounds();
                    AssertTrue(audio.PlaySound(0, 1, 1.0f, 64),
                        "sample did not restart after stopping from pause");
                    FMODEX.Channel restarted = GetActiveChannel(audio, 0);
                    uint restartBefore = GetPcmPosition(restarted);
                    Thread.Sleep(80);
                    UpdateFmod(audio);
                    uint restartAfter = GetPcmPosition(restarted);
                    AssertTrue(restartAfter - restartBefore > 1024,
                        "stopping while paused left future playback muted");
                }
                finally
                {
                    audio.StopAllSounds();
                    Environment.CurrentDirectory = previousDirectory;
                    File.Delete(wavePath);
                }
            });
        }

        // Walks up from the test output until the Release build's fmod folder is found, so
        // the test does not depend on how many directory levels the output folder is below
        // the repo root.
        private static string FindReleaseFmodDirectory()
        {
            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(
                    directory.FullName, "DJMaxEditor", "bin", "Release", "libs", "fmod");
                if (File.Exists(Path.Combine(candidate, "fmodex.dll")))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
            return null;
        }

        private static FMODEX.Channel GetActiveChannel(AudioPlayerFmodEx audio, int index)
        {
            var field = typeof(AudioPlayerFmodEx).GetField("_channels",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var channels = (FMODEX.Channel[])field.GetValue(audio);
            var snapshot = new FMODEX.Channel();
            snapshot.setRaw(channels[index].getRaw());
            return snapshot;
        }

        private static uint GetPcmPosition(FMODEX.Channel channel)
        {
            uint position = 0;
            var result = channel.getPosition(ref position, FMODEX.TIMEUNIT.PCM);
            AssertTrue(result == FMODEX.RESULT.OK, "FMOD could not read sample position");
            return position;
        }

        private static void UpdateFmod(AudioPlayerFmodEx audio)
        {
            var field = typeof(AudioPlayerFmodEx).GetField("m_system",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var system = (FMODEX.System)field.GetValue(audio);
            AssertTrue(system.update() == FMODEX.RESULT.OK, "FMOD update failed");
        }

        private static void WriteSilentWave(string path, int sampleCount)
        {
            const int sampleRate = 44100;
            const short channels = 1;
            const short bitsPerSample = 16;
            int dataLength = sampleCount * channels * (bitsPerSample / 8);
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataLength);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write(channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * (bitsPerSample / 8));
                writer.Write((short)(channels * (bitsPerSample / 8)));
                writer.Write(bitsPerSample);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataLength);
                writer.Write(new byte[dataLength]);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string path);
    }
}
