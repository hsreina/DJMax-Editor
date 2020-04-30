
using System.ComponentModel;

namespace DJMaxEditor.DJMax
{
    public class PlayerData
    {
        #region public defs

        public bool Encrypted = false;

        public byte Version { get; set; }

        /****** tmp Debug *******/
        public float TrackDuration { get; set; }
        
        public ushort TickPerMinute { get; set; } // 192

        public float Tempo { get; set; }

        private uint m_headerEndTick;

        public uint HeaderEndTick {
            get
            {
                return (uint)MaxTick;
            }
            set
            {
                m_headerEndTick = value;
            }
        }
        /****** tmp Debug *******/

        /// <summary>
        /// list of instruments
        /// </summary>
        public BindingList<InstrumentData> Instruments
        {
            get;
            private set;
        }

        /// <summary>
        /// List of tracks
        /// </summary>
        public TracksList Tracks
        {
            get;
            private set;
        }

        public int VirtualMaxTick => Tracks.MaxTick * EventData.VirtualTickSize;

        public int VirtualCurrentTick { get; private set; } = 0;

        public int CurrentTick
        {
            get => VirtualCurrentTick / EventData.VirtualTickSize;
            set => VirtualCurrentTick = value * EventData.VirtualTickSize;
        }

        public int MaxTick => Tracks.MaxTick;

        public PlayerData()
        {
            Tracks = new TracksList();
            Instruments = new BindingList<InstrumentData>();
        }

        public static int Compare(EventData x, EventData y)
        {
            if (x == y)
            {
                return 0;
            }
            else if (x == null)
            {
                return 1;
            }
            else if (y == null)
            {
                return -1;
            }

            var local0 = x.Tick.CompareTo(y.Tick);

            if (local0 == 0)
            {
                local0 = x.TrackId.CompareTo(y.TrackId);
            }
            return local0;
        }

        public void Clear()
        {
            Instruments.Clear();
            Tracks.Clear();
        }

        #endregion // public defs

        #region private defs

        #endregion // private defs
    }
}
