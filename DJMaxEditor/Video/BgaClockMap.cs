using System;
using System.Collections.Generic;
using System.Linq;
using DJMaxEditor.DJMax;
using DJMaxEditor.Files.FormatDetection;

namespace DJMaxEditor.Video
{
    /// <summary>
    /// Chart tick to wall time for BGA seeking. Mirrors Player.SetTempo's
    /// <c>m_period = 60000 / (tempo * 48)</c> so the video cannot drift from the sequencer.
    /// </summary>
    public sealed class BgaClockMap
    {
        private const double PlayerTicksPerBeat = 48.0;
        private const double DefaultTempo = 120.0;

        /// <summary>Technika zone: dedicated BGA cue track (Attr 100 VideoStart).</summary>
        private const uint BgaCueTrack = 17;

        private readonly List<Segment> _segments = new List<Segment>();

        private int _ticksPerMeasure;
        private int _videoStartVirtualTick;
        private double _videoStartMs;
        private bool _hasVideoStartMarker;

        public BgaClockMap()
        {
            Clear();
        }

        /// <summary>
        /// Extra nudge on top of <see cref="VideoStartVirtualTick"/>.
        /// <see cref="TimeForVirtualTick"/> adds this value, so a negative span delays the
        /// video (TECHMANIA positive <c>bgaOffset</c> maps to a negative Offset when no
        /// authored VideoStart marker is present).
        /// </summary>
        public TimeSpan Offset { get; set; }

        public int VideoStartVirtualTick
        {
            get { return _videoStartVirtualTick; }
        }

        /// <summary>
        /// True when an attribute-100 VideoStart note was found. Tech imports already place
        /// that marker at <c>bgaOffset</c> ticks — callers must not also apply BgaOffset.
        /// </summary>
        public bool HasVideoStartMarker
        {
            get { return _hasVideoStartMarker; }
        }

        public int TicksPerMeasure
        {
            get { return _ticksPerMeasure; }
        }

        public int SegmentCount
        {
            get { return _segments.Count; }
        }

        public void Load(PlayerData playerData)
        {
            Clear();

            if (playerData == null)
            {
                return;
            }

            _ticksPerMeasure = playerData.TickPerMinute * EventData.VirtualTickSize;

            double tempo = playerData.Tempo > 0.0f ? playerData.Tempo : DefaultTempo;
            _segments[0] = new Segment(0, MsPerVirtualTick(tempo), 0.0);

            EventData[] events = playerData.Tracks == null ? null : playerData.Tracks.Events;
            if (events == null)
            {
                return;
            }

            IEnumerable<EventData> tempoEvents = events
                .Where(e => e != null && e.EventType == EventType.Tempo)
                .OrderBy(e => e.VirtualTick);

            foreach (EventData tempoEvent in tempoEvents)
            {
                int tick = Math.Max(0, tempoEvent.VirtualTick);
                double next = tempoEvent.Tempo > 0.0f ? tempoEvent.Tempo : tempo;
                tempo = next;

                Segment last = _segments[_segments.Count - 1];
                if (tick <= last.StartVirtualTick)
                {
                    _segments[_segments.Count - 1] =
                        new Segment(last.StartVirtualTick, MsPerVirtualTick(tempo), last.StartMs);
                    continue;
                }

                double startMs = last.StartMs + ((tick - last.StartVirtualTick) * last.MsPerVirtualTick);
                _segments.Add(new Segment(tick, MsPerVirtualTick(tempo), startMs));
            }

            int authored;
            _hasVideoStartMarker = TryAuthoredVideoStart(playerData, out authored);
            if (_hasVideoStartMarker)
            {
                _videoStartVirtualTick = authored;
            }
            else
            {
                _videoStartVirtualTick = PortableBgaSyncTick(playerData);
            }
            _videoStartMs = MsForVirtualTick(_videoStartVirtualTick);
        }

        /// <summary>
        /// Attr 100 is overloaded: VideoStart on the BGA cue track (17), and EndOfScan
        /// markers on lanes 4-7. Only track 17 means "video time zero".
        /// </summary>
        private static bool TryAuthoredVideoStart(PlayerData playerData, out int virtualTick)
        {
            virtualTick = 0;

            int onBga = EarliestVideoStartOnTrack(playerData, BgaCueTrack);
            if (onBga >= 0)
            {
                virtualTick = onBga;
                return true;
            }

            return false;
        }

        private static int PortableBgaSyncTick(PlayerData playerData)
        {
            if (!UsesPortableBgaSync(playerData) || playerData.Tracks == null)
            {
                return 0;
            }

            TrackData syncTrack = null;
            foreach (TrackData track in playerData.Tracks)
            {
                if (track != null && track.Idx == 1)
                {
                    syncTrack = track;
                    break;
                }
            }
            if (syncTrack == null)
            {
                return 0;
            }

            int earliest = -1;
            foreach (EventData candidate in syncTrack.Events)
            {
                if (candidate == null ||
                    (candidate.EventType != EventType.Note && candidate.EventType != EventType.Beat))
                {
                    continue;
                }

                int tick = Math.Max(0, candidate.VirtualTick);
                if (earliest < 0 || tick < earliest)
                {
                    earliest = tick;
                }
            }

            return earliest < 0 ? 0 : earliest;
        }

