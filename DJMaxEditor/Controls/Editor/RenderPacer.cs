using System;
using System.Diagnostics;

namespace DJMaxEditor.Controls.Editor
{
    /// <summary>
    /// Stopwatch-based frame scheduler for playback rendering. Hands out at most one
    /// render slot per monitor refresh interval and drops missed slots instead of
    /// queueing catch-up work, so a slow paint can never snowball into stutter.
    /// </summary>
    class RenderPacer
    {
        public const int DefaultRefreshRateHz = 60;

        public RenderPacer(int refreshRateHz)
        {
            double hz = refreshRateHz < 1 ? DefaultRefreshRateHz : refreshRateHz;
            _intervalMs = 1000.0 / hz;
        }

        public bool ShouldRender()
        {
            double now = _clock.Elapsed.TotalMilliseconds;
            if (!_started)
            {
                _started = true;
                _nextSlotAt = now + _intervalMs;
                return true;
            }
            if (now < _nextSlotAt)
            {
                return false;
            }
            // Schedule from now rather than the missed slot: a frame that took
            // longer than the interval delays the next one instead of bursting.
            _nextSlotAt = now + _intervalMs;
            return true;
        }

        public void Reset()
        {
            _started = false;
        }

        private readonly double _intervalMs;

        private readonly Stopwatch _clock = Stopwatch.StartNew();

        private double _nextSlotAt;

        private bool _started;
    }
}
