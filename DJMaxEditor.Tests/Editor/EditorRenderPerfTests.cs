using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using DJMaxEditor.Controls.Editor;
using DJMaxEditor.Controls.Editor.Renderers;
using DJMaxEditor.Controls.Editor.Renderers.Events;
using DJMaxEditor.DJMax;
using DJMaxEditor.Files.Tech;

namespace DJMaxEditor.Tests
{
    /// <summary>
    /// Headless editor-render benchmark. Drives the real TracksRenderer/EventsRenderer
    /// paint path over a real chart with DrawToBuffer's exact graphics settings so
    /// render optimizations can be measured. Enabled with "--perf &lt;track.tech&gt;";
    /// reports instead of asserting.
    /// </summary>
    internal static partial class Program
    {
        private const int PerfWidth = 1920;
        private const int PerfHeight = 1080;
        private const float PerfZoom = 0.5f;
        private const int PerfNoteValue = 8;
        private const int UpperPaneTrackCount = 16;

        private static void RunEditorRenderPerfTests(string perfFile)
        {
            if (string.IsNullOrEmpty(perfFile))
            {
                return;
            }
            if (!File.Exists(perfFile))
            {
                Console.WriteLine($"[PERF] chart not found: {perfFile}");
                return;
            }

            PlayerData pd = TechmaniaChartSerializer.Parse(File.ReadAllBytes(perfFile), 0);
            int trackCount = pd.Tracks.Count();
            int eventCount = pd.Tracks.Sum(t => t.Events.Count());
            Console.WriteLine($"[PERF] tracks={trackCount} events={eventCount} maxTick={pd.MaxTick} tickPerMinute={pd.TickPerMinute}");

            var eventsRenderer = new EventsRenderer { Theme = new TechnikaThemeRenderer() };
            var tracksRenderer = new TracksRenderer(eventsRenderer, new ZonesRenderer());
            var gw = new GraphicsWrapper();

            var drawableZone = new Rectangle(0, 0, pd.VirtualMaxTick, trackCount * EventsRenderer.VirtualTrackheight);
            int beatSize = EventData.VirtualTickSize * pd.TickPerMinute;
            int blockSize = beatSize / PerfNoteValue;

            using (var bitmap = new Bitmap(PerfWidth, PerfHeight))
            using (var g = Graphics.FromImage(bitmap))
            {
                gw.UpdateGraphics(g);
                foreach (bool split in new[] { false, true })
                {
                    foreach (var quality in new[]
                    {
                        Tuple.Create("current(HQbicubic+AA)", SmoothingMode.AntiAlias, InterpolationMode.HighQualityBicubic, PixelOffsetMode.HighQuality),
                        Tuple.Create("fast(bilinear+AA)", SmoothingMode.AntiAlias, InterpolationMode.Bilinear, PixelOffsetMode.Half),
                        Tuple.Create("fastest(nearest+none)", SmoothingMode.None, InterpolationMode.NearestNeighbor, PixelOffsetMode.None),
                    })
                    {
                        double[] samples = RenderSweep(g, gw, tracksRenderer, pd, drawableZone, beatSize, blockSize, split, quality.Item2, quality.Item3, quality.Item4);
                        Array.Sort(samples);
                        double avg = samples.Average();
                        double p95 = samples[(int)(samples.Length * 0.95)];
                        Console.WriteLine($"[PERF] split={split,-5} {quality.Item1,-24} avg={avg,6:F2}ms p95={p95,6:F2}ms max={samples[samples.Length - 1],6:F2}ms  (~{1000.0 / avg:F0} fps)");
                    }
                }
            }
        }

        private static double[] RenderSweep(
            Graphics g, GraphicsWrapper gw, TracksRenderer tracksRenderer, PlayerData pd,
            Rectangle drawableZone, int beatSize, int blockSize, bool split,
            SmoothingMode smoothing, InterpolationMode interpolation, PixelOffsetMode pixelOffset)
        {
            const int frames = 240;
            var samples = new double[frames];
            int viewW = (int)Math.Ceiling(PerfWidth / PerfZoom);
            int advance = Math.Max(1, viewW / 2);
            int totalVt = Math.Max(1, pd.VirtualMaxTick);
            int upperH = split ? PerfHeight / 2 : PerfHeight;
            int lowerH = Math.Max(1, PerfHeight - upperH);
            int upperPaneVt = (int)Math.Ceiling(upperH / PerfZoom);
            int lowerPaneVt = (int)Math.Ceiling(lowerH / PerfZoom);
            int lowerTop = UpperPaneTrackCount * EventsRenderer.VirtualTrackheight;

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < frames; i++)
            {
                int x = (int)((long)i * (totalVt - viewW) / frames);
                sw.Restart();

                g.SmoothingMode = smoothing;
                g.InterpolationMode = interpolation;
                g.PixelOffsetMode = pixelOffset;

                var upperBounds = new Rectangle(x, 0, viewW, upperPaneVt);
                tracksRenderer.RenderTracskList(gw, pd.Tracks, upperBounds, beatSize, blockSize, pd.VirtualMaxTick, drawableZone);
                if (split)
                {
                    var lowerBounds = new Rectangle(x, lowerTop, viewW, lowerPaneVt);
                    tracksRenderer.RenderTracskList(gw, pd.Tracks, lowerBounds, beatSize, blockSize, pd.VirtualMaxTick, drawableZone);
                }

                g.Flush();
                sw.Stop();
                samples[i] = sw.Elapsed.TotalMilliseconds;
            }
            return samples;
        }
    }
}
