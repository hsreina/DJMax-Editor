using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using DJMaxEditor.Controls.TimelineV2.Renderers;
using DJMaxEditor.UI;

namespace DJMaxEditor.Tests
{
    internal static partial class Program
    {
        private static void RunStudioThemeTests()
        {
            Test("StudioTheme_PaletteKeepsTimingAndSelectionDistinct", () =>
            {
                AssertTrue(StudioTheme.TimingCyan == Color.FromArgb(0x36, 0xD5, 0xFF),
                    "timing cyan changed");
                AssertTrue(StudioTheme.SelectionViolet == Color.FromArgb(0xA7, 0x7B, 0xFF),
                    "selection violet changed");
                AssertTrue(StudioTheme.TimingCyan != StudioTheme.SelectionViolet,
                    "timing and selection signals must remain distinguishable");
            });

            Test("StudioTheme_DockSkinHasDarkActiveAndInactiveTabs", () =>
            {
                var skin = StudioTheme.CreateDockSkin();
                var documents = skin.DockPaneStripSkin.DocumentGradient;
                AssertTrue(documents.ActiveTabGradient.StartColor == StudioTheme.RaisedSlate,
                    "active document tab is not raised");
                AssertTrue(documents.ActiveTabGradient.TextColor == StudioTheme.PrimaryText,
                    "active document text is not primary");
                AssertTrue(documents.InactiveTabGradient.StartColor == StudioTheme.PanelGraphite,
                    "inactive document tab is not graphite");
                AssertTrue(skin.DockPaneStripSkin.ToolWindowGradient.ActiveCaptionGradient.TextColor ==
                    StudioTheme.TimingCyan, "active tool caption lost its timing accent");
            });

            Test("StudioTheme_DataGridRemovesWhitePanelDefaults", () =>
            {
                using (var grid = new DataGridView())
                {
                    StudioTheme.ApplyDataGrid(grid);
                    AssertTrue(grid.BackgroundColor == StudioTheme.ConsoleBlack,
                        "data-grid background is not dark");
                    AssertTrue(grid.DefaultCellStyle.BackColor == StudioTheme.PanelGraphite,
                        "data-grid cells are not graphite");
                    AssertTrue(grid.DefaultCellStyle.SelectionBackColor == StudioTheme.DeepSelection,
                        "data-grid selection is not the studio selection color");
                }
            });

            Test("StudioTheme_TransportIconsAreCompactAndTransparent", () =>
            {
                using (var play = StudioTheme.CreatePlayIcon(StudioTheme.TimingCyan))
                using (var pause = StudioTheme.CreatePauseIcon(StudioTheme.TimingCyan))
                using (var stop = StudioTheme.CreateStopIcon(StudioTheme.MutedText))
                {
                    AssertTrue(play.Size == new Size(18, 18), "play icon size changed");
                    AssertTrue(pause.Size == new Size(18, 18), "pause icon size changed");
                    AssertTrue(stop.Size == new Size(18, 18), "stop icon size changed");
                    AssertTrue(play.GetPixel(0, 0).A == 0, "play icon corner is not transparent");
                }
            });

            Test("StudioShell_RetainsEveryFamiliarEventTheme", () =>
            {
                using (var editor = new EditorControl())
                {
                    string[] names = editor.EventsThemeList.Select(theme => theme.GetName()).ToArray();
                    AssertTrue(names.Contains("Default"), "Default event theme is missing");
                    AssertTrue(names.Contains("Technika"), "Technika event theme is missing");
                    AssertTrue(names.Contains("Trilogy"), "Trilogy event theme is missing");
                    AssertTrue(names.Contains("Cyclon"), "Cyclon event theme is missing");
                }
            });

            Test("Timelines_UseTheSharedStudioSemanticPalette", () =>
            {
                AssertTrue(TimelineRenderTheme.Canvas == StudioDesignSystem.Void,
                    "Timeline V2 canvas is outside the shared design system");
                AssertTrue(TimelineRenderTheme.CanvasAlternate == StudioDesignSystem.Deck,
                    "Timeline V2 alternating rows are outside the shared design system");
                AssertTrue(TimelineRenderTheme.Header == StudioDesignSystem.Lift,
                    "Timeline V2 header is outside the shared design system");
                AssertTrue(TimelineRenderTheme.Playhead == StudioDesignSystem.PulseCyan,
                    "Timeline V2 playhead is not using the timing signal");
                AssertTrue(TimelineRenderTheme.Warning == StudioDesignSystem.SignalAmber,
                    "Timeline V2 warning is not using the warning signal");
                AssertTrue(ColorScheme.evenTrackColor == StudioDesignSystem.Void &&
                    ColorScheme.oddTrackColor == StudioDesignSystem.Deck,
                    "Timeline V1 rows are outside the shared design system");

                using (var editor = new EditorControl())
                {
                    var canvas = (Control)typeof(EditorControl)
                        .GetField("DrawingArea", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(editor);
                    AssertTrue(canvas.BackColor == StudioDesignSystem.Void,
                        "Timeline V1 canvas is outside the shared design system");
                }
            });
        }
    }
}
