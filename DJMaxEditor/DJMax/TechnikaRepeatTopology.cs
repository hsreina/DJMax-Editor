using System.Collections.Generic;

namespace DJMaxEditor.DJMax
{
    /// <summary>
    /// Legacy Technika .pt charts tag every pre-close repeat tick as a head (attribute 10).
    /// Gameplay Preview demotes those intermediates to Repeat/RepeatHold members so only the
    /// real series start keeps the head glyph; the timeline must do the same or each waypoint
    /// still paints as a lone purple head and the hold body never appears on held members.
    /// </summary>
    internal static class TechnikaRepeatTopology
    {
        /// <summary>
        /// Attribute-10 notes that sit inside an open series before its end marker (or before
        /// a same-lane non-repeat note closes it). Callers paint these as repeat tails.
        /// </summary>
        public static HashSet<EventData> FindIntermediateHeads(TracksList tracks)
        {
            var intermediates = new HashSet<EventData>();
            if (tracks == null)
            {
                return intermediates;
            }

            foreach (TrackData track in tracks)
            {
                if (track == null || track.Idx > 3 || track.Events == null)
                {
                    continue;
                }

                bool open = false;
                bool endSeen = false;
                foreach (EventData evt in track.Events)
                {
                    if (evt == null || evt.EventType != EventType.Note)
                    {
                        continue;
                    }

                    byte attr = evt.Attribute;
                    if (attr == (byte)EventAttribute.RepeatNote)
                    {
                        if (open && !endSeen)
                        {
                            intermediates.Add(evt);
                        }
                        else
                        {
                            open = true;
                            endSeen = false;
                        }
                    }
                    else if (attr == (byte)EventAttribute.RepeatNoteEnd)
                    {
                        if (open)
                        {
                            endSeen = true;
                        }
                    }
                    else if (open)
                    {
                        open = false;
                        endSeen = false;
                    }
                }
            }

            return intermediates;
        }
    }
}
