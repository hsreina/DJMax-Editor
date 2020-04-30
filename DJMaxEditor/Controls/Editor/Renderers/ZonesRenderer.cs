using DJMaxEditor.Controls.Editor.Renderers.Zones;
using DJMaxEditor.Controls.Editor.Renderers.Zones.Cyclon;
using DJMaxEditor.Controls.Editor.Renderers.Zones.Null;
using DJMaxEditor.Controls.Editor.Renderers.Zones.Respect;
using DJMaxEditor.Controls.Editor.Renderers.Zones.Technika;
using DJMaxEditor.Controls.Editor.Renderers.Zones.Trilogy;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace DJMaxEditor.Controls.Editor.Renderers
{
    internal class ZonesRenderer
    {
        private IList<IZoneRenderer> m_zones;

        public IEnumerable<IZoneRenderer> Themes
        {
            get
            {
                return m_zones;
            }
        }

        public IZoneRenderer Theme
        {
            get
            {
                return m_theme;
            }

            set
            {
                if (null == value)
                {
                    return;
                }
                m_theme = value;
            }
        }

        public ZonesRenderer()
        {
            m_zones = new List<IZoneRenderer>();
            RegisterZonesRenderers();
            m_theme = m_zones.FirstOrDefault();
        }

        public void DrawZones(GraphicsWrapper g, int trackIndex, int trackX, int trackY, int width, int height, Rectangle bounds)
        {
            m_theme.DrawZones(g, trackIndex, trackX, trackY, width, height, bounds);
        }

        private IZoneRenderer m_theme;

        private void RegisterZonesRenderers()
        {
            m_zones.Add(new NullZoneRenderer());
            m_zones.Add(new CyclonZoneRenderer());
            m_zones.Add(new TechnikaZoneRenderer());
            m_zones.Add(new Trilogy4kZoneRenderer());
            m_zones.Add(new Trilogy5kZoneRenderer());
            m_zones.Add(new Trilogy6kZoneRenderer());
            m_zones.Add(new Trilogy7kZoneRenderer());
            m_zones.Add(new Trilogy8kZoneRenderer());
            m_zones.Add(new Respect5kZoneRenderer());
        }
    }
}
