using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using DJMaxEditor.Undo.Action;
using DJMaxEditor.Controls.Editor.Renderers;
using DJMaxEditor.DJMax;
using DJMaxEditor.Controls.Editor.Renderers.Events;
using DJMaxEditor.Controls.Editor.Renderers.Zones;
using DJMaxEditor.Controls.Editor;
using DJMaxEditor.Controls.Editor.Handlers;
using DJMaxEditor.Editor;

namespace DJMaxEditor 
{
    public sealed partial class EditorControl : UserControl
    {
        #region public defs

        public event EventHandler OnUndoRedo;

        public event EventRequestHandler OnRequestEvent;

        public event EventHandler ViewSettingsChanged;

        /// <summary>
        /// Raised when the owner parks the playhead by clicking empty timeline space.
        /// Argument is the snapped virtual tick. MainForm restarts playback from that
        /// spot when the transport is already running.
        /// </summary>
        public event EventHandler<int> PlayheadSeekRequested;

        public UndoManager UndoManager = UndoManager.GetInstance();

        public const float MinZoom = 0.20f;

        public const float MaxZoom = 2f;

        public const float DefaultZoom = 0.5f;

        // template event to add an event in PlayerData
        public EventData TemplateEvent = null;

        public bool FollowTracksProgressWhilePlaying
        {
            get { return _followTracksProgressWhilePlaying; }
            set
            {
                if (_followTracksProgressWhilePlaying == value) return;
                _followTracksProgressWhilePlaying = value;
                RaiseViewSettingsChanged();
            }
        }

        public bool IsPlayerPlaying
        {
            get { return _isPlayerPlaying; }
            set
            {
                if (_isPlayerPlaying == value) return;
                _isPlayerPlaying = value;
                RaiseViewSettingsChanged();
            }
        }

        /// <summary>
        /// The playhead position at sub-tick precision, in virtual ticks. The playback
        /// pump feeds this every frame; the whole-tick sequencer position alone moves at
        /// tempo * 48 ticks a second, below the display's refresh rate at low tempos, and
        /// this is what keeps the playhead line and the follow scroll moving smoothly.
        /// </summary>
        public double PlayheadPositionVirtualTick
        {
            get { return _smoothVirtualTick; }
        }

        /// <summary>Moves the smooth playhead and repaints; called by the playback pump.</summary>
        public void SetPlayheadPosition(double virtualTick)
        {
            _smoothVirtualTick = Math.Max(0.0, virtualTick);
            RepaintPlayheadStrip();
        }

        /// <summary>
        /// Invalidates just the strip of client area the playhead line occupies -
        /// the union of its previous and new position - when the scene behind it
        /// is static (playback without follow-scroll). The reference project's
        /// pump re-records only its overlay layer per frame; this is the GDI+
        /// equivalent, and it is what keeps playback painting cheap. While
        /// follow-scroll is active the origin moves every frame, so the whole
        /// surface repaints as before.
        /// </summary>
        private void RepaintPlayheadStrip()
        {
            const float pad = 6f;

            if (IsFollowing || !_ready)
            {
                if (PlaybackPaintGateOpen())
                {
                    InvalidatePlayheadStripFull();
                }
                else
                {
                    RememberPlayheadStrip(_viewablePixels.X, _zoom);
                }
                return;
            }

            float zoom = _zoom;
            int originX = _viewablePixels.X;

            if (!_playheadStripValid ||
                _playheadStripOriginX != originX ||
                Math.Abs(_playheadStripZoom - zoom) > 0.0001f)
            {
                if (PlaybackPaintGateOpen())
                {
                    InvalidatePlayheadStripFull();
                }
                else
                {
                    RememberPlayheadStrip(originX, zoom);
                }
                return;
            }

            if (!PlaybackPaintGateOpen())
            {
                // Gated: no new paint this tick, but the position moved - keep the
                // tracked strip origin in step so the next allowed paint erases the
                // line where it actually was.
                RememberPlayheadStrip(originX, zoom);
                return;
            }

            float width = _progress.Width * zoom;
            float oldX = (float)(_playheadStripLastVirtualX - _progress.Width / 2.0 - originX) * zoom;
            float newX = (float)(_smoothVirtualTick - _progress.Width / 2.0 - originX) * zoom;
            float left = Math.Min(oldX, newX) - pad;
            float right = Math.Max(oldX, newX) + width + pad;

            var strip = new Rectangle(
                (int)Math.Floor(left),
                0,
                (int)Math.Ceiling(right - left),
                DrawingArea.Height);
            DrawingArea.Invalidate(strip);

            RememberPlayheadStrip(originX, zoom);
        }

        /// <summary>
        /// During playback, painting faster than the display presents only burns
        /// UI-thread time on extra full scene rasterizations; the reference project
        /// redraws at the composition (display) rate instead. Allows one repaint per
        /// display interval - measured from the last real paint - and drops the rest.
        /// The dropped ticks lose nothing: a paint always reads the latest playhead
        /// position, so the line still lands exactly where the next allowed paint
        /// puts it. Editing and scrubbing are never gated.
        /// </summary>
        private bool PlaybackPaintGateOpen()
        {
            if (!IsPlayerPlaying)
            {
                return true;
            }
            if (_playbackPaintIntervalMs <= 0)
            {
                int hz = DisplayRefreshRate.GetFor(DrawingArea);
                _playbackPaintIntervalMs = 1000.0 / Math.Max(30, hz);
            }
            double nowMs = _debugClock.Elapsed.TotalMilliseconds;
            if (DebugLastPaintAtMs > 0 &&
                nowMs - DebugLastPaintAtMs < _playbackPaintIntervalMs * 0.9)
            {
                return false;
            }
            return true;
        }

        private double _playbackPaintIntervalMs;

        private void InvalidatePlayheadStripFull()
        {
            Redraw();
            RememberPlayheadStrip(_viewablePixels.X, _zoom);
        }

        private void RememberPlayheadStrip(int originX, float zoom)
        {
            _playheadStripLastVirtualX = _smoothVirtualTick;
            _playheadStripOriginX = originX;
            _playheadStripZoom = zoom;
            _playheadStripValid = true;
        }

