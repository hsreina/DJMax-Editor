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
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.Orange, "BGM", 16, 16);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.MediumPurple, "BGA", 17, 17);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.Teal, "Speed", 18, 18);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.SlateGray, "Soundtrack", 19, 19);
            DrawZone(g, trackIndex, trackX, trackY, width, height, bounds, Brushes.DarkOliveGreen, "Keysound", 20, 63);
        }

        public override string GetName()
        {
            return "Technika";
        }
    }
}
