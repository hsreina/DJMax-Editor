using System.Drawing;

namespace DJMaxEditor.Controls.Editor.Renderers.Zones.Null
{
    internal class NullZoneRenderer : ZoneRenderer
    {
        public override void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
        }

        public override string GetName()
        {
            return "Default";
        }
    }
}
