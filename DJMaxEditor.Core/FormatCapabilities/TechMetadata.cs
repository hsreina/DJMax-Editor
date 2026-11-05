using System;
using System.Collections.Generic;

namespace DJMaxEditor.Files.Tech
{
    /// <summary>
    /// TECHMANIA track.tech information the generic DJMAX event model cannot otherwise
    /// retain. Keeping this beside PlayerData makes .tech -> edit -> .tech preserve the
    /// container's identity (GUIDs, titles), pattern setup (control scheme, lane count,
    /// beats per scan) and fields the editor does not interpret but must not destroy
    /// (time stops, AV file names, offsets). The importer fills it for the one pattern it
    /// imports; the exporter writes it back when present.
    /// </summary>
    public sealed class TechMetadata
    {
        // ----- trackMetadata -----

        public string TrackGuid { get; set; } = "";
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public string Genre { get; set; } = "";
        public string AdditionalCredits { get; set; } = "";
        public string EyecatchImage { get; set; } = "";
        public string PreviewTrack { get; set; } = "";
        public double PreviewStartTime { get; set; }
        public double PreviewEndTime { get; set; }
        public string PreviewBga { get; set; } = "";
        public bool AutoOrderPatterns { get; set; }

        // ----- patternMetadata (of the imported pattern) -----

        public string PatternGuid { get; set; } = "";
        public string PatternName { get; set; } = "";
        public int Level { get; set; }

        /// <summary>0 = Touch, 1 = Keys, 2 = KM (TECHMANIA's ControlScheme enum).</summary>
        public int ControlScheme { get; set; }

        /// <summary>Playable lanes, 2-4. The editor field always lays out four; this is
        /// retained verbatim for save-back and read by the preview for scan timing.</summary>
        public int PlayableLanes { get; set; } = 4;

        public string Author { get; set; } = "";
        public string BackingTrack { get; set; } = "";
        public string BackImage { get; set; } = "";
        public string Bga { get; set; } = "";
        public double BgaOffset { get; set; }
        public bool WaitForEndOfBga { get; set; }
        public bool PlayBgaOnLoop { get; set; }

        /// <summary>Seconds before beat 0. Retained verbatim; the editor does not apply it.</summary>
        public double FirstBeatOffset { get; set; }

        /// <summary>Beats per scan. 4 for normal charts; 2 and 8+ also exist.</summary>
        public int Bps { get; set; } = 4;

        /// <summary>
        /// Maps an editor accompaniment track index (BGM track 16 and keysound tracks
        /// 20+) to the .tech format lane it was imported from. Lanes 0-3 are the fixed
        /// playable lanes and are not present; a note authored on format lane 4 or higher
        /// is an invisible/autoplay keysound lane (the format allows lanes up to 63,
        /// including sparse high BGM triggers), so each occupied lane lands on the
        /// TECHNIKA layout (16=BGM, 20+=keysound) and this map remembers its origin so
        /// the writer can emit the same lane on save. Unused format lanes get no track
        /// and no column.
        /// </summary>
        public Dictionary<int, int> FormatLaneByTrack { get; }
            = new Dictionary<int, int>();

        /// <summary>
        /// Model track holding the synthesized start trigger for a soundtrack-only chart:
        /// when the pattern names a <c>backingTrack</c> and its notes carry no keysounds,
        /// import places one silent tap at tick 0 on this track with the backing file as
        /// its instrument, so the editor's transport plays the full song. It is editor-only
        /// scaffolding - the backing file stays in pattern metadata and the writer never
        /// emits this track as a packed note. -1 when no such track was created.
        /// </summary>
        public int BackingTrackModelTrack { get; set; } = -1;

        /// <summary>The format lane a model track's notes belong to.</summary>
        public int FormatLaneForTrack(int trackIndex)
        {
            int lane;
            return FormatLaneByTrack.TryGetValue(trackIndex, out lane) ? lane : trackIndex;
        }

        /// <summary>Time stops ({ pulse, duration }) exactly as authored - the editor has no
        /// time-stop semantics, so they round-trip untouched rather than becoming events.
        /// Per the .tech specification pulse is in pulses and duration is counted in beats,
        /// but both values are passed through verbatim and never interpreted here.</summary>
        public List<TechTimeStop> TimeStops { get; } = new List<TechTimeStop>();

        /// <summary>
        /// Slot (0-based index into the container's <c>patterns</c> array) of the single
        /// pattern this model is editing. A track.tech normally ships one pattern per
        /// difficulty (NM, HD, MX ...) inside one container; the editor opens one chart at a
        /// time, so every other slot is retained verbatim in <see cref="SiblingPatterns"/>.
        /// </summary>
        public int ActivePatternIndex { get; set; }

        /// <summary>
        /// Verbatim raw JSON of the opened pattern at import time. The exporter patches the
        /// fields the editor can change (tempo, packed note tables, time stops) onto this
        /// copy instead of rebuilding the object, which carries format fields the editor
        /// does not model - legacy ruleset/setlist overrides, key ordering, null styles -
        /// through the round trip untouched. Null for a chart converted from another format.
        /// </summary>
        public string ActivePatternJson { get; set; }

        /// <summary>
        /// The container's other difficulty patterns as verbatim raw JSON, each tagged with
        /// its original slot. They are never parsed into the event model and never altered:
        /// saving the edited pattern splices it back into its slot and writes these out
        /// untouched, so open/edit/save of a multi-difficulty track loses nothing.
        /// </summary>
        public List<TechSiblingPattern> SiblingPatterns { get; }
            = new List<TechSiblingPattern>();
    }

    /// <summary>
    /// Summary of one pattern (difficulty) slot in a track.tech container, enough to populate
    /// a chooser without importing the chart.
    /// </summary>
    public sealed class TechPatternInfo
    {
        public TechPatternInfo()
        {
        }

        public TechPatternInfo(int index, string name, int level, int playableLanes)
        {
            Index = index;
            Name = name;
            Level = level;
            PlayableLanes = playableLanes;
        }

        /// <summary>Slot in the container's patterns array, the value Parse takes.</summary>
        public int Index { get; set; }

        public string Name { get; set; } = "";
        public int Level { get; set; }
        public int PlayableLanes { get; set; } = 4;

        public override string ToString()
        {
            string title = string.IsNullOrEmpty(Name) ? "Pattern " + (Index + 1) : Name;
            return title + "  •  Lv " + Level + "  •  " + PlayableLanes + " lanes";
        }
    }

    /// <summary>One non-edited pattern of a multi-pattern .tech container, kept as raw JSON.</summary>
    public sealed class TechSiblingPattern
    {
        public TechSiblingPattern()
        {
        }

        public TechSiblingPattern(int index, string json)
        {
            Index = index;
            Json = json;
        }

        /// <summary>Original slot in the container's patterns array.</summary>
        public int Index { get; set; }

        /// <summary>The pattern object's verbatim JSON.</summary>
        public string Json { get; set; } = "";
    }

    public sealed class TechTimeStop
    {
        public TechTimeStop()
        {
        }

        public TechTimeStop(int pulse, int duration)
        {
            Pulse = pulse;
            Duration = duration;
        }

        public int Pulse { get; set; }

        /// <summary>Stop length in pulses.</summary>
        public int Duration { get; set; }
    }
}
