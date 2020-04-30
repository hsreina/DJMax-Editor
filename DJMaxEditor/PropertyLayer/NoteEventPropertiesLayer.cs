using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.PropertyLayer
{
    public class NoteEventPropertiesLayer : PropertiesLayerBase
    {
        public NoteEventPropertiesLayer (EventData eventData)
            : base(eventData)
        { }

        /// <summary>
        /// Duration of the current event
        /// </summary>
        [DisplayName("Duration")]
        public ushort Duration {
            get {
                return _eventData.Duration;
            }
            set {
                _eventData.Duration = value;
            }
        }

        /// <summary>
        /// Pan for this event
        /// </summary>
        [DisplayName("Pan")]
        public byte Pan {
            get {
                return _eventData.Pan;
            }

            set {
                _eventData.Pan = value;
            }
        }

        [DisplayName("Attribute")]
        public EventAttribute Attribute {
            get {
                return (EventAttribute)_eventData.Attribute;
            }
            set {
                _eventData.Attribute = (byte)value;
            }
        }

        [DisplayName("Attr")]
        public byte Attr {
            get {
                return _eventData.Attribute;
            }
            set {
                _eventData.Attribute = value;
            }
        }

        [DisplayName("Sound")]
        public string Sound {
            get {
                InstrumentData instData = _eventData.Instrument;
                return instData == null ? "Nothing" : instData.Name;
            }
        }

        [DisplayName("Instrument")]
        public ushort Instrument {
            get {
                var instrument = _eventData.Instrument;

                if (null == instrument)
                {
                    return 0;
                }

                return instrument.InsNum;
            }
        }

        [DisplayName("Track")]
        public uint Track {
            get {
                return _eventData.TrackId;
            }
        }

        [DisplayName("Vel")]
        public byte Vel {
            get {
                return _eventData.Vel;
            }
            set
            {
                _eventData.Vel = value;
            }
        }

    }
}
