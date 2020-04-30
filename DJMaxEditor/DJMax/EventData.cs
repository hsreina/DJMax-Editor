using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor.DJMax
{
    /// <summary>
    /// A DJMax event informations
    /// </summary>
    public class EventData : ICloneable
    {
        /// <summary>
        /// Type of the event
        /// </summary>
        public EventType EventType { get; set; }

        /// <summary>
        /// Associated instrument
        /// </summary>
        public InstrumentData Instrument { get; set; }

        public uint TrackId { get; set; }

        public byte Volume { get; set; }

        public float Tempo { get; set; }

        // EventAttribute enum
        public byte Attribute { get; set; }

        public ushort Beat { get; set; }

        public byte Vel { get; set; }

        public byte Pan { get; set; }

        public ushort Duration
        {
            get => (ushort)(VirtualDuration / VirtualTickSize);
            set => VirtualDuration = (ushort)(value * VirtualTickSize);
        }

        public int Tick
        {
            get => (VirtualTick / VirtualTickSize);
            set => VirtualTick = (value * VirtualTickSize);
        }

        public EventData()
        {
            TrackId = 0;
            Volume = 127;
            Attribute = 0;
            Pan = 64;
            Vel = 127;
            Beat = 4;
            Tempo = 140;
            Duration = 6;
        }

        public const byte VirtualTickSize = 6;

        public ushort VirtualDuration { get; set; } = 0;

        public int VirtualTick { get; set; } = 0;

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
