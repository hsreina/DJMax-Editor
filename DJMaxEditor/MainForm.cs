// #define ENABLE_EVENT_FORM

using System;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using WeifenLuo.WinFormsUI.Docking;
using System.Threading;
using System.Diagnostics;
using System.Globalization;
using DJMaxEditor.Undo.Action;
using DJMaxEditor.PropertyLayer;
using DJMaxEditor.Controls.Editor.Renderers.Events;
using DJMaxEditor.DJMax;
using System.Collections.Generic;
using DJMaxEditor.Panels;
using DJMaxEditor.Files;
using DJMaxEditor.Controls.Editor.Renderers.Zones;
using DJMaxEditor.Files.pt;
using DJMaxEditor.Files.bytes;
using DJMaxEditor.Files.bms;
using DJMaxEditor.Files.Cyclon;
using DJMaxEditor.Editor;
using DJMaxEditor.Editor.Commands;
using DJMaxEditor.Preview;
using DJMaxEditor.UI;
using DJMaxEditor.Video;

namespace DJMaxEditor
{
    public partial class MainForm : Form
    {
        #region public defs

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!IsTextEntryFocused())
            {
                string disabledExplanation;
                if (m_commands.TryExecuteShortcut(
                    keyData, GetActiveCommandContext(), out disabledExplanation))
                {
                    return true;
                }
                if (!string.IsNullOrEmpty(disabledExplanation))
                {
                    SetStudioStatus(disabledExplanation);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private StudioCommandContext GetActiveCommandContext()
        {
            StudioCommandContext context = StudioCommandContext.Global |
                StudioCommandContext.Timeline |
                StudioCommandContext.Playback;
            if (_documentContext != null)
            {
                context |= StudioCommandContext.Document;
                if (_documentContext.Selection.Count > 0)
                {
                    context |= StudioCommandContext.Selection;
                }
            }
            return context;
        }

        private bool IsTextEntryFocused()
        {
            Control focused = this;
            while (focused != null)
            {
                Control child = focused.Controls
                    .Cast<Control>()
                    .FirstOrDefault(control => control.ContainsFocus);
                if (child == null) break;
                focused = child;
            }

            return focused is TextBoxBase ||
                focused is ComboBox ||
                focused is StudioDropdown ||
                focused is NumericUpDown ||
                focused is DataGridView;
        }

        private void UpdateAttributesBinding(EventData eventData)
        {
            if (eventData == null)
            {
                m_propertiesForm.PropertyObject = null;
                return;
            }

            switch (eventData.EventType)
            {
                case EventType.Note:
                    {
                        m_propertiesForm.PropertyObject = new NoteEventPropertiesLayer(eventData, _documentContext?.Edits);
                    }
                    break;
                case EventType.Volume:
                    {
                        m_propertiesForm.PropertyObject = new VolumeEventPropertiesLayer(eventData, _documentContext?.Edits);
                    }
                    break;
                case EventType.Tempo:
                    {
                        m_propertiesForm.PropertyObject = new TempoEventPropertiesLayer(eventData, _documentContext?.Edits);
                    }
                    break;
                case EventType.Beat:
                    {
                        m_propertiesForm.PropertyObject = new BeatEventPropertiesLayer(eventData, _documentContext?.Edits);
                    }
                    break;
                default:
                    m_propertiesForm.PropertyObject = null;
                    break;
            }

        }

        public void Editor_OnSelectItem(object sender, EventData[] selectedItems)
        {
            if (null == selectedItems || selectedItems.Length < 1)
            {
                m_selectedEvent = null;
                m_propertiesForm.PropertyObject = null;
                m_audioList.List.ClearSelection();
                return;
            }

            if (selectedItems.Length > 1)
            {
                m_selectedEvent = null;

                if (_documentContext != null &&
                    selectedItems.All(item => item.EventType == EventType.Note))
                {
                    m_propertiesForm.PropertyObject = new MultiNotePropertiesLayer(
                        selectedItems,
                        _documentContext.Edits);

                    InstrumentData sharedInstrument = selectedItems[0].Instrument;
                    if (selectedItems.All(item => object.ReferenceEquals(item.Instrument, sharedInstrument)))
                    {
                        SelectInstrumentRow(sharedInstrument);
                    }
                    else
                    {
                        m_audioList.List.ClearSelection();
                    }
                    RevealInspectorTab();
                }
                else if (_documentContext != null &&
                    selectedItems.All(item => item.EventType == EventType.Volume))
                {
                    m_propertiesForm.PropertyObject = new MultiVolumePropertiesLayer(
                        selectedItems,
                        _documentContext.Edits);
                    m_audioList.List.ClearSelection();
                }
                else if (_documentContext != null &&
                    selectedItems.All(item => item.EventType == EventType.Tempo))
                {
                    m_propertiesForm.PropertyObject = new MultiTempoPropertiesLayer(
                        selectedItems,
                        _documentContext.Edits);
                    m_audioList.List.ClearSelection();
                }
                else if (_documentContext != null &&
                    selectedItems.All(item => item.EventType == EventType.Beat))
                {
                    m_propertiesForm.PropertyObject = new MultiBeatPropertiesLayer(
                        selectedItems,
                        _documentContext.Edits);
                    m_audioList.List.ClearSelection();
                }
                else
                {
                    m_propertiesForm.PropertyObject = null;
                    m_audioList.List.ClearSelection();
                }
                return;
            }

            var firstEvent = m_selectedEvent = selectedItems[0];
            UpdateAttributesBinding(firstEvent);

            // Check the selected note instrument
            SelectInstrumentRow(firstEvent.Instrument);

            if (firstEvent.EventType == EventType.Note)
            {
                RevealInspectorTab();
            }
        }

        /// <summary>
        /// Bring the Inspector dock tab to the front without stealing keyboard focus
        /// from the chart editor (so shortcuts keep working after a note click).
        /// </summary>
        private void RevealInspectorTab()
        {
            if (m_propertiesForm == null || m_propertiesForm.IsDisposed)
            {
                return;
            }

            if (m_propertiesForm.DockPanel == null)
            {
                m_propertiesForm.Show(dockPanel);
                return;
            }

            if (m_propertiesForm.IsHidden)
            {
                m_propertiesForm.Show(dockPanel);
            }

            if (m_propertiesForm.Pane != null)
            {
                m_propertiesForm.Pane.ActiveContent = m_propertiesForm;
            }
        }

        private void SelectInstrumentRow(InstrumentData instrumentData)
        {
            // Highlighting the Sounds row for a note selection must not be treated as the
            // user picking a new instrument (SelectionChanged would rewrite the note).
            _suppressAudioListInstrumentApply = true;
            try
            {
                if (instrumentData != null)
                {
                    var row = m_audioList.List.Rows.Cast<DataGridViewRow>().SingleOrDefault(r => r.DataBoundItem == instrumentData);
                    // If we got something, select and move to this track
                    if (row != null)
                    {
                        row.Selected = true;
                        m_audioList.List.FirstDisplayedScrollingRowIndex = row.Index;
                    }
                }
                else
                {
                    m_audioList.List.ClearSelection();
                }
            }
            finally
            {
                _suppressAudioListInstrumentApply = false;
            }
        }

        public void NoteSelect_OnSelectData(EventData eventData)
        {
            m_currentTemplateEvent = eventData;

            if (!CanMutateThroughActiveSurface())
            {
                return;
            }

            m_editorForm.Editor.TemplateEvent = eventData;

            // if the event is a note, set it's instrument to the current selected one on the musics list
            if (eventData != null && eventData.EventType == EventType.Note)
            {

                DataGridViewSelectedRowCollection rows = m_audioList.List.SelectedRows;

                if (rows.Count > 0)
                {
                    DataGridViewRow selectedRow = rows[0];

                    if (selectedRow != null)
                    {
                        eventData.Instrument = selectedRow.DataBoundItem as InstrumentData;
                    }
                }

            }

        }

        public void Editor_OnRequestEvent(byte index)
        {
            m_notes.SelectEvent(index);
        }

        public void AudioList_onInstrumentChanged(object sender, InstrumentData instrumentData, string filename)
        {
            if (!CanMutateThroughActiveSurface())
            {
                Logs.Write("Instrument replacement is unavailable on the active read-only editor surface.");
                return;
            }

            bool res = m_audioPlayer.LoadSound(instrumentData.InsNum, filename, 0);
            if (!res)
            {
                Logs.Write("Failed to load sound {0} - {1}", instrumentData.InsNum, instrumentData.Name);
            }
            else
            {
                m_editorForm
                    .Editor.UndoManager
                    .ExecAction(
                        new RenameInstrumentEventAction(instrumentData, Path.GetFileName(filename))
                    );
                
                //instrumentData.Name = Path.GetFileName(filename);
            }
        }

        public MainForm() : this(null)
        {
        }

        public MainForm(string[] startupFiles)
        {
            _startupFiles = startupFiles;

            InitializeComponent();

            m_timelineV2MenuItem = new ToolStripMenuItem(
                "Use Timeline V2 (editing rollout)")
            {
                Checked = FeatureFlags.UseTimelineV2,
                CheckOnClick = false
            };
            m_timelineV2MenuItem.Click += TimelineV2MenuItem_Click;
            optionsToolStripMenuItem.DropDownItems.Insert(0, m_timelineV2MenuItem);

            _saveHandler = new SaveHandler();
            _saveHandler.Register(new PTSaveFile());
            _saveHandler.Register(new TQSaveFile());
            _saveHandler.Register(new BMESaveFile());
            _saveHandler.Register(new BmsonSaveFile());
            _saveHandler.Register(new DJMaxEditor.Files.Tech.TechSaveFile());

            _loadHandler = new LoadHandler();
            _loadHandler.Register(new PTOpenFile());
            _loadHandler.Register(new TQOpenFile());
            _loadHandler.Register(new CyclonXmlOpenFile());
            _loadHandler.Register(new BmsOpenFile());
            _loadHandler.Register(new DJMaxEditor.Files.Tech.TechOpenFile());

            var editor = m_editorForm.Editor;

            var audioList = this.m_audioList = new AudioListForm();
            audioList.OnPlayPressed += this.AudioList_onPlayPressed;
            audioList.OnStopPressed += this.AudioList_onStopPressed;
            audioList.OnInstrumentChanged += this.AudioList_onInstrumentChanged;

            Logs.OnLogWrite += this.Logs_OnWrite;

            m_deserializeDockContent = new DeserializeDockContent(GetContentFromPersistString);
            
            m_notes = new NoteSelectForm(editor.EventsRenderer);

            WireEditorForm(m_homeForm);

            m_notes.OnSelectDataEvent += NoteSelect_OnSelectData;

            m_audioList.List.SelectionChanged += AudioList_selectionChanged;

            var eventsThemeList = editor.EventsThemeList;
            var currentTheme = editor.CurrentEventsTheme;
            foreach (var theme in eventsThemeList)
            {
                ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(theme.GetName());
                ThemeDropDownButton.DropDownItems.Add(toolStripMenuItem);
                if (null != toolStripMenuItem && currentTheme == theme)
                {
                    toolStripMenuItem.Checked = true;
                    ApplyEventsTheme(theme);
                }
            }

            var zonesThemeList = editor.ZonesThemeList;
            var currentZoneTheme = editor.CurrentZonesTheme;
            foreach (var theme in zonesThemeList)
            {
                ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(theme.GetName());
                zoneRendererToolStripDropDownButton.DropDownItems.Add(toolStripMenuItem);

                if (currentZoneTheme != theme)
                {
                    continue;
                }

                toolStripMenuItem.Checked = true;
                ApplyZonesTheme(theme);
            }

            var eventDisplayModes = GetAll<EventDisplayMode>();

            var currentEventDisplayMode = editor.EventDisplayMode;
            foreach (var eventDisplayMode in eventDisplayModes)
            {
                var toolStripMenuItem = new ToolStripMenuItem(eventDisplayMode.Value);
                eventDisplayModeToolStripDropDownButton.DropDownItems.Add(toolStripMenuItem);

                if (eventDisplayMode.Key != (int) currentEventDisplayMode)
                {
                    continue;
                }
                toolStripMenuItem.Checked = true;
                eventDisplayModeToolStripDropDownButton.Text = eventDisplayMode.Value;
            }

            ApplyStudioShell();
            RegisterStudioCommands();
            dockPanel.ActiveDocumentChanged += delegate { ActivateDockedDocument(); };
            AllowDrop = true;
        }

        private void ApplyStudioShell()
        {
            StudioTheme.ApplyMainShell(this, menuStrip1, toolStrip1, dockPanel);

            m_documentRail = new StudioDocumentRail();
            m_statusRail = new StudioStatusRail();
            m_statusRail.SetPerformance("idle");
            m_documentRail.TimelineV1Requested += delegate { SetTimelineSurface(false); };
            m_documentRail.TimelineV2Requested += delegate { SetTimelineSurface(true); };
            m_documentRail.PreviewRequested += delegate { ShowGameplayPreview(); };
            m_documentRail.WorkspaceRequested += delegate(object sender, StudioWorkspaceRequestedEventArgs e)
            {
                ApplyWorkspacePreset(e.Preset);
            };
            m_documentRail.CommandPaletteRequested += delegate { ShowCommandPalette(); };
            m_documentRail.OpenRequested += delegate { openToolStripMenuItem_Click(this, EventArgs.Empty); };
            m_documentRail.SaveRequested += delegate { SaveCurrentDocument(); };
            m_documentRail.SaveAsRequested += delegate { saveAsToolStripMenuItem_Click(this, EventArgs.Empty); };
            m_documentRail.UndoRequested += delegate { undoToolStripMenuItem_Click(this, EventArgs.Empty); };
            m_documentRail.RedoRequested += delegate { redoToolStripMenuItem_Click(this, EventArgs.Empty); };
            m_documentRail.FollowPlaybackRequested += delegate
            {
                SetFollowPlayback(!m_editorForm.Editor.FollowTracksProgressWhilePlaying);
            };
            m_documentRail.SelectFilterToggleRequested += delegate
            {
                SetSelectFilter(!EventSelectMode.BoxSelectionFilterEnabled);
            };
            m_documentRail.SelectFilterKindRequested += delegate
            {
                SetSelectFilterKind(m_documentRail.SelectedFilterKind);
            };
            m_documentRail.ZoomResetRequested += delegate { m_editorForm.ResetActiveZoom(); };
            resetZoomToolStripMenuItem.ShortcutKeys = Keys.Control | Keys.D0;

            m_studioTopHost = new TableLayoutPanel
            {
                BackColor = StudioDesignSystem.Void,
                ColumnCount = 1,
                Dock = DockStyle.Top,
                Height = 120,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                RowCount = 3
            };
            m_studioTopHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            m_studioTopHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
            m_studioTopHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f));
            m_studioTopHost.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));

            menuStrip1.Dock = DockStyle.Fill;
            menuStrip1.Margin = Padding.Empty;
            toolStrip1.Dock = DockStyle.Fill;
            toolStrip1.Margin = Padding.Empty;
            m_documentRail.Dock = DockStyle.Fill;
            m_studioTopHost.Controls.Add(menuStrip1, 0, 0);
            m_studioTopHost.Controls.Add(m_documentRail, 0, 1);
            m_studioTopHost.Controls.Add(toolStrip1, 0, 2);
            Controls.Add(m_statusRail);
            Controls.Add(m_studioTopHost);

            toolStripButton1.AutoSize = false;
            toolStripButton1.Size = new System.Drawing.Size(38, 32);
            toolStripButton1.Margin = new Padding(0, 0, 3, 0);
            toolStripButton1.Image = Resources.wps_play;
            toolStripButton1.Text = "Play";

            toolStripButton2.AutoSize = false;
            toolStripButton2.Size = new System.Drawing.Size(38, 32);
            toolStripButton2.Margin = new Padding(0, 0, 8, 0);
            toolStripButton2.Image = Resources.wps_stop;
            toolStripButton2.Text = "Stop";

            currentProgress.AutoSize = false;
            currentProgress.Width = 164;
            currentProgress.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            currentProgress.Font = StudioTheme.MonoFont(9f);
            currentProgress.ForeColor = StudioTheme.PrimaryText;
            currentProgress.Text = "00:00:00  |  TICK 0";

            ConfigureWorkflowControl(toolStripDropDownButton1, 122);
            ConfigureWorkflowControl(ThemeDropDownButton, 178);
            ConfigureWorkflowControl(zoneRendererToolStripDropDownButton, 176);
            ConfigureWorkflowControl(eventDisplayModeToolStripDropDownButton, 132);

            toolStripDropDownButton1.Text = "Quantize  1/8";
            AddToolModeDeck();
            m_debugOutput.TabText = "Output";
            m_fmod.TabText = "Audio Engine";

            StudioTheme.ApplyToForm(m_editorForm);
            StudioTheme.ApplyToForm(m_debugOutput);
            StudioTheme.ApplyToForm(m_audioList);
            StudioTheme.ApplyToForm(m_propertiesForm);
            StudioTheme.ApplyToForm(m_fmod);
            StudioTheme.ApplyToForm(m_notes);
            StudioTheme.ApplyToForm(m_preview);
            StudioTheme.ApplyToForm(m_bgaPreview);
            windowsToolStripMenuItem.DropDownItems.Add(new ToolStripMenuItem(
                "BGA Preview", Resources.videoStart, delegate { ShowBgaPreview(); }));
            // Keep FMOD last in Window, after any items appended at runtime.
            windowsToolStripMenuItem.DropDownItems.Remove(fMODInformationsToolStripMenuItem1);
            windowsToolStripMenuItem.DropDownItems.Add(fMODInformationsToolStripMenuItem1);
            SetFollowPlayback(m_editorForm.Editor.FollowTracksProgressWhilePlaying);
            SetSplitView(_splitViewEnabled);
            SyncSelectFilterUi();
            UpdateStudioRails();
        }

        private void AddToolModeDeck()
        {
            toolStrip1.Items.Add(new ToolStripSeparator());
            var toolsLabel = new ToolStripLabel("TOOLS")
            {
                Font = StudioTheme.MonoFont(7.5f),
                ForeColor = StudioTheme.MutedText,
                Margin = new Padding(8, 0, 5, 0)
            };
            toolStrip1.Items.Add(toolsLabel);

            AddToolButton(TimelineTool.Select, "V  SELECT");
            AddToolButton(TimelineTool.Draw, "B  DRAW");
            AddToolButton(TimelineTool.Erase, "E  ERASE");
            AddToolButton(TimelineTool.Pan, "H  PAN");

            toolStrip1.Items.Add(new ToolStripSeparator());
            var resetButton = new ToolStripButton("R  RESET")
            {
                AutoSize = false,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = StudioTheme.StrongFont(7.75f),
                ForeColor = StudioTheme.MutedText,
                Height = 28,
                Margin = new Padding(2, 1, 2, 1),
                Width = 68,
                ToolTipText = "Reset zoom to 100%"
            };
            resetButton.Click += delegate { m_editorForm.ResetActiveZoom(); };
            toolStrip1.Items.Add(resetButton);

            m_splitViewButton = new ToolStripButton("SPLIT CHART VIEW")
            {
                AutoSize = false,
                CheckOnClick = true,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = StudioTheme.StrongFont(7.75f),
                ForeColor = StudioTheme.MutedText,
                Height = 28,
                Margin = new Padding(2, 1, 2, 1),
                Width = 122,
                ToolTipText = "Split the chart into upper and lower panes"
            };
            m_splitViewButton.Click += delegate { SetSplitView(m_splitViewButton.Checked); };
            toolStrip1.Items.Add(m_splitViewButton);

            m_docSplitButton = new ToolStripButton("DUAL CHART VIEW")
            {
                AutoSize = false,
                CheckOnClick = true,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = StudioTheme.StrongFont(7.75f),
                ForeColor = StudioTheme.MutedText,
                Height = 28,
                Margin = new Padding(2, 1, 2, 1),
                Width = 124,
                ToolTipText = "Show open charts in top and bottom panes (needs 2 or more charts)"
            };
            m_docSplitButton.Click += delegate { SetDocumentSplit(m_docSplitButton.Checked); };
            toolStrip1.Items.Add(m_docSplitButton);

            SetActiveTool(TimelineTool.Select);
        }

        private void AddToolButton(TimelineTool tool, string text)
        {
            var button = new ToolStripButton(text)
            {
                AutoSize = false,
                CheckOnClick = false,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = StudioTheme.StrongFont(7.75f),
                ForeColor = StudioTheme.MutedText,
                Height = 28,
                Margin = new Padding(2, 1, 2, 1),
                Width = text.Length > 8 ? 76 : 68
            };
            button.Click += delegate { SetActiveTool(tool); };
            m_toolButtons.Add(tool, button);
            toolStrip1.Items.Add(button);
        }

        private void RegisterStudioCommands()
        {
            m_commands.Register(new StudioCommand(
                "application.command-palette", "Open command palette", "Application",
                Keys.Control | Keys.K, StudioCommandContext.Global,
                () => true, () => string.Empty, ShowCommandPalette));
            m_commands.Register(new StudioCommand(
                "document.open", "Open chart", "Document", Keys.Control | Keys.O,
                StudioCommandContext.Global, () => true, () => string.Empty,
                () => openToolStripMenuItem_Click(this, EventArgs.Empty)));
            m_commands.Register(new StudioCommand(
                "document.save", "Save chart", "Document", Keys.Control | Keys.S,
                StudioCommandContext.Document, () => _documentContext != null,
                () => "Open a chart before saving.", () => SaveCurrentDocument()));
            m_commands.Register(new StudioCommand(
                "document.save-as", "Save chart as", "Document", Keys.Control | Keys.Shift | Keys.S,
                StudioCommandContext.Document, () => _documentContext != null,
                () => "Open a chart before saving.", () => saveAsToolStripMenuItem_Click(this, EventArgs.Empty)));
            m_commands.Register(new StudioCommand(
                "transport.play-pause", "Play or pause", "Playback", Keys.Space,
                StudioCommandContext.Global, () => true, () => string.Empty,
                () => toolStripButton1_Click(this, EventArgs.Empty)));
            m_commands.Register(new StudioCommand(
                "panel.gameplay-preview", "Show gameplay preview", "Panels", Keys.None,
                StudioCommandContext.Global, () => true, () => string.Empty,
                ShowGameplayPreview));
            m_commands.Register(new StudioCommand(
                "panel.bga-preview", "Show BGA preview", "Panels", Keys.None,
                StudioCommandContext.Global, () => true, () => string.Empty,
                ShowBgaPreview));
            m_commands.Register(new StudioCommand(
                "edit.undo", "Undo", "Edit", Keys.Control | Keys.Z,
                StudioCommandContext.Document, () => _documentContext != null && m_undoManager.CanUndo,
                () => "There is nothing to undo.", () => undoToolStripMenuItem_Click(this, EventArgs.Empty)));
            m_commands.Register(new StudioCommand(
                "edit.redo", "Redo", "Edit", Keys.Control | Keys.Y,
                StudioCommandContext.Document, () => _documentContext != null && m_undoManager.CanRedo,
                () => "There is nothing to redo.", () => redoToolStripMenuItem_Click(this, EventArgs.Empty)));
            RegisterToolCommand("tool.select", "Select tool", Keys.V, TimelineTool.Select);
            RegisterToolCommand("tool.draw", "Draw tool", Keys.B, TimelineTool.Draw);
            RegisterToolCommand("tool.erase", "Erase tool", Keys.E, TimelineTool.Erase);
            RegisterToolCommand("tool.pan", "Pan tool", Keys.H, TimelineTool.Pan);
            m_commands.Register(new StudioCommand(
                "view.reset-zoom", "Reset zoom", "View", Keys.R,
                StudioCommandContext.Timeline, () => _documentContext != null,
                () => "Open a chart before resetting the zoom.",
                () => m_editorForm.ResetActiveZoom()));
            m_commands.Register(new StudioCommand(
                "view.split-panes", "Toggle split editor panes", "View", Keys.S,
                StudioCommandContext.Global, () => true, () => string.Empty,
                () => SetSplitView(!_splitViewEnabled)));
            m_commands.Register(new StudioCommand(
                "view.split-documents", "Split open charts top and bottom", "View", Keys.None,
                StudioCommandContext.Document, () => _documents.Count >= 2,
                () => "Open at least two charts to split the document area.",
                () => SetDocumentSplit(!_documentSplitEnabled)));
            RegisterWorkspaceCommand(
                "workspace.editing", "Use Editing workspace", StudioWorkspacePreset.Editing);
            RegisterWorkspaceCommand(
                "workspace.preview", "Use Preview workspace", StudioWorkspacePreset.Preview);
            RegisterWorkspaceCommand(
                "workspace.compact", "Use Compact workspace", StudioWorkspacePreset.Compact);
        }

        private void RegisterToolCommand(string id, string name, Keys shortcut, TimelineTool tool)
        {
            m_commands.Register(new StudioCommand(
                id, name, "Tools", shortcut, StudioCommandContext.Timeline,
                CanUseEditingTools,
                () => _documentContext == null
                    ? "Open a chart before choosing an editing tool."
                    : "Editing is unavailable for this document or timeline.",
                () => SetActiveTool(tool)));
        }

        private void RegisterWorkspaceCommand(
            string id,
            string name,
            StudioWorkspacePreset preset)
        {
            m_commands.Register(new StudioCommand(
                id, name, "Workspaces", Keys.None, StudioCommandContext.Global,
                () => true, () => string.Empty,
                () => ApplyWorkspacePreset(preset)));
        }

        private void ShowCommandPalette()
        {
            if (m_commandPalette == null || m_commandPalette.IsDisposed)
            {
                m_commandPalette = new StudioCommandPaletteForm(
                    m_commands, GetActiveCommandContext);
                m_commandPalette.FormClosed += delegate { m_commandPalette = null; };
                m_commandPalette.Show(this);
            }
            else
            {
                m_commandPalette.Activate();
            }
        }

        private void ShowGameplayPreview()
        {
            if (m_preview == null || m_preview.IsDisposed)
            {
                m_preview = new GameplayPreviewForm();
                StudioTheme.ApplyToForm(m_preview);
                m_preview.Bind(_documentContext);
            }

            m_preview.Show(dockPanel);
            m_preview.Activate();
            m_preview.RefreshPlaybackImmediately();
            SetStudioStatus("GAMEPLAY PREVIEW  •  VIEW ONLY");
        }

        private void ShowBgaPreview()
        {
            EnsureBgaPreviewForm();
            BindBgaPreview();
            m_bgaPreview.Show(dockPanel);
            m_bgaPreview.Activate();
            m_bgaPreview.SyncNow();
            SetStudioStatus("BGA PREVIEW  •  FOLLOWS PLAYHEAD");
        }

        private void EnsureBgaPreviewForm()
        {
            if (m_bgaPreview != null && !m_bgaPreview.IsDisposed)
            {
                return;
            }
            m_bgaPreview = new BgaPreviewForm();
            StudioTheme.ApplyToForm(m_bgaPreview);
        }

        private void BindBgaPreview()
        {
            EnsureBgaPreviewForm();
            m_bgaPreview.Bind(
                _documentContext,
                () => m_editorForm != null && m_editorForm.ActiveSurface != null
                    ? m_editorForm.ActiveSurface.PlayheadVirtualTick
                    : (_documentContext != null && _documentContext.Model != null
                        ? _documentContext.Model.VirtualCurrentTick
                        : 0));
        }

        private void ApplyWorkspacePreset(StudioWorkspacePreset preset)
        {
            dockPanel.SuspendLayout(true);
            try
            {
                HideStudioToolWindows();
                m_editorForm.Show(dockPanel, DockState.Document);

                switch (preset)
                {
                    case StudioWorkspacePreset.Editing:
                        dockPanel.DockLeftPortion = 0.14;
                        dockPanel.DockRightPortion = 0.18;
                        dockPanel.DockBottomPortion = 0.18;
                        m_notes.Show(dockPanel, DockState.DockLeft);
                        if (m_notes.Pane != null)
                        {
                            m_audioList.Show(m_notes.Pane, (IDockContent)null);
                        }
                        else
                        {
                            m_audioList.Show(dockPanel, DockState.DockLeft);
                        }
                        m_propertiesForm.Show(dockPanel, DockState.DockRight);
                        m_debugOutput.Show(dockPanel, DockState.DockBottom);
                        m_notes.Activate();
                        break;

                    case StudioWorkspacePreset.Preview:
                        dockPanel.DockRightPortion = 0.40;
                        ShowGameplayPreview();
                        break;

                    case StudioWorkspacePreset.Compact:
                        break;
                }
            }
            finally
            {
                dockPanel.ResumeLayout(true, true);
            }

            SetStudioStatus(
                "WORKSPACE  " + preset.ToString().ToUpperInvariant() +
                "  •  SESSION PRESET");
        }

        private void HideStudioToolWindows()
        {
            HideDockContent(m_notes);
            HideDockContent(m_audioList);
            HideDockContent(m_propertiesForm);
            HideDockContent(m_debugOutput);
            HideDockContent(m_fmod);
            HideDockContent(m_preview);
            HideDockContent(m_bgaPreview);
        }

        private static void HideDockContent(DockContent content)
        {
            if (content != null && !content.IsDisposed)
            {
                content.Hide();
            }
        }

        private void EnsureAudioEngineForm()
        {
            if (m_fmod != null && !m_fmod.IsDisposed)
            {
                return;
            }
            m_fmod = new FModForm();
            StudioTheme.ApplyToForm(m_fmod);
        }

        private bool CanUseEditingTools()
        {
            return _documentContext != null &&
                _documentContext.Capabilities.CanEdit &&
                m_editorForm.ActiveSurface.SupportsEditing;
        }

        private void SetActiveTool(TimelineTool tool)
        {
            if (_documentContext != null)
            {
                _documentContext.Interaction.Tool = tool;
            }
            SyncToolButtons(tool);
            SetStudioStatus("TOOL  " + tool.ToString().ToUpperInvariant());
        }

        private void SyncToolButtons(TimelineTool tool)
        {
            foreach (var pair in m_toolButtons)
            {
                pair.Value.Checked = pair.Key == tool;
                pair.Value.ForeColor = pair.Key == tool
                    ? StudioDesignSystem.PulseCyan
                    : StudioDesignSystem.Muted;
            }
        }

        private void SetTimelineSurface(bool useTimelineV2)
        {
            FeatureFlags.SetUseTimelineV2(useTimelineV2);
            m_timelineV2MenuItem.Checked = useTimelineV2;
            m_editorForm.SwitchSurface(useTimelineV2);
            UpdateStudioRails();
        }

        private void UpdateStudioRails()
        {
            if (m_documentRail == null || m_statusRail == null)
            {
                return;
            }

            if (_documentContext == null)
            {
                m_documentRail.ShowEmpty();
                m_documentRail.SetDocumentState(false);
                m_documentRail.SetEditState(false, false);
                m_statusRail.SetStatus("READY", "SNAP 1/8", "NO DOCUMENT");
                UpdateZoomDisplay();
                return;
            }

            DocumentCapabilities capabilities = _documentContext.Capabilities;
            string surface = m_editorForm.IsLegacySurfaceActive
                ? "TIMELINE V1"
                : "TIMELINE V2";
            m_documentRail.ShowDocument(
                Path.GetFileName(_documentContext.SourcePath),
                surface);
            m_documentRail.SetDocumentState(true);
            m_documentRail.SetEditState(
                m_undoManager.CanUndo,
                m_undoManager.CanRedo);

            int selected = _documentContext.Selection.Count;
            m_statusRail.SetStatus(
                selected == 0 ? "READY  •  SELECT TOOL" : selected + " EVENT" + (selected == 1 ? string.Empty : "S") + " SELECTED",
                toolStripDropDownButton1.Text.ToUpperInvariant(),
                surface + "  •  " + capabilities.StatusLabel);
            UpdateZoomDisplay();
        }

        private void SetStudioStatus(string message)
        {
            if (m_statusRail == null)
            {
                return;
            }
            string right = _documentContext == null
                ? "NO DOCUMENT"
                : _documentContext.Capabilities.StatusLabel;
            m_statusRail.SetStatus(message, toolStripDropDownButton1.Text.ToUpperInvariant(), right);
        }

        private static void ConfigureWorkflowControl(ToolStripDropDownButton button, int width)
        {
            button.AutoSize = false;
            button.Width = width;
            button.Height = 32;
            button.Margin = new Padding(2, 0, 2, 0);
            button.Padding = new Padding(8, 0, 4, 0);
            button.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            button.Font = StudioTheme.StrongFont(8.5f);
            button.ForeColor = StudioTheme.PrimaryText;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            StudioTheme.TryApplyDarkTitleBar(this);
        }

        #endregion // public defs

        #region private defs

        private DeserializeDockContent m_deserializeDockContent;
        private PropertiesForm m_propertiesForm = new PropertiesForm();
        private EditorForm m_homeForm = new EditorForm();
        private DebugOutput m_debugOutput = new DebugOutput();
        private AudioListForm m_audioList;
        //private PerformancesForm m_performances = new PerformancesForm();
        private FModForm m_fmod = new FModForm();
        private NoteSelectForm m_notes;
        private EventData m_currentTemplateEvent;
        private SaveHandler _saveHandler;
        private LoadHandler _loadHandler;
        private ToolStripMenuItem m_timelineV2MenuItem;
        private ToolStripButton m_splitViewButton;
        private ToolStripButton m_docSplitButton;
        private bool _splitViewEnabled = Properties.Settings.Default.SplitEditorPanes;
        private bool _documentSplitEnabled;
        private readonly StudioCommandRegistry m_commands = new StudioCommandRegistry();
        private readonly Dictionary<TimelineTool, ToolStripButton> m_toolButtons =
            new Dictionary<TimelineTool, ToolStripButton>();
        private StudioDocumentRail m_documentRail;
        private StudioStatusRail m_statusRail;
        private StudioCommandPaletteForm m_commandPalette;
        private FindSoundNotesForm m_findNotesForm;
        private GameplayPreviewForm m_preview = new GameplayPreviewForm();
        private BgaPreviewForm m_bgaPreview = new BgaPreviewForm();
        private TableLayoutPanel m_studioTopHost;

        private sealed class ChartDocument
        {
            public EditorDocumentContext Context { get; set; }
            public EditorForm Form { get; set; }
            public bool IsDirty { get; set; }
        }

        private readonly List<ChartDocument> _documents = new List<ChartDocument>();
        private readonly HashSet<EditorForm> _wiredEditorForms = new HashSet<EditorForm>();
        private ChartDocument _activeDocument;
        private bool _closingAll;
        private readonly string[] _startupFiles;
        private IDisposable _singleInstanceServer;

        private EditorForm m_editorForm
        {
            get { return _activeDocument != null ? _activeDocument.Form : m_homeForm; }
        }

        private EditorDocumentContext _documentContext
        {
            get { return _activeDocument != null ? _activeDocument.Context : null; }
        }

        private UndoManager m_undoManager
        {
            get {
                return _activeDocument != null
                    ? _activeDocument.Context.UndoManager
                    : UndoManager.GetInstance();
            }
        }

        private readonly PlayerData m_emptyPlayerData = new PlayerData();

        private PlayerData m_playerData
        {
            get { return _activeDocument != null ? _activeDocument.Context.Model : m_emptyPlayerData; }
        }

        private IEventRenderer m_currentEventsTheme;
        private IZoneRenderer m_currentZonesTheme;
        private int m_quantizeNoteValue;
        private int m_lastZoomPercent = -1;

