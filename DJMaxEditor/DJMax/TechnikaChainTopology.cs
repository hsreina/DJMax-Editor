using System.Collections.Generic;

namespace DJMaxEditor.DJMax
{
    /// <summary>
    /// Legacy Technika .pt charts often leave chain waypoints tagged as ordinary Basic
    /// notes. Gameplay and the playfield preview promote those taps to chain nodes when
    /// they fall strictly inside a ChainHead→ChainNode span; the timeline must use the
    /// same rule or its polyline skips the mountain peaks and still paints them pink.
    /// </summary>
    internal static class TechnikaChainTopology
    {
        public static HashSet<EventData> FindImplicitChainNodes(TracksList tracks)
        {
            var implicitNodes = new HashSet<EventData>();
            if (tracks == null)
            {
                return implicitNodes;
            }

            var notes = new List<ChainNote>(64);
            foreach (TrackData track in tracks)
            {
                if (track == null || track.Idx > 3 || track.Events == null)
                {
                    continue;
                }

                foreach (EventData evt in track.Events)
                {
                    if (evt == null || evt.EventType != EventType.Note)
                    {
                        continue;
                    }

                    byte attr = evt.Attribute;
                    if (attr != (byte)EventAttribute.BasicNote &&
                        attr != (byte)EventAttribute.PressNote &&
                        attr != (byte)EventAttribute.PressNoteEnd)
                    {
                        continue;
                    }

                    // Duration > 6 on attribute 0 is a drag hold, never a chain waypoint.
                    if (attr == (byte)EventAttribute.BasicNote && evt.Duration > 6)
                    {
                        continue;
                    }

                    notes.Add(new ChainNote(evt, (int)track.Idx));
                }
            }

            if (notes.Count == 0)
            {
                return implicitNodes;
            }

            notes.Sort(CompareNotes);

            var heads = new List<ChainNote>();
            var spanEndTick = new List<int>();
            var spanNodeTicks = new List<HashSet<int>>();
            int openSpan = -1;

            foreach (ChainNote note in notes)
            {
                if (note.Event.Attribute == (byte)EventAttribute.PressNote)
                {
                    heads.Add(note);
                    spanEndTick.Add(note.Event.Tick);
                    spanNodeTicks.Add(new HashSet<int>());
                    openSpan = heads.Count - 1;
                }
                else if (note.Event.Attribute == (byte)EventAttribute.PressNoteEnd)
                {
                    if (openSpan < 0)
                    {
                        continue;
                    }

                    spanEndTick[openSpan] = note.Event.Tick;
                    spanNodeTicks[openSpan].Add(note.Event.Tick);
                }
            }

            openSpan = -1;
            int headIndex = 0;
            foreach (ChainNote note in notes)
            {
                if (note.Event.Attribute == (byte)EventAttribute.PressNote)
                {
                    while (headIndex < heads.Count && !ReferenceEquals(heads[headIndex].Event, note.Event))
                    {
                        headIndex++;
                    }

                    openSpan = headIndex < heads.Count ? headIndex : -1;
                    headIndex++;
                    continue;
                }

                if (note.Event.Attribute == (byte)EventAttribute.PressNoteEnd)
                {
                    continue;
                }

                if (openSpan >= 0 &&
                    note.Event.Attribute == (byte)EventAttribute.BasicNote &&
                    note.Event.Tick > heads[openSpan].Event.Tick &&
                    note.Event.Tick <= spanEndTick[openSpan] &&
                    !spanNodeTicks[openSpan].Contains(note.Event.Tick))
                {
                    implicitNodes.Add(note.Event);
                }
            }

            return implicitNodes;
        }

        private static int CompareNotes(ChainNote a, ChainNote b)
        {
            int tick = a.Event.Tick.CompareTo(b.Event.Tick);
            if (tick != 0)
            {
                return tick;
            }

            return a.Lane.CompareTo(b.Lane);
        }

        private struct ChainNote
        {
            public ChainNote(EventData evt, int lane)
            {
                Event = evt;
                Lane = lane;
            }

            public readonly EventData Event;
            public readonly int Lane;
        }
    }
}