        private bool _playheadStripValid;

        private double _playheadStripLastVirtualX;

        private int _playheadStripOriginX;

        private float _playheadStripZoom;

        // Paint diagnostics for the hosted benchmark: a shared monotonic clock so the
        // harness can ask "how long since the last paint" in the same timebase.
        public int DebugPaintCount { get; private set; }

        public double DebugPaintMilliseconds { get; private set; }

        public double DebugLastPaintAtMs { get; private set; }

        /// <summary>
        /// Milliseconds between the two most recent paint passes - the real
        /// displayed frame time, which is what the status rail's "ms / fps"
        /// readout shows (the playback pump's own tick cadence is not).
        /// </summary>
        public double DebugLastPaintDeltaMs { get; private set; }

        public double DebugClockNowMs
        {
            get { return _debugClock.Elapsed.TotalMilliseconds; }
        }

        /// <summary>Where the last paint's time went - the hosted benchmark prints
        /// these next to paintMs.</summary>
        public double DebugEventsMs => _diagEventsMs;

        public double DebugFillMs => TracksRenderer.DebugFillMs;

        public double DebugChromeMs => TracksRenderer.DebugChromeMs;

        public double DebugScrollMs => _diagScrollMs;

        public int DebugNoteDraws => TracksRenderer.DebugNoteDraws;

        public double DebugPanesMs => _diagPanesMs;

        public double DebugProgressMs => _diagProgressMs;

        public double DebugSelectMs => _diagSelectMs;

        private static readonly Stopwatch _debugClock = Stopwatch.StartNew();

        public event EventDataHandler OnSelectItem;

        internal readonly TracksRenderer TracksRenderer;

        private readonly ZonesRenderer ZonesRenderer;

        public IEnumerable<IEventRenderer> EventsThemeList => EventsRenderer.Themes;

        public IEventRenderer CurrentEventsTheme
        {
            get => EventsRenderer.Theme;
            set
            {
                EventsRenderer.Theme = value;
                Redraw();
                RaiseViewSettingsChanged();
            }
        }

        internal IEnumerable<IZoneRenderer> ZonesThemeList => ZonesRenderer.Themes;

        internal IZoneRenderer CurrentZonesTheme
        {
            get => ZonesRenderer.Theme;
            set
            {
                ZonesRenderer.Theme = value;
                Redraw();
                RaiseViewSettingsChanged();
            }
        }

        internal readonly EventsRenderer EventsRenderer;

        public int NoteValue
        {
            get => _noteValue;

            set
            {
                _noteValue = value;
                UpdateBlockSize();
                RaiseViewSettingsChanged();
            }
        }

        private readonly TextBox _mTextBox;

        private TrackData _mSelectedTrack;

        public EditorControl() 
        {
            DoubleBuffered = true;
            ZonesRenderer = new ZonesRenderer();
            EventsRenderer = new EventsRenderer();
            TracksRenderer = new TracksRenderer(EventsRenderer, ZonesRenderer);
            ContextMenu contextMenu = new ContextMenu();
            contextMenu.Popup += MenuContextPopup;
            ContextMenu = contextMenu;

            _mTextBox = new TextBox();
            _mTextBox.Leave += TextBoxLeave;
            _mTextBox.Visible = false;
            _mTextBox.Parent = this;
            _mTextBox.MaxLength = 0x40;
            _mTextBox.TextChanged += TextBoxTextChanged;
            _mTextBox.LostFocus += TextBoxLostFocus;
            _mTextBox.KeyPress += TextBoxKeyPressed;

            InitializeComponent();
            InitializeSplitScrollbars();

            DrawingArea.BackColor = UI.StudioDesignSystem.Void;
            MouseWheel += DrawingArea_MouseWheel;
            DrawingArea.MouseWheel += DrawingArea_MouseWheel;

            _autoScrollTimer = new Timer { Interval = 40 };
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;
            Disposed += delegate { _autoScrollTimer.Dispose(); };

            SetStyle(ControlStyles.Selectable, true);
        }