#if ENABLE_EVENT_FORM
        private EventsListForm _eventsForm = new  EventsListForm();
#endif

        private bool m_isPaused = true;

        private LoadingForm m_loadingForm = new LoadingForm();

        private const string APP_NAME = "DJMax Editor";

        private const int ReservedChannel = 99;

        // the PT player
        private Player m_player = new Player();

        // the audio player
        private IAudioPlayer m_audioPlayer = new AudioPlayerFmodEx();

        private EventData m_selectedEvent = null;

        // When true, Sounds-list SelectionChanged only stops preview audio and must not
        // rewrite TemplateEvent / selected-note instruments (DataSource rebind, UI sync).
        private bool _suppressAudioListInstrumentApply;

        private bool m_isFullScreen = false;

        private FormWindowState _oldState = FormWindowState.Maximized;

        private bool CanMutateThroughActiveSurface()
        {
            string reason;
            return DocumentMutationGuard.CanMutate(
                m_playerData,
                m_editorForm.ActiveSurface.SupportsEditing,
                out reason);
        }

        private void TimelineV2MenuItem_Click(object sender, EventArgs e)
        {
            bool enabled = !m_timelineV2MenuItem.Checked;
            SetTimelineSurface(enabled);
        }

        public static IDictionary<int, string> GetAll<TEnum>() where TEnum : struct
        {
            var enumerationType = typeof(TEnum);

            if (!enumerationType.IsEnum)
                throw new ArgumentException("Enumeration type is expected.");

            var dictionary = new Dictionary<int, string>();

            foreach (int value in Enum.GetValues(enumerationType))
            {
                var name = Enum.GetName(enumerationType, value);
                dictionary.Add(value, name);
            }

            return dictionary;
        }

        private void AudioList_selectionChanged(object sender, EventArgs e)
        {
            this.m_audioPlayer.StopSound(ReservedChannel);

            // DataSource rebinds (tab switch) and programmatic row highlights also fire
            // SelectionChanged; those must not rewrite note instruments.
            if (_suppressAudioListInstrumentApply)
            {
                return;
            }

            if (!CanMutateThroughActiveSurface())
            {
                return;
            }

            DataGridView audioList = sender as DataGridView;

            if (audioList == null) { return; }

            DataGridViewSelectedRowCollection rows = audioList.SelectedRows;

            if (rows == null || rows.Count == 0) { return; }

            DataGridViewRow selectedRow = rows[0];

            if (selectedRow == null) { return; }

            EventData eventData = m_editorForm.Editor.TemplateEvent;

            if (eventData == null) { return; }

            InstrumentData instrumentData = selectedRow.DataBoundItem as InstrumentData;

            // then try to update the current instrument
            eventData.Instrument = instrumentData;

            // update the instrument on every selected event (single or multi selection)
            if (_documentContext != null && _documentContext.Selection.Count > 0)
            {
                _documentContext.Edits.SetSelectionInstrument(instrumentData);
            }
        }

        private void Logs_OnWrite(string data)
        {

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    m_debugOutput.Log(data);
                }));
            }
            else
            {
                m_debugOutput.Log(data);
            }

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //this.TopMost = true;
            //this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;

