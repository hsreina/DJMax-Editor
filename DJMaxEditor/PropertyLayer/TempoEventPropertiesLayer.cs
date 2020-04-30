using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.PropertyLayer
{
    public class TempoEventPropertiesLayer : PropertiesLayerBase
    {
        public TempoEventPropertiesLayer (EventData eventData)
            : base(eventData) { }

        [DisplayName("Tempo")]
        public float Tempo {
            get {
                return _eventData.Tempo;
            }
            set {
                _eventData.Tempo = value;
            }
        }
    }
}
