using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DJMaxEditor.Controls.TimelineV2;
using DJMaxEditor.Diagnostics;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.Files.Tech
{
    /// <summary>
    /// Writer half of the TECHMANIA track.tech round trip. See
    /// <see cref="TechmaniaChartSerializer"/> for the format and the grid conversion.
    ///
    /// <para>
    /// The shared model is one TECHNIKA-shaped chart, so the output is one v3 pattern on a
    /// four-lane Touch control scheme: tracks 0-3 become the note tables, tracks 4-7 are read
    /// purely for their attribute-100 end-of-scan flags, and tempo events from any track
    /// become <c>bpmEvents</c>. The note kinds map one-to-one onto the packed type names; a
    /// drag is re-emitted as a two-node straight Bézier curve whose length is its duration.
    /// When the save edits an imported .tech, that re-emission only hits drags that actually
    /// changed: every untouched drag keeps its original packed object verbatim, curves and
    /// lane-crossing anchors included.
    /// </para>
    ///
    /// <para>
    /// Container metadata retained at import (time stops, first-beat offset, AV references,
    /// GUIDs, bps, sibling difficulties) is written back from <see cref="TechMetadata"/>;
    /// accompaniment tracks (8+ that are not tempo) remain the one thing a chart-only save
    /// has no place for (the dropped count goes to the diagnostics log). Volume/pan defaults
    /// are the model defaults (127 / center 64); anything else is written through and forces
    /// the extended packed form.
    /// </para>
    /// </summary>
    internal static partial class TechmaniaChartSerializer
    {
        // The model byte defaults, matching the EventData constructor.
        private const byte ModelDefaultVolume = 127;
        private const byte ModelDefaultPan = 64;
        // TECHMANIA's own packed-note defaults.
        private const int TechDefaultVolume = 100;
        private const int TechDefaultPan = 0;

        public static string Serialize(PlayerData player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var packedNotes = new List<string>();
            var packedHolds = new List<string>();
            // Mixes verbatim original drag objects (untouched notes keep their curves and
            // lane-crossing anchors) and freshly built straight ones (new or edited drags).
            var packedDrags = new JsonArray();
            IDictionary<long, VerbatimDrag> verbatimDrags =
                LoadVerbatimDrags(player.TechMetadata);

            // End-of-scan flags keyed (lane, pulse) from the marker tracks 4-7.
            var endOfScan = new HashSet<long>();
            CollectEndOfScan(player, endOfScan);

            TechMetadata retained = player.TechMetadata;
            int dropped = 0;
            var ordered = new List<KeyValuePair<int, EventData>>();
            // Lanes 0-3 are fixed (TEMPO also sits on track 0); marker/reserved tracks
            // 4-15 are not packed notes.
            // Specials 16-19 and keysound tracks 20+ map back to their original format lane
            // through the retained metadata (BGA VideoStart markers stay editor-only).
            for (int trackIndex = 0; trackIndex <= MaxModelTrackIndex; trackIndex++)
            {
                if (trackIndex >= FirstMarkerTrack && trackIndex < FirstSpecialTrack)
                {
                    continue;
                }
                // The synthesized backing-track trigger is editor-only scaffolding: the
                // backing file stays in pattern metadata and must never appear as a packed
                // note (the original chart deliberately had no such note).
                if (retained != null && trackIndex == retained.BackingTrackModelTrack)
                {
                    continue;
                }
                // BGA / Speed / Soundtrack scaffolding is not a .tech packed note.
                if (trackIndex == BgaTrack || trackIndex == SpeedTrack ||
                    trackIndex == SoundtrackTrack)
                {
                    continue;
                }
                // Special/keysound tracks only exist for an imported .tech, whose metadata
                // carries their original format lane. A chart converted from another format
                // keeps the historical behaviour: its four fixed lanes are all that is written.
                if (trackIndex >= FirstSpecialTrack && retained == null)
                {
                    continue;
                }
                TrackData track = player.Tracks.GetTrackAtIndex((uint)trackIndex);
                if (track == null)
                {
                    continue;
                }
                int formatLane = retained != null
                    ? retained.FormatLaneForTrack(trackIndex)
                    : trackIndex;
                foreach (EventData evt in track.Events)
                {
                    if (evt == null || evt.EventType != EventType.Note ||
                        evt.Attribute == 100)
                    {
                        continue;
                    }
                    if (formatLane < 0 || formatLane > MaxWritableFormatLane)
                    {
                        dropped++;
                        continue;
                    }
                    ordered.Add(new KeyValuePair<int, EventData>(formatLane, evt));
                }
            }
            // TECHMANIA keeps its tables in pulse/lane order; the game's own NoteComparer is
            // pulse then lane.
            ordered.Sort((a, b) =>
            {
                int pulse = ToPulse(a.Value.VirtualTick).CompareTo(ToPulse(b.Value.VirtualTick));
                if (pulse != 0)
                {
                    return pulse;
                }
                return a.Key.CompareTo(b.Key);
            });

            foreach (KeyValuePair<int, EventData> entry in ordered)
            {
                int lane = entry.Key;
                EventData evt = entry.Value;
                int pulse = ToPulse(evt.VirtualTick);
                int duration = ToPulse(evt.VirtualDuration);
                int volume = OutVolume(evt.Volume);
                int pan = OutPan(evt.Pan);
                bool eos = endOfScan.Contains(Key(lane, pulse));
                string sound = evt.Instrument != null && evt.Instrument.Name != "none"
                    ? evt.Instrument.Name ?? string.Empty
                    : string.Empty;

                TechnikaNoteKind kind = TechnikaNoteClassifier.Classify(evt);
                switch (kind)
                {
                    case TechnikaNoteKind.Basic:
                    case TechnikaNoteKind.ChainHead:
                    case TechnikaNoteKind.ChainNode:
                    case TechnikaNoteKind.RepeatHead:
                    case TechnikaNoteKind.Repeat:
                        packedNotes.Add(
                            PackNote(kind.ToString(), pulse, lane, volume, pan, eos, sound));
                        break;

                    case TechnikaNoteKind.Hold:
                    case TechnikaNoteKind.RepeatHeadHold:
                    case TechnikaNoteKind.RepeatHold:
                        packedHolds.Add(
                            PackHold(kind.ToString(), pulse, lane, duration, volume, pan, eos, sound));
                        break;

                    case TechnikaNoteKind.Drag:
                        VerbatimDrag original;
                        if (verbatimDrags.TryGetValue(Key(lane, pulse), out original) &&
                            original.Duration == duration &&
                            original.Volume == volume &&
                            original.Pan == pan &&
                            original.Keysound == sound)
                        {
                            // Head pulse/lane, length, volume, pan and keysound all match the
                            // import: the note was never edited, so its authored curve rides
                            // through verbatim. Any edit (a moved, resized, re-voiced or newly
                            // drawn drag) misses a key and is rebuilt as a straight drag.
                            packedDrags.Add(JsonNode.Parse(original.Json));
                        }
                        else
                        {
                            packedDrags.Add(JsonSerializer.SerializeToNode(new PackedDragDto
                            {
                                packedNote = PackDrag(pulse, lane, volume, pan, sound),
                                packedNodes = StraightDragNodes(duration)
                            }));
                        }
                        break;

                    default:
                        dropped++;
                        break;
                }
            }


            var pattern = new PatternDto
            {
                patternMetadata = new PatternMetadataDto
                {
                    // Keep the imported pattern's identity and setup; a chart converted from
                    // another container gets fresh defaults.
                    guid = NonEmptyGuid(retained != null ? retained.PatternGuid : null),
                    patternName = retained != null ? retained.PatternName : string.Empty,
                    level = retained != null ? retained.Level : 0,
                    controlScheme = retained != null ? retained.ControlScheme : 0,
                    playableLanes = retained != null && retained.PlayableLanes >= 2
                        ? retained.PlayableLanes
                        : LaneCount,
                    author = retained != null ? retained.Author : string.Empty,
                    backingTrack = retained != null ? retained.BackingTrack : string.Empty,
                    backImage = retained != null ? retained.BackImage : string.Empty,
                    bga = retained != null ? retained.Bga : string.Empty,
                    bgaOffset = retained != null ? retained.BgaOffset : 0,
                    waitForEndOfBga = retained != null && retained.WaitForEndOfBga,
                    playBgaOnLoop = retained != null && retained.PlayBgaOnLoop,
                    firstBeatOffset = retained != null ? retained.FirstBeatOffset : 0,
                    initBpm = player.Tempo > 0 ? Math.Round(player.Tempo, 3) : 120.0,
                    bps = retained != null && retained.Bps > 0
                        ? retained.Bps
                        : DefaultBeatsPerScan
                },
                bpmEvents = CollectBpmEvents(player),
                timeStops = retained != null
                    ? retained.TimeStops.Select(s => new TimeStopDto
                    {
                        pulse = s.Pulse,
                        duration = s.Duration
                    }).ToList()
                    : new List<TimeStopDto>(),
                packedNotes = packedNotes,
                packedHoldNotes = packedHolds,
                packedDragNotes = packedDrags
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // One editor model edits exactly one difficulty slot; the other patterns from
            // the imported container are kept as verbatim JSON and re-spliced in their
            // original slots so a multi-difficulty track survives open/edit/save intact.
            int activeSlot = retained != null ? retained.ActivePatternIndex : 0;
            if (activeSlot < 0)
            {
                activeSlot = 0;
            }
            string activeRawJson = retained != null ? retained.ActivePatternJson : null;
            JsonNode activeNode = !string.IsNullOrEmpty(activeRawJson)
                ? PatchActivePattern(activeRawJson, pattern, options)
                : JsonSerializer.SerializeToNode(pattern, options);
            var patternSlots = new SortedList<int, JsonNode>
            {
                { activeSlot, activeNode }
            };
            int siblingCount = 0;
            if (retained != null)
            {
                foreach (TechSiblingPattern sibling in retained.SiblingPatterns)
                {
                    if (sibling == null || sibling.Index == activeSlot ||
                        sibling.Index < 0 || string.IsNullOrEmpty(sibling.Json))
                    {
                        continue;
                    }
                    JsonNode node = null;
                    try
                    {
                        node = JsonNode.Parse(sibling.Json);
                    }
                    catch (JsonException)
                    {
                        DiagnosticLog.Write("tech.export",
                            "Dropped an unreadable sibling pattern at slot " + sibling.Index);
                        continue;
                    }
                    if (node != null && !patternSlots.ContainsKey(sibling.Index))
                    {
                        patternSlots.Add(sibling.Index, node);
                        siblingCount++;
                    }
                }
            }
            var patternsArray = new JsonArray();
            foreach (KeyValuePair<int, JsonNode> slot in patternSlots)
            {
                patternsArray.Add(slot.Value);
            }

            var file = new TrackFileDto
            {
                version = SupportedVersion,
                patterns = patternsArray,
                trackMetadata = new TrackMetadataDto
                {
                    guid = NonEmptyGuid(retained != null ? retained.TrackGuid : null),
                    title = retained != null ? retained.Title : string.Empty,
                    artist = retained != null ? retained.Artist : string.Empty,
                    genre = retained != null ? retained.Genre : string.Empty,
                    additionalCredits = retained != null
                        ? retained.AdditionalCredits : string.Empty,
                    eyecatchImage = retained != null ? retained.EyecatchImage : string.Empty,
                    previewTrack = retained != null ? retained.PreviewTrack : string.Empty,
                    previewStartTime = retained != null ? retained.PreviewStartTime : 0,
                    previewEndTime = retained != null ? retained.PreviewEndTime : 0,
                    previewBga = retained != null ? retained.PreviewBga : string.Empty,
                    autoOrderPatterns = retained != null && retained.AutoOrderPatterns
                }
            };

            if (dropped > 0)
            {
                DiagnosticLog.Write("tech.export",
                    "Dropped " + dropped + " note(s) with no TECHMANIA equivalent.");
            }
            if (siblingCount > 0)
            {
                DiagnosticLog.Write("tech.export",
                    "Re-spliced " + siblingCount + " unedited sibling pattern(s).");
            }

            string json = JsonSerializer.Serialize(file, options);

            // TECHMANIA's parser is a tolerant JSON reader but is case-sensitive about the
            // packed type names; nothing else here relies on key order.
            return json;
        }

        /// <summary>
        /// Rebuilds the edited pattern on top of its verbatim import JSON. Only the surfaces
        /// the editor can actually change are replaced - the tempo/initBpm field, the tempo
        /// event and time-stop tables, and the three packed note tables; every other key
        /// (legacy ruleset/setlist overrides, the metadata fields the editor never edits,
        /// key order and null styles) rides through untouched. The integrity fingerprint is
        /// removed because the edit invalidated it and the game recomputes a missing one.
        /// </summary>
        private static JsonNode PatchActivePattern(
            string rawJson,
            PatternDto edited,
            JsonSerializerOptions options)
        {
            JsonObject node;
            try
            {
                node = JsonNode.Parse(rawJson).AsObject();
            }
            catch (JsonException)
            {
                DiagnosticLog.Write("tech.export",
                    "The opened pattern's original JSON was unreadable; rebuilding it.");
                return JsonSerializer.SerializeToNode(edited, options);
            }

            JsonObject metadata = node["patternMetadata"] as JsonObject;
            if (metadata == null)
            {
                node["patternMetadata"] =
                    JsonSerializer.SerializeToNode(edited.patternMetadata, options);
            }
            else
            {
                metadata["initBpm"] = JsonValue.Create(edited.patternMetadata.initBpm);
            }

            node["bpmEvents"] = JsonSerializer.SerializeToNode(edited.bpmEvents, options);
            node["timeStops"] = JsonSerializer.SerializeToNode(edited.timeStops, options);
            node["packedNotes"] = JsonSerializer.SerializeToNode(edited.packedNotes, options);
            node["packedHoldNotes"] =
                JsonSerializer.SerializeToNode(edited.packedHoldNotes, options);
            node["packedDragNotes"] =
                JsonSerializer.SerializeToNode(edited.packedDragNotes, options);
            node.Remove("fingerprint");
            return node;
        }

        private static void CollectEndOfScan(PlayerData player, HashSet<long> flags)
        {
            int orphan = 0;
            for (int markerTrack = FirstMarkerTrack;
                 markerTrack < FirstMarkerTrack + LaneCount;
                 markerTrack++)
            {
                TrackData track = player.Tracks.GetTrackAtIndex((uint)markerTrack);
                if (track == null)
                {
                    continue;
                }
                int lane = markerTrack - FirstMarkerTrack;
                foreach (EventData marker in track.Events)
                {
                    if (marker == null || marker.EventType != EventType.Note ||
                        marker.Attribute != 100)
                    {
                        continue;
                    }
                    int pulse = ToPulse(marker.VirtualTick);
                    if (HasNoteAt(player, lane, pulse))
                    {
                        flags.Add(Key(lane, pulse));
                    }
                    else
                    {
                        orphan++;
                    }
                }
            }
            if (orphan > 0)
            {
                DiagnosticLog.Write("tech.export",
                    "Ignored " + orphan + " end-of-scan marker(s) with no matching note.");
            }
        }

        private static bool HasNoteAt(PlayerData player, int lane, int pulse)
        {
            TrackData track = player.Tracks.GetTrackAtIndex((uint)lane);
            if (track == null)
            {
                return false;
            }
            foreach (EventData evt in track.Events)
            {
                if (evt != null && evt.EventType == EventType.Note &&
                    evt.Attribute != 100 && ToPulse(evt.VirtualTick) == pulse)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<BpmEventDto> CollectBpmEvents(PlayerData player)
        {
            var events = new List<BpmEventDto>();
            foreach (TrackData track in player.Tracks)
            {
                foreach (EventData evt in track.Events)
                {
                    if (evt == null || evt.EventType != EventType.Tempo || evt.Tempo <= 0)
                    {
                        continue;
                    }
                    // Playback-only marker synthesized from initBpm - never fold it into
                    // bpmEvents. Authored entries keep Attribute 0 and stay at their pulse,
                    // including pulse 0 when the chart wrote one.
                    if (evt.Attribute == InitBpmTempoAttribute)
                    {
                        continue;
                    }
                    events.Add(new BpmEventDto
                    {
                        pulse = ToPulse(evt.VirtualTick),
                        bpm = Math.Round(evt.Tempo, 3)
                    });
                }
            }
            events.Sort((a, b) => a.pulse.CompareTo(b.pulse));
            return events;
        }

        private static string PackNote(
            string type, int pulse, int lane, int volume, int pan, bool eos, string sound)
        {
            if (volume != TechDefaultVolume || pan != TechDefaultPan || eos)
            {
                return string.Join("|", "E", type,
                    pulse.ToString(CultureInfo.InvariantCulture),
                    lane.ToString(CultureInfo.InvariantCulture),
                    volume.ToString(CultureInfo.InvariantCulture),
                    pan.ToString(CultureInfo.InvariantCulture),
                    eos ? "1" : "0",
                    sound);
            }
            return string.Join("|", type,
                pulse.ToString(CultureInfo.InvariantCulture),
                lane.ToString(CultureInfo.InvariantCulture),
                sound);
        }

        private static string PackHold(
            string type, int pulse, int lane, int duration,
            int volume, int pan, bool eos, string sound)
        {
            if (volume != TechDefaultVolume || pan != TechDefaultPan || eos)
            {
                return string.Join("|", "E", type,
                    pulse.ToString(CultureInfo.InvariantCulture),
                    lane.ToString(CultureInfo.InvariantCulture),
                    duration.ToString(CultureInfo.InvariantCulture),
                    volume.ToString(CultureInfo.InvariantCulture),
                    pan.ToString(CultureInfo.InvariantCulture),
                    eos ? "1" : "0",
                    sound);
            }
            return string.Join("|", type,
                pulse.ToString(CultureInfo.InvariantCulture),
                lane.ToString(CultureInfo.InvariantCulture),
                duration.ToString(CultureInfo.InvariantCulture),
                sound);
        }

        private static string PackDrag(int pulse, int lane, int volume, int pan, string sound)
        {
            // Field 7 of an extended drag is the curve type, not end-of-scan (drags never
            // carry the flag); a flattened drag is always Bézier, so it stays 0.
            if (volume != TechDefaultVolume || pan != TechDefaultPan)
            {
                return string.Join("|", "E", "Drag",
                    pulse.ToString(CultureInfo.InvariantCulture),
                    lane.ToString(CultureInfo.InvariantCulture),
                    volume.ToString(CultureInfo.InvariantCulture),
                    pan.ToString(CultureInfo.InvariantCulture),
                    "0",
                    sound);
            }
            return string.Join("|", "Drag",
                pulse.ToString(CultureInfo.InvariantCulture),
                lane.ToString(CultureInfo.InvariantCulture),
                sound);
        }

        /// <summary>
        /// Two anchors with their control points pulled onto the straight line between them,
        /// so the Bézier interpolation the game performs stays on that line. The first node's
        /// left control and the last node's right control are ignored by the game but are
        /// still emitted as 0, as its editor writes them.
        /// </summary>
        private static List<string> StraightDragNodes(int durationPulses)
        {
            int duration = Math.Max(1, durationPulses);
            double half = duration / 2.0;
            return new List<string>
            {
                "0|0|0|0|" + Float(half) + "|0",
                Float(duration) + "|0|" + Float(-half) + "|0|0|0"
            };
        }

        /// <summary>
        /// Reads the opened pattern's original drag table and indexes it by
        /// <c>(lane, head pulse)</c>, keeping each entry's verbatim JSON. An exported drag
        /// whose head, length, volume, pan and keysound all match its import is considered
        /// untouched and its authored curve (anchors, B-spline control points, lane-crossing
        /// shape) is written back byte-for-byte; anything the user changed misses a key and
        /// is rebuilt straight.
        /// </summary>
        private static Dictionary<long, VerbatimDrag> LoadVerbatimDrags(TechMetadata retained)
        {
            var result = new Dictionary<long, VerbatimDrag>();
            if (retained == null || string.IsNullOrEmpty(retained.ActivePatternJson))
            {
                return result;
            }

            try
            {
                JsonObject active = JsonNode.Parse(retained.ActivePatternJson).AsObject();
                JsonArray table = active["packedDragNotes"] as JsonArray;
                if (table == null)
                {
                    return result;
                }

                foreach (JsonNode node in table)
                {
                    JsonObject drag = node as JsonObject;
                    if (drag == null)
                    {
                        continue;
                    }
                    string head = drag["packedNote"] == null
                        ? null
                        : drag["packedNote"].ToJsonString();
                    // ToJsonString quotes the value; undo the surrounding quotes.
                    if (head != null && head.Length >= 2 && head[0] == '"')
                    {
                        head = JsonSerializer.Deserialize<string>(head);
                    }
                    if (string.IsNullOrEmpty(head) ||
                        !TryUnpack(head, false, true, out PackedNote note))
                    {
                        continue;
                    }

                    int duration;
                    using (JsonDocument doc = JsonDocument.Parse(drag.ToJsonString()))
                    {
                        duration = DragDurationPulses(doc.RootElement);
                    }
                    result[Key(note.Lane, note.Pulse)] = new VerbatimDrag
                    {
                        Json = drag.ToJsonString(),
                        Duration = duration,
                        Volume = note.Volume,
                        Pan = note.Pan,
                        Keysound = note.Keysound ?? string.Empty
                    };
                }
            }
            catch (JsonException ex)
            {
                DiagnosticLog.Write("tech.export",
                    "Could not index the original drag table; drags will be rebuilt straight: "
                    + ex.Message);
            }
            return result;
        }

        private sealed class VerbatimDrag
        {
            public string Json;
            public int Duration;
            public int Volume;
            public int Pan;
            public string Keysound;
        }

        private static int OutVolume(byte model)
        {
            if (model == ModelDefaultVolume)
            {
                return TechDefaultVolume;
            }
            return Math.Max(0, Math.Min(100, (int)model));
        }

        private static int OutPan(byte model)
        {
            if (model == ModelDefaultPan)
            {
                return TechDefaultPan;
            }
            // Reverse of the reader's 64 + pan * 0.64.
            int pan = (int)Math.Round((model - ModelDefaultPan) / 0.64,
                MidpointRounding.AwayFromZero);
            return Math.Max(-100, Math.Min(100, pan));
        }

        private static int ToPulse(int virtualTick)
        {
            // virtualTick * 240 / 288 = virtualTick * 5 / 6, rounded.
            long rounded = (long)Math.Round(virtualTick * 5L / 6.0,
                MidpointRounding.AwayFromZero);
            return (int)Math.Max(0, Math.Min(int.MaxValue, rounded));
        }

        private static long Key(int lane, int pulse)
        {
            return ((long)lane << 32) | (uint)pulse;
        }

        private static string NonEmptyGuid(string guid)
        {
            return string.IsNullOrWhiteSpace(guid) ? Guid.NewGuid().ToString() : guid;
        }

        private static string Float(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 0.0001)
            {
                return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
            }
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // -------------------------------------------------------------------------------
        // Wire DTOs: property names are camelCased by the serializer to match track.tech.
        // -------------------------------------------------------------------------------

        private sealed class TrackFileDto
        {
            public string version { get; set; }
            public TrackMetadataDto trackMetadata { get; set; }

            // Active slot serialized from PatternDto, sibling slots kept as verbatim
            // parsed JSON nodes; JsonArray serializes each node in slot order.
            public JsonArray patterns { get; set; }
        }

        private sealed class TrackMetadataDto
        {
            public string guid { get; set; }
            public string title { get; set; }
            public string artist { get; set; }
            public string genre { get; set; }
            public string additionalCredits { get; set; }
            public string eyecatchImage { get; set; }
            public string previewTrack { get; set; }
            public double previewStartTime { get; set; }
            public double previewEndTime { get; set; }
            public string previewBga { get; set; }
            public bool autoOrderPatterns { get; set; }
        }

        private sealed class PatternDto
        {
            public PatternMetadataDto patternMetadata { get; set; }
            public List<BpmEventDto> bpmEvents { get; set; }
            public List<TimeStopDto> timeStops { get; set; }
            public List<string> packedNotes { get; set; }
            public List<string> packedHoldNotes { get; set; }
            // Verbatim untouched drags and freshly built straight drags, in pulse order.
            public JsonArray packedDragNotes { get; set; }
        }

        private sealed class PatternMetadataDto
        {
            public string guid { get; set; }
            public string patternName { get; set; }
            public int level { get; set; }
            public int controlScheme { get; set; }
            public int playableLanes { get; set; }
            public string author { get; set; }
            public string backingTrack { get; set; }
            public string backImage { get; set; }
            public string bga { get; set; }
            public double bgaOffset { get; set; }
            public bool waitForEndOfBga { get; set; }
            public bool playBgaOnLoop { get; set; }
            public double firstBeatOffset { get; set; }
            public double initBpm { get; set; }
            public int bps { get; set; }
        }

        private sealed class BpmEventDto
        {
            public int pulse { get; set; }
            public double bpm { get; set; }
        }

        private sealed class TimeStopDto
        {
            public int pulse { get; set; }
            public int duration { get; set; }
        }

        private sealed class PackedDragDto
        {
            public string packedNote { get; set; }
            public List<string> packedNodes { get; set; }
        }
    }
}