        private void InitializeSplitScrollbars()
        {
            _vScrollBarUpper = new VScrollBar
            {
                Dock = DockStyle.Fill,
                Width = 15,
                LargeChange = 16,
                SmallChange = 16
            };
            _vScrollBarUpper.ValueChanged += vScrollBarUpper_ValueChanged;

            _scrollPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                ColumnCount = 1,
                RowCount = 2
            };
            _scrollPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 15F));
            _scrollPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            _scrollPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

            tableLayoutPanel1.Controls.Remove(vScrollBar);
            vScrollBar.Dock = DockStyle.Fill;
            _scrollPanel.Controls.Add(_vScrollBarUpper, 0, 0);
            _scrollPanel.Controls.Add(vScrollBar, 0, 1);
            tableLayoutPanel1.Controls.Add(_scrollPanel, 1, 0);

            tableLayoutPanel1.ColumnStyles[1].SizeType = SizeType.Absolute;
            tableLayoutPanel1.ColumnStyles[1].Width = 15F;

            ApplySplitLayout();
        }

        private void ApplySplitLayout()
        {
            if (_scrollPanel == null || _vScrollBarUpper == null)
            {
                return;
            }
            _vScrollBarUpper.Visible = _splitView;
            _scrollPanel.RowStyles[0].Height = _splitView ? 50F : 0F;
            _scrollPanel.RowStyles[1].Height = _splitView ? 50F : 100F;
        }

        private void vScrollBarUpper_ValueChanged(object sender, EventArgs e)
        {
            if (IsFollowing)
            {
                return;
            }
            UpdateScrollbars();
            DrawingArea.Invalidate();
        }

        public void SelectAll()
        {
            _selectMode.SelectAll();
        }

        public int SelectedEventCount
        {
            get { return _selectMode == null ? 0 : _selectMode.SelectedItems.Count; }
        }

        public IList<EventData> SelectedEvents
        {
            get
            {
                return _selectMode == null
                    ? new List<EventData>().AsReadOnly()
                    : _selectMode.SelectedItems;
            }
        }

        public void Deselect()
        {
            _selectMode.ClearSelection();
            Redraw();
        }

        public void InverseSelection()
        {
            _selectMode.InvertSelection();
            Redraw();
        }

        private void TextBoxKeyPressed(object sender, KeyPressEventArgs e)
        {
            if (!CanMutateDocument)
            {
                e.Handled = true;
                return;
            }

            if (e.KeyChar == Convert.ToChar(Keys.Enter))
            {
                ActiveControl = null;
                UndoManager.ExecAction(new RenameTrackAction(_mSelectedTrack, _mTextBox.Text));
                e.Handled = true;
            }
        }

        private void TextBoxLostFocus(object sender, EventArgs e)
        {
            if (!CanMutateDocument)
            {
                return;
            }

            UndoManager.ExecAction(new RenameTrackAction(_mSelectedTrack, _mTextBox.Text));
        }

        private void TextBoxTextChanged(object sender, EventArgs e)
        {
            if (!(sender is TextBox textBox))
            {
                return;
            }
        }

        private void MenuContextPopup(object sender, EventArgs e)
        {
            if (!(sender is ContextMenu contextMenu))
            {
                return;
            }

            if (null == _selectMode)
            {
                return;
            }

            contextMenu.MenuItems.Clear();

            if (!CanMutateDocument)
            {
                return;
            }

            var screenPoint = Cursor.Position;
            var pictureBoxPoint = contextMenu.SourceControl.PointToClient(screenPoint);


            var eventData = _selectMode.GetEventAtPos(
                (int)(pictureBoxPoint.X / _zoom) + _viewablePixels.X,
                ScreenToVirtualY(pictureBoxPoint.Y)
            );

            if (eventData != null)
            {
                if (_documentContext != null)
                {
                    if (!_documentContext.Selection.Items.Contains(eventData))
                    {
                        _documentContext.Selection.Replace(new[] { eventData });
                    }
                    contextMenu.MenuItems.Add("Cu&t", delegate
                    {
                        _documentContext.Clipboard.CutSelection();
                    });
                    contextMenu.MenuItems.Add("&Copy", delegate
                    {
                        _documentContext.Clipboard.CopySelection();
                    });
                    contextMenu.MenuItems.Add("&Duplicate", delegate
                    {
                        _documentContext.Clipboard.DuplicateSelection(QuantizeVirtualStep);
                    });
                    contextMenu.MenuItems.Add("-");
                    contextMenu.MenuItems.Add("&Delete", delegate
                    {
                        _documentContext.Edits.DeleteSelection();
                    });
                    contextMenu.MenuItems.Add("Reveal in &Inspector", delegate
                    {
                        OnSelectItem?.Invoke(this, _documentContext.Selection.Items.ToArray());
                    });
                }
                return;
            }

            TrackData trackData = _selectMode.GetTrackAtPos(
                (int)(pictureBoxPoint.X / _zoom) + _viewablePixels.X,
                ScreenToVirtualY(pictureBoxPoint.Y)
            );

            if (null == trackData)
            {
                return;
            }

            _mSelectedTrack = trackData;

            _mTextBox.Text = trackData.TrackName;
            _mTextBox.Left = pictureBoxPoint.X;
            _mTextBox.Top = pictureBoxPoint.Y;
            contextMenu.MenuItems.Add("&Rename track", new EventHandler(OpenTrackRename));
        }

        private void OpenTrackRename(object sender, EventArgs e)
        {
            _mTextBox.Visible = true;
            _mTextBox.Focus();
        }

        private void TextBoxLeave(object sender, EventArgs e)
        {
            _mTextBox.Visible = false;
        }

        public void UpdateDrawableZone()
        {
            if (null == _playerData)
            {
                return;
            }

            _drawableZone.Height = _playerData.Tracks.Count * EventsRenderer.VirtualTrackheight;
            _drawableZone.Width = (int)_playerData.VirtualMaxTick;
        }

        public void Bind(EditorDocumentContext document)
        {
            if (document == null) throw new ArgumentNullException("document");
            UnbindCurrentDocument();
            _documentContext = document;
            UndoManager = document.UndoManager;
            _documentContext.Selection.SelectionChanged += SharedSelectionChanged;
            InitializeCore(document.Model, document.Selection);
        }

        public void Initialize(PlayerData playerData)
        {
            UnbindCurrentDocument();
            _documentContext = null;
            InitializeCore(playerData, new ChartSelectionService());
        }

        private void UnbindCurrentDocument()
        {
            if (_documentContext != null)
            {
                _documentContext.Selection.SelectionChanged -= SharedSelectionChanged;
            }
            if (_playerData != null)
            {
                _playerData.Tracks.EventAdded -= NoteAddedOrRemoved;
                _playerData.Tracks.EventRemoved -= NoteAddedOrRemoved;
            }
            if (UndoManager != null)
            {
                UndoManager.OnUndoRedo -= UndoManager_OnUndoRedo;
            }
        }

        private void InitializeCore(PlayerData playerData, ChartSelectionService selection)
        {
            hScrollBar.Visible = true;
            vScrollBar.Visible = true;

            hScrollBar.Value = 0;
            vScrollBar.Value = 0;
            _vScrollBarUpper.Value = 0;
            _smoothVirtualTick = 0;
            _playheadStripValid = false;

            DrawingArea.Width = Math.Max((int)playerData.MaxTick, _drawableZone.Width);

            _playerData = playerData;

            var tracks = playerData.Tracks;
            tracks.EventAdded += NoteAddedOrRemoved;
            tracks.EventRemoved += NoteAddedOrRemoved;

            UpdateDrawableZone();
            
            _selectMode?.Dispose();
            _selectMode = new EventSelectMode(this, _playerData, EventsRenderer, selection);

            _selectMode.OnSelectEvents += selectMode_OnSelect;

            _selectMode.OnChangeEventPosition += selectMode_OnChangePosition;
            _selectMode.OnDeleteEvent += SelectMode_OnDeleteEvent;

            UndoManager.OnUndoRedo += UndoManager_OnUndoRedo;

            _ready = true;

            SetZoom(_zoom);
        }

        private void SharedSelectionChanged(object sender, EventArgs e)
        {
            Redraw();
        }

        public void SetZoom(float nZoom) 
        {
            // Make the top corner stay the same between zooms
            hScrollBar.Value = Math.Max(hScrollBar.Minimum, Math.Min(hScrollBar.Maximum, (int)(hScrollBar.Value * (nZoom / _zoom))));
            vScrollBar.Value = Math.Max(vScrollBar.Minimum, Math.Min(vScrollBar.Maximum, (int)(vScrollBar.Value * (nZoom / _zoom))));
            if (_vScrollBarUpper != null)
            {
                _vScrollBarUpper.Value = Math.Max(_vScrollBarUpper.Minimum, Math.Min(_vScrollBarUpper.Maximum, (int)(_vScrollBarUpper.Value * (nZoom / _zoom))));
            }

            this._zoom = nZoom;

            UpdateScrollbars();

            UpdateBlockSize();

            this.Redraw();
        }

        public float GetZoom()
        {
            return _zoom;
        }

        /// <summary>
        /// Next wheel zoom step on a 10%-of-default grid, so the default zoom
        /// (100%) is always reachable no matter how many wheel events fired.
        /// </summary>
        public static float NextZoomStep(float current, int direction)
        {
            float gridStep = DefaultZoom / 10f;
            int gridIndex = (int)Math.Round(current / gridStep) + Math.Sign(direction);
            float zoom = gridIndex * gridStep;
            if (Math.Abs(zoom - DefaultZoom) < gridStep / 2f)
            {
                zoom = DefaultZoom;
            }
            return Math.Min(MaxZoom, Math.Max(MinZoom, zoom));
        }

        public void ScrollEditorPixel(int? x = null, int? y = null) 
        {
            if (x < 0) { x = 0; }
            if (y < 0) { y = 0; }

            if (x != null) {
                hScrollBar.Value = Math.Max(hScrollBar.Minimum, Math.Min(hScrollBar.Maximum, x ?? 0));
            }

            if (y != null) {
                vScrollBar.Value = Math.Max(vScrollBar.Minimum, Math.Min(vScrollBar.Maximum, y ?? 0));
            }
        }

        public void Repaint() 
        {
            DrawingArea.Invalidate();
        }

        public void ScrollTo(int x, int y) 
        {
            if (x > -1) {
                hScrollBar.Value = Math.Max(hScrollBar.Minimum, Math.Min(hScrollBar.Maximum, x));
            }

            if (y > -1) {
                vScrollBar.Value = Math.Max(vScrollBar.Minimum, Math.Min(vScrollBar.Maximum, y));
            }
        }

        public void Redraw() 
        {
            Repaint();
        }

        #endregion // public defs

        #region private defs

        private const int VirtualLeftMargin = EventsRenderer.VirtualNoteWidth / 2 + 4;

        private const int UpperPaneTrackCount = 16;

        private Rectangle _viewablePixels = new Rectangle();

        private VScrollBar _vScrollBarUpper;

        private TableLayoutPanel _scrollPanel;

        private bool _splitView;

        private bool _dragUpperPane;

        private readonly Timer _autoScrollTimer;

        private Point _autoScrollMouse;

        public bool SplitViewEnabled
        {
            get { return _splitView; }
            set
            {
                if (_splitView == value) return;
                _splitView = value;
                ApplySplitLayout();
                UpdateScrollbars();
                Redraw();
                RaiseViewSettingsChanged();
            }
        }

        private int SplitTrackCount
        {
            get
            {
                if (!_splitView) return 0;
                return Math.Min(UpperPaneTrackCount, _playerData == null ? 0 : _playerData.Tracks.Count);
            }
        }

        private int LowerPaneVirtualTop
        {
            get { return SplitTrackCount * EventsRenderer.VirtualTrackheight; }
        }

        private int UpperPaneHeight
        {
            get { return _splitView ? DrawingArea.Height / 2 : 0; }
        }

        private int UpperViewY
        {
            get { return _vScrollBarUpper == null ? 0 : (int)(_vScrollBarUpper.Value / _zoom); }
        }

        private int LowerViewY
        {
            get { return (int)(vScrollBar.Value / _zoom); }
        }

        private int ScreenToVirtualY(int screenY)
        {
            if (screenY < UpperPaneHeight)
            {
                return (int)(screenY / _zoom) + UpperViewY;
            }
            return (int)((screenY - UpperPaneHeight) / _zoom) + LowerPaneVirtualTop + LowerViewY;
        }

        private static void SetBarValue(ScrollBar bar, int value)
        {
            bar.Value = Math.Max(bar.Minimum, Math.Min(bar.Maximum, value));
        }

        private Rectangle _drawableZone = new Rectangle();

        private PlayerData _playerData;

        private EditorDocumentContext _documentContext;

        private object _activeMoveUndoGroup;

        private object _activeResizeUndoGroup;

        private int _activeResizeLastX;

        private bool CanMutateDocument
        {
            get
            {
                string reason;
                return DocumentMutationGuard.CanMutate(_playerData, true, out reason);
            }
        }

        private bool _ignoreMouse = false;

        private bool _followTracksProgressWhilePlaying;

        private bool _isPlayerPlaying;

        private double _smoothVirtualTick;

        private float _zoom = DefaultZoom;

        private bool _ready = false;

        private int _noteValue = 8;

        private readonly ProgressBar _progress = new ProgressBar();

        private readonly Drag _drag = new Drag();

        private EventSelectMode _selectMode = null;

        public EventDisplayMode EventDisplayMode
        {
            get => EventsRenderer.EventDisplayMode;

            set
            {
                EventsRenderer.EventDisplayMode = value;
                Redraw();
                RaiseViewSettingsChanged();
            }
        }

        private void RaiseViewSettingsChanged()
        {
            ViewSettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EditorControl_SizeChanged(object sender, EventArgs e) 
        {
            //DrawingArea.Invalidate();
            //updateTextBoxPosition();
        }

        private void UpdateBlockSize() 
        {
            if (!_ready) { return; }
            _selectMode.BlockSize.X = QuantizeVirtualStep;
            _selectMode.BlockSize.Y = EventsRenderer.VirtualTrackheight;
        }

        private int QuantizeVirtualStep
        {
            get
            {
                return Math.Max(
                    EventData.VirtualTickSize,
                    (int)((_playerData.TickPerMinute / Math.Max(1, _noteValue)) *
                        EventData.VirtualTickSize));
            }
        }

        private void selectMode_OnChangePosition(List<EventData> eventData, int trackDelta, int positionDelta) 
        {
            if (!CanMutateDocument)
            {
                return;
            }

            if (positionDelta != 0 || trackDelta != 0)
            {
                if (_documentContext != null)
                {
                    _documentContext.Edits.MoveSelection(
                        trackDelta,
                        positionDelta,
                        _activeMoveUndoGroup);
                }
                else
                {
                    UndoManager.ExecAction(new MoveEventAction(_playerData, eventData, trackDelta, positionDelta));
                }
            }
        }

        private void SelectMode_OnDeleteEvent(object sender, EventData[] events)
        {
            if (!CanMutateDocument)
            {
                return;
            }

            if (_documentContext != null)
            {
                _documentContext.Edits.DeleteSelection();
            }
            else
            {
                UndoManager.ExecAction(new RemoveEventAction(_playerData, events));
            }
        }

        private void selectMode_OnSelect(object sender, EventData[] events) 
        {
            if (_documentContext == null)
            {
                OnSelectItem?.Invoke(this, events);
            }
        }

        private void DrawingArea_MouseWheel(object sender, MouseEventArgs e)
        {
            switch (Control.ModifierKeys)
            {
                case Keys.Alt:
                    float nextZoom = NextZoomStep(_zoom, e.Delta);
                    if (nextZoom != _zoom)
                    {
                        SetZoom(nextZoom);
                    }
                    break;
                case Keys.Control:
                    ScrollEditorPixel(hScrollBar.Value - e.Delta / 4, null);
                    break;
                default:
                    if (DrawingArea.PointToClient(Cursor.Position).Y < UpperPaneHeight)
                    {
                        SetBarValue(_vScrollBarUpper, _vScrollBarUpper.Value - e.Delta / 4);
                    }
                    else
                    {
                        SetBarValue(vScrollBar, vScrollBar.Value - e.Delta / 4);
                    }
                    break;
            }
        }

        private GraphicsWrapper m_gw = new GraphicsWrapper();

        private bool IsFollowing => FollowTracksProgressWhilePlaying && IsPlayerPlaying && (_playerData.CurrentTick < _playerData.MaxTick);

        // Paint-phase diagnostics (see DebugEventsMs); reset per frame.
        private double _diagScrollMs;
        private double _diagPanesMs;
        private double _diagProgressMs;
        private double _diagSelectMs;
        private double _diagEventsMs;

        private void DrawToBuffer(Graphics g)
        {
            if (!_ready) { return; }

            var frameWatch = Stopwatch.StartNew();
            var gw = m_gw;
            gw.UpdateGraphics(g);

            GraphicsWrapper.DebugDrawImageCalls = 0;
            GraphicsWrapper.DebugUncachedDraws = 0;
            TracksRenderer.BeginFrameDiagnostics();
            _diagScrollMs = 0;
            _diagPanesMs = 0;
            _diagProgressMs = 0;
            _diagSelectMs = 0;
            _diagEventsMs = 0;

            // If checked, follow playing track progression
            if (IsFollowing) {

                const double spacing = 150;
                var pos = _smoothVirtualTick > spacing ? _smoothVirtualTick - spacing : _smoothVirtualTick;

                ScrollTo((int)Math.Round(pos * _zoom), -1);
                UpdateScrollbars();
            }

            _diagScrollMs = frameWatch.Elapsed.TotalMilliseconds;

            if (_playerData == null) {
                return;
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Bicubic buys nothing visible when zoomed out (the common playback
            // case) but costs several times more per scaled sprite; reserve it
            // for zoomed-in editing.
            g.InterpolationMode = _zoom >= 1.0f
                ? InterpolationMode.HighQualityBicubic
                : InterpolationMode.HighQualityBilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            var beatSize = EventData.VirtualTickSize * _playerData.TickPerMinute;
            var blockSize = beatSize / _noteValue;

            int upperHeight = UpperPaneHeight;
            if (_splitView)
            {
                DrawPane(g, gw, 0, upperHeight, 0, UpperViewY, beatSize, blockSize);
                DrawPane(g, gw, upperHeight, DrawingArea.Height - upperHeight, LowerPaneVirtualTop, LowerViewY, beatSize, blockSize);
                DrawPaneDivider(g, upperHeight);
            }
            else
            {
                DrawPane(g, gw, 0, DrawingArea.Height, 0, LowerViewY, beatSize, blockSize);
            }
            _diagPanesMs = frameWatch.Elapsed.TotalMilliseconds - _diagScrollMs;
        }

        private void DrawPane(Graphics g, GraphicsWrapper gw, int screenTop, int screenHeight, int virtualTop, int viewY, int beatSize, int blockSize)
        {
            if (screenHeight <= 0) { return; }

            int boundsY = virtualTop + viewY;
            float translateY = -(boundsY - screenTop / _zoom);
            var bounds = new Rectangle(
                _viewablePixels.X,
                boundsY,
                _viewablePixels.Width,
                (int)Math.Ceiling(screenHeight / _zoom));

            var phaseWatch = Stopwatch.StartNew();
            GraphicsState state = g.Save();
            try
            {
                g.ResetTransform();
                // Intersect - not replace - so a strip invalidate from the playback
                // pump keeps WM_PAINT's dirty-region clip instead of having it widened
                // back to the whole pane.
                g.SetClip(new Rectangle(0, screenTop, DrawingArea.Width, screenHeight), CombineMode.Intersect);
                g.ScaleTransform(_zoom, _zoom, MatrixOrder.Prepend);
                g.TranslateTransform(-_viewablePixels.X, translateY);

                TracksRenderer.RenderTracskList(gw, _playerData.Tracks, bounds, beatSize, blockSize, _playerData.VirtualMaxTick, _drawableZone);
                double afterTracks = phaseWatch.Elapsed.TotalMilliseconds;

                _progress.Position = _smoothVirtualTick;
                _progress.Render(gw, bounds);
                double afterProgress = phaseWatch.Elapsed.TotalMilliseconds;
                _diagProgressMs += afterProgress - afterTracks;

                _selectMode?.Render(gw);
                double afterSelect = phaseWatch.Elapsed.TotalMilliseconds;
                _diagSelectMs += afterSelect - afterProgress;

                // Events in device space: capture the virtual-to-device mapping,
                // drop the world transform, and let every note go out as a plain
                // integer blit (see GraphicsWrapper.BeginDeviceSpace).
                using (var deviceTransform = g.Transform)
                {
                    gw.BeginDeviceSpace(deviceTransform);
                    g.ResetTransform();
                    TracksRenderer.RenderTrackEvents(gw, _playerData.Tracks, bounds, _drawableZone);
                    gw.EndDeviceSpace();
                }
                _diagEventsMs += phaseWatch.Elapsed.TotalMilliseconds - afterSelect;
            }
            finally
            {
                g.Restore(state);
            }
        }

        private void DrawPaneDivider(Graphics g, int y)
        {
            using (var brush = new SolidBrush(UI.StudioDesignSystem.Border))
            {
                g.FillRectangle(brush, 0, y - 1, DrawingArea.Width, 3);
            }
        }

        private void EditorControl_KeyPress(object sender, KeyPressEventArgs e) 
        {
            byte countFrom = (byte)'1';
            byte countTo = (byte)'9';
            byte kc = (byte)e.KeyChar;

            if ((kc >= countFrom) && (kc <= countTo)) {
                OnRequestEvent?.Invoke((byte)(kc - countFrom));
            }            
        }

        private void DrawingArea_DoubleClick(object sender, EventArgs e) 
        {
            if (!(e is MouseEventArgs mouseEvent)) { return; }

            _selectMode.MouseDoubleClick(
                (int)(mouseEvent.X / _zoom) + _viewablePixels.X,
                ScreenToVirtualY(mouseEvent.Y),
                mouseEvent.Button
            );
        }

        private void UndoManager_OnUndoRedo(object sender, UndoManager.Action action) 
        {
            if (
                action == UndoManager.Action.Undo ||
                action == UndoManager.Action.Redo
            ) {
                // _selectMode.ClearSelection();
            }

            OnUndoRedo?.Invoke(UndoManager, null);
        }

        private void DrawingArea_Paint(object sender, PaintEventArgs e)
        {
            var paintWatch = Stopwatch.StartNew();
            try
            {
                DrawToBuffer(e.Graphics);
            }
            finally
            {
                paintWatch.Stop();
                DebugPaintCount++;
                DebugPaintMilliseconds = paintWatch.Elapsed.TotalMilliseconds;
                double nowMs = _debugClock.Elapsed.TotalMilliseconds;
                DebugLastPaintDeltaMs = DebugLastPaintAtMs > 0 ? nowMs - DebugLastPaintAtMs : 0;
                DebugLastPaintAtMs = nowMs;
            }
        }

        public void UpdateScrollbars() 
        {
            hScrollBar.Maximum = (int)Math.Floor((_drawableZone.Width + VirtualLeftMargin) * _zoom);
            hScrollBar.LargeChange = DrawingArea.Width;
            hScrollBar.Value = Math.Min(hScrollBar.Value, Math.Max(hScrollBar.Minimum, hScrollBar.Maximum - hScrollBar.LargeChange));
            hScrollBar.Enabled = hScrollBar.Maximum > hScrollBar.LargeChange;

            int upperPaneHeight = UpperPaneHeight;
            int lowerPaneHeight = Math.Max(1, DrawingArea.Height - upperPaneHeight);

            if (_vScrollBarUpper != null)
            {
                _vScrollBarUpper.Maximum = (int)Math.Floor(SplitTrackCount * EventsRenderer.VirtualTrackheight * _zoom);
                _vScrollBarUpper.LargeChange = Math.Max(1, upperPaneHeight);
                _vScrollBarUpper.Value = Math.Min(_vScrollBarUpper.Value, Math.Max(_vScrollBarUpper.Minimum, _vScrollBarUpper.Maximum - _vScrollBarUpper.LargeChange));
                _vScrollBarUpper.Enabled = _vScrollBarUpper.Maximum > _vScrollBarUpper.LargeChange;
            }

            vScrollBar.Maximum = (int)Math.Floor(Math.Max(0, _drawableZone.Height - SplitTrackCount * EventsRenderer.VirtualTrackheight) * _zoom);
            vScrollBar.LargeChange = lowerPaneHeight;
            vScrollBar.Value = Math.Min(vScrollBar.Value, Math.Max(vScrollBar.Minimum, vScrollBar.Maximum - vScrollBar.LargeChange));
            vScrollBar.Enabled = vScrollBar.Maximum > vScrollBar.LargeChange;

            _viewablePixels.X = (int)(hScrollBar.Value / _zoom) - VirtualLeftMargin;
            _viewablePixels.Width = (int)Math.Ceiling((float)DrawingArea.Width / _zoom);
            _viewablePixels.Height = (int)Math.Ceiling((float)DrawingArea.Height / _zoom);
        }

        private void vScrollBar_ValueChanged(object sender, EventArgs e) 
        {
            if (IsFollowing)
            {
                return;
            }
            UpdateScrollbars();
            DrawingArea.Invalidate();
        }

        private void hScrollBar_ValueChanged(object sender, EventArgs e)
        {
            if (IsFollowing)
            {
                return;
            }

            UpdateScrollbars();
            DrawingArea.Invalidate();
        }

        private void DrawingArea_MouseDown(object sender, MouseEventArgs e) 
        {
            ActiveControl = null;

            if (null == _selectMode)
            {
                return;
            }

            if (!IsPlayerPlaying)
            {
                Redraw();
            }

            TimelineTool activeTool = _documentContext == null
                ? TimelineTool.Select
                : _documentContext.Interaction.Tool;

            // Alt + left, middle click, or the explicit Pan tool enters viewport movement.
            if (e.Button == MouseButtons.Middle ||
                e.Button == MouseButtons.Left &&
                    (Control.ModifierKeys == Keys.Alt || activeTool == TimelineTool.Pan))
            {
                _dragUpperPane = e.Y < UpperPaneHeight;
                _drag.Start(e.X, e.Y, 0, 0);
                return;
            }

            int virtualX = (int)(e.X / _zoom) + _viewablePixels.X;
            int virtualY = ScreenToVirtualY(e.Y);

            if (e.Button == MouseButtons.Left &&
                activeTool == TimelineTool.Draw &&
                _documentContext != null)
            {
                CreateEventAt(virtualX, virtualY);
                return;
            }

            if (e.Button == MouseButtons.Left &&
                activeTool == TimelineTool.Erase &&
                _documentContext != null)
            {
                EventData eraseTarget = _selectMode.GetEventAtPos(virtualX, virtualY);
                if (eraseTarget != null)
                {
                    _documentContext.Selection.Replace(new[] { eraseTarget });
                    _documentContext.Edits.DeleteSelection();
                }
                return;
            }

            if (e.Button == MouseButtons.Left &&
                activeTool == TimelineTool.Resize &&
                _documentContext != null)
            {
                EventData resizeTarget = _selectMode.GetEventAtPos(virtualX, virtualY);
                if (resizeTarget != null && resizeTarget.EventType == EventType.Note)
                {
                    if (!_documentContext.Selection.Items.Contains(resizeTarget))
                    {
                        _documentContext.Selection.Replace(new[] { resizeTarget });
                    }
                    _activeResizeUndoGroup = new object();
                    _activeResizeLastX = _selectMode.EvaluateBlock(virtualX, true);
                    _documentContext.Interaction.Begin(
                        TimelineInteractionKind.ResizingEnd,
                        new TimelineInteractionAnchor(
                            resizeTarget.VirtualTick + resizeTarget.VirtualDuration,
                            (int)resizeTarget.TrackId));
                    Cursor = Cursors.SizeWE;
                }
                return;
            }

            bool leftAndControlPressed =
                e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Control;
            bool additiveSelection =
                e.Button == MouseButtons.Left &&
                (Control.ModifierKeys == Keys.Shift || leftAndControlPressed);

            _activeMoveUndoGroup = new object();

            if (_selectMode != null)
            {
                bool res = _selectMode.MouseDown(
                    virtualX,
                    virtualY,
                    e.Button,
                    additiveSelection
                );

                if (res)
                {
                    return;
                }
            }

            // Empty Select click: park the playhead so Play can start mid-chart
            // (keysound calibration). Snapped to the same grid as note placement.
            if (e.Button == MouseButtons.Left &&
                activeTool == TimelineTool.Select &&
                !additiveSelection &&
                _selectMode != null)
            {
                int seekTick = Math.Max(0, _selectMode.EvaluateBlock(virtualX, true));
                SetPlayheadPosition(seekTick);
                if (_playerData != null)
                {
                    _playerData.CurrentTick = seekTick / EventData.VirtualTickSize;
                }
                if (PlayheadSeekRequested != null)
                {
                    PlayheadSeekRequested(this, seekTick);
                }
                if (!IsPlayerPlaying)
                {
                    Redraw();
                }
                return;
            }

            if (leftAndControlPressed) {

                if (!CanMutateDocument)
                {
                    return;
                }

                if (TemplateEvent == null)
                {
                    return;
                }

                CreateEventAt(virtualX, virtualY);
                return;
            }

            Focus();
        }

        private void CreateEventAt(int virtualX, int virtualY)
        {
            if (!CanMutateDocument || TemplateEvent == null)
            {
                return;
            }

            int trackIndex =
                _selectMode.EvaluateBlock(virtualY, false) /
                EventsRenderer.VirtualTrackheight;
            int virtualTick = _selectMode.EvaluateBlock(virtualX, true);

            if (_documentContext != null)
            {
                _documentContext.Edits.CreateEvent(
                    TemplateEvent,
                    (uint)Math.Max(0, trackIndex),
                    Math.Max(0, virtualTick));
            }
            else
            {
                var created = TemplateEvent.Clone() as EventData;
                created.VirtualTick = Math.Max(0, virtualTick);
                created.TrackId = (uint)Math.Max(0, trackIndex);
                UndoManager.ExecAction(new AddEventAction(
                    _playerData,
                    new List<EventData> { created }));
            }
            Redraw();
        }

        private void DrawingArea_SizeChanged(object sender, EventArgs e) 
        {
            _ignoreMouse = true;
            hScrollBar.LargeChange = DrawingArea.Width + 16;
            hScrollBar.Value = Math.Max(0, Math.Min(hScrollBar.Value, hScrollBar.Maximum - hScrollBar.LargeChange));
            if (_vScrollBarUpper != null)
            {
                _vScrollBarUpper.LargeChange = UpperPaneHeight + 16;
                _vScrollBarUpper.Value = Math.Max(0, Math.Min(_vScrollBarUpper.Value, _vScrollBarUpper.Maximum - _vScrollBarUpper.LargeChange));
            }
            vScrollBar.LargeChange = (DrawingArea.Height - UpperPaneHeight) + 16;
            vScrollBar.Value = Math.Max(0, Math.Min(vScrollBar.Value, vScrollBar.Maximum - vScrollBar.LargeChange));
        }

        private void DrawingArea_MouseMove(object sender, MouseEventArgs e) 
        {
            if (_ignoreMouse)
            {
                _ignoreMouse = false;
                return;
            }

            var xx = (int)(e.X / _zoom) + _viewablePixels.X;
            var yy = ScreenToVirtualY(e.Y);

            var shouldReDraw = false;

            if (_drag.Active)
            {
                var newX = e.X;
                var newY = e.Y;
                var xDelta = newX - _drag.X;
                var yDelta = newY - _drag.Y;
                ScrollEditorPixel(hScrollBar.Value - xDelta, null);
                if (_dragUpperPane)
                {
                    SetBarValue(_vScrollBarUpper, _vScrollBarUpper.Value - yDelta);
                }
                else
                {
                    SetBarValue(vScrollBar, vScrollBar.Value - yDelta);
                }
                _drag.X = newX;
                _drag.Y = newY;
                shouldReDraw = true;
            }
            else if (_activeResizeUndoGroup != null &&
                e.Button == MouseButtons.Left &&
                _documentContext != null)
            {
                int snappedX = _selectMode.EvaluateBlock(xx, true);
                int delta = snappedX - _activeResizeLastX;
                if (delta != 0 &&
                    _documentContext.Edits.ResizeSelection(
                        delta,
                        _activeResizeUndoGroup))
                {
                    _activeResizeLastX = snappedX;
                }
                shouldReDraw = true;
            }
            else if ((e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) && _selectMode != null)
            {
                if (_selectMode != null)
                {
                    _selectMode.MouseDrag(xx, yy);
                    if (_selectMode.IsBoxSelecting)
                    {
                        UpdateBoxSelectAutoScroll(e.X, e.Y);
                    }
                    shouldReDraw = true;
                }
            }
            else
            {
                if (_documentContext != null &&
                    _documentContext.Interaction.Tool == TimelineTool.Resize)
                {
                    EventData hover = _selectMode.GetEventAtPos(xx, yy);
                    Cursor = hover != null && hover.EventType == EventType.Note
                        ? Cursors.SizeWE
                        : Cursors.Default;
                }
                else
                {
                    _selectMode?.MouseMove(xx, yy);
                }
            }

            if (shouldReDraw && !IsPlayerPlaying)
            {
                Redraw();
            }
        }

        private void DrawingArea_MouseUp(object sender, MouseEventArgs e) 
        {
            _drag.Stop();
            _autoScrollTimer.Stop();

            _selectMode?.MouseUp();
            _activeMoveUndoGroup = null;
            _activeResizeUndoGroup = null;
            if (_documentContext != null)
            {
                _documentContext.Interaction.Complete();
            }

            if (!IsPlayerPlaying)
            {
                Redraw();
            }
        }

        /// <summary>
        /// Auto scroll step in screen pixels for a mouse overshoot beyond the editor edge
        /// </summary>
        private static int AutoScrollStep(int overshoot)
        {
            return Math.Min(48, 8 + overshoot / 4);
        }

        /// <summary>
        /// Track the box selection mouse position and toggle edge auto scrolling
        /// </summary>
        private void UpdateBoxSelectAutoScroll(int x, int y)
        {
            _autoScrollMouse = new Point(x, y);

            bool outside = x < 0 || x > DrawingArea.Width || y < 0 || y > DrawingArea.Height;
            if (outside)
            {
                if (!_autoScrollTimer.Enabled)
                {
                    _autoScrollTimer.Start();
                }
            }
            else
            {
                _autoScrollTimer.Stop();
            }
        }

        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_selectMode == null || !_selectMode.IsBoxSelecting)
            {
                _autoScrollTimer.Stop();
                return;
            }

            int x = _autoScrollMouse.X;
            int y = _autoScrollMouse.Y;

            int dx = 0;
            if (x < 0) dx = -AutoScrollStep(-x);
            else if (x > DrawingArea.Width) dx = AutoScrollStep(x - DrawingArea.Width);

            int dy = 0;
            if (y < 0) dy = -AutoScrollStep(-y);
            else if (y > DrawingArea.Height) dy = AutoScrollStep(y - DrawingArea.Height);

            if (dx == 0 && dy == 0)
            {
                _autoScrollTimer.Stop();
                return;
            }

            if (dx != 0)
            {
                SetBarValue(hScrollBar, hScrollBar.Value + dx);
            }
            if (dy != 0)
            {
                ScrollBar bar = _splitView && y < UpperPaneHeight ? (ScrollBar)_vScrollBarUpper : vScrollBar;
                SetBarValue(bar, bar.Value + dy);
            }

            // Re-evaluate the selection so the box keeps tracking the mouse while scrolling
            _selectMode.MouseDrag(
                (int)(x / _zoom) + _viewablePixels.X,
                ScreenToVirtualY(y));
            Redraw();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) 
        {
            if (_documentContext == null)
            {
                if (keyData != Keys.Delete)
                {
                    return base.ProcessCmdKey(ref msg, keyData);
                }
                if (CanMutateDocument)
                {
                    _selectMode.DeleteSelection();
                }
                return true;
            }

            if (keyData == Keys.Escape)
            {
                _documentContext.Interaction.Cancel();
                _documentContext.Selection.Clear();
                Redraw();
                return true;
            }
            if (keyData == Keys.Delete || keyData == Keys.Back)
            {
                _documentContext.Edits.DeleteSelection();
                return true;
            }
            if (keyData == (Keys.Control | Keys.A))
            {
                SelectAll();
                return true;
            }
            if (keyData == (Keys.Control | Keys.C))
            {
                _documentContext.Clipboard.CopySelection();
                return true;
            }
            if (keyData == (Keys.Control | Keys.X))
            {
                _documentContext.Clipboard.CutSelection();
                return true;
            }
            if (keyData == (Keys.Control | Keys.V))
            {
                _documentContext.Clipboard.PasteAt(_documentContext.Model.VirtualCurrentTick);
                return true;
            }

            int multiplier = (keyData & Keys.Shift) == Keys.Shift ? 4 : 1;
            Keys keyCode = keyData & Keys.KeyCode;
            if (keyCode == Keys.Left || keyCode == Keys.Right)
            {
                int direction = keyCode == Keys.Left ? -1 : 1;
                _documentContext.Edits.MoveSelection(
                    0,
                    direction * QuantizeVirtualStep * multiplier);
                return true;
            }
            if (keyCode == Keys.Up || keyCode == Keys.Down)
            {
                int direction = keyCode == Keys.Up ? -1 : 1;
                _documentContext.Edits.MoveSelection(direction * multiplier, 0);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        #endregion // private defs

        private void DrawingArea_Resize(object sender, EventArgs e)
        {
            UpdateScrollbars();
            DrawingArea.Invalidate();
        }

        private void NoteAddedOrRemoved(object sender, EventArgs e)
        {
            UpdateDrawableZone();
        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
