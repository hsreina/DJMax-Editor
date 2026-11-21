using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor.DJMax
{
    /// <summary>
    /// Tracks informations
    /// </summary>
    public class TrackData
    {
        /// <summary>
        /// The displayed track name
        /// </summary>
        public string DisplayedTrackName { get; set; }

        /// <summary>
        /// Actual volume on the track (not saved in the *.pt)
        /// </summary>
        public float Volume { get; set; }

        /// <summary>
        /// List of event Events, ordered by tick.
        ///
        /// The renderers and writers enumerate every track repeatedly (the paint
        /// loop hits all of them on every frame), so this hands out a cached
        /// ordering instead of a lazy OrderBy - which would re-sort each track on
        /// every single pass. The cache is rebuilt when the track mutates
        /// (add/remove) and self-heals on in-place tick edits (drag/undo,
        /// inspector) via the sortedness scan on the next read.
        /// </summary>
        public IEnumerable<EventData> Events
        {
            get
            {
                List<EventData> ordered = m_orderedEvents;
                if (m_orderDirty || !IsOrderedByVirtualTick(ordered))
                {
                    ordered = m_events.OrderBy(x => x.Tick).ToList();
                    m_orderedEvents = ordered;
                    m_orderDirty = false;
                }
                return ordered;
            }
            private set
            {
                m_orderedEvents = (value as List<EventData>) ?? new List<EventData>();
                m_orderDirty = false;
            }
        }

        /// <summary>
        /// Track index
        /// </summary>
        public uint Idx { get; set; }

        public int MaxTick
        {
            get
            {
                return _maxTick;
            }
        }

        /// <summary>
        /// Initialize trackData with an index
        /// </summary>
        /// <param name="idx"></param>
        public TrackData(uint idx)
        {
            // actually fixed limit for all tracks
            //Events = new List<EventData>();
            m_events = new List<EventData>();
            Events = new List<EventData>();

            Idx = idx;

            Volume = 1;

            DisplayedTrackName = "Track " + idx;
        }

        /// <summary>
        /// The track name
        /// </summary>
        public string TrackName
        {
            get
            {
                return m_trackName;
            }
            set
            {

                if (String.IsNullOrEmpty(value))
                {
                    DisplayedTrackName = String.Format("Track {0}", Idx);
                }
                else
                {
                    DisplayedTrackName = String.Format("Track {0} - {1}", Idx, value);
                }

                m_trackName = value;
            }
        }

        public event EventHandler EventAdded;

        public event EventHandler EventRemoved;

        /// <summary>
        /// Add an event to the TrackData
        /// </summary>
        /// <param name="eventData"></param>
        public void AddEvent(EventData eventData)
        {
            eventData.TrackId = Idx;
            m_events.Add(eventData);
            UpdateOrderedList();
            UpdateMaxTick();
            TriggerEventAdded(eventData);
        }

        /// <summary>Add many events with one sort/notification. Used by large text chart imports.</summary>
        public void AddEvents(IEnumerable<EventData> eventData)
        {
            if (eventData == null) return;
            var additions = eventData.Where(x => x != null).ToList();
            if (additions.Count == 0) return;
            foreach (var item in additions) item.TrackId = Idx;
            m_events.AddRange(additions);
            UpdateOrderedList();
            UpdateMaxTick();
            TriggerEventAdded(null);
        }

        /// <summary>
        /// Remove an event from the TrackData
        /// </summary>
        /// <param name="eventData"></param>
        public void RemoveEvent(EventData eventData)
        {
            m_events.Remove(eventData);
            UpdateOrderedList();
            UpdateMaxTick();
            TriggerEventRemoved(eventData);
        }

        private string m_trackName = null;

        private List<EventData> m_events;

        private List<EventData> m_orderedEvents = new List<EventData>();

        private bool m_orderDirty;

        private int _maxTick = 1;

        private static bool IsOrderedByVirtualTick(List<EventData> ordered)
        {
            for (int i = 1; i < ordered.Count; i++)
            {
                if (ordered[i - 1].VirtualTick > ordered[i].VirtualTick)
                {
                    return false;
                }
            }
            return true;
        }

        private void TriggerEventAdded(EventData eventData)
        {
            EventAdded?.Invoke(this, null);
        }

        private void TriggerEventRemoved(EventData eventData)
        {
            EventRemoved?.Invoke(this, null);
        }

        private void UpdateOrderedList()
        {
            // Defer the re-sort to the next enumeration; add/remove bursts during
            // imports would otherwise sort once per event.
            m_orderDirty = true;
        }

        private void UpdateMaxTick()
        {
            var eventsCount = m_events.Count;
            if (eventsCount == 0)
            {
                _maxTick = 0;
            }
            else
            {
                _maxTick = m_events.Max(x => x.Tick);
            }
        }
    }
}
