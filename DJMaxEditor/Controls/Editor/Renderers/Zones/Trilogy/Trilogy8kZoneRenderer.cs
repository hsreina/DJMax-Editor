using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Zones.Trilogy
{
    internal class Trilogy8kZoneRenderer : ZoneRenderer
    {
        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "8 Keys", 3, 8);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "8 Keys", 10, 11);
        }

        public override string GetName()
        {
            return "Trilogy 8 keys";
        }
    }
}
