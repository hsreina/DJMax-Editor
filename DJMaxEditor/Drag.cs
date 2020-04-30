
namespace DJMaxEditor
{
    class Drag
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int LX { get; set; }

        public int LY { get; set; }

        public bool Active { get; private set; }

        public Drag()
        {
            X = 0;
            Y = 0;
            Active = false;
            LX = 0;
            LY = 0;
        }

        public void Start(int x, int y, int lx, int ly)
        {
            X = x;
            Y = y;
            LX = lx;
            LY = ly;
            Active = true;
        }

        public void Stop()
        {
            Active = false;
        }
    }
}
