using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DJMaxEditor.Controls.Editor
{
    public class GraphicsWrapper
    {
        private Graphics m_graphics;

        /// <summary>
        /// Device-space mapping for the events pass. While active, every drawing
        /// call's coordinates are mapped from virtual units through this affine
        /// transform and issued with an identity world transform.
        ///
        /// Why: a DrawImage issued through a world transform (even a same-size
        /// cached sprite) goes down GDI+'s generic scaling pipeline - measured at
        /// ~25 us per call regardless of clip, versus ~1-2 us for a plain blit.
        /// On a dense chart that difference is the entire frame budget.
        /// </summary>
        private bool m_deviceSpace;

        private float m_m11, m_m12, m_m21, m_m22, m_dx, m_dy;

        public void BeginDeviceSpace(Matrix transform)
        {
            float[] e = transform.Elements;
            m_m11 = e[0];
            m_m12 = e[1];
            m_m21 = e[2];
            m_m22 = e[3];
            m_dx = e[4];
            m_dy = e[5];
            m_deviceSpace = true;
        }

        public void EndDeviceSpace()
        {
            m_deviceSpace = false;
        }

        private float MapX(float x, float y)
        {
            return m_m11 * x + m_m21 * y + m_dx;
        }

        private float MapY(float x, float y)
        {
            return m_m12 * x + m_m22 * y + m_dy;
        }

        public void UpdateGraphics(Graphics graphics)
        {
            m_graphics = graphics;
        }

        public void FillRectangle(Brush brush, RectangleF rect)
        {
            var graphics = m_graphics;
            if (graphics == null)
            {
                return;
            }
            if (m_deviceSpace)
            {
                float x = MapX(rect.X, rect.Y);
                float y = MapY(rect.X, rect.Y);
                graphics.FillRectangle(brush, x, y,
                    rect.Width * m_m11, rect.Height * m_m22);
                return;
            }
            graphics.FillRectangle(brush, rect);
        }

        public void DrawImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, GraphicsUnit srcUnit, ImageAttributes imageAttrs)
        {
            var graphics = m_graphics;
            if (graphics == null)
            {
                return;
            }

            Bitmap scaled = GetScaledImage(image, destRect, srcX, srcY, srcWidth, srcHeight, imageAttrs);
            if (scaled != null)
            {
                if (m_deviceSpace)
                {
                    // Integer device coordinates + identity transform: the fast
                    // blit path. Sub-pixel placement is surrendered here (at most
                    // half a device pixel per note) - the pre-scaled cache already
                    // committed to this zoom's pixel grid.
                    int x = (int)Math.Round(MapX(destRect.X, destRect.Y));
                    int y = (int)Math.Round(MapY(destRect.X, destRect.Y));
                    graphics.DrawImageUnscaled(scaled, x, y);
                }
                else
                {
                    // Same-size copy: the expensive resampling happened once, when
                    // the cached bitmap was created.
                    graphics.DrawImage(scaled, destRect, 0, 0, scaled.Width, scaled.Height, GraphicsUnit.Pixel, null);
                }
            }
            else if (m_deviceSpace)
            {
                float x = MapX(destRect.X, destRect.Y);
                float y = MapY(destRect.X, destRect.Y);
                graphics.DrawImage(image,
                    new RectangleF(x, y, destRect.Width * m_m11, destRect.Height * m_m22),
                    new RectangleF(srcX, srcY, srcWidth, srcHeight), srcUnit);
                DebugUncachedDraws++;
            }
            else
            {
                graphics.DrawImage(image, destRect, srcX, srcY, srcWidth, srcHeight, srcUnit, imageAttrs);
                DebugUncachedDraws++;
            }
            DebugDrawImageCalls++;
        }

        /// <summary>Diagnostics: how many Graphics.DrawImage calls the current
        /// frame issued and how many of them bypassed the sprite cache (reset by
        /// the hosted benchmark).</summary>
        public static long DebugDrawImageCalls { get; set; }

        public static long DebugUncachedDraws { get; set; }

        /// <summary>
        /// Pre-scaled sprite cache. During playback the timeline redraws the same
        /// note art at the same on-screen size dozens of times per second, and a
        /// scaled GDI+ DrawImage costs several times more than a 1:1 blit - on a
        /// dense chart that difference is the whole frame budget. Zoom is constant
        /// while playing, so each (image, size) pair is resampled exactly once and
        /// then served as a plain copy. Two draw shapes qualify: full-image draws
        /// (note heads) and full-height vertical slices (hold bodies stretched to
        /// their length); everything else keeps the per-frame path.
        /// </summary>
        private Bitmap GetScaledImage(Image image, Rectangle destRect, float srcX, float srcY, float srcWidth, float srcHeight, ImageAttributes imageAttrs)
        {
            if (image == null || imageAttrs != null)
            {
                return null;
            }
            if (srcX != 0f || srcY != 0f || srcHeight != image.Height)
            {
                return null;
            }
            bool fullImage = srcWidth == image.Width;
            bool fullHeightSlice = srcWidth <= 16f && srcWidth >= 1f;
            if (!fullImage && !fullHeightSlice)
            {
                return null;
            }

            float scaleX;
            float scaleY;
            if (m_deviceSpace)
            {
                scaleX = Math.Abs(m_m11);
                scaleY = Math.Abs(m_m22);
            }
            else
            {
                using (var transform = m_graphics.Transform)
                {
                    float[] elements = transform.Elements;
                    scaleX = Math.Abs(elements[0]);
                    scaleY = Math.Abs(elements[3]);
                }
            }
            int width = (int)Math.Round(destRect.Width * scaleX);
            int height = (int)Math.Round(destRect.Height * scaleY);
            if (width <= 0 || height <= 0 || (fullImage && width == image.Width && height == image.Height))
            {
                return null;
            }

            var key = new ScaledImageKey(image, width, height, (int)srcWidth);
            lock (ScaledImageCache)
            {
                Bitmap cached;
                if (ScaledImageCache.TryGetValue(key, out cached) && !cached.Size.IsEmpty)
                {
                    return cached;
                }

                DebugCacheMisses++;

                if (ScaledImageCache.Count >= MaxScaledImageCacheEntries)
                {
                    // Zoom excursions accumulate sizes; drop everything rather than
                    // growing without bound, then refill with what is actually used.
                    foreach (Bitmap entry in ScaledImageCache.Values)
                    {
                        entry.Dispose();
                    }
                    ScaledImageCache.Clear();
                }

                cached = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(cached))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(image, new Rectangle(0, 0, width, height),
                        0, 0, srcWidth, srcHeight, GraphicsUnit.Pixel);
                }
                ScaledImageCache[key] = cached;
                return cached;
            }
        }

        private struct ScaledImageKey : IEquatable<ScaledImageKey>
        {
            private readonly Image m_image;
            private readonly int m_width;
            private readonly int m_height;
            private readonly int m_srcWidth;

            public ScaledImageKey(Image image, int width, int height, int srcWidth)
            {
                m_image = image;
                m_width = width;
                m_height = height;
                m_srcWidth = srcWidth;
            }

            public bool Equals(ScaledImageKey other)
            {
                return ReferenceEquals(m_image, other.m_image) &&
                    m_width == other.m_width &&
                    m_height == other.m_height &&
                    m_srcWidth == other.m_srcWidth;
            }

            public override bool Equals(object obj)
            {
                return obj is ScaledImageKey && Equals((ScaledImageKey)obj);
            }

            public override int GetHashCode()
            {
                return m_image.GetHashCode() ^ (m_width * 397) ^ (m_height * 131) ^ m_srcWidth;
            }
        }

        private static readonly Dictionary<ScaledImageKey, Bitmap> ScaledImageCache =
            new Dictionary<ScaledImageKey, Bitmap>();

        /// <summary>Diagnostics: how often the per-zoom sprite cache had to
        /// resample (should stay near zero after the first frames).</summary>
        public static long DebugCacheMisses { get; private set; }

        private const int MaxScaledImageCacheEntries = 256;

        public void DrawLine(Pen pen, float x1, float y1, float x2, float y2)
        {
            var graphics = m_graphics;
            if (graphics == null || pen == null)
            {
                return;
            }
            if (m_deviceSpace)
            {
                graphics.DrawLine(pen,
                    MapX(x1, y1), MapY(x1, y1),
                    MapX(x2, y2), MapY(x2, y2));
                return;
            }
            graphics.DrawLine(pen, x1, y1, x2, y2);
        }

        public void DrawRectangle(Pen pen, int x, int y, int width, int height)
        {
            var graphics = m_graphics;
            if (graphics == null)
            {
                return;
            }
            if (m_deviceSpace)
            {
                graphics.DrawRectangle(pen,
                    (int)Math.Round(MapX(x, y)),
                    (int)Math.Round(MapY(x, y)),
                    (int)Math.Round(width * m_m11),
                    (int)Math.Round(height * m_m22));
                return;
            }
            graphics.DrawRectangle(pen, x, y, width, height);
        }

        public void DrawRectangle(Pen pen, Rectangle rect)
        {
            DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }

        public void FillRectangle(Brush brush, Rectangle rect)
        {
            FillRectangle(brush, rect.X, rect.Y, rect.Width, rect.Height);
        }

        public void FillRectangle(Brush brush, int x, int y, int width, int height)
        {
            var graphics = m_graphics;
            if (graphics == null)
            {
                return;
            }
            if (m_deviceSpace)
            {
                graphics.FillRectangle(brush,
                    (int)Math.Round(MapX(x, y)),
                    (int)Math.Round(MapY(x, y)),
                    (int)Math.Round(width * m_m11),
                    (int)Math.Round(height * m_m22));
                return;
            }
            graphics.FillRectangle(brush, x, y, width, height);
        }

        public void DrawString(string s, Font font, Brush brush, float x, float y)
        {
            var graphics = m_graphics;
            if (graphics == null)
            {
                return;
            }
            if (m_deviceSpace)
            {
                graphics.DrawString(s, GetDeviceFont(font), brush, MapX(x, y), MapY(x, y));
                return;
            }
            graphics.DrawString(s, font, brush, x, y);
        }

        public void DrawString(string s, Font font, Brush brush, float x, float y, StringFormat format)
        {
            var graphics = m_graphics;
            if (graphics == null)
            {
                return;
            }
            if (m_deviceSpace)
            {
                graphics.DrawString(s, GetDeviceFont(font), brush, MapX(x, y), MapY(x, y), format);
                return;
            }
            graphics.DrawString(s, font, brush, x, y, format);
        }

        /// <summary>
        /// A world transform scales glyphs; device-space drawing does not. Text
        /// drawn by the event themes keeps its on-screen size by pre-scaling the
        /// font through this cache (keyed by zoom, so playback keeps hitting one
        /// entry per font).
        /// </summary>
        private Font GetDeviceFont(Font font)
        {
            float size = font.Size * Math.Abs(m_m11);
            if (size <= 0f)
            {
                return font;
            }

            var key = new ScaledFontKey(font.Name, font.Style, (int)Math.Round(size * 20f));
            lock (ScaledFontCache)
            {
                Font cached;
                if (ScaledFontCache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                if (ScaledFontCache.Count >= MaxScaledFontCacheEntries)
                {
                    foreach (Font entry in ScaledFontCache.Values)
                    {
                        entry.Dispose();
                    }
                    ScaledFontCache.Clear();
                }

                cached = new Font(font.Name, size, font.Style, font.Unit);
                ScaledFontCache[key] = cached;
                return cached;
            }
        }

        private struct ScaledFontKey : IEquatable<ScaledFontKey>
        {
            private readonly string m_name;
            private readonly FontStyle m_style;
            private readonly int m_size;

            public ScaledFontKey(string name, FontStyle style, int size)
            {
                m_name = name;
                m_style = style;
                m_size = size;
            }

            public bool Equals(ScaledFontKey other)
            {
                return m_name == other.m_name && m_style == other.m_style && m_size == other.m_size;
            }

            public override bool Equals(object obj)
            {
                return obj is ScaledFontKey && Equals((ScaledFontKey)obj);
            }

            public override int GetHashCode()
            {
                return (m_name != null ? m_name.GetHashCode() : 0) ^ ((int)m_style * 397) ^ m_size;
            }
        }

        private static readonly Dictionary<ScaledFontKey, Font> ScaledFontCache =
            new Dictionary<ScaledFontKey, Font>();

        private const int MaxScaledFontCacheEntries = 128;
    }
}
