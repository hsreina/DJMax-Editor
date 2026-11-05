using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;
using DJMaxEditor.Files.FormatDetection;
using DJMaxEditor.Files.Tech;
using DJMaxEditor.Preview;

namespace DJMaxEditor.Tests
{
    internal static partial class Program
    {
        private static void RunGameplayPreviewTests()
        {
            Test("GameplayPreview_ProfileResolverRoutesTechnikaVocabularyCharts", () =>
            {
                var ptff = PreviewModel(ChartFormat.PtffDecrypted, 4);
                GameplayPreviewProfileSuggestion suggestion =
                    GameplayPreviewProfileResolver.Suggest(ptff);

                AssertTrue(suggestion.Profile == GameplayPreviewProfile.Technika,
                    "PTFF should offer the TECHNIKA projection");
                AssertTrue(suggestion.RequiresConfirmation,
                    "ambiguous Technika/Trilogy PTFF must require explicit confirmation");

                var bytes = PreviewModel(ChartFormat.TrailerRespectV, 4);
                suggestion = GameplayPreviewProfileResolver.Suggest(bytes);
                AssertTrue(suggestion.Profile == GameplayPreviewProfile.Technika,
                    "BYTES (Technika Q trailer) should render on the TECHNIKA playfield");
                AssertTrue(!suggestion.RequiresConfirmation,
                    "BYTES data is unambiguous Technika vocabulary");

                var bms = PreviewModel(ChartFormat.BmsClassic, 4);
                suggestion = GameplayPreviewProfileResolver.Suggest(bms);
                AssertTrue(suggestion.Profile == GameplayPreviewProfile.Generic,
                    "BMS must keep the generic lane preview");
                AssertTrue(!suggestion.RequiresConfirmation,
                    "generic BMS projection should not request TECHNIKA confirmation");

                var tech = PreviewModel(ChartFormat.TechmaniaTrack, 4);
                suggestion = GameplayPreviewProfileResolver.Suggest(tech);
                AssertTrue(suggestion.Profile == GameplayPreviewProfile.Technika,
                    "track.tech should render on the TECHNIKA playfield");
                AssertTrue(!suggestion.RequiresConfirmation,
                    "track.tech metadata is unambiguous TECHNIKA vocabulary");
            });

            Test("GameplayPreview_UsesExactTwoWayTechnikaGeometry", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AddPreviewNote(model, 0, 0, 0, 6);
                AddPreviewNote(model, 1, 192, 0, 6);
                AddPreviewNote(model, 3, 96, 0, 6);

                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);
                ProjectedGameplayNote bottom = chart.Notes.Single(n => n.Source.Tick == 0);
                ProjectedGameplayNote top = chart.Notes.Single(n => n.Source.Tick == 192);

                AssertPreviewNear(bottom.X, 1116.5 / 1280.0, 0.0001,
                    "scan 0 must begin at the measured bottom-right field edge");
                AssertPreviewNear(top.X, 156.5 / 1280.0, 0.0001,
                    "scan 1 must begin at the measured top-left field edge");
                AssertTrue(!bottom.IsTopHalf && top.IsTopHalf,
                    "even/odd scans did not alternate bottom/top");
                AssertPreviewNear(chart.Notes.Single(n => n.Source.TrackId == 0).Y,
                    0.58125, 0.0001, "four-lane bottom center changed");
            });

