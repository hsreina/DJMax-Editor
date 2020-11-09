// #define ENABLE_EVENT_FORM

using System;
using System.Linq;
using System.Windows.Forms;
using System.IO;
using WeifenLuo.WinFormsUI.Docking;
using System.Threading;
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

namespace DJMaxEditor
{
    public partial class MainForm : Form
    {
        #region public defs

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
                        m_propertiesForm.PropertyObject = new NoteEventPropertiesLayer(eventData);
                    }
                    break;
                case EventType.Volume:
                    {
                        m_propertiesForm.PropertyObject = new VolumeEventPropertiesLayer(eventData);
                    }
                    break;
                case EventType.Tempo:
                    {
                        m_propertiesForm.PropertyObject = new TempoEventPropertiesLayer(eventData);
                    }
                    break;
                case EventType.Beat:
                    {
                        m_propertiesForm.PropertyObject = new BeatEventPropertiesLayer(eventData);
                    }
                    break;
                default:
                    m_propertiesForm.PropertyObject = null;
                    break;
            }

        }

        public void Editor_OnSelectItem(object sender, EventData[] selectedItems)
        {
            if (null == selectedItems)
            {
                m_selectedEvent = null;
                m_propertiesForm.PropertyObject = null;
                m_audioList.List.ClearSelection();
                return;
            }

            if (selectedItems.Count() != 1)
            {
                m_selectedEvent = null;
                m_propertiesForm.PropertyObject = null;
                m_audioList.List.ClearSelection();
                return;
            }

            if (selectedItems.Count() < 1)
            {
                m_selectedEvent = null;
                m_propertiesForm.PropertyObject = null;
                m_audioList.List.ClearSelection();
                return;
            }

            var firstEvent = m_selectedEvent = selectedItems[0];
            UpdateAttributesBinding(firstEvent);

            // Check the selected note instrument
            InstrumentData instrumentData = firstEvent.Instrument;

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

        public void NoteSelect_OnSelectData(EventData eventData)
        {

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

        public MainForm()
        {
            InitializeComponent();

            _saveHandler = new SaveHandler();
            _saveHandler.Register(new PTSaveFile());
            _saveHandler.Register(new TQSaveFile());
            _saveHandler.Register(new BMESaveFile());

            _loadHandler = new LoadHandler();
            _loadHandler.Register(new PTOpenFile());
            _loadHandler.Register(new TQOpenFile());

            var editor = m_editorForm.Editor;

            var audioList = this.m_audioList = new AudioListForm();
            audioList.OnPlayPressed += this.AudioList_onPlayPressed;
            audioList.OnStopPressed += this.AudioList_onStopPressed;
            audioList.OnInstrumentChanged += this.AudioList_onInstrumentChanged;

            Logs.OnLogWrite += this.Logs_OnWrite;

            m_deserializeDockContent = new DeserializeDockContent(GetContentFromPersistString);
            
            m_notes = new NoteSelectForm(editor.EventsRenderer);

            editor.OnSelectItem += Editor_OnSelectItem;

            m_notes.OnSelectDataEvent += NoteSelect_OnSelectData;

            editor.OnRequestEvent += Editor_OnRequestEvent;

            m_audioList.List.SelectionChanged += AudioList_selectionChanged;

            editor.OnUndoRedo += UndoManager_OnUndoRedo;

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

            AllowDrop = true;
        }

        #endregion // public defs

        #region private defs

        private DeserializeDockContent m_deserializeDockContent;
        private PropertiesForm m_propertiesForm = new PropertiesForm();
        private EditorForm m_editorForm = new EditorForm();
        private DebugOutput m_debugOutput = new DebugOutput();
        private AudioListForm m_audioList;
        //private PerformancesForm m_performances = new PerformancesForm();
        private FModForm m_fmod = new FModForm();
        private NoteSelectForm m_notes;
        private SaveHandler _saveHandler;
        private LoadHandler _loadHandler;

        private UndoManager m_undoManager = UndoManager.GetInstance();

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

        private PlayerData m_playerData = new PlayerData();

        private EventData m_selectedEvent = null;

        private bool m_isFullScreen = false;

        private FormWindowState _oldState = FormWindowState.Maximized;

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

            // if a note is selected, update it's instrument
            if (m_selectedEvent != null && m_selectedEvent.Instrument != instrumentData)
            {
                m_undoManager.ExecAction(new SetSoundAction(m_selectedEvent, instrumentData));
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

            string configFile = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "layout.config");

            Logs.Write("configFile : {0}", configFile);
            if (File.Exists(configFile))
            {
                Logs.Write("loading config from xml");
                dockPanel.LoadFromXml(configFile, m_deserializeDockContent);
            }
            else
            {
                Logs.Write("not found. Loading default");
                m_editorForm.Show(dockPanel);

                m_debugOutput.Show(dockPanel);
                m_audioList.Show(dockPanel);

#if ENABLE_EVENT_FORM
                _eventsForm.Show(dockPanel);
#else
                eventsToolStripMenuItem.Visible = false;
#endif

                m_propertiesForm.Show(dockPanel);
                m_fmod.Show(dockPanel);
                m_notes.Show(dockPanel);
            }

            m_player.OnEvent += Player_OnEvent;
            m_player.OnStatusChange += Player_OnStatusChange;            
        }

        private void Player_OnStatusChange(object sender) 
        {
            var editor = m_editorForm.Editor;
            editor.IsPlayerPlaying = m_player.IsPlaying;
            editor.Invalidate();
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
                        if (eventData.Tick < m_player.GetCurrentTick() - 50)
                        {
                            // TODO: seek to the current position somehow
                            return;
                        }
                        m_audioPlayer.SetVolume(trackIndex, eventData.Vel);

                        if (!m_audioPlayer.PlaySound(trackIndex, soundIndex, track.Volume, pan))
                        {
                            Logs.Write("Failed to play sound on track : {0}, soundIndex : {1}", trackIndex, soundIndex);
                        };
                    }

                    break;
                case EventType.Volume:

                    ushort volume = eventData.Volume;

                    track.Volume = ((float)volume / (float)sbyte.MaxValue);
                    m_audioPlayer.SetVolume(trackIndex, track.Volume);

                    break;
            }

        }

        private Binding m_previousBinding;

        private void OpenFileComplete(PlayerData playerData, string filename, bool success)
        {
            m_loadingForm.Close();
            if (false == success) 
            {
                MessageBox.Show("Failed to load the file", "Load file error", MessageBoxButtons.OK, MessageBoxIcon.Error); 
                return;
            }

            this.Text = String.Format("{0} - {1}", APP_NAME, filename);

            m_editorForm.Title = filename;

            m_playerData = playerData;

            this.m_audioPlayer.StopAllSounds();
            m_player.LoadPlayerData(playerData);

            m_audioList.List.DataSource = playerData.Instruments;

#if ENABLE_EVENT_FORM
            _eventsForm.SetPlayerData(playerData);
#endif

            for (int i = 0, l = playerData.Instruments.Count; i < l; i++)
            {
                InstrumentData instrument = playerData.Instruments[i];
            }

            m_editorForm.Editor.Initialize(m_playerData);
        }

        private void SaveFile(string filename)
        {
            var extension = Path.GetExtension(filename).ToLower();

            var handler = _saveHandler.GetHandlerForExtension(extension);
            if (handler == null)
            {
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
                handler.Save(filename, m_playerData);
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        m_loadingForm.Close();
                    }));
                }
            });
            loadDataThread.Start();
            m_loadingForm.DisplayedMessage = "Saving pattern...";
            m_loadingForm.ShowDialog(this);
        }

        private IOpenFile GetOpenFileForName(string filename)
        {
            var extension = Path.GetExtension(filename).ToLower();
            return _loadHandler.GetHandlerForExtension(extension);
        }

        private bool OpenFileAsync(string filename)
        {
            Logs.Write(String.Format("Openning file {0}", filename));

            IOpenFile file = GetOpenFileForName(filename);
            PlayerData playerData;
            FileInfo fi = new FileInfo(filename);

            if (!file.Open(filename, out playerData))
            {

                Logs.Write("Failed to open pt ({0})", filename);
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() =>
                    {
                        OpenFileComplete(playerData, fi.Name, false);
                    }));
                }
                return false;
            }

            for (int i = 0, l = playerData.Instruments.Count; i < l; i++)
            {

                InstrumentData instrument = playerData.Instruments[i];

                if (instrument == null || instrument.InsNum == 0)
                {
                    continue;
                }

                bool res = m_audioPlayer.LoadSound(instrument.InsNum, fi.Directory + "\\" + instrument.Name, i == 0 ? 1 : 0);
                if (!res)
                {
                    Logs.Write("Failed to load sound {0} - {1}", instrument.InsNum, instrument.Name);
                }

            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    OpenFileComplete(playerData, fi.Name, true);
                }));
            }

            return true;
        }

        private void OpenFile(string filename)
        {
            if (false == File.Exists(filename)) 
            {
                return;
            }
            var extension = Path.GetExtension(filename).ToLower();

            var handler = _loadHandler.GetHandlerForExtension(extension);
            if (handler == null)
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

            Thread loadDataThread = new Thread(delegate () { OpenFileAsync(filename); });
            loadDataThread.Start();
            m_loadingForm.DisplayedMessage = "Loading pattern...";
            m_loadingForm.ShowDialog(this);

        }

        private void TryOpenFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            if (!(args.Length > 1))
            {
                return;
            }

            var fileToLoad = args[1];
            if (String.IsNullOrEmpty(fileToLoad))
            {
                return;
            }

            if (!File.Exists(fileToLoad))
            {
                return;
            }

            OpenFile(fileToLoad);
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
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
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (m_player.IsReady && m_player.IsPlaying)
            {
                m_editorForm.Editor.Redraw(); ;
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
                    toolStripButton1.Image = Resources.icon_play;
                    playPauseToolStripMenuItem.Image = Resources.icon_play;
                }
                else
                {
                    toolStripButton1.Image = Resources.icon_pause;
                    playPauseToolStripMenuItem.Image = Resources.icon_pause;
                }
            }
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            var editor = m_editorForm.Editor;

            if (m_player.IsStopped)
            {
                // enable the timer
                //RenderTimer.Enabled = true;
                //UpdateTimer.Enabled = true;
                // then play on tick 0
                m_player.Play(0);
            }
            else
            {

                if (m_isPaused)
                {
                    m_audioPlayer.PauseAllSounds();
                    m_player.Resume();
                }
                else
                {
                    m_player.Pause();
                    m_audioPlayer.PauseAllSounds();
                }
            }
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

            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                OpenFile(openFileDialog1.FileName);
            }
        }

        private void follwTrackPrgressToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void follwTrackPrgressToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = sender as ToolStripMenuItem;
            if (item != null)
            {
                item.Checked = !item.Checked;
                m_editorForm.Editor.FollowTracksProgressWhilePlaying = item.Checked;
            }
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
            m_editorForm.Editor.SetZoom(0.5f);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog1.Filter = _saveHandler.GetFilter();
            saveFileDialog1.DefaultExt = _saveHandler.GetDefaultExtension();

            if (saveFileDialog1.ShowDialog() != DialogResult.OK)
            {
                return;
            }
            SaveFile(saveFileDialog1.FileName);
        }

        private void fMODInformationsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_fmod.IsDisposed || m_fmod == null)
            {
                m_fmod = new Panels.FModForm();
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
            string stringValue = s.Replace("1/", "");

            int value = 1;
            if (Int32.TryParse(stringValue, out value))
            {
                var editor = m_editorForm.Editor;
                editor.NoteValue = value;
                editor.Redraw();
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
                    break;
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
            m_editorForm.Editor.UndoManager.Undo();
        }

        private void redoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_editorForm.Editor.UndoManager.Redo();
        }

        private void UndoManager_OnUndoRedo(object sender, EventArgs e)
        {

            UndoManager manager = sender as UndoManager;

            if (manager == null)
            {
                return;
            }

            redoToolStripMenuItem.Enabled = manager.CanRedo;
            undoToolStripMenuItem.Enabled = manager.CanUndo;
            m_editorForm.Editor.Redraw();
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
            this.m_audioPlayer.PlaySound(ReservedChannel, selection.InsNum, 1, 64);
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

            m_editorForm.Editor.CurrentEventsTheme = theme;
            //this.ThemeDropDownButton.Text = theme.GetName();
            m_notes.ApplyTheme(theme);
        }

        private void ApplyZonesTheme(IZoneRenderer theme)
        {
            if (null == theme)
            {
                return;
            }

            m_editorForm.Editor.CurrentZonesTheme = theme;
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

        private void PlayerTimer_Tick(object sender, EventArgs e)
        {
            var tm = TimeSpan.FromMilliseconds(m_player.GetCurrentMsTime());
            var tick = m_player.GetCurrentTick();
            var date = new DateTime(tm.Ticks);

            currentProgress.Text = date.ToString("HH:mm:ss") + " - " + tick;

            if (m_player.IsReady)
            {
                m_playerData.CurrentTick = tick;
            }
        }

        private void currentProgress_Click(object sender, EventArgs e)
        {
            PlayTickDialog t = new PlayTickDialog(m_player.GetCurrentTick());
            var closeDialogSuccess = t.ShowDialog(this) == DialogResult.OK;
            if (!closeDialogSuccess)
            {
                return;
            }
            int target = int.Parse(t.SetTick);

            m_audioPlayer.StopAllSounds();
            m_player.Reset();
            m_player.Play(target);
        }
    }
}

/*
richtextbox1.Select(richtextbox1.TextLength, 0) 
richtextbox1.SelectionColor = Color.Green 
richtextbox1.AppendText("Append Text might be good idea since select text puts text where the carret is")
 */
