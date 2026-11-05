using System;
using System.Linq;

namespace DJMaxEditor.DJMax
{
    public class Player
    {
        #region public definitions

        /// <summary>
        /// Event triggered when an EventData event occur
        /// </summary>
        public OnEventDelegate OnEvent { get; set; }

        /// <summary>
        /// Event triggered when play status changed
        /// </summary>
        public OnStatusChangeDelegate OnStatusChange { get; set; }

        public delegate void OnEventDelegate(EventData eventData, uint track, EventType eventType, byte pan = 0);
        
        public delegate void OnStatusChangeDelegate(object sender);

        public bool IsReady => m_playerData != null;

        public bool IsPaused { get; private set; }

        public bool IsStopped { get; private set; }

        public bool IsPlaying => !IsPaused && !IsStopped;

        public Player()
        {
            m_timer = new CustomTimer();
            Reset();
        }

        public void LoadPlayerData(PlayerData playerData)
        {
            m_playerData = playerData;
            Reset();
        }

        // Reset the player
        public void Reset()
        {
            m_curEventIdx = 0;
            m_curTick = 0;
            m_curMsTime = 0;
            m_period = 1.0;
            m_curTempo = m_playerData?.Tempo ?? 120.0f;
            if (m_playerData?.SourceFormat == Files.FormatDetection.ChartFormat.BmsClassic &&
                m_curTempo > 0.0f)
            {
                SetTempo(m_curTempo);
            }
            IsStopped = true;
            IsPaused = false;
            TriggerStatusChange();
        }

        // Play from tick (keysound check / play-from-spot). Events strictly before
        // the target are applied silently - tempo and volume catch up, notes do not
        // fire - so seeking mid-chart does not dump every keysound up to that point.
        public void Play(int tick)
        {
            if (false == IsReady)
            {
                return;
            }

            m_timer.Reset();
            ResetMsTime();

            m_curEventIdx = 0;
            m_curTick = Math.Max(0, tick);
            m_curTempo = m_playerData?.Tempo ?? 120.0f;
            SetTempo(m_curTempo);
            // Integrates wall-clock ms across tempo changes while skipping notes.
            m_curMsTime = CatchUpToCurrentTick();

            IsPaused = false;
            IsStopped = false;
            TriggerStatusChange();
        }

        // Pause the player
        public void Pause()
        {
            if (false == IsReady)
            {
                return;
            }

            if (IsPaused)
            {
                return;
            }

            IsPaused = true;
            TriggerStatusChange();
        }

        // Resume the player
        public void Resume()
        {
            if (false == IsReady)
            {
                return;
            }

            if (!IsPaused)
            {
                return;
            }

            ResetMsTime();

            IsPaused = false;
            TriggerStatusChange();
        }

        // Update player timer
        public void Update()
        {
            if (false == IsReady)
            {
                return;
            }

            if (IsPaused || IsStopped)
            {
                return;
            }

            UpdateTimer();
            UpdateAutoPlayEvents();
        }

        // Set thhe player tempo
        public void SetTempo(float tempo)
        {
            m_period = 60000.0 / ((double)tempo * 48);
            m_curTempo = tempo;
        }

        // Retrieve the current tick
        public int GetCurrentTick()
        {
            return m_curTick;
        }

        /// <summary>
        /// The playhead position including the sub-tick remainder the sequencer already
        /// carries. <see cref="GetCurrentTick"/> only moves at the sequencer's whole-tick
        /// rate - tempo * 48 ticks a second, so 48/s at 60 BPM - which is slower than a
        /// 60 Hz display no matter how often the caller asks. The remainder in
        /// m_leftMsTime is the part of the elapsed time that has not rounded into a tick
        /// yet; dividing it by the period in force puts the playhead between ticks, so a
        /// surface that wants motion finer than the sequencer clock can round it as finely
        /// as it likes. The playback pump rounds to virtual ticks (six per native tick).
        /// </summary>
        public double GetCurrentTickExact()
        {
            double fraction = m_period > 0.0 ? m_leftMsTime / m_period : 0.0;
            if (fraction <= 0.0 || fraction >= 1.0)
            {
                return m_curTick;
            }
            return m_curTick + fraction;
        }

        // Retrieve the current ms time
        public long GetCurrentMsTime()
        {
            return m_curMsTime;
        }

