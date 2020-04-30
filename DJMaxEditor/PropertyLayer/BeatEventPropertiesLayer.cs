using System.ComponentModel;
using DJMaxEditor.DJMax;

namespace DJMaxEditor.PropertyLayer
{
    public class BeatEventPropertiesLayer : PropertiesLayerBase
    {
        public BeatEventPropertiesLayer (EventData eventData)
            : base(eventData) { }

        [DisplayName("Beat")]
        public ushort Beat {
            get {
                return _eventData.Beat;
            }
            set {
                _eventData.Beat = value;
            }
        }
    }
}
