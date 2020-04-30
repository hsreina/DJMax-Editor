using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Zones.Trilogy
{
    internal class Trilogy7kZoneRenderer : ZoneRenderer
    {
        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "7 Keys", 3, 7);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "7 Keys", 10, 11);
        }

        public override string GetName()
        {
            return "Trilogy 7 keys";
        }
    }
}
