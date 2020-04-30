using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.PropertyLayer
{
    public class VolumeEventPropertiesLayer : PropertiesLayerBase
    {
        public VolumeEventPropertiesLayer (EventData eventData)
            : base(eventData)
        {
            EventData = eventData;
        }

        [DisplayName("Volume")]
        public byte Volume {
            get {
                return _eventData.Volume;
            }
            set {
                _eventData.Volume = value;
            }
        }

        [Browsable(false)]
        public EventData EventData { get; }
    }
}
