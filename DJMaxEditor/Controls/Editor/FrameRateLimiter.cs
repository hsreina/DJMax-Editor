using System;

namespace DJMaxEditor.Controls.Editor
{
    class FrameRateLimiter
    {
        public FrameRateLimiter(int desiredFrameRate)
        {
            m_desiredFrameRate = desiredFrameRate;
        }

        public bool ShouldAnimateNextFrame()
        {
            var currentTime = Environment.TickCount * 0.001f;
            var deltaTime = currentTime - m_lastTime;
            var desiredFPS = 1.0f / m_desiredFrameRate;

            m_elapsedTime += deltaTime;
            m_lastTime = currentTime;

            if (m_elapsedTime > desiredFPS)
            {
                m_elapsedTime -= desiredFPS;
                return true;
            }

            return false;
        }

        private int m_desiredFrameRate;

        private float m_lastTime = Environment.TickCount * 0.001f;

        private float m_elapsedTime = 0.0f;
    }
}
