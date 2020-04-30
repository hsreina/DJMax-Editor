using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Zones.Technika
{
    internal class TechnikaZoneRenderer : ZoneRenderer
    {
        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player1Visible, "P1 Visible", 0, 3);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.Red, "P1 notes special", 4, 7);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, CustomBrushes.Player2Visible, "P2 Visible", 8, 11);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.Azure, "P2 notes special", 12, 15);
            // Can add track 16 for main audio
            // Can add track 17 for video
        }

        public override string GetName()
        {
            return "Technika";
        }
    }
}
