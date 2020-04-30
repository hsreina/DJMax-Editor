using System;

namespace DJMaxEditor 
{
    public sealed class CustomTimer 
    {
        public long StartupTime = DateTime.Now.Ticks;

        public CustomTimer() { }

        public void Reset() 
        {
            StartupTime = DateTime.Now.Ticks;
        }

        public uint GetMsTime_u() 
        {
            return (uint)((ulong)(DateTime.Now.Ticks - StartupTime) / 10000UL);
        }

        public long GetMsTime() 
        {
            return (DateTime.Now.Ticks - StartupTime) / 10000L;
        }

        public uint GetTimeIntervalue_u(uint cur, uint prev) 
        {
            if (prev <= cur)
            {
                return cur - prev;
            }
            else
            {
                return (uint)(cur + 4294967296UL - prev);
            }            
        }

        public long GetTimeIntervalue(long cur, long prev) 
        {
          return cur - prev;
        }
    }
}