#if !ENABLE_EVENT_FORM
            eventsToolStripMenuItem.Visible = false;
#endif

            ApplyWorkspacePreset(StudioWorkspacePreset.Editing);

            m_player.OnEvent += Player_OnEvent;
            m_player.OnStatusChange += Player_OnStatusChange;            
        }

        private void Player_OnStatusChange(object sender) 
        {
            m_editorForm.Editor.IsPlayerPlaying = m_player.IsPlaying;

            m_editorForm.ActiveSurface.InvalidateView();
            CheckAndUpdatePlayPauseIcons();
        }

        private void Player_OnEvent(EventData eventData, uint trackIndex, EventType eventType, byte pan)
        {
            if (m_playerData == null)
            {
                return;
            }

            ushort soundIndex = (ushort)((eventData.Instrument != null) ? eventData.Instrument.InsNum : 0);

            TrackData track = m_playerData.Tracks.GetTrackAtIndex(trackIndex);
            if (track == null)
            {
                return;
            }

            switch (eventType)
            {
                case EventType.Note:

                    if (soundIndex > 0)
                    {
                        m_audioPlayer.SetVolume(trackIndex, eventData.Vel);

                        var evTick = eventData.Tick;
                        var currentTick = m_player.GetCurrentTick();
                        if (evTick < currentTick - 50)
                        {
                            // TODO: seek to the current position somehow
                            //var tps = m_playerData.TickPerMinute;
                            //int offset = (currentTick - evTick) * 1000 / tps;
                            //if (!m_audioPlayer.PlaySound(trackIndex, soundIndex, track.Volume, pan, (uint)offset))
                            //{
                            //    Logs.Write("Failed to play sound on track : {0}, soundIndex : {1}", trackIndex, soundIndex);
                            //};
                        } else
                        {
                            if (!m_audioPlayer.PlaySound(trackIndex, soundIndex, track.Volume, pan))
                            {
                                Logs.Write("Failed to play sound on track : {0}, soundIndex : {1}", trackIndex, soundIndex);
                            };
                        }

                    }

                    break;
                case EventType.Volume:

                    ushort volume = eventData.Volume;

                    track.Volume = ((float)volume / (float)sbyte.MaxValue);
                    m_audioPlayer.SetVolume(trackIndex, track.Volume);

                    break;
            }

        }

        private void OpenFileComplete(PlayerData playerData, string filename, bool success)
        {
            m_loadingForm.Close();
            if (false == success) 
            {
                MessageBox.Show(this, "Failed to load the file", "Load file error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool readOnly = playerData != null && playerData.IsReadOnly;
            string displayName = Path.GetFileName(filename);

            if (readOnly)
            {
                DJMaxEditor.Diagnostics.DiagnosticLog.Write("open.readonly",
                    filename + " opened read-only (source=" + playerData.SourceFormat + ")");
            }

            ApplyTechnikaThemesForChart(playerData.SourceFormat);

            // A chart that is already open gets its tab activated instead of a duplicate.
            ChartDocument existing = _documents.Find(doc =>
                string.Equals(doc.Context.SourcePath, filename, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Form.Show(dockPanel, DockState.Document);
                existing.Form.Activate();
                SetActiveDocument(existing);
                return;
            }

            var context = new EditorDocumentContext(playerData, filename, new UndoManager());
            var document = new ChartDocument { Context = context };

            EditorForm form;
            if (_documents.Count == 0 && m_homeForm != null && !m_homeForm.IsDisposed)
            {
                form = m_homeForm;
                m_homeForm = null;
            }
            else
            {
                form = CreateDocumentForm();
            }
            document.Form = form;
            _documents.Add(document);

            context.Selection.SelectionChanged += DocumentSelectionChanged;
            context.Interaction.ToolChanged += DocumentToolChanged;

            form.Bind(context);
            form.Title = readOnly ? displayName + "  (read-only)" : displayName;
            if (!form.Visible)
            {
                form.Show(dockPanel, DockState.Document);
            }
            form.Activate();

            SetActiveDocument(document);
        }

        private void WireEditorForm(EditorForm form)
        {
            if (form == null || !_wiredEditorForms.Add(form))
            {
                return;
            }
            form.OpenRequested += delegate { openToolStripMenuItem_Click(this, EventArgs.Empty); };
            form.ActiveSurfaceChanged += delegate { UpdateStudioRails(); };
            form.FormClosing += DocumentForm_FormClosing;
            form.FormClosed += DocumentForm_FormClosed;
            var editor = form.Editor;
            editor.OnSelectItem += Editor_OnSelectItem;
            editor.OnRequestEvent += Editor_OnRequestEvent;
            editor.OnUndoRedo += UndoManager_OnUndoRedo;
            editor.PlayheadSeekRequested += Editor_PlayheadSeekRequested;
            editor.SplitViewEnabled = _splitViewEnabled;
            StudioTheme.ApplyToForm(form);
        }

        private void Editor_PlayheadSeekRequested(object sender, int virtualTick)
        {
            SeekPlayheadAndMaybePlay(virtualTick, forcePlay: false);
        }

        private EditorForm CreateDocumentForm()
        {
            var form = new EditorForm(FeatureFlags.UseTimelineV2);
            WireEditorForm(form);
            if (m_currentEventsTheme != null)
            {
                form.Editor.CurrentEventsTheme = m_currentEventsTheme;
            }
            if (m_currentZonesTheme != null)
            {
                form.Editor.CurrentZonesTheme = m_currentZonesTheme;
            }
            if (m_quantizeNoteValue > 0)
            {
                form.Editor.NoteValue = m_quantizeNoteValue;
            }
            form.Editor.FollowTracksProgressWhilePlaying =
                follwTrackPrgressToolStripMenuItem.Checked;
            return form;
        }

        private void ActivateDockedDocument()
        {
            ChartDocument document = _documents.Find(doc =>
                object.ReferenceEquals(doc.Form, dockPanel.ActiveDocument));
            if (document != null)
            {
                SetActiveDocument(document);
            }
        }

        private void SetActiveDocument(ChartDocument document)
        {
            if (object.ReferenceEquals(_activeDocument, document))
            {
                return;
            }

            m_audioPlayer.StopAllSounds();
            _activeDocument = document;
            m_lastPlaybackVirtualTick = -1;

            if (document == null)
            {
                m_player.LoadPlayerData(m_emptyPlayerData);
                m_audioPlayer.SetSoundContext(string.Empty);
                this.Text = APP_NAME;
                _suppressAudioListInstrumentApply = true;
                try
                {
                    m_audioList.List.DataSource = null;
                }
                finally
                {
                    _suppressAudioListInstrumentApply = false;
                }
                m_propertiesForm.PropertyObject = null;
                UpdateStudioRails();
                return;
            }

            PlayerData model = document.Context.Model;
            m_player.LoadPlayerData(model);
            string soundKey = document.Context.SourcePath ?? string.Empty;
            m_audioPlayer.SetSoundContext(soundKey);
            if (!m_audioPlayer.IsSoundContextLoaded(soundKey))
            {
                LoadDocumentSounds(model, document.Context.SourcePath);
                m_audioPlayer.MarkSoundContextLoaded(soundKey);
            }
            // Rebinding the shared Sounds grid fires SelectionChanged and would otherwise
            // apply the auto-selected row (usually InsNum 0 / "none") onto any notes that
            // were still selected in the document being activated.
            _suppressAudioListInstrumentApply = true;
            try
            {
                m_audioList.List.DataSource = model.Instruments;
            }
            finally
            {
                _suppressAudioListInstrumentApply = false;
            }
            m_propertiesForm.Bind(document.Context);
            m_preview.Bind(document.Context);
            // The playfield is where a TECHNIKA chart is read - like the reference project,
            // opening one brings the preview up on its own instead of waiting for a menu.
            // BGA discovery/attach runs inside GameplayPreview.Bind so the video paints
            // under the notes instead of opening a separate dock panel.
            if (m_preview.Profile == GameplayPreviewProfile.Technika &&
                (!m_preview.Visible || m_preview.IsDisposed))
            {
                ShowGameplayPreview();
            }
            if (m_bgaPreview != null && !m_bgaPreview.IsDisposed && m_bgaPreview.Visible)
            {
                BindBgaPreview();
            }
            SyncToolButtons(document.Context.Interaction.Tool);

            // Restore inspector + Sounds highlight from this document's persisted selection
            // before re-applying the Notes template (which reads the current Sounds row).
            Editor_OnSelectItem(this, document.Context.Selection.Items.ToArray());

            // Re-apply the Notes panel template to the newly active editor so Ctrl+click
            // note creation works right after a tab switch and uses the template currently
            // picked in the Notes panel instead of the editor's stale one.
            if (m_currentTemplateEvent != null)
            {
                NoteSelect_OnSelectData(m_currentTemplateEvent);
            }

            string displayName = Path.GetFileName(document.Context.SourcePath);
            bool readOnly = model.IsReadOnly;
            this.Text = String.Format("{0} - {1}{2}", APP_NAME, displayName, readOnly ? "   [READ-ONLY]" : "");
            CheckAndUpdatePlayPauseIcons();
            UpdateStudioRails();
        }

        private void LoadDocumentSounds(PlayerData playerData, string sourcePath)
        {
            string directory = Path.GetDirectoryName(sourcePath);
            for (int i = 0, l = playerData.Instruments.Count; i < l; i++)
            {
                InstrumentData instrument = playerData.Instruments[i];
                if (instrument == null || instrument.InsNum == 0)
                {
                    continue;
                }

                bool res = m_audioPlayer.LoadSound(
                    instrument.InsNum, directory + "\\" + instrument.Name, i == 0 ? 1 : 0);
                if (!res)
                {
                    Logs.Write("Failed to load sound {0} - {1}", instrument.InsNum, instrument.Name);
                }
            }
        }

        private void DocumentForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_closingAll)
            {
                return;
            }

            ChartDocument document = _documents.Find(doc => object.ReferenceEquals(doc.Form, sender));
            if (document == null || !document.IsDirty)
            {
                return;
            }

            DialogResult result = MessageBox.Show(this,
                "Save changes to " + Path.GetFileName(document.Context.SourcePath) + "?",
                "Close chart", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                return;
            }
            if (result == DialogResult.Yes)
            {
                SaveDocument(document);
            }
            document.IsDirty = false;
        }

        private bool ConfirmCloseDirtyDocuments()
        {
            foreach (ChartDocument document in _documents.ToArray())
            {
                if (!document.IsDirty)
                {
                    continue;
                }

                document.Form.Activate();
                DialogResult result = MessageBox.Show(this,
                    "Save changes to " + Path.GetFileName(document.Context.SourcePath) + "?",
                    "DJMax Editor", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel)
                {
                    return false;
                }
                if (result == DialogResult.Yes)
                {
                    SaveDocument(document);
                }
                document.IsDirty = false;
            }
            _closingAll = true;
            return true;
        }

        private void SaveDocument(ChartDocument document)
        {
            if (document == null)
            {
                return;
            }

            string sourcePath = document.Context.SourcePath;
            if (string.IsNullOrEmpty(sourcePath))
            {
                document.Form.Activate();
                saveAsToolStripMenuItem_Click(this, EventArgs.Empty);
                return;
            }
            SaveFile(sourcePath, 0, document.Context.Model);
        }

        private void DocumentForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            EditorForm form = sender as EditorForm;
            if (form == null)
            {
                return;
            }

            if (object.ReferenceEquals(form, m_homeForm))
            {
                m_homeForm = null;
                return;
            }

            ChartDocument document = _documents.Find(doc => object.ReferenceEquals(doc.Form, form));
            if (document == null)
            {
                return;
            }

            document.Context.Selection.SelectionChanged -= DocumentSelectionChanged;
            document.Context.Interaction.ToolChanged -= DocumentToolChanged;
            _documents.Remove(document);
            m_audioPlayer.ReleaseSoundContext(document.Context.SourcePath ?? string.Empty);

            if (object.ReferenceEquals(_activeDocument, document))
            {
                _activeDocument = null;
                if (_documents.Count > 0)
                {
                    ChartDocument next = _documents[_documents.Count - 1];
                    next.Form.Activate();
                    SetActiveDocument(next);
                }
                else
                {
                    SetActiveDocument(null);
                    ShowHomeForm();
                }
            }
        }

        private void ShowHomeForm()
        {
            if (m_homeForm == null || m_homeForm.IsDisposed)
            {
                m_homeForm = new EditorForm();
                WireEditorForm(m_homeForm);
            }
            m_homeForm.Show(dockPanel, DockState.Document);
        }

        private void DocumentSelectionChanged(object sender, EventArgs e)
        {
            if (_documentContext == null ||
                !object.ReferenceEquals(sender, _documentContext.Selection))
            {
                return;
            }
            Editor_OnSelectItem(this, _documentContext.Selection.Items.ToArray());
            UpdateStudioRails();
        }

        private void DocumentToolChanged(object sender, EventArgs e)
        {
            if (_documentContext == null)
            {
                return;
            }
            SyncToolButtons(_documentContext.Interaction.Tool);
            SetStudioStatus(
                "TOOL  " +
                _documentContext.Interaction.Tool.ToString().ToUpperInvariant());
        }

        private void SaveFile(string filename, int filterIndex = 0, PlayerData model = null)
        {
            model = model ?? m_playerData;
            var extension = Path.GetExtension(filename).ToLower();

            var handler = filterIndex > 0 ?
                _saveHandler.GetHandlerForFilterIndex(filterIndex) :
                _saveHandler.GetHandlerForExtension(extension);
            if (handler == null && model != null && model.SourceFormat.HasValue)
            {
                // Extensionless charts carry no extension for the lookup above (detection
                // is content-based): fall back to the detected format so they still save
                // back to their own path instead of silently doing nothing.
                handler = GetSaveHandlerForFormat(model.SourceFormat.Value);
            }
            if (handler == null)
            {
                return;
            }

            if (handler is BMESaveFile &&
                BmsonChartSerializer.ShouldUseForClassicBmsOverflow(model))
            {
                MessageBox.Show(this,
                    "This chart needs " + BmsChartSerializer.CountRequiredKeysounds(model) +
                    " unique keysounds, but classic BMS only has 1,295 usable IDs.\n\n" +
                    "DJMax Editor will save it as BMSON instead. BMSON keeps every keysound and " +
                    "is supported by Pulsus and BmsONE.",
                    "Using BMSON for this chart", MessageBoxButtons.OK, MessageBoxIcon.Information);

                using (var bmsonDialog = new SaveFileDialog())
                {
                    bmsonDialog.Filter = "Be-Music JSON (*.bmson)|*.bmson";
                    bmsonDialog.DefaultExt = "bmson";
                    bmsonDialog.AddExtension = true;
                    bmsonDialog.OverwritePrompt = true;
                    bmsonDialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(filename));
                    bmsonDialog.FileName = Path.GetFileNameWithoutExtension(filename) + ".bmson";
                    if (bmsonDialog.ShowDialog(this) != DialogResult.OK) return;
                    filename = bmsonDialog.FileName;
                }
                handler = new BmsonSaveFile();
            }

            if (model != null && model.IsReadOnly)
            {
                MessageBox.Show(this,
                    "This chart was opened read-only" +
                    (model.SourceFormat.HasValue ? " (" + model.SourceFormat + ")" : "") +
                    ".\n\nSaving it back is disabled because lossless round-trip and in-game compatibility " +
                    "have not been verified for this format.",
                    "Read-only chart", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var settingsForm = handler.GetSettingsForm();
            if (settingsForm != null)
            {
                settingsForm.Icon = this.Icon;
                settingsForm.StartPosition = FormStartPosition.CenterParent;
                settingsForm.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                settingsForm.Text = "Save as...";
                var closeDialogSuccess =  settingsForm.ShowDialog(this) == DialogResult.OK;
                if (!closeDialogSuccess)
                {
                    return;
                }
            }

            Thread loadDataThread = new Thread(delegate ()
            {
                bool ok;
                try
                {
                    ok = handler.Save(filename, model);
                }
                catch (Exception ex)
                {
                    DJMaxEditor.Diagnostics.DiagnosticLog.Exception("save.error", ex);
                    ok = false;
                }

                ShowOnUi(() =>
                {
                    m_loadingForm.Close();
                    if (!ok)
                    {
                        MessageBox.Show(this,
                            "The file could not be saved. The target may be unchanged or incomplete; " +
                            "see the local diagnostics log.\n\nLog: " + DJMaxEditor.Diagnostics.DiagnosticLog.LogPath,
                            "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        DJMaxEditor.Diagnostics.DiagnosticLog.Write("save.ok", filename);
                        SetStudioStatus("SAVED  " + Path.GetFileName(filename).ToUpperInvariant());

                        ChartDocument saved = _documents.Find(doc =>
                            string.Equals(doc.Context.SourcePath, filename, StringComparison.OrdinalIgnoreCase));
                        if (saved != null)
                        {
                            saved.IsDirty = false;
                        }
                        else if (_activeDocument != null &&
                            object.ReferenceEquals(model, _activeDocument.Context.Model))
                        {
                            // Save As to a new path: the document adopts it as its source.
                            _activeDocument.Context.SetSourcePath(filename);
                            _activeDocument.IsDirty = false;
                            _activeDocument.Form.Title = Path.GetFileName(filename);
                            UpdateStudioRails();
                        }
                    }
                });
            });
            loadDataThread.Start();
            m_loadingForm.DisplayedMessage = "Saving pattern...";
            m_loadingForm.ShowDialog(this);
        }

        private void SaveCurrentDocument()
        {
            if (_activeDocument == null || m_playerData == null)
            {
                return;
            }

            SaveDocument(_activeDocument);
        }

        private void SetFollowPlayback(bool follow)
        {
            m_editorForm.Editor.FollowTracksProgressWhilePlaying = follow;
            follwTrackPrgressToolStripMenuItem.Checked = follow;
            if (m_documentRail != null)
            {
                m_documentRail.SetFollowPlayback(follow);
            }
        }

        private void SetSplitView(bool split)
        {
            _splitViewEnabled = split;
            foreach (var form in _wiredEditorForms)
            {
                if (form.IsDisposed)
                {
                    continue;
                }
                form.Editor.SplitViewEnabled = split;
            }
            if (m_splitViewButton != null)
            {
                m_splitViewButton.Checked = split;
                m_splitViewButton.ForeColor = split
                    ? StudioDesignSystem.PulseCyan
                    : StudioDesignSystem.Muted;
            }
            Properties.Settings.Default.SplitEditorPanes = split;
            Properties.Settings.Default.Save();
        }

        private void SetDocumentSplit(bool split)
        {
            if (split && _documents.Count < 2)
            {
                if (m_docSplitButton != null)
                {
                    m_docSplitButton.Checked = false;
                }
                SetStudioStatus("OPEN TWO CHARTS TO USE DUAL CHART VIEW");
                return;
            }

            _documentSplitEnabled = split;
            if (m_docSplitButton != null)
            {
                m_docSplitButton.Checked = split;
                m_docSplitButton.ForeColor = split
                    ? StudioDesignSystem.PulseCyan
                    : StudioDesignSystem.Muted;
            }

            if (split)
            {
                ApplyDocumentSplit();
            }
            else
            {
                MergeDocumentPanes();
            }
        }

        private void ApplyDocumentSplit()
        {
            if (_activeDocument == null || _activeDocument.Form.IsDisposed)
            {
                return;
            }

            ChartDocument anchor = _documents.Find(
                doc => !object.ReferenceEquals(doc, _activeDocument));
            DockPane anchorPane = anchor == null ? null : anchor.Form.DockHandler.Pane;
            EditorForm activeForm = _activeDocument.Form;
            if (anchorPane == null)
            {
                return;
            }

            if (object.ReferenceEquals(activeForm.DockHandler.Pane, anchorPane))
            {
                activeForm.Show(anchorPane, DockAlignment.Bottom, 0.5);
            }
            activeForm.Activate();
        }

        private void MergeDocumentPanes()
        {
            if (_documents.Count == 0)
            {
                return;
            }

            DockPane primary = _documents[0].Form.DockHandler.Pane;
            if (primary == null)
            {
                return;
            }

            foreach (ChartDocument doc in _documents)
            {
                DockPane pane = doc.Form.DockHandler.Pane;
                if (pane != null && !object.ReferenceEquals(pane, primary))
                {
                    doc.Form.Show(primary, (IDockContent)null);
                }
            }

            if (_activeDocument != null && !_activeDocument.Form.IsDisposed)
            {
                _activeDocument.Form.Activate();
            }
        }

        private int GetActiveZoomPercent()
        {
            if (m_editorForm == null)
            {
                return 100;
            }
            return (int)Math.Round(m_editorForm.ActiveZoomFactor * 100f);
        }

        private void UpdateZoomDisplay()
        {
            int percent = GetActiveZoomPercent();
            if (percent != m_lastZoomPercent)
            {
                m_lastZoomPercent = percent;
                if (m_documentRail != null)
                {
                    m_documentRail.SetZoomPercent(percent);
                }
            }
        }

        private IOpenFile GetHandlerForFormat(DJMaxEditor.Files.FormatDetection.ChartFormat format)
        {
            switch (format)
            {
                case DJMaxEditor.Files.FormatDetection.ChartFormat.PtffDecrypted:
                    return _loadHandler.GetHandlerForExtension(".pt");
                case DJMaxEditor.Files.FormatDetection.ChartFormat.TrailerRespectV:
                    return _loadHandler.GetHandlerForExtension(".bytes");
                case DJMaxEditor.Files.FormatDetection.ChartFormat.CyclonXml:
                    return _loadHandler.GetHandlerForExtension(".xml");
                case DJMaxEditor.Files.FormatDetection.ChartFormat.BmsClassic:
                    return _loadHandler.GetHandlerForExtension(".bms");
                case DJMaxEditor.Files.FormatDetection.ChartFormat.TechmaniaTrack:
                    return _loadHandler.GetHandlerForExtension(".tech");
                default:
                    return null;
            }
        }

        // Save-side counterpart of GetHandlerForFormat: the fallback that lets charts
        // opened without an extension (or with one no handler claims) save back to their
        // own path with the serializer their detected format implies.
        private ISaveFile GetSaveHandlerForFormat(DJMaxEditor.Files.FormatDetection.ChartFormat format)
        {
            switch (format)
            {
                case DJMaxEditor.Files.FormatDetection.ChartFormat.PtffDecrypted:
                case DJMaxEditor.Files.FormatDetection.ChartFormat.PtffEncryptedTechnika:
                    return _saveHandler.GetHandlerForExtension(".pt");
                case DJMaxEditor.Files.FormatDetection.ChartFormat.TrailerRespectV:
                    return _saveHandler.GetHandlerForExtension(".bytes");
                case DJMaxEditor.Files.FormatDetection.ChartFormat.BmsClassic:
                    return _saveHandler.GetHandlerForExtension(".bms");
                case DJMaxEditor.Files.FormatDetection.ChartFormat.TechmaniaTrack:
                    return _saveHandler.GetHandlerForExtension(".tech");
                default:
                    return null;
            }
        }

        private void ShowOnUi(Action action)
        {
            if (InvokeRequired) BeginInvoke(action);
            else action();
        }

        // A multi-difficulty track.tech container opens one pattern at a time; the chooser
        // picks which slot imports while every other slot is retained verbatim for save-back.
        private bool ChooseTechPattern(DJMaxEditor.Files.Tech.TechOpenFile handler, string filename)
        {
            handler.SelectedPatternIndex = 0;
            var patterns = handler.EnumeratePatterns(filename);
            if (patterns == null || patterns.Count <= 1)
            {
                return true;
            }

            using (var picker = new DJMaxEditor.Files.Tech.TechPatternPickerForm(patterns))
            {
                picker.Icon = this.Icon;
                if (picker.ShowDialog(this) != DialogResult.OK)
                {
                    return false;
                }
                handler.SelectedPatternIndex = picker.SelectedPatternIndex;
                return true;
            }
        }

        private bool OpenFileAsync(IOpenFile file, string filename, bool readOnly,
            DJMaxEditor.Files.FormatDetection.ChartFormat sourceFormat)
        {
            Logs.Write(String.Format("Openning file {0}", filename));

            PlayerData playerData = null;
            FileInfo fi = new FileInfo(filename);

            try
            {
                if (!file.Open(filename, out playerData) || playerData == null)
                {
                    Logs.Write("Failed to open ({0})", filename);
                    ShowOnUi(() => OpenFileComplete(null, fi.Name, false));
                    return false;
                }
            }
            catch (DJMaxEditor.Files.ChartLoadException ex)
            {
                DJMaxEditor.Diagnostics.DiagnosticLog.Write("open.error", fi.Name + ": " + ex);
                ShowOnUi(() => ShowChartLoadError(fi.Name, ex));
                return false;
            }
            catch (Exception ex)
            {
                DJMaxEditor.Diagnostics.DiagnosticLog.Exception("open.unexpected", ex);
                ShowOnUi(() => ShowUnexpectedLoadError(fi.Name, ex));
                return false;
            }

            // Preserve read-only / source-format decisions from detection.
            playerData.IsReadOnly = playerData.IsReadOnly || readOnly;
            if (playerData.SourceFormat == null) playerData.SourceFormat = sourceFormat;

            ShowOnUi(() => OpenFileComplete(playerData, fi.FullName, true));
            return true;
        }

        private void OpenFile(string filename, int filterIndex = 0)
        {
            if (false == File.Exists(filename))
            {
                return;
            }

            byte[] data;
            try
            {
                data = File.ReadAllBytes(filename);
            }
            catch (Exception ex)
            {
                DJMaxEditor.Diagnostics.DiagnosticLog.Exception("open.read", ex);
                MessageBox.Show(this, "The file could not be read.\n\n" + ex.Message,
                    "Load file error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Content-based detection is authoritative; the extension is only a hint (a .pt file that is
            // really a Respect V trailer chart must NOT be force-routed to the PTFF parser, and a .bytes
            // file is still validated). Detection never decrypts and never touches the network.
            var extension = Path.GetExtension(filename).ToLower();
            var detection = DJMaxEditor.Files.FormatDetection.ChartFormatDetector.Detect(data, extension);
            DJMaxEditor.Diagnostics.DiagnosticLog.Write("open.detect", Path.GetFileName(filename) + ": " + detection);

            // Positively-identified encrypted Technika/Trilogy charts are decrypted OFFLINE (no network)
            // via the local PtCodec, on explicit user confirmation. This only runs after a positive
            // encrypted identification — never as a fallback for a generic parse failure.
            if (detection.Format == DJMaxEditor.Files.FormatDetection.ChartFormat.PtffEncryptedTechnika)
            {
                OpenEncryptedPt(filename, data, detection);
                return;
            }

            if (!detection.IsOpenable)
            {
                ShowLoadDiagnostic(Path.GetFileName(filename), detection);
                return;
            }

            var handler = GetHandlerForFormat(detection.Format);
            if (handler == null)
            {
                ShowLoadDiagnostic(Path.GetFileName(filename), detection);
                return;
            }

            var techHandler = handler as DJMaxEditor.Files.Tech.TechOpenFile;
            if (techHandler != null && !ChooseTechPattern(techHandler, filename))
            {
                return;
            }

            var settingsForm = handler.GetSettingsForm();
            if (settingsForm != null)
            {
                settingsForm.Icon = this.Icon;
                settingsForm.StartPosition = FormStartPosition.CenterParent;
                settingsForm.FormBorderStyle = FormBorderStyle.FixedToolWindow;
                settingsForm.Text = "Loading file...";
                var closeDialogSuccess = settingsForm.ShowDialog(this) == DialogResult.OK;
                if (!closeDialogSuccess)
                {
                    return;
                }
            }

            var det = detection;
            Thread loadDataThread = new Thread(delegate () { OpenFileAsync(handler, filename, det.IsReadOnly, det.Format); });
            loadDataThread.Start();
            m_loadingForm.DisplayedMessage = "Loading pattern...";
            m_loadingForm.ShowDialog(this);
        }

        // Offline decrypt-to-open for encrypted Technika/Trilogy charts. No network, no external process,
        // no temp files: PtCodec runs in-process on the bytes already in memory. The decrypted output is
        // re-validated as a real PTFF chart before it is allowed near the parser.
        private void OpenEncryptedPt(string filename, byte[] data,
            DJMaxEditor.Files.FormatDetection.FormatDetectionResult detection)
        {
            string name = Path.GetFileName(filename);

            var choice = MessageBox.Show(this,
                "This is an encrypted Technika/Trilogy chart.\n\n" +
                "Decrypt it offline and open it? Decryption runs entirely on this machine " +
                "(no upload, no network request). The original file is not modified.\n\n" +
                "File: " + name +
                (string.IsNullOrEmpty(detection.Evidence) ? "" : "\nEvidence: " + detection.Evidence),
                "Decrypt encrypted chart?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (choice != DialogResult.Yes)
            {
                DJMaxEditor.Diagnostics.DiagnosticLog.Write("open.decrypt", name + ": declined by user");
                return;
            }

            byte[] decrypted;
            try
            {
                decrypted = DJMaxEditor.Files.pt.PtCodec.Decrypt(data);
            }
            catch (Exception ex)
            {
                DJMaxEditor.Diagnostics.DiagnosticLog.Exception("open.decrypt", ex);
                MessageBox.Show(this,
                    "The chart could not be decrypted.\n\n" + ex.Message +
                    "\n\nFile: " + name + "\nThe file was not modified.",
                    "Decryption failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Re-validate: the decrypted bytes must present as a genuine decrypted PTFF chart (PTFF + EZTR),
            // otherwise the wrong key/format produced garbage and we must not feed it to the parser.
            var reDetect = DJMaxEditor.Files.FormatDetection.ChartFormatDetector.Detect(decrypted, ".pt");
            DJMaxEditor.Diagnostics.DiagnosticLog.Write("open.decrypt", name + " -> " + reDetect);

            if (reDetect.Format != DJMaxEditor.Files.FormatDetection.ChartFormat.PtffDecrypted)
            {
                MessageBox.Show(this,
                    "Decryption did not produce a valid chart (no PTFF/EZTR structure was found). " +
                    "This file may use a different key or format.\n\n" +
                    "File: " + name +
                    (string.IsNullOrEmpty(reDetect.Evidence) ? "" : "\nDecrypted evidence: " + reDetect.Evidence),
                    "Decryption produced invalid data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var handler = GetHandlerForFormat(DJMaxEditor.Files.FormatDetection.ChartFormat.PtffDecrypted)
                as PTOpenFile;
            if (handler == null)
            {
                ShowLoadDiagnostic(name, detection);
                return;
            }

            handler.SourceOverride = decrypted;
            handler.FromEncryptedSource = true;

            Thread loadDataThread = new Thread(delegate ()
            {
                OpenFileAsync(handler, filename, false,
                    DJMaxEditor.Files.FormatDetection.ChartFormat.PtffEncryptedTechnika);
            });
            loadDataThread.Start();
            m_loadingForm.DisplayedMessage = "Decrypting and loading pattern...";
            m_loadingForm.ShowDialog(this);
        }

        private void ShowLoadDiagnostic(string filename, DJMaxEditor.Files.FormatDetection.FormatDetectionResult detection)
        {
            m_loadingForm.Close();

            string title;
            string message;
            switch (detection.Format)
            {
                case DJMaxEditor.Files.FormatDetection.ChartFormat.PtffEncryptedTechnika:
                    title = "Encrypted DJMax chart";
                    message =
                        "This file appears to be an encrypted Technika or Trilogy chart.\n\n" +
                        "Offline decryption is available (no network is used), but it could not be " +
                        "started for this file. No data was uploaded and the file was not modified.";
                    break;
                case DJMaxEditor.Files.FormatDetection.ChartFormat.Malformed:
                    title = "Chart file is malformed";
                    message =
                        "The chart contains an invalid offset, count, or truncated data block. " +
                        "The file was not modified.";
                    break;
                default:
                    title = "Unsupported chart format";
                    message =
                        "The file does not match PTFF, encrypted Technika, Technika Q trailer format, or a supported XML format. " +
                        "No decryption or network request was attempted.";
                    break;
            }

            message += "\n\nFile: " + filename;
            if (!string.IsNullOrEmpty(detection.Evidence)) message += "\nEvidence: " + detection.Evidence;
            if (detection.FailureReason != null) message += "\nReason: " + detection.FailureReason;
            if (detection.Offset.HasValue) message += "\nOffset: 0x" + detection.Offset.Value.ToString("X");

            MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowChartLoadError(string filename, DJMaxEditor.Files.ChartLoadException ex)
        {
            m_loadingForm.Close();
            string offset = ex.Offset.HasValue ? "\nOffset: 0x" + ex.Offset.Value.ToString("X") : "";
            MessageBox.Show(this,
                "The chart could not be read: " + ex.Message +
                "\n\nFile: " + filename + "\nError type: " + ex.Kind + offset +
                "\n\nThe file was not modified.",
                "Chart could not be read", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void ShowUnexpectedLoadError(string filename, Exception ex)
        {
            m_loadingForm.Close();
            MessageBox.Show(this,
                "An unexpected error occurred while loading the chart. It was logged locally.\n\n" +
                ex.Message + "\n\nFile: " + filename + "\nLog: " + DJMaxEditor.Diagnostics.DiagnosticLog.LogPath,
                "Load file error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void TryOpenFromCommandLine()
        {
            if (_startupFiles == null)
            {
                return;
            }

            foreach (var fileToLoad in _startupFiles)
            {
                if (String.IsNullOrEmpty(fileToLoad) || !File.Exists(fileToLoad))
                {
                    continue;
                }
                OpenFile(fileToLoad);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Host the single-instance pipe: files passed to later processes are forwarded
            // here and opened as tabs in this one.
            try
            {
                _singleInstanceServer = SingleInstance.StartServer(paths =>
                {
                    try
                    {
                        if (IsDisposed || !IsHandleCreated)
                        {
                            return;
                        }
                        BeginInvoke(new Action(() => OnExternalOpen(paths)));
                    }
                    catch (Exception ex)
                    {
                        DJMaxEditor.Diagnostics.DiagnosticLog.Exception("singleinstance.invoke", ex);
                    }
                });
            }
            catch (Exception ex)
            {
                DJMaxEditor.Diagnostics.DiagnosticLog.Exception("singleinstance.server.start", ex);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_singleInstanceServer != null)
            {
                _singleInstanceServer.Dispose();
            }
            base.OnFormClosed(e);
        }

        private void OnExternalOpen(string[] paths)
        {
            // Bring the editor up before any dialog is shown: the settings dialog and the
            // loading form are modal, and a background process cannot pop them to the front.
            ForceForeground();
            if (paths != null)
            {
                foreach (var path in paths)
                {
                    if (String.IsNullOrEmpty(path) || !File.Exists(path))
                    {
                        continue;
                    }
                    OpenFile(path);
                }
            }
        }

        private void ForceForeground()
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }
            if (!Visible)
            {
                Show();
            }

            // SetForegroundWindow is restricted for background processes; attaching to the
            // foreground thread's input queue is the usual way to make it stick.
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            uint foregroundProcessId;
            uint foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out foregroundProcessId);
            uint currentThread = NativeMethods.GetCurrentThreadId();
            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);
                NativeMethods.SetForegroundWindow(Handle);
                NativeMethods.BringWindowToTop(Handle);
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
            else
            {
                NativeMethods.SetForegroundWindow(Handle);
            }
            Activate();
        }

        private static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool SetForegroundWindow(IntPtr hWnd);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

            [System.Runtime.InteropServices.DllImport("kernel32.dll")]
            public static extern uint GetCurrentThreadId();

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            public static extern bool BringWindowToTop(IntPtr hWnd);
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            StudioTheme.ApplyNativeDarkMode(this);
            TryOpenFromCommandLine();
        }

        private void LongIntervaleTimer_Tick(object sender, EventArgs e)
        {

            if (m_fmod != null)
            {
                m_fmod.UpdateDebugInfo(m_audioPlayer.GetDebugInfo());
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            m_player.Update();
            UpdateZoomDisplay();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (m_player.IsReady && m_player.IsPlaying)
            {
                m_editorForm.ActiveSurface.InvalidateView();
            }
        }

        private void CheckAndUpdatePlayPauseIcons()
        {
            bool isPaused = m_player.IsPaused;
            bool isStopped = m_player.IsStopped;

            if (isPaused != m_isPaused || isStopped)
            {
                m_isPaused = isPaused;

                if (m_isPaused || isStopped)
                {
                    m_isPaused = true;
                    toolStripButton1.Image = Resources.wps_play;
                    playPauseToolStripMenuItem.Image = Resources.wps_play;
                }
                else
                {
                    toolStripButton1.Image = Resources.wps_pause;
                    playPauseToolStripMenuItem.Image = Resources.wps_pause;
                }
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            if (!m_player.IsReady)
            {
                return;
            }

            // Stopped or paused: start from the parked playhead so keysound checks can
            // begin mid-chart (click empty timeline to move it). Playing: pause.
            if (m_player.IsStopped || m_player.IsPaused)
            {
                int tick = GetPlayheadNativeTick();
                m_audioPlayer.StopAllSounds();
                m_player.Play(tick);
                RestoreSoundingVoices(tick);
                SyncPreviewToPlayhead();
            }
            else
            {
                m_player.Pause();
                m_audioPlayer.PauseAllSounds();
            }
        }

        /// <summary>Native sequencer tick under the playhead (virtual / VirtualTickSize).</summary>
        private int GetPlayheadNativeTick()
        {
            if (m_editorForm == null || m_editorForm.ActiveSurface == null)
            {
                return 0;
            }
            int virtualTick = m_editorForm.ActiveSurface.PlayheadVirtualTick;
            int tick = virtualTick / EventData.VirtualTickSize;
            return tick < 0 ? 0 : tick;
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            m_audioPlayer.StopAllSounds();
            //RenderTimer.Enabled = false;
            m_player.Reset();
        }

        private void zoomMenu_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            /*
            foreach (ToolStripMenuItem it in zoomMenu.DropDown.Items) {
                it.Checked = false;
            }
                
            (e.ClickedItem as ToolStripMenuItem).Checked = true;

            String s = e.ClickedItem.Text;

            int ind = s.IndexOf(" %");
            s = s.Remove(ind);

            float z = Int32.Parse(s);
            _editorForm.Editor.SetZoom(z / 100);
            */
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = _loadHandler.GetFilter();
            openFileDialog1.Multiselect = true;
            DialogResult result = openFileDialog1.ShowDialog(this);
            if (result == DialogResult.OK)
            {
                foreach (string name in openFileDialog1.FileNames)
                {
                    OpenFile(name, openFileDialog1.FilterIndex);
                }
            }
        }

        private void follwTrackPrgressToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void follwTrackPrgressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SetFollowPlayback(!m_editorForm.Editor.FollowTracksProgressWhilePlaying);
        }

        private void propertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_propertiesForm.Show(dockPanel);
        }

        private void debugOutputToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_debugOutput.Show(dockPanel);
        }

        private void soundsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_audioList.Show(dockPanel);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_closingAll && !ConfirmCloseDirtyDocuments())
            {
                e.Cancel = true;
                return;
            }

            string configFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "layout.config");
            //if (m_bSaveLayout)
            dockPanel.SaveAsXml(configFile);
            //else if (File.Exists(configFile))
            //    File.Delete(configFile);

        }

        private IDockContent GetContentFromPersistString(string persistString)
        {
            if (persistString == typeof(PropertiesForm).ToString())
                return m_propertiesForm;

            else if (persistString == typeof(EditorForm).ToString())
                return m_editorForm;

            else if (persistString == typeof(DebugOutput).ToString())
                return m_debugOutput;

            else if (persistString == typeof(AudioListForm).ToString())
                return m_audioList;

            else if (persistString == typeof(Panels.FModForm).ToString())
                return m_fmod;

            else if (persistString == typeof(Panels.NoteSelectForm).ToString())
                return m_notes;

            else if (persistString == typeof(BgaPreviewForm).ToString())
            {
                EnsureBgaPreviewForm();
                return m_bgaPreview;
            }


#if ENABLE_EVENT_FORM
            else if (persistString == typeof(Panels.EventsListForm).ToString())
                return _eventsForm;
#endif

            else
            {
                return null;
            }
        }

        private void resetZoomToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_editorForm.ResetActiveZoom();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveCurrentDocument();
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Filter = _saveHandler.GetFilter();
            saveFileDialog1.DefaultExt = _saveHandler.GetDefaultExtension();

            // Default to the active document's own folder and name: the dialog is a
            // shared instance, so without this it would reopen wherever (and as
            // whatever) a previously saved document left it.
            string sourcePath = _activeDocument != null
                ? _activeDocument.Context.SourcePath
                : null;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                try
                {
                    saveFileDialog1.InitialDirectory =
                        Path.GetDirectoryName(Path.GetFullPath(sourcePath));
                    saveFileDialog1.FileName = Path.GetFileName(sourcePath);
                }
                catch (ArgumentException)
                {
                    // A malformed stored path must not break Save As altogether.
                }
                catch (PathTooLongException)
                {
                }
            }
            else
            {
                saveFileDialog1.FileName = string.Empty;
            }

            if (saveFileDialog1.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }
            SaveFile(saveFileDialog1.FileName, saveFileDialog1.FilterIndex);
        }

        private void fMODInformationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_fmod == null || m_fmod.IsDisposed)
            {
                m_fmod = new Panels.FModForm();
                StudioTheme.ApplyToForm(m_fmod);
            }
            if (!m_fmod.Visible)
            {
                m_fmod.Show(dockPanel);
            }
        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            Console.WriteLine("ok");
        }

        private void toolStripDropDownButton1_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            foreach (ToolStripMenuItem it in toolStripDropDownButton1.DropDown.Items)
            {
                it.Checked = false;
            }
            (e.ClickedItem as ToolStripMenuItem).Checked = true;

            String s = e.ClickedItem.Text;
            toolStripDropDownButton1.Text = "Quantize  " + s;
            string stringValue = s.Replace("1/", "");

            int value = 1;
            if (Int32.TryParse(stringValue, out value))
            {
                m_quantizeNoteValue = value;
                foreach (EditorForm form in AllEditorForms())
                {
                    form.Editor.NoteValue = value;
                    form.Editor.Redraw();
                }
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutForm form = new AboutForm();
            form.ShowDialog(this);
        }

        private void notesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_notes.Show(dockPanel);
        }

        private void fullScreenToolStripMenuItem_Click(object sender, EventArgs e)
        {

            m_isFullScreen = !m_isFullScreen;

            fullScreenToolStripMenuItem.Checked = m_isFullScreen;

            if (m_isFullScreen)
            {
                _oldState = this.WindowState;

                //this.WindowState = FormWindowState.Normal;

                //DJMaxEditor.libs.WinApi.SetWinFullScreen(this.Handle);
                this.TopMost = true;
                this.WindowState = FormWindowState.Maximized;
                this.FormBorderStyle = FormBorderStyle.None;


            }
            else
            {
                this.TopMost = false;
                this.FormBorderStyle = FormBorderStyle.Sizable;
                this.WindowState = _oldState;
            }
            //this.TopMost = true;
            //
        }

        private void dockPanel_DragEnter(object sender, DragEventArgs e) { }

        private void dockPanel_DragDrop(object sender, DragEventArgs e) { }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    OpenFile(file);
                }
            }

        }

        private void eventsToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if ENABLE_EVENT_FORM
            _eventsForm.Show(dockPanel);