            Test("GameplayPreview_EndOfScanMarkerKeepsDividerNoteOnPreviousHalf", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 8);
                EventData visible = AddPreviewNote(model, 0, 192, 0, 6);
                AddPreviewNote(model, 4, 192, 0, 6);

                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);
                ProjectedGameplayNote note = chart.Notes.Single(n => n.Source == visible);

                AssertTrue(note.EndOfScan, "matching special-track marker was ignored");
                AssertTrue(note.ScanIndex == 0 && !note.IsTopHalf,
                    "divider note moved to the next scan");
                AssertPreviewNear(note.RelativeScan, 1.0, 0.0001,
                    "divider note should land at the outgoing edge");
                AssertPreviewNear(note.X, 156.5 / 1280.0, 0.0001,
                    "bottom RTL outgoing edge is incorrect");
            });

            Test("GameplayPreview_AppliesChartWideChainAndRepeatFixups", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AddPreviewNote(model, 0, 12, 5, 6);
                EventData promoted = AddPreviewNote(model, 1, 24, 0, 6);
                EventData reverted = AddPreviewNote(model, 2, 36, 0, 6);
                AddPreviewNote(model, 3, 36, 6, 6);
                AddPreviewNote(model, 0, 48, 10, 6);
                EventData repeatMember = AddPreviewNote(model, 0, 60, 10, 12);
                AddPreviewNote(model, 0, 72, 11, 6);

                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);

                AssertTrue(chart.Notes.Single(n => n.Source == promoted).Kind ==
                    GameplayPreviewNoteKind.ChainNode,
                    "basic note inside the global chain was not promoted");
                AssertTrue(chart.Notes.Single(n => n.Source == reverted).Kind ==
                    GameplayPreviewNoteKind.Basic,
                    "simultaneous implicit node was not reverted at chain close");
                AssertTrue(chart.Notes.Single(n => n.Source == repeatMember).Kind ==
                    GameplayPreviewNoteKind.RepeatHold,
                    "second per-lane repeat head was not converted to a repeat member");
            });

            Test("GameplayPreview_FrameUsesTheSharedModelTickAndActivationBoundary", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                EventData next = AddPreviewNote(model, 0, 192, 0, 6);
                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);

                model.CurrentTick = 167;
                GameplayPreviewFrame before = chart.CreateFrame(model.CurrentTick);
                model.CurrentTick = 168;
                GameplayPreviewFrame boundary = chart.CreateFrame(model.CurrentTick);

                AssertTrue(before.Notes.Single(n => n.Source == next).State ==
                    GameplayPreviewNoteState.Prepare,
                    "next scan activated before the 0.875 boundary");
                AssertTrue(boundary.Notes.Single(n => n.Source == next).State ==
                    GameplayPreviewNoteState.Active,
                    "next scan did not activate at the 0.875 boundary");
                AssertTrue(boundary.CurrentTick == model.CurrentTick,
                    "preview frame did not consume the shared model tick");
            });

            Test("GameplayPreview_RenderableFrameOnlyClonesTheVisiblePlaybackWindow", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                EventData current = AddPreviewNote(model, 0, 0, 0, 6);
                EventData next = AddPreviewNote(model, 1, 192, 0, 6);
                AddPreviewNote(model, 2, 384, 0, 6);
                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);

                GameplayPreviewFrame frame = chart.CreateRenderableFrame(24);

                AssertTrue(frame.Notes.Any(note => note.Source == current),
                    "current Technika scan was omitted from the renderable frame");
                AssertTrue(frame.Notes.Any(note => note.Source == next),
                    "next Technika scan was omitted from the renderable frame");
                AssertTrue(frame.Notes.Count < chart.Notes.Count,
                    "renderable frame cloned notes outside the visible playback window");
            });

            Test("GameplayPreview_HoldSpanningAScanSurvivesTheRenderableWindow", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                // 192 ticks per measure and four beats per scan give 960 pulses per
                // scan (5 per tick): duration 240 puts the tail at float scan 1.25.
                EventData hold = AddPreviewNote(model, 0, 0, 12, 240);
                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);

                // The sweep is inside scan 1 while the tail is still ahead; the head sits
                // a full scan behind, and testing the head alone dropped the note here.
                GameplayPreviewFrame frame = chart.CreateRenderableFrame(192);
                ProjectedGameplayNote note = frame.Notes.SingleOrDefault(n => n.Source == hold);

                AssertTrue(note != null,
                    "hold whose head is a scan behind vanished from the renderable frame");
                AssertTrue(note.State == GameplayPreviewNoteState.Active,
                    "hold with its tail still ahead was not Active");
            });

            Test("GameplayPreview_NoteResolvesOnlyAfterTheSweepPassesItsEnd", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                EventData hold = AddPreviewNote(model, 0, 0, 12, 240); // tail at float scan 1.25
                EventData tap = AddPreviewNote(model, 1, 0, 0, 6);
                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);

                AssertTrue(chart.CreateFrame(192).Notes.Single(n => n.Source == hold).State ==
                    GameplayPreviewNoteState.Active,
                    "hold resolved while its tail was still ahead of the sweep");
                AssertTrue(chart.CreateFrame(288).Notes.Single(n => n.Source == hold).State ==
                    GameplayPreviewNoteState.Resolved,
                    "hold did not resolve once the sweep passed its tail");
                AssertTrue(chart.CreateFrame(96).Notes.Single(n => n.Source == tap).State ==
                    GameplayPreviewNoteState.Resolved,
                    "tap did not resolve the moment the sweep cleared its head");
            });

            Test("GameplayPreview_MultiNodeChainSpanKeepsEveryExplicitNode", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AddPreviewNote(model, 0, 12, 5, 6); // chain head
                EventData firstNode = AddPreviewNote(model, 0, 24, 6, 6);
                EventData sharedTickTap = AddPreviewNote(model, 1, 24, 0, 6);
                EventData absorbed = AddPreviewNote(model, 2, 30, 0, 6);
                EventData secondNode = AddPreviewNote(model, 3, 36, 6, 6);

                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);

                AssertTrue(chart.Notes.Single(n => n.Source == firstNode).Kind ==
                    GameplayPreviewNoteKind.ChainNode,
                    "first explicit chain node was not kept");
                AssertTrue(chart.Notes.Single(n => n.Source == secondNode).Kind ==
                    GameplayPreviewNoteKind.ChainNode,
                    "second explicit chain node was demoted to an orphan");
                ProjectedGameplayNote absorbedNote = chart.Notes.Single(n => n.Source == absorbed);
                AssertTrue(absorbedNote.Kind == GameplayPreviewNoteKind.ChainNode &&
                    absorbedNote.IsImplicitChainNode,
                    "basic note strictly inside the multi-node span was not absorbed");
                AssertTrue(chart.Notes.Single(n => n.Source == sharedTickTap).Kind ==
                    GameplayPreviewNoteKind.Basic,
                    "tap sharing a waypoint pulse was swallowed into the chain");
                AssertTrue(!chart.Diagnostics.Any(d =>
                        d.IndexOf("Orphan chain node", StringComparison.Ordinal) >= 0),
                    "explicit chain nodes were reported as orphans");
            });

            Test("GameplayPreview_TechnikaReadsBeatsPerScanAndPlayableLanesFromTechMetadata", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AddPreviewNote(model, 0, 192, 0, 6);
                AddPreviewNote(model, 3, 192, 0, 6);
                model.TechMetadata = new TechMetadata { Bps = 8, PlayableLanes = 2 };

                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);

                AssertTrue(chart.BeatsPerScan == 8, "declared beats per scan was ignored");
                // 960 pulses at 240*8 pulses per scan is float scan 0.5, not scan 1.
                AssertTrue(chart.Notes.All(n => n.ScanIndex == 0),
                    "notes were placed against the default four-beat scan");
                AssertTrue(chart.LaneCount == 2, "declared playable lanes were ignored");
                AssertTrue(chart.Notes.All(n => n.Lane < 2),
                    "notes on non-playable autopilot lanes were projected");

                model.TechMetadata = new TechMetadata { Bps = 0 };
                chart = GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);
                AssertTrue(chart.BeatsPerScan == 4,
                    "illegal beats per scan did not fall back to four");
            });

            Test("GameplayPreview_GenericModeDoesNotApplyTechnikaSpecialTracks", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.TrailerRespectV, 8);
                EventData visible = AddPreviewNote(model, 0, 192, 0, 6);
                AddPreviewNote(model, 4, 192, 0, 6);

                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Generic);
                ProjectedGameplayNote note = chart.Notes.Single(n => n.Source == visible);

                AssertTrue(!note.EndOfScan,
                    "generic preview applied TECHNIKA special-track semantics");
                AssertTrue(chart.StatusLabel.IndexOf("APPROX", StringComparison.OrdinalIgnoreCase) >= 0,
                    "generic preview is not visibly labelled as an approximation");
            });

            Test("GameplayPreviewDock_BindsTheSameDocumentAndNeverEdits", () =>
            {
                var document = new EditorDocumentContext(
                    PreviewModel(ChartFormat.PtffDecrypted, 4),
                    "preview.pt");
                using (var dock = new GameplayPreviewForm())
                {
                    dock.Bind(document);
                    AssertTrue(object.ReferenceEquals(dock.Document, document),
                        "preview cloned or replaced the editor document context");
                    AssertTrue(!dock.SupportsEditing,
                        "preview must remain a visualization-only surface");
                }
            });

            Test("GameplayPreviewDock_DefaultsToTechnikaForBytes", () =>
            {
                var document = new EditorDocumentContext(
                    PreviewModel(ChartFormat.TrailerRespectV, 4),
                    "preview.bytes");
                using (var dock = new GameplayPreviewForm())
                {
                    dock.Bind(document);
                    AssertTrue(dock.Profile == GameplayPreviewProfile.Technika,
                        "a BYTES chart did not open in the TECHNIKA projection by default");
                }
            });

            Test("GameplayPreviewDock_DefaultsToTechnikaForPtff", () =>
            {
                var document = new EditorDocumentContext(
                    PreviewModel(ChartFormat.PtffDecrypted, 4),
                    "preview.pt");
                using (var dock = new GameplayPreviewForm())
                {
                    dock.Bind(document);
                    AssertTrue(dock.Profile == GameplayPreviewProfile.Technika,
                        "a PTFF chart did not open in the TECHNIKA projection by default");
                }
            });

            Test("GameplayPreviewControl_RendersAResizableReadOnlyFrame", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AddPreviewNote(model, 0, 24, 0, 6);
                AddPreviewNote(model, 3, 96, 12, 48);
                model.CurrentTick = 30;
                using (var control = new GameplayPreviewControl())
                using (var bitmap = new Bitmap(960, 540))
                {
                    control.Size = bitmap.Size;
                    control.Bind(new EditorDocumentContext(model, "render.pt"));
                    control.SetProfile(GameplayPreviewProfile.Technika);
                    control.NoteZoom = 1.8f;
                    control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));

                    AssertTrue(bitmap.GetPixel(480, 270) != Color.Empty,
                        "preview produced no drawable frame");
                    AssertTrue(control.NoteZoom == 1.8f,
                        "preview note-size zoom was not retained");
                }
            });

            Test("GameplayPreview_ScrollDirectionReprojectsNotePlacement", () =>
            {
                // tick 960 at 192 tpm = pulse 1200: scan 1 (upper half), a quarter in.
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AddPreviewNote(model, 0, 960, 0, 6);

                GameplayPreviewProjection cw =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);
                GameplayPreviewProjection allLeft = GameplayPreviewProjector.Project(
                    model, GameplayPreviewProfile.Technika,
                    TechnikaScrollDirection.AllLeft);

                ProjectedGameplayNote noteCw = cw.Notes.Single();
                ProjectedGameplayNote noteLeft = allLeft.Notes.Single();
                double left = GameplayPreviewProjector.TechnikaFieldLeft;
                double right = GameplayPreviewProjector.TechnikaFieldRight;

                AssertTrue(noteCw.IsTopHalf, "setup note should sit in the upper half");
                AssertTrue(Math.Abs(noteLeft.X - (right - (noteCw.X - left))) < 1e-9,
                    "AllLeft did not mirror the clockwise placement about the field");
                AssertTrue(allLeft.ScrollDirection == TechnikaScrollDirection.AllLeft,
                    "projection did not keep the scroll direction it was built with");
                AssertTrue(!GameplayPreviewProjector.TechnikaSweepRightward(true,
                        TechnikaScrollDirection.AllLeft),
                    "sweep direction rule disagrees with the projector for AllLeft");
                AssertTrue(GameplayPreviewProjector.TechnikaSweepRightward(false,
                        TechnikaScrollDirection.AllRight),
                    "sweep direction rule disagrees with the projector for AllRight");
            });

            Test("TechnikaHitFlash_KeepsFrameProgressThroughScaling", () =>
            {
                TechnikaHitFlash flash = TechnikaHitFlash.At(0.5);
                AssertTrue(Math.Abs(flash.FrameProgress - 0.5) < 1e-9,
                    "frame progress not carried by At");
                TechnikaHitFlash scaled = flash.Scaled(0.75, 0.5);
                AssertTrue(Math.Abs(scaled.FrameProgress - 0.5) < 1e-9,
                    "Scaled must pass the frame sequence progress through untouched");
                AssertTrue(TechnikaHitFlash.At(1.4).FrameProgress == 0.0 &&
                    TechnikaHitFlash.At(-0.2).FrameProgress == 0.0,
                    "out-of-window flashes must not index the frame sequence");
            });

            Test("TechnikaNoteSprite_SweepPicksFrameByWhereItsLightSits", () =>
            {
                // Two frames: a bright blob at the far left of the first, the far right of
                // the second - the shape of note_circle's opening and closing crescents.
                TechnikaNoteSprite sprite = TechnikaNoteSprite.Sequence(new Image[]
                {
                    FrameWithBlob(20, 2), FrameWithBlob(20, 17)
                });

                Image near = sprite.Sweep(0.55);
                Image far = sprite.Sweep(-5.0);
                AssertTrue(object.ReferenceEquals(near, sprite.Frame(0.999)),
                    "sweep did not pick the frame whose light sits at the offset");
                AssertTrue(far == null,
                    "sweep should answer null beyond the strip's own travel");
            });

            Test("GameplayPreview_EffectorsPaintWithoutThrowing", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AddPreviewNote(model, 0, 24, 0, 6);
                AddPreviewNote(model, 3, 96, 12, 48);
                model.CurrentTick = 30;
                using (var control = new GameplayPreviewControl())
                using (var bitmap = new Bitmap(960, 540))
                {
                    control.Size = bitmap.Size;
                    control.Bind(new EditorDocumentContext(model, "effectors.pt"));
                    control.SetProfile(GameplayPreviewProfile.Technika);
                    control.ScrollDirection = TechnikaScrollDirection.AllLeft;
                    control.NoteFader = TechnikaNoteFader.FadeOut;
                    control.LineEffector = TechnikaLineEffector.Blind;
                    control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    AssertTrue(bitmap.GetPixel(480, 270) != Color.Empty,
                        "effector paint produced no drawable frame");
                }
            });

            Test("GameplayPreview_ScrollSpeedDrivesTheSweepNotTheNotes", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                EventData behind = AddPreviewNote(model, 0, 96, 0, 6);
                EventData ahead = AddPreviewNote(model, 1, 192, 0, 6);
                EventData far = AddPreviewNote(model, 2, 384, 0, 6);
                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);

                GameplayPreviewFrame normal = chart.CreateRenderableFrame(96, 1.0);
                GameplayPreviewFrame fast = chart.CreateRenderableFrame(96, 2.0);

                // The sweep itself runs at twice the musical rate.
                AssertPreviewNear(fast.CurrentScan, normal.CurrentScan * 2.0, 1e-9,
                    "2x did not double the sweep position");
                AssertTrue(fast.CurrentIntScan == 1,
                    "2x sweep did not advance into the next scan");

                // Notes never move: the same note keeps its authored scan, half
                // and X at any speed.
                ProjectedGameplayNote normalAhead =
                    normal.Notes.Single(n => n.Source == ahead);
                ProjectedGameplayNote fastAhead =
                    fast.Notes.Single(n => n.Source == ahead);
                AssertTrue(fastAhead.ScanIndex == normalAhead.ScanIndex &&
                    fastAhead.RelativeScan == normalAhead.RelativeScan &&
                    fastAhead.X == normalAhead.X &&
                    fastAhead.IsTopHalf == normalAhead.IsTopHalf,
                    "a note moved when the sweep speed changed");

                // The state machine answers to the faster sweep: the note one scan
                // ahead is already Active, and the note the fast sweep crossed has
                // fallen off the stage behind it (at musical speed it is still on).
                AssertTrue(fastAhead.State == GameplayPreviewNoteState.Active,
                    "the faster sweep did not activate the waiting note sooner");
                AssertTrue(!fast.Notes.Any(n => n.Source == behind),
                    "the note the fast sweep passed should be off stage");
                AssertTrue(normal.Notes.Single(n => n.Source == behind).State ==
                    GameplayPreviewNoteState.Active,
                    "the musical sweep should still be on that note");

                // The stage follows the sweep: a note two scans ahead musically is
                // off stage at 1x but already waiting at 2x.
                AssertTrue(!normal.Notes.Any(n => n.Source == far),
                    "default-speed frame staged a note two scans ahead");
                AssertTrue(fast.Notes.Any(n => n.Source == far),
                    "2x sweep did not bring the further note on stage");
            });

            Test("GameplayPreview_HalfSpeedKeepsEarlierNotesOnStage", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                EventData early = AddPreviewNote(model, 0, 96, 0, 6);
                GameplayPreviewProjection chart =
                    GameplayPreviewProjector.Project(model, GameplayPreviewProfile.Technika);

                // At tick 192 the musical sweep has left scan 0; at half speed it
                // is still back there, so the scan-0 note stays on stage.
                AssertTrue(!chart.CreateRenderableFrame(192, 1.0).Notes.Any(n => n.Source == early),
                    "default-speed frame staged a note the sweep already left");
                AssertTrue(chart.CreateRenderableFrame(192, 0.5).Notes.Any(n => n.Source == early),
                    "half-speed sweep dropped the note it has not reached");
            });

            Test("GameplayPreview_BytesScrollSpeedComesFromTrack19", () =>
            {
                AssertTrue(GameplayPreviewForm.DetectScrollSpeed(null) == 1.0,
                    "no model must keep the default speed");

                PlayerData pt = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AssertTrue(GameplayPreviewForm.DetectScrollSpeed(pt) == 1.0,
                    "PT charts must not auto-detect speed");

                PlayerData bytes = PreviewModel(ChartFormat.TrailerRespectV, 4);
                AssertTrue(GameplayPreviewForm.DetectScrollSpeed(bytes) == 1.0,
                    "BYTES without a speed track must keep the default");

                var speedTrack = new TrackData(19);
                bytes.Tracks.AddTrack(speedTrack);
                speedTrack.AddEvent(new EventData
                {
                    EventType = EventType.Note,
                    Tick = 0,
                    Attribute = 1
                });
                AssertTrue(GameplayPreviewForm.DetectScrollSpeed(bytes) == 0.5,
                    "Track 19 attribute 1 must mean half speed");

                speedTrack.RemoveEvent(speedTrack.Events.First());
                speedTrack.AddEvent(new EventData
                {
                    EventType = EventType.Note,
                    Tick = 0,
                    Attribute = 2
                });
                AssertTrue(GameplayPreviewForm.DetectScrollSpeed(bytes) == 2.0,
                    "Track 19 attribute 2 must mean double speed");

                speedTrack.RemoveEvent(speedTrack.Events.First());
                speedTrack.AddEvent(new EventData
                {
                    EventType = EventType.Note,
                    Tick = 0,
                    Attribute = 0
                });
                AssertTrue(GameplayPreviewForm.DetectScrollSpeed(bytes) == 1.0,
                    "an empty Track 19 attribute must keep the default");
            });

            Test("GameplayPreviewDock_BytesLocksSpeedComboToTrack19", () =>
            {
                PlayerData bytes = PreviewModel(ChartFormat.TrailerRespectV, 4);
                AddPreviewNote(bytes, 0, 0, 0, 6);
                var speedTrack = new TrackData(19);
                bytes.Tracks.AddTrack(speedTrack);
                speedTrack.AddEvent(new EventData
                {
                    EventType = EventType.Note,
                    Tick = 0,
                    Attribute = 2
                });

                PlayerData pt = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AddPreviewNote(pt, 0, 0, 0, 6);

                using (var dock = new GameplayPreviewForm())
                {
                    var speedField = typeof(GameplayPreviewForm).GetField(
                        "_speed", BindingFlags.Instance | BindingFlags.NonPublic);

                    dock.Bind(new EditorDocumentContext(bytes, "speed.bytes"));
                    var combo = (DJMaxEditor.UI.StudioDropdown)speedField.GetValue(dock);
                    AssertTrue(!combo.Enabled, "BYTES must lock the speed combo");
                    AssertTrue(combo.SelectedIndex == 2, "BYTES attr 2 must select 2x");

                    dock.Bind(new EditorDocumentContext(pt, "speed.pt"));
                    AssertTrue(combo.Enabled, "PT must keep the speed combo manual");
                }
            });

            Test("GameplayPreview_ScrollSpeedRendersWithoutThrowing", () =>
            {
                PlayerData model = PreviewModel(ChartFormat.PtffDecrypted, 4);
                AddPreviewNote(model, 0, 24, 0, 6);
                AddPreviewNote(model, 3, 96, 12, 48);
                model.CurrentTick = 30;
                using (var control = new GameplayPreviewControl())
                using (var bitmap = new Bitmap(960, 540))
                {
                    control.Size = bitmap.Size;
                    control.Bind(new EditorDocumentContext(model, "speed.pt"));
                    control.SetProfile(GameplayPreviewProfile.Technika);
                    control.ScrollSpeed = 2.0;
                    control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                    AssertTrue(bitmap.GetPixel(480, 270) != Color.Empty,
                        "speed-scaled paint produced no drawable frame");
                }
            });

            Test("TechnikaNoteSprites_PicksUpLocalRootDroppedInAtRuntime", () =>
            {
                string temp = Path.Combine(
                    Path.GetTempPath(), "technika_sprites_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temp);
                string previous = Environment.GetEnvironmentVariable(
                    "DJMAX_EDITOR_TECHNIKA_ASSETS");
                try
                {
                    Environment.SetEnvironmentVariable(
                        "DJMAX_EDITOR_TECHNIKA_ASSETS", temp);
                    TechnikaNoteSprites sprites = TechnikaNoteSprites.Load();
                    AssertTrue(!sprites.UsesLocalAssets,
                        "an empty folder should not resolve as a sprite root");

                    // A sprite folder that appears after Load must be found by the refresh
                    // the preview runs on rebind - no editor restart.
                    DJMaxEditor.Resources.Note_Basic.Save(
                        Path.Combine(temp, "Note_Basic.png"));
                    AssertTrue(sprites.RefreshLocalRoot(),
                        "refresh did not pick up the sprite folder dropped in at runtime");
                    AssertTrue(sprites.UsesLocalAssets,
                        "refreshed sprites do not report local assets");
                    AssertTrue(sprites.For(GameplayPreviewNoteKind.Basic) != null,
                        "local note sheet did not resolve after refresh");
                }
                finally
                {
                    Environment.SetEnvironmentVariable(
                        "DJMAX_EDITOR_TECHNIKA_ASSETS", previous);
                    Directory.Delete(temp, true);
                }
            });
        }

        private static Bitmap FrameWithBlob(int size, int blobX)
        {
            var bitmap = new Bitmap(size, size);
            using (var graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Color.White))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillRectangle(brush, blobX, 2, 3, size - 4);
            }
            return bitmap;
        }

        private static PlayerData PreviewModel(ChartFormat format, int tracks)
        {
            var model = new PlayerData
            {
                SourceFormat = format,
                TickPerMinute = 192,
                Tempo = 120
            };
            for (uint i = 0; i < tracks; i++)
            {
                model.Tracks.AddTrack(new TrackData(i));
            }
            return model;
        }

        private static EventData AddPreviewNote(
            PlayerData model,
            uint track,
            int tick,
            byte attribute,
            ushort duration)
        {
            var note = new EventData
            {
                EventType = EventType.Note,
                Tick = tick,
                Attribute = attribute,
                Duration = duration
            };
            model.Tracks.GetTrackAtIndex(track).AddEvent(note);
            return note;
        }

        private static void AssertPreviewNear(
            double actual,
            double expected,
            double tolerance,
            string message)
        {
            AssertTrue(Math.Abs(actual - expected) <= tolerance,
                message + " (expected " + expected + ", got " + actual + ")");
        }
    }
}
