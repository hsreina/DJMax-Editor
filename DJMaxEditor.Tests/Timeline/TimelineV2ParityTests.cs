using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using DJMaxEditor.Controls.Editor;
using DJMaxEditor.Controls.Editor.Renderers.Events;
using DJMaxEditor.Controls.TimelineV2;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;
using DJMaxEditor.Files.pt;
using DJMaxEditor.Tests.Fixtures;

namespace DJMaxEditor.Tests
{
    internal static partial class Program
    {
        private static void RunTimelineV2ParityTests(string fixtures)
        {
            Test("TechnikaNoteClassifier_UsesTechmaniaPtSemantics", () =>
            {
                AssertTechnikaKind(0, 6, "Basic");
                AssertTechnikaKind(0, 7, "Drag");
                AssertTechnikaKind(5, 6, "ChainHead");
                AssertTechnikaKind(6, 6, "ChainNode");
                AssertTechnikaKind(10, 6, "RepeatHead");
                AssertTechnikaKind(10, 7, "RepeatHeadHold");
                AssertTechnikaKind(11, 6, "Repeat");
                AssertTechnikaKind(11, 7, "RepeatHold");
                AssertTechnikaKind(12, 6, "Hold");
                AssertTechnikaKind(99, 6, "Unknown");
            });

            FixtureTest("TechnikaNoteClassifier_RecognizesSonOfSunSpecialNotes",
                fixtures, "TECHNIKA 2 Patterns/sonof/sonof_pop_3.pt", () =>
            {
                string path = Path.Combine(
                    fixtures,
                    "TECHNIKA 2 Patterns",
                    "sonof",
                    "sonof_pop_3.pt");
                PlayerData model = LoadPtff(PtCodec.Decrypt(File.ReadAllBytes(path)), true);
                var kinds = model.Tracks
                    .SelectMany(track => track.Events)
                    .Select(ClassifyTechnika)
                    .ToList();

                AssertTrue(kinds.Count(kind => kind == "ChainHead") == 19,
                    "Son of Sun attr 5 chain heads were not recognized");
                AssertTrue(kinds.Count(kind => kind == "ChainNode") == 19,
                    "Son of Sun attr 6 chain nodes were not recognized");
                AssertTrue(kinds.Count(kind => kind == "RepeatHead") == 133,
                    "Son of Sun attr 10 repeat heads were not recognized");
                AssertTrue(kinds.Count(kind => kind == "Repeat") == 88,
                    "Son of Sun attr 11 repeat nodes were not recognized");
                AssertTrue(kinds.Count(kind => kind == "Hold") == 36,
                    "Son of Sun attr 12 holds were not recognized");
            });

            FixtureTest("TechnikaNoteClassifier_MatchesTutorialReference",
                fixtures, "TECHNIKA 2 Patterns/tutorial/tutorial_pop_1.pt", () =>
            {
                string path = Path.Combine(
                    fixtures,
                    "TECHNIKA 2 Patterns",
                    "tutorial",
                    "tutorial_pop_1.pt");
                PlayerData model = LoadPtff(File.ReadAllBytes(path), false);
                var kinds = model.Tracks
                    .SelectMany(track => track.Events)
                    .Select(ClassifyTechnika)
                    .ToList();

                AssertTrue(kinds.Count(kind => kind == "ChainHead") == 2,
                    "tutorial attr 5 chain heads were not recognized");
                AssertTrue(kinds.Count(kind => kind == "ChainNode") == 2,
                    "tutorial attr 6 chain nodes were not recognized");
                AssertTrue(kinds.Count(kind => kind == "RepeatHead") == 6,
                    "tutorial short attr 10 repeat heads were not recognized");
                AssertTrue(kinds.Count(kind => kind == "RepeatHeadHold") == 2,
                    "tutorial long attr 10 repeat heads were not recognized");
                AssertTrue(kinds.Count(kind => kind == "Repeat") == 4,
                    "tutorial attr 11 repeat nodes were not recognized");
                AssertTrue(kinds.Count(kind => kind == "Hold") == 2,
                    "tutorial attr 12 holds were not recognized");
            });

            FixtureTest("TechnikaNoteArt_UsesAuthoritativeTechmaniaAssets",
                fixtures,
                "Techmania MXGG build 2 source/Techmania source/TECHMANIA/Assets/Sprites/Notes/Basic.png", () =>
            {
                string noteAssets = Path.GetFullPath(Path.Combine(
                    fixtures,
                    "..",
                    "Techmania MXGG build 2 source",
                    "Techmania source",
                    "TECHMANIA",
                    "Assets",
                    "Sprites",
                    "Notes"));

                AssertTechnikaArt(noteAssets, "Basic", "Basic.png");
                AssertTechnikaArt(noteAssets, "Drag", "ChainHead.png");
                AssertTechnikaArt(noteAssets, "ChainHead", "ChainHead.png");
                AssertTechnikaArt(noteAssets, "ChainNode", "ChainNode.png");
                AssertTechnikaArt(noteAssets, "RepeatHead", "RepeatHead.png");
                AssertTechnikaArt(noteAssets, "RepeatHeadHold", "RepeatHead.png");
                AssertTechnikaArt(noteAssets, "Repeat", "Repeat.png");
                AssertTechnikaArt(noteAssets, "RepeatHold", "Repeat.png");
                AssertTechnikaArt(noteAssets, "Hold", "HoldHead.png");
            });

            Test("TimelineV2_TechnikaNotesBypassLegacyThemeSprites", () =>
            {
                var model = SyntheticChartFactory.Create(1, 1, 192);
                EventData note = model.Tracks.GetTrackAtIndex(0).Events.First();
                note.EventType = EventType.Note;
                note.Attribute = 11;
                note.Duration = 6;
                var legacyTheme = new ThrowingTechnikaRenderer();

                using (var surface = new TimelineV2Control())
                {
                    surface.EventTheme = legacyTheme;
                    surface.Bind(new EditorDocumentContext(model, "repeat-node.pt"));
                    using (surface.RenderSnapshot(800, 396))
                    {
                    }
                }

                AssertTrue(!legacyTheme.RenderWasRequested,
                    "V2 still rendered the old DJMax Technika sprite for a repeat node");
            });

            Test("TimelineV2_QuantizeControlChangesVisibleGrid", () =>
            {
                var model = SyntheticChartFactory.Create(4, 24, 96);
                using (var form = BoundTimelineForm(model))
                using (var before = RenderActiveTimeline(form))
                {
                    form.Editor.NoteValue = 16;
                    using (var after = RenderActiveTimeline(form))
                    {
                        AssertTrue(BitmapsDiffer(before, after),
                            "changing Quantize left the V2 grid unchanged");
                    }
                }
            });

            Test("TimelineV2_EventsThemeControlChangesEventArt", () =>
            {
                var model = SyntheticChartFactory.Create(4, 12, 192);
                using (var form = BoundTimelineForm(model))
                using (var before = RenderActiveTimeline(form))
                {
                    form.Editor.CurrentEventsTheme = form.Editor.EventsThemeList
                        .Single(theme => theme.GetName() == "Technika");
                    using (var after = RenderActiveTimeline(form))
                    {
                        AssertTrue(BitmapsDiffer(before, after),
                            "selecting Technika left the V2 event art unchanged");
                    }
                }
            });

            Test("TimelineV2_ZonesThemeControlChangesTrackZones", () =>
            {
                var model = SyntheticChartFactory.Create(16, 2, 384);
                using (var form = BoundTimelineForm(model))
                using (var before = RenderActiveTimeline(form))
                {
                    form.Editor.CurrentZonesTheme = form.Editor.ZonesThemeList
                        .Single(theme => theme.GetName() == "Technika");
                    using (var after = RenderActiveTimeline(form))
                    {
                        AssertTrue(BitmapsDiffer(before, after),
                            "selecting Technika left the V2 track zones unchanged");
                    }
                }
            });

            Test("TimelineV2_AttributeControlChangesDefaultEventLabels", () =>
            {
                var model = SyntheticChartFactory.Create(2, 5, 192);
                model.Tracks.GetTrackAtIndex(0).Events.First().Attribute = 17;
                using (var form = BoundTimelineForm(model))
                using (var before = RenderActiveTimeline(form))
                {
                    form.Editor.EventDisplayMode = EventDisplayMode.Duration;
                    using (var after = RenderActiveTimeline(form))
                    {
                        AssertTrue(BitmapsDiffer(before, after),
                            "changing Attribute display mode left V2 labels unchanged");
                    }
                }
            });

            Test("TimelineV2_FollowWhilePlayingKeepsPlayheadInView", () =>
            {
                var model = SyntheticChartFactory.Create(2, 80, 96);
                using (var form = BoundTimelineForm(model))
                {
                    form.Editor.FollowTracksProgressWhilePlaying = true;
                    form.Editor.IsPlayerPlaying = true;
                    form.ActiveSurface.PlayheadVirtualTick = 6000;

                    var state = form.ActiveSurface.CaptureViewState();
                    AssertTrue(state.OriginTick > 0,
                        "Follow while playing did not move the V2 viewport");
                    double playheadX = 180 +
                        ((form.ActiveSurface.PlayheadVirtualTick - state.OriginTick) *
                            state.PixelsPerTick);
                    AssertTrue(playheadX >= 180 && playheadX <= 800,
                        "followed V2 playhead is outside the visible timeline");
                }
            });

            Test("TimelineV2_FollowDoesNotMoveViewportWhilePaused", () =>
            {
                var model = SyntheticChartFactory.Create(2, 80, 96);
                using (var form = BoundTimelineForm(model))
                {
                    form.Editor.FollowTracksProgressWhilePlaying = true;
                    form.Editor.IsPlayerPlaying = false;
                    form.ActiveSurface.PlayheadVirtualTick = 6000;

                    AssertNear(0, form.ActiveSurface.CaptureViewState().OriginTick, 0.000001,
                        "paused V2 followed the playhead");
                }
            });

            Test("MainForm_SpaceIsThePlayPauseShortcut", () =>
            {
                Type bindings = typeof(MainForm).Assembly.GetType(
                    "DJMaxEditor.MainFormInputBindings");
                AssertTrue(bindings != null, "the Space keyboard command is missing");
                MethodInfo method = bindings.GetMethod(
                    "IsPlayPauseKey",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                AssertTrue(method != null, "the Space keyboard command is not callable");
                AssertTrue((bool)method.Invoke(null, new object[] { Keys.Space }),
                    "Space does not map to play/pause");
                AssertTrue(!(bool)method.Invoke(null, new object[] { Keys.Control | Keys.Space }),
                    "Ctrl+Space is still required for play/pause");
            });

            Test("TimelineV2_TouchpadPinchUsesContinuousZoom", () =>
            {
                Type bindings = typeof(TimelineV2Control).Assembly.GetType(
                    "DJMaxEditor.Controls.TimelineV2.TimelineInputBindings");
                AssertTrue(bindings != null, "Timeline V2 mouse bindings are missing");
                MethodInfo method = bindings.GetMethod(
                    "ResolveWheelAction",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                AssertTrue(method != null, "Timeline V2 wheel bindings are not callable");
                AssertTrue(
                    method.Invoke(null, new object[] { Keys.Alt }).ToString() == "Zoom",
                    "Alt+wheel does not zoom like V1");
                AssertTrue(
                    method.Invoke(null, new object[] { Keys.Control }).ToString() == "Zoom",
                    "touchpad pinch is still interpreted as horizontal scrolling");
                AssertTrue(
                    method.Invoke(null, new object[] { Keys.Shift }).ToString() ==
                        "HorizontalScroll",
                    "Shift+wheel no longer scrolls horizontally");
                AssertTrue(
                    method.Invoke(null, new object[] { Keys.None }).ToString() ==
                        "VerticalScroll",
                    "plain wheel does not scroll tracks like V1");
            });

            Test("TimelineV2_TouchpadSpreadEnlargesNotesAndPreservesAnchor", () =>
            {
                var model = SyntheticChartFactory.Create(8, 16, 192);
                using (var surface = new TimelineV2Control())
                {
                    surface.ClientSize = new Size(1000, 600);
                    surface.Bind(new EditorDocumentContext(model, "touchpad.pt"));
                    TimelineFrame before = surface.CreateFrameForTesting(1000, 600);
                    const int anchorX = 620;
                    double anchorTick = before.Viewport.TickAtScreenX(anchorX);
                    double beforePixelsPerTick = before.Viewport.PixelsPerTick;
                    int beforeRowHeight = before.Coordinates.RowHeight;

                    MethodInfo apply = typeof(TimelineV2Control).GetMethod(
                        "ApplyTouchpadZoom",
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic);
                    AssertTrue(apply != null, "V2 has no touchpad spread handler");
                    apply.Invoke(surface, new object[] { anchorX, 120 });

                    TimelineFrame after = surface.CreateFrameForTesting(1000, 600);
                    AssertTrue(after.Viewport.PixelsPerTick > beforePixelsPerTick,
                        "touchpad spread did not zoom the time axis");
                    AssertTrue(after.Coordinates.RowHeight > beforeRowHeight,
                        "touchpad spread did not enlarge note rows");
                    AssertNear(
                        anchorTick,
                        after.Viewport.TickAtScreenX(anchorX),
                        0.01,
                        "touchpad zoom moved the point under the fingers");
                }
            });

            Test("TimelineV2_TouchpadSpreadEnlargesDefaultNoteGlyphs", () =>
            {
                var model = SyntheticChartFactory.Create(1, 1, 192);
                EventData note = model.Tracks.GetTrackAtIndex(0).Events.First();
                note.Duration = 0;
                using (var surface = new TimelineV2Control())
                {
                    surface.ClientSize = new Size(800, 396);
                    surface.Bind(new EditorDocumentContext(model, "touchpad-glyph.pt"));
                    using (Bitmap before = surface.RenderSnapshot(800, 396))
                    {
                        int beforePixels = CountPixelsWithColor(
                            before,
                            DJMaxEditor.Controls.TimelineV2.Renderers.TimelineRenderTheme.Note);
                        surface.ApplyTouchpadZoom(500, 240);
                        using (Bitmap after = surface.RenderSnapshot(800, 396))
                        {
                            int afterPixels = CountPixelsWithColor(
                                after,
                                DJMaxEditor.Controls.TimelineV2.Renderers.TimelineRenderTheme.Note);
                            AssertTrue(afterPixels > beforePixels,
                                "touchpad spread left default V2 note glyphs at the old size");
                        }
                    }
                }
            });

            Test("TimelineV2_PlaybackCoalescesRapidRepaintRequests", () =>
            {
                Type schedulerType = typeof(TimelineV2Control).Assembly.GetType(
                    "DJMaxEditor.Controls.TimelineV2.PlaybackFrameScheduler");
                AssertTrue(schedulerType != null,
                    "V2 has no playback frame scheduler");
                object scheduler = Activator.CreateInstance(
                    schedulerType,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null,
                    new object[] { 33L },
                    null);
                MethodInfo shouldRender = schedulerType.GetMethod(
                    "ShouldRenderAt",
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic);
                AssertTrue(shouldRender != null,
                    "V2 playback frame scheduler is not callable");
                AssertTrue((bool)shouldRender.Invoke(scheduler, new object[] { 1000L }),
                    "first playback update was incorrectly delayed");
                AssertTrue(!(bool)shouldRender.Invoke(scheduler, new object[] { 1004L }),
                    "4ms playback update was not coalesced");
                AssertTrue(!(bool)shouldRender.Invoke(scheduler, new object[] { 1032L }),
                    "32ms playback update was not coalesced");
                AssertTrue((bool)shouldRender.Invoke(scheduler, new object[] { 1033L }),
                    "33ms playback frame was incorrectly delayed");
            });

            FixtureTest("TimelineV2_SonOfSunPlaybackFrameFitsSmooth30HzBudget",
                fixtures, "TECHNIKA 2 Patterns/sonof/sonof_pop_3.pt", () =>
            {
                string path = Path.Combine(
                    fixtures,
                    "TECHNIKA 2 Patterns",
                    "sonof",
                    "sonof_pop_3.pt");
                PlayerData model = LoadPtff(PtCodec.Decrypt(File.ReadAllBytes(path)), true);
                EventData firstRepeat = model.Tracks
                    .SelectMany(track => track.Events)
                    .First(note => note.Attribute == 10);
                using (var editor = new EditorControl())
                using (var surface = new TimelineV2Control())
                {
                    surface.ClientSize = new Size(1920, 1080);
                    surface.EventTheme = editor.EventsThemeList
                        .Single(theme => theme.GetName() == "Technika");
                    surface.Bind(new EditorDocumentContext(model, path));
                    surface.RestoreViewState(new EditorViewState
                    {
                        OriginTick = Math.Max(0, firstRepeat.VirtualTick - 1200),
                        PixelsPerTick = 0.09,
                        FirstVisibleRow = Math.Max(0, (int)firstRepeat.TrackId - 4),
                        PlayheadVirtualTick = firstRepeat.VirtualTick
                    });
                    using (surface.RenderSnapshot(1920, 1080))
                    {
                    }

                    var frameTimes = new List<double>();
                    for (int frame = 0; frame < 7; frame++)
                    {
                        surface.PlayheadVirtualTick =
                            firstRepeat.VirtualTick + (frame * 24);
                        using (surface.RenderSnapshot(1920, 1080))
                        {
                        }
                        frameTimes.Add(surface.Performance.LastFrameMilliseconds);
                    }
                    frameTimes.Sort();
                    double median = frameTimes[frameTimes.Count / 2];
                    Console.WriteLine(
                        "[METRIC] TimelineV2 Son of Sun playback median={0:F2}ms, visible={1}",
                        median,
                        surface.Performance.LastVisibleItemCount);
                    AssertTrue(median <= 34.0,
                        "Son of Sun V2 playback frame took " +
                        median.ToString("F2") + "ms (30Hz budget is 34ms)");
                }
            });

            Test("LegacyTimeline_RapidPaintNeverReturnsABlankFrame", () =>
            {
                var model = SyntheticChartFactory.Create(4, 20, 96);
                using (var editor = new EditorControl())
                {
                    editor.ClientSize = new Size(800, 420);
                    editor.PerformLayout();
                    editor.Initialize(model);
                    Control drawingArea = editor.Controls.Find("DrawingArea", true).Single();
                    drawingArea.Size = new Size(780, 396);
                    Thread.Sleep(20);
                    MethodInfo draw = typeof(EditorControl).GetMethod(
                        "DrawToBuffer",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    AssertTrue(draw != null, "V1 paint routine is missing");

                    using (var warmup = new Bitmap(780, 396))
                    using (var immediate = new Bitmap(780, 396))
                    using (Graphics warmupGraphics = Graphics.FromImage(warmup))
                    using (Graphics immediateGraphics = Graphics.FromImage(immediate))
                    {
                        warmupGraphics.Clear(drawingArea.BackColor);
                        immediateGraphics.Clear(drawingArea.BackColor);
                        draw.Invoke(editor, new object[] { warmupGraphics });

                        FieldInfo limiterField = typeof(EditorControl).GetField(
                            "m_rameRateLimiter",
                            BindingFlags.Instance | BindingFlags.NonPublic);
                        if (limiterField != null)
                        {
                            object limiter = limiterField.GetValue(editor);
                            FieldInfo lastTime = limiter.GetType().GetField(
                                "m_lastTime",
                                BindingFlags.Instance | BindingFlags.NonPublic);
                            FieldInfo elapsed = limiter.GetType().GetField(
                                "m_elapsedTime",
                                BindingFlags.Instance | BindingFlags.NonPublic);
                            lastTime.SetValue(
                                limiter,
                                (Environment.TickCount * 0.001f) + 10f);
                            elapsed.SetValue(limiter, 0f);
                        }

                        draw.Invoke(editor, new object[] { immediateGraphics });
                        AssertTrue(
                            CountPixelsDifferentFrom(immediate, drawingArea.BackColor) > 1000,
                            "V1 frame limiter skipped Paint and exposed a blank background");
                    }
                }
            });

            Test("TimelineV2_PanMatchesV1Bindings", () =>
            {
                Type bindings = typeof(TimelineV2Control).Assembly.GetType(
                    "DJMaxEditor.Controls.TimelineV2.TimelineInputBindings");
                AssertTrue(bindings != null, "Timeline V2 mouse bindings are missing");
                MethodInfo method = bindings.GetMethod(
                    "IsPanGesture",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                AssertTrue(method != null, "Timeline V2 pan bindings are not callable");
                AssertTrue((bool)method.Invoke(
                        null,
                        new object[] { MouseButtons.Middle, Keys.None, false }),
                    "middle-drag does not pan");
                AssertTrue((bool)method.Invoke(
                        null,
                        new object[] { MouseButtons.Left, Keys.Alt, false }),
                    "Alt+left-drag does not pan like V1");
                AssertTrue((bool)method.Invoke(
                        null,
                        new object[] { MouseButtons.Left, Keys.None, true }),
                    "H+left-drag compatibility pan was lost");
            });
        }

        private static void AssertTechnikaKind(byte attribute, ushort duration, string expected)
        {
            string actual = ClassifyTechnika(new EventData
            {
                EventType = EventType.Note,
                Attribute = attribute,
                Duration = duration
            });
            AssertTrue(actual == expected,
                "attr " + attribute + " duration " + duration +
                " classified as " + actual + " instead of " + expected);
        }

        private static string ClassifyTechnika(EventData source)
        {
            Type classifier = typeof(TimelineV2Control).Assembly.GetType(
                "DJMaxEditor.Controls.TimelineV2.TechnikaNoteClassifier");
            AssertTrue(classifier != null, "the TECHMANIA note classifier is missing");
            MethodInfo method = classifier.GetMethod(
                "Classify",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            AssertTrue(method != null, "the TECHMANIA note classifier is not callable");
            return method.Invoke(null, new object[] { source }).ToString();
        }

        private static void AssertTechnikaArt(
            string sourceDirectory,
            string kind,
            string sourceFile)
        {
            Type art = typeof(TimelineV2Control).Assembly.GetType(
                "DJMaxEditor.Controls.TimelineV2.Renderers.TechnikaNoteArt");
            AssertTrue(art != null, "the authentic TECHMANIA note-art provider is missing");
            MethodInfo method = art.GetMethod(
                "GetPngBytes",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            AssertTrue(method != null, "the authentic TECHMANIA note art is not callable");
            Type kindType = method.GetParameters()[0].ParameterType;
            object kindValue = Enum.Parse(kindType, kind);
            var actual = method.Invoke(null, new[] { kindValue }) as byte[];
            byte[] expected = File.ReadAllBytes(Path.Combine(sourceDirectory, sourceFile));
            AssertTrue(actual != null && Equal(actual, expected),
                kind + " does not use the authoritative " + sourceFile);
        }

        private sealed class ThrowingTechnikaRenderer : IEventRenderer
        {
            public bool RenderWasRequested { get; private set; }

            public string GetName()
            {
                return "Technika";
            }

            public void RenderNote(
                GraphicsWrapper graphics,
                EventData eventData,
                Rectangle eventRectangle,
                int centerX,
                int centerY)
            {
                RenderWasRequested = true;
                throw new InvalidOperationException("legacy Technika note renderer was called");
            }

            public void RenderEventData(
                GraphicsWrapper graphics,
                EventData eventData,
                Rectangle eventRectangle,
                int centerX,
                int centerY)
            {
                RenderWasRequested = true;
                throw new InvalidOperationException("legacy Technika event renderer was called");
            }

            public void DrawZones(
                GraphicsWrapper graphics,
                int trackIndex,
                int trackX,
                int trackY,
                int width,
                int height,
                Rectangle bounds)
            {
            }

            public IEnumerable<KeyValuePair<string, EventData>> GetTemplates()
            {
                return Enumerable.Empty<KeyValuePair<string, EventData>>();
            }
        }

        private static EditorForm BoundTimelineForm(DJMaxEditor.DJMax.PlayerData model)
        {
            var form = new EditorForm(true);
            form.ClientSize = new Size(800, 420);
            form.PerformLayout();
            form.ActiveSurface.View.Size = new Size(800, 396);
            form.Bind(new EditorDocumentContext(model, "v2-parity.pt"));
            return form;
        }

        private static Bitmap RenderActiveTimeline(EditorForm form)
        {
            var timeline = form.ActiveSurface as TimelineV2Control;
            AssertTrue(timeline != null, "Timeline V2 is not active");
            return timeline.RenderSnapshot(800, 396);
        }

        private static bool BitmapsDiffer(Bitmap left, Bitmap right)
        {
            AssertTrue(left.Size == right.Size, "bitmap sizes differ");
            for (int y = 0; y < left.Height; y += 2)
            {
                for (int x = 0; x < left.Width; x += 2)
                {
                    if (left.GetPixel(x, y) != right.GetPixel(x, y))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static int CountPixelsDifferentFrom(Bitmap bitmap, Color background)
        {
            int count = 0;
            for (int y = 0; y < bitmap.Height; y += 3)
            {
                for (int x = 0; x < bitmap.Width; x += 3)
                {
                    if (bitmap.GetPixel(x, y).ToArgb() != background.ToArgb())
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static int CountPixelsWithColor(Bitmap bitmap, Color color)
        {
            int count = 0;
            int expected = color.ToArgb();
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).ToArgb() == expected)
                    {
                        count++;
                    }
                }
            }
            return count;
        }
    }
}
