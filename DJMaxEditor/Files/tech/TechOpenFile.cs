using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Files.Tech
{
    /// <summary>
    /// Opens a TECHMANIA <c>track.tech</c> file. TECHMANIA is the TECHNIKA-style fan
    /// game, and its native note vocabulary is the one the TECHNIKA preview already
    /// speaks, so the import lands on the same four-lane layout with end-of-scan marker
    /// tracks and the same attribute grammar a real .pt uses. Notes authored on format
    /// lanes past the playable set (the format's invisible/autoplay keysound lanes,
    /// up to the format ceiling of 63) map onto the TECHNIKA specials (16 BGM /
    /// 17 BGA / 18 Speed / 19 Soundtrack) and keysound tracks from 20 upward.
    ///
    /// A container packs one pattern per difficulty; <see cref="SelectedPatternIndex"/>
    /// selects which slot opens (set from the chooser dialog before parsing) while every
    /// other slot is retained verbatim for save-back.
    /// </summary>
    internal sealed class TechOpenFile : IOpenFile
    {
        /// <summary>Slot in the container's patterns array to import, chosen by the chooser.</summary>
        public int SelectedPatternIndex { get; set; }

        public bool Open(string filename, out PlayerData playerData)
        {
            try
            {
                playerData = TechmaniaChartSerializer.Parse(
                    File.ReadAllBytes(filename), Math.Max(0, SelectedPatternIndex));
                // Some DJMAX-converted patterns (e.g. Everything Star/Pop) start a long
                // 0-*.ogg BGM stem but place no trailing end marker past the last keysound.
                // Extend the document end so playback covers that audio tail.
                ApplyBgmAudioTail(playerData, Path.GetDirectoryName(filename));
                return true;
            }
            catch (ChartLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ChartLoadException(ChartLoadError.Unexpected,
                    "The TECHMANIA track file could not be read.", null, ex);
            }
        }

        /// <summary>
        /// Raises <see cref="PlayerData.HeaderEndTick"/> to cover each <c>0-…</c> BGM stem's
        /// file length past its trigger tick. Notes-only MaxTick stops early on charts that
        /// omit a silent end marker (Everything Star/Pop); Q difficulties that already carry
        /// one keep the later authored end via the max().
        /// </summary>
        private static void ApplyBgmAudioTail(PlayerData playerData, string chartDirectory)
        {
            if (playerData == null || playerData.Tracks == null ||
                string.IsNullOrEmpty(chartDirectory) || playerData.Tempo <= 0f)
            {
                return;
            }

            int endTick = playerData.MaxTick;
            foreach (TrackData track in playerData.Tracks)
            {
                if (track == null || track.Events == null)
                {
                    continue;
                }

                foreach (EventData evt in track.Events)
                {
                    if (evt == null || evt.EventType != EventType.Note ||
                        evt.Instrument == null)
                    {
                        continue;
                    }

                    string name = evt.Instrument.Name;
                    if (string.IsNullOrEmpty(name) ||
                        !name.StartsWith("0-", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string path = Path.Combine(chartDirectory, name);
                    double seconds = OggVorbisDuration.TryGetSeconds(path);
                    if (seconds <= 0.0)
                    {
                        continue;
                    }

                    int tail = evt.Tick + SecondsToNativeTicks(seconds, playerData.Tempo);
                    if (tail > endTick)
                    {
                        endTick = tail;
                    }
                }
            }

            if (endTick > playerData.MaxTick)
            {
                playerData.HeaderEndTick = (uint)endTick;
            }

            // Keep TrackDuration in sync with the resolved end for transports that read it.
            if (endTick > 0 && playerData.Tempo > 0f)
            {
                double seconds = endTick / (playerData.Tempo / 60.0 * 48.0);
                if (seconds > playerData.TrackDuration)
                {
                    playerData.TrackDuration = (float)seconds;
                }
            }
        }

        private static int SecondsToNativeTicks(double seconds, float tempo)
        {
            if (seconds <= 0.0 || tempo <= 0f)
            {
                return 0;
            }

            double ticks = seconds * (tempo / 60.0) * 48.0;
            if (double.IsNaN(ticks) || double.IsInfinity(ticks) || ticks <= 0.0)
            {
                return 0;
            }

            return (int)Math.Ceiling(ticks);
        }

        /// <summary>
        /// Lists the difficulty patterns a container holds without importing one, for the
        /// open-time chooser. Returns null for anything that is not a readable track.tech.
        /// </summary>
        public IList<TechPatternInfo> EnumeratePatterns(string filename)
        {
            try
            {
                return TechmaniaChartSerializer.ListPatterns(File.ReadAllBytes(filename));
            }
            catch (ChartLoadException)
            {
                return null;
            }
        }

        public string GetName() => "tech";
        public string GetDescription() => "TECHMANIA track";
        public string GetExtension() => "tech";
        public Form GetSettingsForm() => null;
    }
}
