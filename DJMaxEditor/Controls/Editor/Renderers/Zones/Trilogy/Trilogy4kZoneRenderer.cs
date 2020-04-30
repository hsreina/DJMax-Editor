using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Zones.Trilogy
{
    internal class Trilogy4kZoneRenderer : ZoneRenderer
    {
        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "4 Keys", 3, 6);
        }

        public override string GetName()
        {
            return "Trilogy 4 keys";
        }
    }
}
