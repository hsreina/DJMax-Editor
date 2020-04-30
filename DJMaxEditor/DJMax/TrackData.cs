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
        /// List of event Events
        /// </summary>
        public IEnumerable<EventData> Events { get; private set; }

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

        private int _maxTick = 1;

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
            Events = m_events.OrderBy(x => x.Tick);
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
