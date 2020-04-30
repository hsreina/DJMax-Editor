using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace DJMaxEditor.libs {

    public static class GDI {

        public struct BlendFunction {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("msimg32.dll")]
        public static extern bool AlphaBlend(
            IntPtr hdcDest,                         // handle to destination DC
            int nXOriginDest,                       // x-coord of upper-left corner
            int nYOriginDest,                       // y-coord of upper-left corner
            int nWidthDest,                         // destination width
            int nHeightDest,                        // destination height
            IntPtr hdcSrc,                          // handle to source DC
            int nXOriginSrc,                        // x-coord of upper-left corner
            int nYOriginSrc,                        // y-coord of upper-left corner
            int nWidthSrc,                          // source width
            int nHeightSrc,                         // source height
            BlendFunction blendFunction             // alpha-blending function
        );

        [DllImport("gdi32.dll")]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    }
}
