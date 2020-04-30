using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor.DJMax
{
    public class TracksList : IEnumerable<TrackData>
    {
        public int MaxTick
        {
            get
            {
                return m_trackData.Max(x => x.MaxTick);
            }
        }

        public UInt16 Count
        {
            get
            {
                return (UInt16)m_trackData.Count;
            }
        }

        public event EventHandler EventAdded;

        public event EventHandler EventRemoved;

        public TracksList()
        {            
        }

        #region IEnumerable

        public IEnumerator<TrackData> GetEnumerator()
        {
            return m_trackData.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_trackData.GetEnumerator();
        }

        #endregion // IEnumerable

        public void Clear()
        {
            m_trackData.Clear();
        }

        public TrackData GetTrackAtIndex(UInt32 trackIndex)
        {
            if (trackIndex < m_trackData.Count)
            {
                return m_trackData[(int)trackIndex];
            }

            return null;
        }

        public TrackData GetTrackForEvent(EventData eventData)
        {
            if (null == eventData)
            {
                return null;
            }
            return GetTrackAtIndex(eventData.TrackId);
        }

        public void AddTrack(TrackData trackData)
        {
            trackData.EventAdded += new EventHandler(NoteAdded);
            trackData.EventRemoved += new EventHandler(NoteRemoved);
            m_trackData.Add(trackData);
        }

        private List<TrackData> m_trackData = new List<TrackData>();

        public EventData[] Events;

        private void NoteAdded(object sender, EventArgs e)
        {
            Events = m_trackData.SelectMany(x => x.Events).OrderBy(x => x.Tick).ToArray();
            TriggerEventAdded(null);
        }

        private void NoteRemoved(object sender, EventArgs e)
        {
            Events = m_trackData.SelectMany(x => x.Events).OrderBy(x => x.Tick).ToArray();
            TriggerEventAdded(null);
        }

        private void TriggerEventAdded(EventData eventData)
        {
            EventAdded?.Invoke(this, null);
        }

        private void TriggerEventRemoved(EventData eventData)
        {
            EventRemoved?.Invoke(this, null);
        }
    }
}
