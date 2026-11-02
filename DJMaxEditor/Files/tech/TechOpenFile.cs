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
