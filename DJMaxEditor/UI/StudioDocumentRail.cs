using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DJMaxEditor.UI
{
    public sealed class StudioDocumentRail : UserControl
    {
        private const int ToolbarIconSize = 18;

        private readonly Label _documentLabel;
        private readonly Button _timelineV1;
        private readonly Button _timelineV2;
        private readonly Button _workspace;
        private readonly Button _palette;
        private readonly ContextMenuStrip _workspaceMenu;
        private readonly Button _openButton;
        private readonly Button _saveButton;
        private readonly Button _saveAsButton;
        private readonly Button _undoButton;
        private readonly Button _redoButton;
        private readonly Button _followButton;
        private readonly Button _selectFilterButton;
        private readonly Button _selectFilterMenuButton;
        private readonly ContextMenuStrip _selectFilterMenu;
        private readonly Image _selectFilterCheckIcon;
        private readonly List<KeyValuePair<ToolStripMenuItem, EventSelectMode.SpecialEventKind>> _selectFilterItems =
            new List<KeyValuePair<ToolStripMenuItem, EventSelectMode.SpecialEventKind>>();
        private readonly Button _zoomButton;
        private readonly ToolTip _toolTip;

        public StudioDocumentRail()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = StudioDesignSystem.Deck;
            Dock = DockStyle.Top;
            Height = 44;
            MinimumSize = new Size(760, 44);
            Padding = new Padding(12, 5, 10, 5);

            _toolTip = new ToolTip();
            _openButton = CreateIconButton(ScaleIcon(Resources.zw_open_16), "Open");
            _saveButton = CreateIconButton(ScaleIcon(Resources.zw_save_16), "Save");
            _saveAsButton = CreateIconButton(ScaleIcon(Resources.zw_saveas_16), "Save As");
            _undoButton = CreateIconButton(ScaleIcon(Resources.zw_undo_16), "Undo");
            _redoButton = CreateIconButton(ScaleIcon(Resources.zw_redo_16), "Redo");
            _followButton = CreateIconButton(
                StudioTheme.CreatePlayIcon(StudioDesignSystem.PulseCyan),
                "Follow Playback");
            _selectFilterButton = CreateIconButton(ScaleIcon(Resources.zw_filter_16), "Select Filter");
            _selectFilterMenuButton = CreateIconButton(ScaleIcon(Resources.zw_arrowdown_16), "Select Filter options");
            _selectFilterMenuButton.Margin = new Padding(0, 3, 2, 3);
            _selectFilterMenuButton.Width = 22;
            _selectFilterCheckIcon = CreateCheckIcon();
            _selectFilterMenu = BuildSelectFilterMenu();
            _zoomButton = StudioDesignSystem.CreateDeckButton("100%");
            _zoomButton.AutoSize = true;
            _zoomButton.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _zoomButton.MinimumSize = new Size(0, 27);
            _zoomButton.Margin = new Padding(3, 3, 3, 3);
            _zoomButton.Font = StudioDesignSystem.BodyFont(8.5f, FontStyle.Bold);
            _zoomButton.ForeColor = StudioDesignSystem.Muted;
            _zoomButton.Click += delegate { if (ZoomResetRequested != null) ZoomResetRequested(this, EventArgs.Empty); };
            _toolTip.SetToolTip(_zoomButton, "Reset zoom to 100%");

            _openButton.Click += delegate { if (OpenRequested != null) OpenRequested(this, EventArgs.Empty); };
            _saveButton.Click += delegate { if (SaveRequested != null) SaveRequested(this, EventArgs.Empty); };
            _saveAsButton.Click += delegate { if (SaveAsRequested != null) SaveAsRequested(this, EventArgs.Empty); };
            _undoButton.Click += delegate { if (UndoRequested != null) UndoRequested(this, EventArgs.Empty); };
            _redoButton.Click += delegate { if (RedoRequested != null) RedoRequested(this, EventArgs.Empty); };
            _followButton.Click += delegate { if (FollowPlaybackRequested != null) FollowPlaybackRequested(this, EventArgs.Empty); };
            _selectFilterButton.Click += delegate { if (SelectFilterToggleRequested != null) SelectFilterToggleRequested(this, EventArgs.Empty); };
            _selectFilterMenuButton.Click += delegate
            {
                _selectFilterMenu.Show(_selectFilterMenuButton, new Point(0, _selectFilterMenuButton.Height));
            };

            var toolbar = new FlowLayoutPanel
            {
                AutoSize = false,
                BackColor = StudioDesignSystem.Deck,
                Dock = DockStyle.Left,
                FlowDirection = FlowDirection.LeftToRight,
                Height = 34,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Width = 380,
                WrapContents = false
            };
            toolbar.Controls.Add(_openButton);
            toolbar.Controls.Add(_saveButton);
            toolbar.Controls.Add(_saveAsButton);
            toolbar.Controls.Add(_undoButton);
            toolbar.Controls.Add(_redoButton);
            toolbar.Controls.Add(_followButton);
            toolbar.Controls.Add(_selectFilterButton);
            toolbar.Controls.Add(_selectFilterMenuButton);

            _documentLabel = CreateLabel("NO DOCUMENT", 230, StudioDesignSystem.Frost);
            _documentLabel.AutoEllipsis = true;
            _documentLabel.Font = StudioDesignSystem.BodyFont(9f, FontStyle.Bold);

            _timelineV1 = CreateRailButton("TIMELINE V1");
            _timelineV2 = CreateRailButton("TIMELINE V2");
            _workspace = CreateRailButton("WORKSPACE  ▾");
            _palette = CreateRailButton("COMMANDS  Ctrl+K");
            _workspaceMenu = BuildWorkspaceMenu();

            _timelineV1.Click += delegate { if (TimelineV1Requested != null) TimelineV1Requested(this, EventArgs.Empty); };
            _timelineV2.Click += delegate { if (TimelineV2Requested != null) TimelineV2Requested(this, EventArgs.Empty); };
            _workspace.Click += delegate
            {
                _workspaceMenu.Show(_workspace, new Point(0, _workspace.Height));
            };
            _palette.Click += delegate { if (CommandPaletteRequested != null) CommandPaletteRequested(this, EventArgs.Empty); };

            var right = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = StudioDesignSystem.Deck,
                Dock = DockStyle.Right,
                FlowDirection = FlowDirection.LeftToRight,
                Height = 34,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                WrapContents = false
            };
            right.Controls.Add(_zoomButton);
            right.Controls.Add(_timelineV1);
            right.Controls.Add(_timelineV2);
            right.Controls.Add(_workspace);
            right.Controls.Add(_palette);

            Controls.Add(_documentLabel);
            Controls.Add(right);
            Controls.Add(toolbar);
            right.BringToFront();
            _documentLabel.BringToFront();

            ShowEmpty();
            SetActiveSurface(false);
            SetDocumentState(false);
            SetEditState(false, false);
            SetFollowPlayback(false);
            SetSelectFilter(false, EventSelectMode.SpecialEventKind.Volume);
            SetZoomPercent(100);
        }

        public event EventHandler TimelineV1Requested;
        public event EventHandler TimelineV2Requested;
        public event EventHandler PreviewRequested;
        public event EventHandler<StudioWorkspaceRequestedEventArgs> WorkspaceRequested;
        public event EventHandler CommandPaletteRequested;
        public event EventHandler OpenRequested;
        public event EventHandler SaveRequested;
        public event EventHandler SaveAsRequested;
        public event EventHandler UndoRequested;
        public event EventHandler RedoRequested;
        public event EventHandler FollowPlaybackRequested;
        public event EventHandler SelectFilterToggleRequested;
        public event EventHandler SelectFilterKindRequested;
        public event EventHandler ZoomResetRequested;

        internal EventSelectMode.SpecialEventKind SelectedFilterKind { get; private set; }

        public StudioWorkspacePreset[] WorkspacePresets
        {
            get
            {
                return new[]
                {
                    StudioWorkspacePreset.Editing,
                    StudioWorkspacePreset.Compact
                };
            }
        }

        public string DocumentName { get; private set; }
        public string SurfaceName { get; private set; }

        public void ShowEmpty()
        {
            ShowDocument("NO DOCUMENT", "TIMELINE V1");
        }

        public void ShowDocument(string documentName, string surfaceName)
        {
            DocumentName = string.IsNullOrWhiteSpace(documentName) ? "UNTITLED" : documentName;
            SurfaceName = string.IsNullOrWhiteSpace(surfaceName) ? "TIMELINE V1" : surfaceName;

            _documentLabel.Text = DocumentName;
            SetActiveSurface(SurfaceName.IndexOf("V2", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public void SetActiveSurface(bool timelineV2)
        {
            SurfaceName = timelineV2 ? "TIMELINE V2" : "TIMELINE V1";
            StyleSegment(_timelineV1, !timelineV2);
            StyleSegment(_timelineV2, timelineV2);
        }

        public void SetDocumentState(bool hasDocument)
        {
            _saveButton.Enabled = hasDocument;
            _saveAsButton.Enabled = hasDocument;
        }

        public void SetEditState(bool canUndo, bool canRedo)
        {
            _undoButton.Enabled = canUndo;
            _redoButton.Enabled = canRedo;
        }

        public void SetFollowPlayback(bool following)
        {
            StyleSegment(_followButton, following);
        }

        internal void SetSelectFilter(bool enabled, EventSelectMode.SpecialEventKind kind)
        {
            SelectedFilterKind = kind;
            StyleSegment(_selectFilterButton, enabled);
            foreach (var pair in _selectFilterItems)
            {
                pair.Key.Image = pair.Value == kind ? _selectFilterCheckIcon : null;
            }
            _toolTip.SetToolTip(
                _selectFilterButton,
                enabled ? "Select Filter: " + kind.ToString().ToUpperInvariant() : "Select Filter");
        }

        private ContextMenuStrip BuildSelectFilterMenu()
        {
            var menu = new ContextMenuStrip
            {
                BackColor = StudioDesignSystem.Deck,
                Font = StudioDesignSystem.BodyFont(9f),
                ForeColor = StudioDesignSystem.Frost,
                ShowImageMargin = true
            };
            menu.Renderer = new StudioToolStripRenderer(false);
            AddSelectFilterItem(menu, "VOLUME", EventSelectMode.SpecialEventKind.Volume);
            AddSelectFilterItem(menu, "TEMPO", EventSelectMode.SpecialEventKind.Tempo);
            AddSelectFilterItem(menu, "BEAT", EventSelectMode.SpecialEventKind.Beat);
            AddSelectFilterItem(menu, "VIDEOSTART", EventSelectMode.SpecialEventKind.VideoStart);
            return menu;
        }

        private void AddSelectFilterItem(
            ContextMenuStrip menu,
            string label,
            EventSelectMode.SpecialEventKind kind)
        {
            var item = new ToolStripMenuItem(label)
            {
                BackColor = StudioDesignSystem.Deck,
                ForeColor = StudioDesignSystem.Frost
            };
            item.Click += delegate
            {
                SelectedFilterKind = kind;
                if (SelectFilterKindRequested != null) SelectFilterKindRequested(this, EventArgs.Empty);
            };
            _selectFilterItems.Add(
                new KeyValuePair<ToolStripMenuItem, EventSelectMode.SpecialEventKind>(item, kind));
            menu.Items.Add(item);
        }

        private static Image CreateCheckIcon()
        {
            var bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var pen = new Pen(StudioDesignSystem.PulseCyan, 2f))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.DrawLines(pen, new[]
                {
                    new Point(3, 8),
                    new Point(7, 12),
                    new Point(13, 4)
                });
            }
            return bitmap;
        }

        public void SetZoomPercent(int percent)
        {
            string text = percent + "%";
            if (_zoomButton.Text != text)
            {
                _zoomButton.Text = text;
            }
        }

        public void RequestWorkspace(StudioWorkspacePreset preset)
        {
            EventHandler<StudioWorkspaceRequestedEventArgs> handler = WorkspaceRequested;
            if (handler != null)
            {
                handler(this, new StudioWorkspaceRequestedEventArgs(preset));
            }
        }

        private ContextMenuStrip BuildWorkspaceMenu()
        {
            var menu = new ContextMenuStrip
            {
                BackColor = StudioDesignSystem.Deck,
                Font = StudioDesignSystem.BodyFont(9f),
                ForeColor = StudioDesignSystem.Frost,
                ShowImageMargin = false
            };
            AddWorkspaceItem(menu, "EDITING", StudioWorkspacePreset.Editing);
            // Former top-rail PREVIEW: open Gameplay Preview (not a layout preset).
            AddWorkspaceAction(menu, "PREVIEW", delegate
            {
                if (PreviewRequested != null)
                {
                    PreviewRequested(this, EventArgs.Empty);
                }
            });
            AddWorkspaceItem(menu, "COMPACT", StudioWorkspacePreset.Compact);
            return menu;
        }

        private void AddWorkspaceItem(
            ContextMenuStrip menu,
            string label,
            StudioWorkspacePreset preset)
        {
            AddWorkspaceAction(menu, label, delegate { RequestWorkspace(preset); });
        }

        private static void AddWorkspaceAction(
            ContextMenuStrip menu,
            string label,
            EventHandler onClick)
        {
            var item = new ToolStripMenuItem(label)
            {
                BackColor = StudioDesignSystem.Deck,
                ForeColor = StudioDesignSystem.Frost
            };
            item.Click += onClick;
            menu.Items.Add(item);
        }

        private static Label CreateLabel(string text, int width, Color foreground)
        {
            return new Label
            {
                BackColor = StudioDesignSystem.Deck,
                Dock = DockStyle.Left,
                ForeColor = foreground,
                Height = 34,
                Margin = Padding.Empty,
                Padding = new Padding(0, 8, 8, 0),
                Text = text,
                Width = width
            };
        }

        private static Button CreateRailButton(string text)
        {
            Button button = StudioDesignSystem.CreateDeckButton(text);
            button.AutoSize = true;
            button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            button.MinimumSize = new Size(0, 28);
            button.Margin = new Padding(2, 3, 2, 3);
            return button;
        }

        private Button CreateIconButton(Image image, string toolTipText)
        {
            Button button = StudioDesignSystem.CreateDeckButton(string.Empty);
            button.Height = 28;
            button.Margin = new Padding(2, 3, 2, 3);
            button.Width = 34;
            button.Image = image;
            button.ImageAlign = ContentAlignment.MiddleCenter;
            _toolTip.SetToolTip(button, toolTipText);
            return button;
        }

        private static Bitmap ScaleIcon(Bitmap master)
        {
            var scaled = new Bitmap(ToolbarIconSize, ToolbarIconSize);
            using (Graphics graphics = Graphics.FromImage(scaled))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(master, 0, 0, ToolbarIconSize, ToolbarIconSize);
            }
            return scaled;
        }

        private static void StyleSegment(Button button, bool active)
        {
            button.BackColor = active ? StudioDesignSystem.Selected : StudioDesignSystem.Lift;
            button.ForeColor = active ? StudioDesignSystem.PulseCyan : StudioDesignSystem.Muted;
            button.FlatAppearance.BorderColor = active
                ? StudioDesignSystem.PulseCyan
                : StudioDesignSystem.Border;
        }
    }
}
