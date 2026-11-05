using System;
using System.ComponentModel;

namespace DJMaxEditor.DJMax
{
    public class PlayerData
    {
        #region public defs

        public bool Encrypted = false;

        /// <summary>
        /// True when the document was loaded from a format the editor can read but must not write back
        /// through the selected handler. Editable Respect/trailer documents use a separate save-time
        /// guard which permits BMS export but blocks unverified save-back to the source container.
        /// </summary>
        public bool IsReadOnly = false;

        /// <summary>The detected source format this document was loaded from (for diagnostics / save gating).</summary>
        public Files.FormatDetection.ChartFormat? SourceFormat = null;

        /// <summary>Classic BMS headers, lane mappings, and variable-measure data retained for round trips.</summary>
        public Files.bms.BmsMetadata BmsMetadata { get; set; }

        /// <summary>TECHMANIA track.tech container metadata retained for round trips.</summary>
        public Files.Tech.TechMetadata TechMetadata { get; set; }

        public byte Version { get; set; }

        /****** tmp Debug *******/
        public float TrackDuration { get; set; }
        
        public ushort TickPerMinute { get; set; } // 192

        public float Tempo { get; set; }

        private uint m_headerEndTick;

        /// <summary>
        /// Authored chart end. Takes the max of: file header end tick, last event start across
        /// all tracks, and <see cref="TrackDuration"/> converted at the Player's 48-ticks-per-beat
        /// rate — so an audio tail past the last note still plays.
        /// </summary>
        public uint HeaderEndTick {
            get
            {
                return (uint)Math.Max(0, ResolveEndTick());
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

        public int VirtualMaxTick => MaxTick * EventData.VirtualTickSize;

        public int VirtualCurrentTick { get; private set; } = 0;

        /// <summary>
        /// Playhead position at sub-tick precision, in virtual ticks. The whole-tick
        /// <see cref="VirtualCurrentTick"/> advances at tempo * 48 ticks a second, below a
        /// display's refresh rate at low tempos; whoever drives playback each frame (the
        /// hosted benchmark harness writes it directly; MainForm feeds
        /// IEditorSurface.PlayheadPositionVirtualTick from Player.GetCurrentTickExact)
        /// keeps the sub-tick remainder here so smooth playhead surfaces can read it back.
        /// </summary>
        public double SmoothVirtualCurrentTick { get; set; }

        public int CurrentTick
        {
            get => VirtualCurrentTick / EventData.VirtualTickSize;
            set => VirtualCurrentTick = value * EventData.VirtualTickSize;
        }

        /// <summary>Playback and canvas end — same as <see cref="HeaderEndTick"/>.</summary>
        public int MaxTick => ResolveEndTick();

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
            // Instruments/tracks only — PT/Trailer loaders set HeaderEndTick and TrackDuration
            // before Clear(), then refill events. Wiping the header here dropped the authored
            // song end and stopped playback at the last note.
            Instruments.Clear();
            Tracks.Clear();
        }

        #endregion // public defs

        #region private defs

        /// <summary>
        /// Effective document end: file header, last event on any track, and audio length from
        /// <see cref="TrackDuration"/>.
        /// </summary>
        private int ResolveEndTick()
        {
            int end = Tracks != null ? Math.Max(0, Tracks.MaxTick) : 0;

            if (m_headerEndTick > (uint)end)
            {
                end = (int)m_headerEndTick;
            }

            int fromDuration = EndTickFromTrackDuration();
            if (fromDuration > end)
            {
                end = fromDuration;
            }

            return end;
        }

        /// <summary>
        /// <see cref="TrackDuration"/> (seconds) → ticks at the Player clock (48 ticks/beat).
        /// </summary>
        private int EndTickFromTrackDuration()
        {
            if (TrackDuration <= 0f || Tempo <= 0f)
            {
                return 0;
            }

            double ticks = TrackDuration * (double)Tempo / 60.0 * 48.0;
            if (double.IsNaN(ticks) || double.IsInfinity(ticks) || ticks <= 0.0)
            {
                return 0;
            }

            return (int)Math.Ceiling(ticks);
        }

        #endregion // private defs
    }
}
