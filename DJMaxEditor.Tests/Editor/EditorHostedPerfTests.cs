using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DJMaxEditor.Controls.Editor;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;
using DJMaxEditor.Files.Tech;
using DJMaxEditor.Preview;

namespace DJMaxEditor.Tests
{
    /// <summary>
    /// Hosted editor benchmark: the real EditorControl in a real Form with a real
    /// message pump, driven exactly like MainForm drives playback (8 ms timer, pacer,
    /// playhead + preview refresh), so the true on-screen paint rate is measured.
    /// Enabled with "--perf &lt;track.tech&gt;".
    /// </summary>
    internal static partial class Program
    {
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);

        private static void RunEditorHostedPerfTests(string perfFile)
        {
            if (string.IsNullOrEmpty(perfFile) || !File.Exists(perfFile))
            {
                return;
            }

            PlayerData pd = TechmaniaChartSerializer.Parse(File.ReadAllBytes(perfFile), 0);
            timeBeginPeriod(1);
            try
            {
                foreach (bool split in new[] { false, true })
                {
                    RunHostedPass(pd, perfFile, split, true);
                    RunHostedPass(pd, perfFile, split, false);
                }
            }
            finally
            {
                timeEndPeriod(1);
            }
        }

        private static void RunHostedPass(PlayerData pd, string perfFile, bool split, bool follow)
        {
            // Seek to the densest region so the benchmark measures the worst case
            // the user actually sees, not the sparse intro.
            int denseStart = FindDensestWindowStart(pd);

            using (var form = new Form
            {
                Text = "perf host",
                Width = 1952,
                Height = 1150,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(60, 60), // must intersect the visible desktop to receive real paints
                TopMost = true // an obscured window loses its 1 ms timer resolution (=> ~63 Hz cap)
            })
            {
                var editor = new EditorControl { Dock = DockStyle.Fill };
                form.Controls.Add(editor);
                var preview = new GameplayPreviewControl { Dock = DockStyle.Bottom, Height = 260 };
                form.Controls.Add(preview);

                var context = new EditorDocumentContext(pd, perfFile, new UndoManager());
                preview.Bind(context);
                var surface = new LegacyEditorSurfaceAdapter(editor);
                surface.Bind(context);
                // Production parity: MainForm auto-switches the event theme to
                // Technika for track.tech charts before playback.
                editor.CurrentEventsTheme =
                    editor.EventsThemeList.FirstOrDefault(t => t.GetName() == "Technika");
                editor.FollowTracksProgressWhilePlaying = follow;
                editor.IsPlayerPlaying = true;
                editor.SplitViewEnabled = split;

                form.Show();
                Application.DoEvents();

                int pacerDiagnostics = 0;
                double tempoTicksPerSecond = pd.Tempo > 0 ? pd.Tempo * 48.0 / 60.0 : 96.0;
                double accumulatedTicks = denseStart;
                int lastVirtualTick = -1;

                // Mirror MainForm's playback render thread: paced slots on a worker
                // thread with a depth-1 frame queue, marshalled with BeginInvoke.
                int refreshHz = DisplayRefreshRate.GetFor(form);
                double intervalMs = 1000.0 / refreshHz;
                bool running = true;
                double lastModelAt = 0;
                var modelClock = Stopwatch.StartNew();
                var frameDone = new ManualResetEventSlim(true);
                var renderThread = new Thread(() =>
                {
                    var clock = Stopwatch.StartNew();
                    double nextSlotAt = 0;
                    while (running)
                    {
                        double now = clock.Elapsed.TotalMilliseconds;
                        double wait = nextSlotAt - now;
                        if (wait > 2.5) { Thread.Sleep((int)(wait - 2)); continue; }
                        if (wait > 0) { Thread.SpinWait(50); continue; }
                        if (!frameDone.Wait(0)) { nextSlotAt = now + intervalMs; continue; }
                        frameDone.Reset();
                        try
                        {
                            form.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    double t = modelClock.Elapsed.TotalMilliseconds;
                                    double deltaSec = (t - lastModelAt) / 1000.0;
                                    lastModelAt = t;
                                    accumulatedTicks += tempoTicksPerSecond * deltaSec;
                                    int tick = (int)accumulatedTicks;
                                    if (tick > pd.MaxTick - 2000)
                                    {
                                        accumulatedTicks = denseStart;
                                        tick = denseStart;
                                    }
                                    pd.CurrentTick = tick;
                                    pd.SmoothVirtualCurrentTick = accumulatedTicks * EventData.VirtualTickSize;
                                    surface.PlayheadVirtualTick = pd.VirtualCurrentTick;
                                    if (pd.VirtualCurrentTick != lastVirtualTick)
                                    {
                                        pacerDiagnostics++;
                                        lastVirtualTick = pd.VirtualCurrentTick;
                                        preview.RefreshPlayback();
                                    }
                                }
                                finally
                                {
                                    frameDone.Set();
                                }
                            }));
                        }
                        catch (InvalidOperationException) { frameDone.Set(); break; }
                        nextSlotAt = now + intervalMs;
                    }
                })
                { IsBackground = true };
                renderThread.Start();

                int paintBefore = editor.DebugPaintCount;
                int diagnosticTick = 0;
                var sw = Stopwatch.StartNew();
                var diagnosticTimer = new System.Windows.Forms.Timer { Interval = 1000 };
                diagnosticTimer.Tick += (s, e) =>
                {
                    Console.WriteLine(
                        $"[HOSTED]   t+{++diagnosticTick}s paints={editor.DebugPaintCount} " +
                        $"lastPaintAgeMs={(editor.DebugClockNowMs - editor.DebugLastPaintAtMs):F0} " +
                        $"frames={pacerDiagnostics} paintMs={editor.DebugPaintMilliseconds:F2} " +
                        $"scrollMs={editor.DebugScrollMs:F2} panesMs={editor.DebugPanesMs:F2} " +
                        $"fillMs={editor.DebugFillMs:F2} eventsMs={editor.DebugEventsMs:F2} " +
                        $"chromeMs={editor.DebugChromeMs:F2} progressMs={editor.DebugProgressMs:F2} " +
                        $"selectMs={editor.DebugSelectMs:F2} notes={editor.DebugNoteDraws} " +
                        $"cacheMisses={GraphicsWrapper.DebugCacheMisses} " +
                        $"drawImageCalls={GraphicsWrapper.DebugDrawImageCalls} " +
                        $"uncached={GraphicsWrapper.DebugUncachedDraws}");
                };
                diagnosticTimer.Start();

                // Warmup + measurement inside a real message loop. DoEvents cannot
                // drain the queue while the paced BeginInvoke producer keeps it fed -
                // the first call never returns - so run the loop properly and stop it
                // from a timer instead.
                var runTimer = new System.Threading.Timer(_ =>
                {
                    running = false;
                    try { form.BeginInvoke(new Action(() => form.Close())); }
                    catch (InvalidOperationException) { }
                }, null, 4800, System.Threading.Timeout.Infinite);

                Application.Run(form);
                sw.Stop();
                runTimer.Dispose();
                diagnosticTimer.Stop();
                renderThread.Join(500);

                double seconds = sw.Elapsed.TotalSeconds;
                int paints = editor.DebugPaintCount - paintBefore;
                Console.WriteLine(
                    $"[HOSTED] split={split,-5} follow={follow,-5} hz={refreshHz} paints={paints,5}  fps={paints / seconds,6:F1}  " +
                    $"paintMs={editor.DebugPaintMilliseconds,5:F2}  wall={seconds:F2}s");
                form.Close();
            }
        }

        private static int FindDensestWindowStart(PlayerData pd)
        {
            const int windowTicks = 2000; // ticks per histogram bin
            int bins = Math.Max(1, pd.MaxTick / windowTicks + 1);
            var counts = new int[bins];
            foreach (var track in pd.Tracks)
            {
                foreach (var ev in track.Events)
                {
                    if (ev.EventType != EventType.Note)
                    {
                        continue;
                    }
                    int bin = ev.Tick / windowTicks;
                    if (bin >= 0 && bin < bins)
                    {
                        counts[bin]++;
                    }
                }
            }
            int best = 0, bestCount = -1;
            for (int i = 0; i < bins; i++)
            {
                if (counts[i] > bestCount)
                {
                    bestCount = counts[i];
                    best = i;
                }
            }
            int start = best * windowTicks - 400;
            Console.WriteLine($"[HOSTED] densest window at tick {best * windowTicks} ({bestCount} notes), starting at {start}");
            return Math.Max(0, start);
        }
    }
}
