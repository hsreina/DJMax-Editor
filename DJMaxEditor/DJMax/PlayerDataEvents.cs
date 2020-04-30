using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor.DJMax
{
    class PlayerDataEvents
    {
        public IEnumerable<EventData> Events
        {
            get
            {
                return _Events;
            }
        }

        public void Clear()
        {
            _Events.Clear();
        }

        public void Add(EventData eventData)
        {
            _Events.Add(eventData);
        }

        public void Remove(EventData eventData)
        {
            _Events.Remove(eventData);
        }

        private List<EventData> _Events = new List<EventData>(/*6000*/);
    }
}
