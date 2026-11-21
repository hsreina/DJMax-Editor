using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DJMaxEditor.Controls.Editor
{
    /// <summary>
    /// Reads the active display refresh rate through EnumDisplaySettings so playback
    /// rendering can pace itself to the monitor instead of assuming 60 Hz.
    /// </summary>
    static class DisplayRefreshRate
    {
        private const int ENUM_CURRENT_SETTINGS = -1;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            // 16-byte union: printer fields overlap with
            // { POINT dmPosition; DWORD dmDisplayOrientation; DWORD dmDisplayFixedOutput; }
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        public static int GetFor(Control anchor)
        {
            Screen screen = null;
            try
            {
                if (anchor != null && anchor.IsHandleCreated)
                {
                    screen = Screen.FromHandle(anchor.Handle);
                }
            }
            catch
            {
                // no handle yet; fall through to the primary screen
            }
            return GetFor(screen);
        }

        public static int GetFor(Screen screen)
        {
            try
            {
                var devMode = new DEVMODE();
                devMode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                string deviceName = screen == null ? null : screen.DeviceName;
                if (EnumDisplaySettings(deviceName, ENUM_CURRENT_SETTINGS, ref devMode) &&
                    devMode.dmDisplayFrequency >= 10 &&
                    devMode.dmDisplayFrequency <= 1000)
                {
                    return devMode.dmDisplayFrequency;
                }
            }
            catch
            {
                // fall through to default
            }
            return RenderPacer.DefaultRefreshRateHz;
        }
    }
}
