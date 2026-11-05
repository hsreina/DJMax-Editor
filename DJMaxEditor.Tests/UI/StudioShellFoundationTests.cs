using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DJMaxEditor.Editor.Commands;
using DJMaxEditor.UI;

namespace DJMaxEditor.Tests
{
    internal static partial class Program
    {
        private static void RunStudioShellFoundationTests()
        {
            Test("StudioDesignSystem_UsesApprovedProductionPalette", () =>
            {
                AssertTrue(StudioDesignSystem.Void == Color.FromArgb(0x0B, 0x0F, 0x17),
                    "Void token does not match the approved palette");
                AssertTrue(StudioDesignSystem.Deck == Color.FromArgb(0x12, 0x1A, 0x27),
                    "Deck token does not match the approved palette");
                AssertTrue(StudioDesignSystem.Lift == Color.FromArgb(0x1C, 0x29, 0x3A),
                    "Lift token does not match the approved palette");
                AssertTrue(StudioDesignSystem.PulseCyan == Color.FromArgb(0x36, 0xD5, 0xFF),
                    "Pulse cyan does not match the approved palette");
                AssertTrue(StudioDesignSystem.BeatViolet == Color.FromArgb(0xA7, 0x7B, 0xFF),
                    "Beat violet does not match the approved palette");
                AssertTrue(StudioDesignSystem.SignalAmber == Color.FromArgb(0xFF, 0xCB, 0x5C),
                    "Signal amber does not match the approved palette");
                AssertTrue(StudioDesignSystem.FaultRed == Color.FromArgb(0xFF, 0x5F, 0x73),
                    "Fault red does not match the approved palette");
                AssertTrue(StudioDesignSystem.Frost == Color.FromArgb(0xEA, 0xF2, 0xFF),
                    "Frost does not match the approved palette");
            });

            Test("StudioDesignSystem_ScalesLogicalPixelsDeterministically", () =>
            {
                AssertTrue(StudioDesignSystem.Scale(12, 96) == 12, "100% DPI scale changed");
                AssertTrue(StudioDesignSystem.Scale(12, 120) == 15, "125% DPI scale is incorrect");
                AssertTrue(StudioDesignSystem.Scale(12, 144) == 18, "150% DPI scale is incorrect");
                AssertTrue(StudioDesignSystem.Scale(12, 192) == 24, "200% DPI scale is incorrect");
            });

            Test("StudioCommandRegistry_ReportsOverlappingShortcutConflicts", () =>
            {
                var registry = new StudioCommandRegistry();
                registry.Register(new StudioCommand(
                    "timeline.draw", "Draw tool", "Tools", Keys.B,
                    StudioCommandContext.Timeline, () => true, () => string.Empty, () => { }));
                registry.Register(new StudioCommand(
                    "document.build", "Build chart", "Document", Keys.B,
                    StudioCommandContext.Timeline, () => true, () => string.Empty, () => { }));
                registry.Register(new StudioCommand(
                    "global.bookmark", "Bookmark", "Navigation", Keys.B,
                    StudioCommandContext.Global, () => true, () => string.Empty, () => { }));

                var conflicts = registry.FindShortcutConflicts().ToArray();

                AssertTrue(conflicts.Length == 1,
                    "commands sharing a shortcut should produce one grouped conflict");
                AssertTrue(conflicts[0].CommandIds.Contains("timeline.draw") &&
                    conflicts[0].CommandIds.Contains("document.build") &&
                    conflicts[0].CommandIds.Contains("global.bookmark"),
                    "conflict did not identify every simultaneously active command ID");
            });

            Test("StudioCommand_ExplainsDisabledStateAndDoesNotExecute", () =>
            {
                bool executed = false;
                var command = new StudioCommand(
                    "selection.delete", "Delete selection", "Selection", Keys.Delete,
                    StudioCommandContext.Selection, () => false,
                    () => "Select one or more chart events first.", () => executed = true);

                string reason;
                bool result = command.TryExecute(out reason);

                AssertTrue(!result && !executed, "disabled command unexpectedly executed");
                AssertTrue(reason == "Select one or more chart events first.",
                    "disabled command did not expose its explanation");
            });

            Test("StudioCommandRegistry_SearchesStableIdsNamesAndCategories", () =>
            {
                var registry = new StudioCommandRegistry();
                registry.Register(new StudioCommand(
                    "timeline.switch-v2", "Switch to Timeline V2", "Timeline", Keys.None,
                    StudioCommandContext.Global, () => true, () => string.Empty, () => { }));
                registry.Register(new StudioCommand(
                    "transport.play-pause", "Play or pause", "Playback", Keys.Space,
                    StudioCommandContext.Global, () => true, () => string.Empty, () => { }));

                AssertTrue(registry.Search("v2").Single().Id == "timeline.switch-v2",
                    "palette search did not match a command name");
                AssertTrue(registry.Search("transport").Single().Id == "transport.play-pause",
                    "palette search did not match a stable command ID");
                AssertTrue(registry.Search("playback").Single().Id == "transport.play-pause",
                    "palette search did not match a command category");
            });

            Test("StudioCommandRegistry_ReassignsAndResetsSessionShortcuts", () =>
            {
                var registry = new StudioCommandRegistry();
                registry.Register(new StudioCommand(
                    "tool.draw", "Draw tool", "Tools", Keys.B,
                    StudioCommandContext.Timeline, () => true, () => string.Empty, () => { }));

                AssertTrue(registry.GetCurrentShortcut("tool.draw") == Keys.B,
                    "newly registered command did not use its default shortcut");
                AssertTrue(registry.ReassignShortcut("tool.draw", Keys.Control | Keys.D),
                    "session shortcut reassignment was rejected");
                AssertTrue(registry.GetCurrentShortcut("tool.draw") == (Keys.Control | Keys.D),
                    "registry did not expose the reassigned session shortcut");
                AssertTrue(registry.Get("tool.draw").DefaultShortcut == Keys.B,
                    "session reassignment mutated the command's default shortcut");

                AssertTrue(registry.ResetShortcut("tool.draw"),
                    "shortcut reset was rejected");
                AssertTrue(registry.GetCurrentShortcut("tool.draw") == Keys.B,
                    "shortcut reset did not restore the registered default");
            });

            Test("StudioCommandRegistry_UsesSessionShortcutsForDispatchAndConflicts", () =>
            {
                int drawCount = 0;
                int eraseCount = 0;
                var registry = new StudioCommandRegistry();
                registry.Register(new StudioCommand(
                    "tool.draw", "Draw tool", "Tools", Keys.B,
                    StudioCommandContext.Timeline, () => true, () => string.Empty, () => drawCount++));
                registry.Register(new StudioCommand(
                    "tool.erase", "Erase tool", "Tools", Keys.E,
                    StudioCommandContext.Timeline, () => true, () => string.Empty, () => eraseCount++));

                registry.ReassignShortcut("tool.erase", Keys.B);
                AssertTrue(registry.FindShortcutConflicts().Single().CommandIds.Length == 2,
                    "reassigned shortcut conflict was not reported");

                string reason;
                AssertTrue(!registry.TryExecuteShortcut(
                        Keys.B, StudioCommandContext.Timeline, out reason),
                    "conflicted session shortcut unexpectedly executed");
                AssertTrue(reason.IndexOf("tool.draw", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    reason.IndexOf("tool.erase", StringComparison.OrdinalIgnoreCase) >= 0,
                    "shortcut dispatch did not explain the current conflict");
                AssertTrue(drawCount == 0 && eraseCount == 0,
                    "a conflicted shortcut executed one of its commands");

                registry.ReassignShortcut("tool.erase", Keys.Control | Keys.E);
                AssertTrue(registry.TryExecuteShortcut(
                        Keys.Control | Keys.E, StudioCommandContext.Timeline, out reason),
                    "reassigned session shortcut did not dispatch");
                AssertTrue(eraseCount == 1 && drawCount == 0,
                    "reassigned shortcut dispatched the wrong command");
            });

            Test("CommandPalette_SearchesAndShowsAvailabilityShortcutAndConflict", () =>
            {
                var registry = new StudioCommandRegistry();
                registry.Register(new StudioCommand(
                    "document.open", "Open chart", "Document", Keys.Control | Keys.O,
                    StudioCommandContext.Global, () => true, () => string.Empty, () => { }));
                registry.Register(new StudioCommand(
                    "document.save-as", "Save chart as", "Document", Keys.Control | Keys.Shift | Keys.S,
                    StudioCommandContext.Document, () => false,
                    () => "Open a chart before saving.", () => { }));
                registry.ReassignShortcut("document.save-as", Keys.Control | Keys.O);

                using (var palette = new StudioCommandPaletteForm(
                    registry, () => StudioCommandContext.Global))
                {
                    palette.SearchText = "save";
                    var entry = palette.VisibleEntries.Single();

                    AssertTrue(entry.Id == "document.save-as" &&
                        entry.Category == "Document",
                        "palette search did not return the matching registered command");
                    AssertTrue(entry.Shortcut == (Keys.Control | Keys.O),
                        "palette did not show the current session shortcut");
                    AssertTrue(!entry.IsAvailable &&
                        entry.DisabledExplanation == "Open a chart before saving.",
                        "palette did not show the disabled command explanation");
                    AssertTrue(entry.HasShortcutConflict &&
                        entry.ConflictExplanation.IndexOf("document.open", StringComparison.OrdinalIgnoreCase) >= 0,
                        "palette did not show the current shortcut conflict");
                }
            });

            Test("CommandPalette_ExecutesAndEditsSelectedCommandShortcut", () =>
            {
                int executed = 0;
                var registry = new StudioCommandRegistry();
                registry.Register(new StudioCommand(
                    "transport.play-pause", "Play or pause", "Playback", Keys.Space,
                    StudioCommandContext.Global, () => true, () => string.Empty, () => executed++));

                using (var palette = new StudioCommandPaletteForm(
                    registry, () => StudioCommandContext.Global))
                {
                    palette.SearchText = "transport";
                    palette.SelectedCommandId = "transport.play-pause";

                    string feedback;
                    AssertTrue(palette.ReassignSelectedShortcut(
                            Keys.Control | Keys.Space, out feedback),
                        "palette rejected a session-only shortcut reassignment");
                    AssertTrue(registry.GetCurrentShortcut("transport.play-pause") ==
                        (Keys.Control | Keys.Space),
                        "palette did not apply the selected shortcut reassignment");
                    AssertTrue(palette.ExecuteSelected(out feedback),
                        "palette did not execute the selected available command");
                    AssertTrue(executed == 1,
                        "palette executed the selected command an unexpected number of times");

                    palette.ResetSelectedShortcut();
                    AssertTrue(registry.GetCurrentShortcut("transport.play-pause") == Keys.Space,
                        "palette reset did not restore the selected command default");
                }
            });

            Test("CommandPalette_ExplainsCommandsOutsideTheActiveContext", () =>
            {
                var registry = new StudioCommandRegistry();
                registry.Register(new StudioCommand(
                    "selection.delete", "Delete selection", "Selection", Keys.Delete,
                    StudioCommandContext.Selection, () => true, () => string.Empty, () => { }));

                using (var palette = new StudioCommandPaletteForm(
                    registry, () => StudioCommandContext.Global))
                {
                    var entry = palette.VisibleEntries.Single();
                    AssertTrue(!entry.IsAvailable,
                        "palette marked a selection-only command ready without an active selection context");
                    AssertTrue(entry.DisabledExplanation.IndexOf(
                            "current editor context", StringComparison.OrdinalIgnoreCase) >= 0,
                        "palette did not explain why the command context is inactive");
                }
            });

            Test("StudioDocumentRail_SeparatesDocumentSurfaceAndCapabilityState", () =>
            {
                using (var rail = new StudioDocumentRail())
                {
                    rail.ShowDocument(
                        "sonof_pop_3.pt",
                        "TIMELINE V2",
                        "PTFF ENCRYPTED",
                        "EDITABLE",
                        false);

                    AssertTrue(rail.DocumentName == "sonof_pop_3.pt",
                        "document rail lost the chart name");
                    AssertTrue(rail.SurfaceName == "TIMELINE V2",
                        "document rail lost the active surface");
                    AssertTrue(rail.CapabilityText == "EDITABLE",
                        "document rail lost the capability state");
                    AssertTrue(!rail.IsLocked, "editable document rail appears locked");

                    rail.ShowDocument(
                        "locked.pt",
                        "TIMELINE V1",
                        "RESPECT V",
                        "READ ONLY",
                        true);
                    AssertTrue(rail.IsLocked, "read-only document rail did not expose its lock");
                }
            });

            Test("Inspector_PreservesAdvancedLegacyPropertyEditing", () =>
            {
                using (var inspector = new PropertiesForm())
                {
                    var selected = new object();
                    inspector.PropertyObject = selected;
                    PropertyGrid advanced = inspector.Controls
                        .OfType<PropertyGrid>()
                        .Single();

                    AssertTrue(advanced.Height > 0 && advanced.Dock != DockStyle.None,
                        "redesigned Inspector did not reserve an advanced legacy property editor");
                    AssertTrue(object.ReferenceEquals(inspector.PropertyObject, selected),
                        "redesigned Inspector did not retain the legacy property selection");
                }
            });

            Test("StudioDocumentRail_OffersAllTransientWorkspacePresets", () =>
            {
                using (var rail = new StudioDocumentRail())
                {
                    AssertTrue(rail.WorkspacePresets.SequenceEqual(new[]
                    {
                        StudioWorkspacePreset.Editing,
                        StudioWorkspacePreset.Preview,
                        StudioWorkspacePreset.Compact
                    }), "workspace rail is missing a production preset");

                    StudioWorkspacePreset? requested = null;
                    rail.WorkspaceRequested += (sender, args) =>
                        requested = args.Preset;
                    rail.RequestWorkspace(StudioWorkspacePreset.Preview);
                    AssertTrue(requested == StudioWorkspacePreset.Preview,
                        "workspace request was not routed to the shell");
                }
            });
        }
    }
}
