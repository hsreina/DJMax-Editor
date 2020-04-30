using System;
using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Zones.Cyclon
{
    internal class CyclonZoneRenderer : ZoneRenderer
    {
        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "Left", 0, 5);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player2Visible, "Right", 6, 11);
        }

        public override string GetName()
        {
            return "Cyclon";
        }
    }
}