#endif
        }

        private void undoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CanMutateThroughActiveSurface())
            {
                m_editorForm.Editor.UndoManager.Undo();
            }
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (CanMutateThroughActiveSurface())
            {
                m_editorForm.Editor.UndoManager.Redo();
            }
        }

        private void findNotesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_activeDocument == null)
            {
                return;
            }

            if (m_findNotesForm == null || m_findNotesForm.IsDisposed)
            {
                m_findNotesForm = new FindSoundNotesForm(
                    () => _documentContext == null ? null : _documentContext.Model,
                    () => _documentContext == null ? 0 : _documentContext.Model.VirtualCurrentTick,
                    LocateNote);
            }

            m_findNotesForm.Show(this);
            m_findNotesForm.WindowState = FormWindowState.Normal;
            m_findNotesForm.Focus();
        }

        private void LocateNote(EventData eventData)
        {
            if (eventData == null || _documentContext == null)
            {
                return;
            }

            _documentContext.Selection.Replace(new[] { eventData });

            var surface = m_editorForm == null ? null : m_editorForm.ActiveSurface;
            if (surface != null)
            {
                surface.RevealPosition(eventData.VirtualTick, (int)eventData.TrackId);
            }
        }

        private void UndoManager_OnUndoRedo(object sender, EventArgs e)
        {

            UndoManager manager = sender as UndoManager;

            if (manager == null)
            {
                return;
            }

            ChartDocument edited = _documents.Find(doc =>
                object.ReferenceEquals(doc.Context.UndoManager, manager));
            if (edited != null)
            {
                edited.IsDirty = true;
            }

            redoToolStripMenuItem.Enabled = manager.CanRedo;
            undoToolStripMenuItem.Enabled = manager.CanUndo;
            if (m_documentRail != null)
            {
                m_documentRail.SetEditState(
                    _documentContext != null && manager.CanUndo,
                    _documentContext != null && manager.CanRedo);
            }
            m_editorForm.ActiveSurface.InvalidateView();
        }

        private void AudioList_onPlayPressed(object sender)
        {
            AudioListForm audioListForm = sender as AudioListForm;
            if (null == audioListForm) 
            {
                return;
            }

            var selection = audioListForm.GetCurrentSelection();
            if (null == selection)
            {
                return;
            }

            this.m_audioPlayer.StopSound(ReservedChannel);
            this.m_audioPlayer.PlaySound(ReservedChannel, selection.InsNum, 1, 64, audition: true);
        }

        private void AudioList_onStopPressed(object sender)
        {
            AudioListForm audioListForm = sender as AudioListForm;
            if (null == audioListForm)
            {
                return;
            }

            var selection = audioListForm.GetCurrentSelection();
            if (null == selection)
            {
                return;
            }

            this.m_audioPlayer.StopSound(ReservedChannel);
        }