        #endregion // public definitions

        #region private definitions

        private int m_curTick;

        private long m_curEventIdx;

        private long m_beforeMsTime;

        private double m_leftMsTime;

        private double m_period;

        private long m_curMsTime;

        private float m_curTempo;

        private PlayerData m_playerData;

        private CustomTimer m_timer;

        private void TriggerStatusChange() 
        {
            OnStatusChange?.Invoke(this);
        }

        // Reset the ms timer
        private void ResetMsTime()
        {
            var msTime = m_timer.GetMsTime();
            m_beforeMsTime = msTime;
            m_leftMsTime = 0.0;
        }

        // Update timers 
        private void UpdateTimer()
        {
            var curMs = m_timer.GetMsTime();

            var msInterval = m_timer.GetTimeIntervalue(curMs, m_beforeMsTime);
            m_leftMsTime += msInterval;
            m_beforeMsTime = curMs;
            if (m_leftMsTime > 0.0)
            {
                var period = m_period;
                var num = (long)(m_leftMsTime / period);
                m_curTick += (int)num;
                m_leftMsTime -= num * period;
            }

            m_curMsTime += msInterval;

            if (m_curTick > m_playerData.MaxTick)
            {
                IsStopped = true;
                TriggerStatusChange();
                Reset();
            }
        }

        private void UpdateAutoPlayEvents()
        {
            DrainEvents(playNotes: true);
        }

        /// <summary>
        /// Advances the event cursor up to <see cref="m_curTick"/> without firing notes.
        /// Returns the integrated song time in ms at the seek point (tempo-aware).
        /// </summary>
        private long CatchUpToCurrentTick()
        {
            if (m_playerData == null)
            {
                Logs.Write("PlayerData is null");
                return 0;
            }

            var events = m_playerData.Tracks.Events;
            double elapsedMs = 0.0;
            int lastTick = 0;

            while (m_curEventIdx < events.Count())
            {
                var scoreEvent = events[(int)m_curEventIdx];
                if (scoreEvent == null)
                {
                    throw new Exception($"ERROR: bad event idx = {m_curEventIdx}");
                }
                if (scoreEvent.Tick > m_curTick)
                {
                    break;
                }

                switch (scoreEvent.EventType)
                {
                    case EventType.Note:
                        // Notes are restored by the shell with a sample offset (BGM).
                        break;
                    case EventType.Tempo:
                        elapsedMs += (scoreEvent.Tick - lastTick) * m_period;
                        lastTick = scoreEvent.Tick;
                        SetTempo(scoreEvent.Tempo);
                        break;
                    case EventType.Volume:
                        HandlePlayEvent(scoreEvent.TrackId, scoreEvent, 0);
                        break;
                }

                m_curEventIdx++;
            }

            elapsedMs += (m_curTick - lastTick) * m_period;
            return (long)elapsedMs;
        }

        private void DrainEvents(bool playNotes)
        {
            if (m_playerData == null)
            {
                Logs.Write("PlayerData is null");
                return;
            }

            var events = m_playerData.Tracks.Events;

            // todo: Should found a faster way without reordering notes at each add/remove
            while (m_curEventIdx < events.Count())
            {
                var scoreEvent = events[(int)m_curEventIdx];

                if (scoreEvent == null)
                {
                    throw new Exception($"ERROR: bad event idx = {m_curEventIdx}");
                }

                if (scoreEvent.Tick > m_curTick)
                {
                    break;
                }

                if (playNotes ||
                    scoreEvent.EventType == EventType.Tempo ||
                    scoreEvent.EventType == EventType.Volume)
                {
                    HandlePlayEvent(scoreEvent.TrackId, scoreEvent, m_curTick - scoreEvent.Tick);
                }

                m_curEventIdx++;
            }
        }

        private void HandlePlayEvent(uint trackIndex, EventData eventData, int elapsedTick)
        {
            switch (eventData.EventType)
            {
                case EventType.Note:
                    OnEvent?.Invoke(eventData, trackIndex, EventType.Note, eventData.Pan);
                    break;
                case EventType.Tempo:
                    SetTempo(eventData.Tempo);
                    break;
                case EventType.Volume:
                    OnEvent?.Invoke(eventData, trackIndex, EventType.Volume);
                    break;
            }
        }

        #endregion // private definitions
    }
}