        private static int EarliestVideoStartOnTrack(PlayerData playerData, uint trackIdx)
        {
            if (playerData == null || playerData.Tracks == null)
            {
                return -1;
            }

            foreach (TrackData track in playerData.Tracks)
            {
                if (track == null || track.Idx != trackIdx)
                {
                    continue;
                }

                int earliest = -1;
                foreach (EventData candidate in track.Events)
                {
                    if (!IsVideoStartMarker(candidate))
                    {
                        continue;
                    }

                    int tick = Math.Max(0, candidate.VirtualTick);
                    if (earliest < 0 || tick < earliest)
                    {
                        earliest = tick;
                    }
                }
                return earliest;
            }

            return -1;
        }

        private static bool IsVideoStartMarker(EventData candidate)
        {
            return candidate != null &&
                candidate.EventType == EventType.Note &&
                candidate.Attribute == (byte)EventAttribute.VideoStart;
        }

        /// <summary>
        /// Button-style charts cue BGA from song track 1. TECHNIKA / .tech use attribute-100
        /// instead (track 1 is a playable lane there).
        /// </summary>
        private static bool UsesPortableBgaSync(PlayerData playerData)
        {
            if (playerData == null)
            {
                return false;
            }

            ChartFormat? format = playerData.SourceFormat;
            if (format == ChartFormat.PtffEncryptedTechnika ||
                format == ChartFormat.TechmaniaTrack ||
                playerData.TechMetadata != null)
            {
                return false;
            }

            bool supportedButtonFormat = format == ChartFormat.PtffDecrypted ||
                format == ChartFormat.TrailerRespectV;
            if (!supportedButtonFormat)
            {
                return false;
            }

            // Decrypted Technika-shaped PTs still author VideoStart; track 1 is a playable lane.
            return !LooksTechnikaShaped(playerData);
        }

        /// <summary>
        /// Same structural test Studio uses: TECHNIKA keeps notes on track 0 and never on
        /// the portable side/shoulder band 9-11.
        /// </summary>
        private static bool LooksTechnikaShaped(PlayerData playerData)
        {
            if (playerData == null || playerData.Tracks == null)
            {
                return false;
            }

            bool firstLane = false;
            foreach (TrackData track in playerData.Tracks)
            {
                if (track == null || track.Events == null)
                {
                    continue;
                }

                bool hasNote = false;
                foreach (EventData evt in track.Events)
                {
                    if (evt != null &&
                        (evt.EventType == EventType.Note || evt.EventType == EventType.Beat))
                    {
                        hasNote = true;
                        break;
                    }
                }
                if (!hasNote)
                {
                    continue;
                }

                if (track.Idx >= 9 && track.Idx <= 11)
                {
                    return false;
                }
                if (track.Idx == 0)
                {
                    firstLane = true;
                }
            }

            return firstLane;
        }

        public void Clear()
        {
            _segments.Clear();
            _segments.Add(new Segment(0, MsPerVirtualTick(DefaultTempo), 0.0));
            _ticksPerMeasure = 192 * EventData.VirtualTickSize;
            _videoStartVirtualTick = 0;
            _videoStartMs = 0.0;
            _hasVideoStartMarker = false;
            Offset = TimeSpan.Zero;
        }

        /// <summary>
        /// Wall time in the video's own clock. Negative before the chart's start marker;
        /// the preview treats that as "no frame yet".
        /// </summary>
        public TimeSpan TimeForVirtualTick(int virtualTick)
        {
            if (virtualTick < 0)
            {
                virtualTick = 0;
            }

            return TimeSpan.FromMilliseconds(MsForVirtualTick(virtualTick) - _videoStartMs) + Offset;
        }

        private double MsForVirtualTick(int virtualTick)
        {
            Segment segment = _segments[IndexForTick(virtualTick)];
            return segment.StartMs +
                ((virtualTick - segment.StartVirtualTick) * segment.MsPerVirtualTick);
        }

        public TimeSpan TimeForTick(int tick)
        {
            return TimeForVirtualTick(tick * EventData.VirtualTickSize);
        }

        private int IndexForTick(int virtualTick)
        {
            int low = 0;
            int high = _segments.Count - 1;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                if (_segments[mid].StartVirtualTick <= virtualTick)
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }
            return low;
        }

        private static double MsPerVirtualTick(double tempo)
        {
            if (tempo <= 0.0)
            {
                tempo = DefaultTempo;
            }
            return 60000.0 / (tempo * PlayerTicksPerBeat * EventData.VirtualTickSize);
        }

        private struct Segment
        {
            public Segment(int startVirtualTick, double msPerVirtualTick, double startMs)
            {
                StartVirtualTick = startVirtualTick;
                MsPerVirtualTick = msPerVirtualTick;
                StartMs = startMs;
            }

            public readonly int StartVirtualTick;
            public readonly double MsPerVirtualTick;
            public readonly double StartMs;
        }
    }
}
