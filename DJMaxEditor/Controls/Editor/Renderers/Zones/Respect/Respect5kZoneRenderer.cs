using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DJMaxEditor.Controls.Editor.Renderers.Zones.Respect
{
    internal class Respect5kZoneRenderer : ZoneRenderer
    {
        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "5 Keys", 3, 7);
        }

        public override string GetName()
        {
            return "Respect 5 keys";
        }

    }
}