#endregion // private defs

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
            Application.Exit();
        }

        private void trackLengthTextBox_TextChanged(object sender, EventArgs e)
        {
            if (!m_editorForm.IsLegacySurfaceActive)
            {
                return;
            }

            this.m_editorForm.Editor.UpdateDrawableZone();
            this.m_editorForm.Editor.UpdateScrollbars();

        }

        private void ThemeDropDownButton_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (!(sender is ToolStripDropDownButton toolStripDropDownButton))
            {
                return;
            }

            if (!(e.ClickedItem is ToolStripMenuItem toolStripMenuItem))
            {
                return;
            }

            var editor = m_editorForm.Editor;
            var themes = editor.EventsThemeList;
            foreach (var theme in themes)
            {
                if (toolStripMenuItem.Text != theme.GetName())
                {
                    continue;
                }

                foreach (ToolStripMenuItem it in toolStripDropDownButton.DropDown.Items)
                {
                    it.Checked = false;
                }

                toolStripMenuItem.Checked = true;
                ApplyEventsTheme(theme);
                break;
            }
        }

        private void ApplyEventsTheme(IEventRenderer theme)
        {
            if (null == theme)
            {
                return;
            }

            m_currentEventsTheme = theme;
            foreach (EditorForm form in AllEditorForms())
            {
                form.Editor.CurrentEventsTheme = theme;
            }
            ThemeDropDownButton.Text = "Events theme  " + theme.GetName();
            m_notes.ApplyTheme(theme);
        }

        // .pt (PTFF), .bytes (Technika Q trailer) and .tech (TECHMANIA) charts all speak the
        // TECHNIKA note vocabulary: switch the events and zones themes to Technika so the
        // lanes render properly.
        private void ApplyTechnikaThemesForChart(DJMaxEditor.Files.FormatDetection.ChartFormat? sourceFormat)
        {
            if (sourceFormat != DJMaxEditor.Files.FormatDetection.ChartFormat.PtffDecrypted &&
                sourceFormat != DJMaxEditor.Files.FormatDetection.ChartFormat.PtffEncryptedTechnika &&
                sourceFormat != DJMaxEditor.Files.FormatDetection.ChartFormat.TrailerRespectV &&
                sourceFormat != DJMaxEditor.Files.FormatDetection.ChartFormat.TechmaniaTrack)
            {
                return;
            }

            var editor = m_editorForm.Editor;
            var eventsTheme = editor.EventsThemeList.FirstOrDefault(theme => theme.GetName() == "Technika");
            if (eventsTheme != null && !object.ReferenceEquals(eventsTheme, m_currentEventsTheme))
            {
                CheckThemeItem(ThemeDropDownButton, eventsTheme.GetName());
                ApplyEventsTheme(eventsTheme);
            }

            var zonesTheme = editor.ZonesThemeList.FirstOrDefault(theme => theme.GetName() == "Technika");
            if (zonesTheme != null && !object.ReferenceEquals(zonesTheme, m_currentZonesTheme))
            {
                CheckThemeItem(zoneRendererToolStripDropDownButton, zonesTheme.GetName());
                ApplyZonesTheme(zonesTheme);
            }
        }

        private static void CheckThemeItem(ToolStripDropDownButton button, string themeName)
        {
            foreach (ToolStripItem item in button.DropDownItems)
            {
                var menuItem = item as ToolStripMenuItem;
                if (menuItem != null)
                {
                    menuItem.Checked = menuItem.Text == themeName;
                }
            }
        }

        private void ApplyZonesTheme(IZoneRenderer theme)
        {
            if (null == theme)
            {
                return;
            }

            m_currentZonesTheme = theme;
            foreach (EditorForm form in AllEditorForms())
            {
                form.Editor.CurrentZonesTheme = theme;
            }
            zoneRendererToolStripDropDownButton.Text = "Zones theme  " + theme.GetName();
        }

        private IEnumerable<EditorForm> AllEditorForms()
        {
            if (m_homeForm != null && !m_homeForm.IsDisposed)
            {
                yield return m_homeForm;
            }
            foreach (ChartDocument document in _documents)
            {
                yield return document.Form;
            }
        }


        private void eventDisplayModeToolStripDropDownButton_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (!(sender is ToolStripDropDownButton toolStripDropDownButton))
            {
                return;
            }

            if (!(e.ClickedItem is ToolStripMenuItem toolStripMenuItem))
            {
                return;
            }

            var eventDisplayModes = GetAll<EventDisplayMode>();
            foreach (var eventDisplayMode in eventDisplayModes)
            {
                if (eventDisplayMode.Value != toolStripMenuItem.Text)
                {
                    continue;
                }

                foreach (ToolStripMenuItem it in toolStripDropDownButton.DropDown.Items)
                {
                    it.Checked = false;
                }

                m_editorForm.Editor.EventDisplayMode = (EventDisplayMode)eventDisplayMode.Key;
                toolStripMenuItem.Checked = true;
                toolStripDropDownButton.Text = eventDisplayMode.Value;
            }
        }

        private void allToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_editorForm.SelectAll();
        }

        private void deselectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_editorForm.Deselect();
        }

        private void inverseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_editorForm.InverseSelection();
        }

        private void boxSelectFilterToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            SetSelectFilter(boxSelectFilterToolStripMenuItem.Checked);
        }

        private void boxSelectFilterKindToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender == boxSelectFilterVolumeToolStripMenuItem)
            {
                SetSelectFilterKind(EventSelectMode.SpecialEventKind.Volume);
            }
            else if (sender == boxSelectFilterTempoToolStripMenuItem)
            {
                SetSelectFilterKind(EventSelectMode.SpecialEventKind.Tempo);
            }
            else if (sender == boxSelectFilterBeatToolStripMenuItem)
            {
                SetSelectFilterKind(EventSelectMode.SpecialEventKind.Beat);
            }
            else if (sender == boxSelectFilterVideoStartToolStripMenuItem)
            {
                SetSelectFilterKind(EventSelectMode.SpecialEventKind.VideoStart);
            }
        }

        private void SetSelectFilter(bool enabled)
        {
            EventSelectMode.BoxSelectionFilterEnabled = enabled;
            SyncSelectFilterUi();
            SetStudioStatus(enabled
                ? "SELECT FILTER  " + EventSelectMode.BoxSelectionFilterKind.ToString().ToUpperInvariant()
                : "SELECT FILTER  OFF");
        }

        private void SetSelectFilterKind(EventSelectMode.SpecialEventKind kind)
        {
            EventSelectMode.BoxSelectionFilterKind = kind;
            // Picking a kind also turns the filter on.
            EventSelectMode.BoxSelectionFilterEnabled = true;
            SyncSelectFilterUi();
            SetStudioStatus("SELECT FILTER  " + kind.ToString().ToUpperInvariant());
        }

        private void SyncSelectFilterUi()
        {
            boxSelectFilterToolStripMenuItem.Checked = EventSelectMode.BoxSelectionFilterEnabled;
            boxSelectFilterVolumeToolStripMenuItem.Checked =
                EventSelectMode.BoxSelectionFilterKind == EventSelectMode.SpecialEventKind.Volume;
            boxSelectFilterTempoToolStripMenuItem.Checked =
                EventSelectMode.BoxSelectionFilterKind == EventSelectMode.SpecialEventKind.Tempo;
            boxSelectFilterBeatToolStripMenuItem.Checked =
                EventSelectMode.BoxSelectionFilterKind == EventSelectMode.SpecialEventKind.Beat;
            boxSelectFilterVideoStartToolStripMenuItem.Checked =
                EventSelectMode.BoxSelectionFilterKind == EventSelectMode.SpecialEventKind.VideoStart;
            m_documentRail.SetSelectFilter(
                EventSelectMode.BoxSelectionFilterEnabled,
                EventSelectMode.BoxSelectionFilterKind);
        }

        private void zoneRendererToolStripDropDownButton_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (!(sender is ToolStripDropDownButton toolStripDropDownButton))
            {
                return;
            }

            if (!(e.ClickedItem is ToolStripMenuItem toolStripMenuItem))
            {
                return;
            }

            var editor = m_editorForm.Editor;
            var themes = editor.ZonesThemeList;
            foreach (var theme in themes)
            {
                if (toolStripMenuItem.Text != theme.GetName())
                {
                    continue;
                }

                foreach (ToolStripMenuItem it in toolStripDropDownButton.DropDown.Items)
                {
                    it.Checked = false;
                }
                toolStripMenuItem.Checked = true;
                ApplyZonesTheme(theme);
                break;
            }
        }

        private int m_lastPlaybackVirtualTick = -1;

        private readonly Stopwatch m_playbackPumpClock = Stopwatch.StartNew();

        private double m_lastPlaybackFrameMilliseconds = -1;

        private bool m_playbackPumpWasPlaying;

        private string m_lastPerformanceText;

        /// <summary>
        /// The playback pump, on an 8 ms timer so it runs faster than any display. The
        /// sequencer's whole-tick rate is tempo * 48 ticks a second - 48/s at 60 BPM, below
        /// a 60 Hz display - so driving the playhead off the integer tick alone steps at
        /// best every other frame at low tempos. <see cref="Player.GetCurrentTickExact"/>
        /// adds the sub-tick remainder and gives a fresh position six times as often (288
        /// virtual ticks/s at 60 BPM), which is what keeps the playhead line, the follow
        /// scroll and the preview sweeping smoothly; Invalidate coalesces in WM_PAINT to
        /// the display's own rate, which is the WinForms frame cap.
        /// </summary>
        private void PlayerTimer_Tick(object sender, EventArgs e)
        {
            var tm = TimeSpan.FromMilliseconds(m_player.GetCurrentMsTime());
            var tick = m_player.GetCurrentTick();

            string progressText = new DateTime(tm.Ticks).ToString("HH:mm:ss") + "  |  TICK " + tick;
            if (currentProgress.Text != progressText)
            {
                currentProgress.Text = progressText;
            }

            if (!m_player.IsReady)
            {
                return;
            }

            bool playing = m_player.IsPlaying;
            if (playing && !m_playbackPumpWasPlaying)
            {
                // (Re)start: drop the stale frame-time baseline so the first reading is honest.
                m_lastPlaybackFrameMilliseconds = -1;
                m_lastPlaybackVirtualTick = -1;
            }
            m_playbackPumpWasPlaying = playing;
            if (!playing)
            {
                return;
            }

            double exactTick = m_player.GetCurrentTickExact();
            int virtualTick = (int)(exactTick * EventData.VirtualTickSize);

            if (virtualTick != m_lastPlaybackVirtualTick)
            {
                // The integer position still drives the document model and the preview
                // frame; the smooth position below drives the paint pass.
                m_lastPlaybackVirtualTick = virtualTick;
                m_playerData.CurrentTick = virtualTick / EventData.VirtualTickSize;
                m_editorForm.ActiveSurface.PlayheadVirtualTick = virtualTick;
                m_preview.RefreshPlayback();
                if (m_bgaPreview != null && !m_bgaPreview.IsDisposed)
                {
                    m_bgaPreview.RefreshPlayback();
                }
            }

            m_editorForm.ActiveSurface.PlayheadPositionVirtualTick =
                exactTick * EventData.VirtualTickSize;

            // One ms / fps entry per painted frame, the same contract as the reference
            // project's perf readout: the delta is measured between real paint passes
            // of the timeline surface (the WinForms analogue of the WPF composition
            // clock), not between this pump's own ticks. The last value stays on the
            // rail when playback stops.
            double now = m_playbackPumpClock.Elapsed.TotalMilliseconds;
            double frameDelta = 0;
            if (m_editorForm.IsLegacySurfaceActive)
            {
                var editor = m_editorForm.Editor;
                // Only a fresh paint counts - right after pressing play the most recent
                // paint can be an idle one from long before playback started.
                if (editor.DebugClockNowMs - editor.DebugLastPaintAtMs < 100)
                {
                    frameDelta = editor.DebugLastPaintDeltaMs;
                }
            }
            else
            {
                frameDelta = m_lastPlaybackFrameMilliseconds > 0
                    ? now - m_lastPlaybackFrameMilliseconds
                    : 0;
            }
            if (frameDelta > 0 && m_statusRail != null)
            {
                string performanceText = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:0.0} ms  {1:0} fps",
                    frameDelta,
                    1000.0 / frameDelta);
                if (performanceText != m_lastPerformanceText)
                {
                    m_lastPerformanceText = performanceText;
                    m_statusRail.SetPerformance(performanceText);
                }
            }
            m_lastPlaybackFrameMilliseconds = now;
        }

        private void currentProgress_Click(object sender, EventArgs e)
        {
            PlayTickDialog t = new PlayTickDialog(GetPlayheadNativeTick());
            var closeDialogSuccess = t.ShowDialog(this) == DialogResult.OK;
            if (!closeDialogSuccess)
            {
                return;
            }
            int target = int.Parse(t.SetTick);
            SeekPlayheadAndMaybePlay(target * EventData.VirtualTickSize, forcePlay: true);
        }

        /// <summary>
        /// Parks the playhead at <paramref name="virtualTick"/>. When already running
        /// (or <paramref name="forcePlay"/>), restarts playback from that spot so KEY
        /// sound calibration can jump around the chart without returning to tick 0.
        /// Always pushes the BGA/preview to the same tick - the playback pump alone
        /// would leave the video stranded until the next timer tick (or forever while
        /// stopped).
        /// </summary>
        private void SeekPlayheadAndMaybePlay(int virtualTick, bool forcePlay)
        {
            virtualTick = Math.Max(0, virtualTick);
            if (m_editorForm != null && m_editorForm.ActiveSurface != null)
            {
                m_editorForm.ActiveSurface.PlayheadVirtualTick = virtualTick;
                m_editorForm.ActiveSurface.PlayheadPositionVirtualTick = virtualTick;
            }
            if (_documentContext != null && _documentContext.Model != null)
            {
                _documentContext.Model.CurrentTick = virtualTick / EventData.VirtualTickSize;
            }

            int nativeTick = virtualTick / EventData.VirtualTickSize;
            currentProgress.Text = "00:00:00  |  TICK " + nativeTick;
            // Force the next pump to treat this as a fresh position.
            m_lastPlaybackVirtualTick = -1;

            if (m_player.IsReady)
            {
                bool restart = forcePlay || m_player.IsPlaying || m_player.IsPaused;
                if (restart)
                {
                    m_audioPlayer.StopAllSounds();
                    m_player.Play(nativeTick);
                    RestoreSoundingVoices(nativeTick);
                }
                else if (m_editorForm != null && m_editorForm.ActiveSurface != null)
                {
                    m_editorForm.ActiveSurface.InvalidateView();
                }
            }

            SyncPreviewToPlayhead();
        }

        /// <summary>
        /// Seeks Gameplay Preview BGA and the standalone BGA Preview to the parked
        /// playhead immediately (scrub while stopped, or mid-chart play start).
        /// </summary>
        private void SyncPreviewToPlayhead()
        {
            if (m_preview != null && !m_preview.IsDisposed)
            {
                m_preview.RefreshPlaybackImmediately();
            }
            if (m_bgaPreview != null && !m_bgaPreview.IsDisposed)
            {
                m_bgaPreview.SyncNow();
            }
        }

        /// <summary>
        /// Re-strikes whatever each track would still be sounding at
        /// <paramref name="nativeTick"/>, mid-sample. The sequencer skips past notes
        /// on seek; without this the BGM (often a short-duration note carrying a long
        /// ogg) restarts silent or from 0:00.
        /// </summary>
        private void RestoreSoundingVoices(int nativeTick)
        {
            if (nativeTick <= 0 || m_playerData == null || m_audioPlayer == null)
            {
                return;
            }

            foreach (TrackData track in m_playerData.Tracks)
            {
                if (track == null)
                {
                    continue;
                }

                EventData sounding = null;
                foreach (EventData ev in track.Events)
                {
                    if (ev == null || ev.EventType != EventType.Note)
                    {
                        continue;
                    }
                    if (ev.Tick > nativeTick)
                    {
                        break;
                    }
                    if (ev.Instrument == null || ev.Instrument.InsNum == 0)
                    {
                        continue;
                    }
                    sounding = ev;
                }

                if (sounding == null)
                {
                    continue;
                }

                double elapsedMs = ElapsedMillisecondsBetween(
                    sounding.Tick, nativeTick, m_playerData);
                if (elapsedMs < 0)
                {
                    continue;
                }

                uint lengthMs = m_audioPlayer.GetSoundLengthMilliseconds(sounding.Instrument.InsNum);
                if (lengthMs == 0 || elapsedMs >= lengthMs)
                {
                    continue;
                }

                m_audioPlayer.SetVolume(track.Idx, sounding.Vel);
                if (!m_audioPlayer.PlaySound(
                        track.Idx,
                        sounding.Instrument.InsNum,
                        track.Volume,
                        sounding.Pan,
                        (uint)elapsedMs))
                {
                    Logs.Write(
                        "Failed to restore sounding voice on track : {0}, soundIndex : {1}",
                        track.Idx,
                        sounding.Instrument.InsNum);
                }
            }
        }

        /// <summary>
        /// Wall-clock ms from <paramref name="fromTick"/> to <paramref name="toTick"/>,
        /// integrating the chart tempo map the same way the sequencer does
        /// (48 ticks per beat).
        /// </summary>
        private static double ElapsedMillisecondsBetween(int fromTick, int toTick, PlayerData model)
        {
            if (model == null || toTick <= fromTick)
            {
                return 0.0;
            }

            double period = PeriodForTempo(model.Tempo);
            EventData[] timeline = model.Tracks != null ? model.Tracks.Events : null;
            if (timeline != null)
            {
                for (int i = 0; i < timeline.Length; i++)
                {
                    EventData ev = timeline[i];
                    if (ev == null || ev.EventType != EventType.Tempo || ev.Tick > fromTick)
                    {
                        continue;
                    }
                    if (ev.Tempo > 0)
                    {
                        period = PeriodForTempo(ev.Tempo);
                    }
                }
            }

            double ms = 0.0;
            int cursor = fromTick;
            if (timeline != null)
            {
                for (int i = 0; i < timeline.Length; i++)
                {
                    EventData ev = timeline[i];
                    if (ev == null || ev.EventType != EventType.Tempo)
                    {
                        continue;
                    }
                    if (ev.Tick <= fromTick || ev.Tick >= toTick)
                    {
                        continue;
                    }
                    ms += (ev.Tick - cursor) * period;
                    cursor = ev.Tick;
                    if (ev.Tempo > 0)
                    {
                        period = PeriodForTempo(ev.Tempo);
                    }
                }
            }

            ms += (toTick - cursor) * period;
            return ms;
        }

        private static double PeriodForTempo(float tempo)
        {
            if (tempo <= 0)
            {
                tempo = 120.0f;
            }
            return 60000.0 / (tempo * 48.0);
        }
    }
}

/*
richtextbox1.Select(richtextbox1.TextLength, 0) 
richtextbox1.SelectionColor = Color.Green 
richtextbox1.AppendText("Append Text might be good idea since select text puts text where the carret is")
 */
