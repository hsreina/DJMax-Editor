using System;
using System.Collections.Generic;
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

namespace DJMaxEditor 
{
    public sealed partial class EditorControl : UserControl
    {
        #region public defs

        public event EventHandler OnUndoRedo;

        public event EventRequestHandler OnRequestEvent;

        public UndoManager UndoManager = UndoManager.GetInstance();

        public const float MinZoom = 0.20f;

        public const float MaxZoom = 2f;

        // template event to add an event in PlayerData
        public EventData TemplateEvent = null;

        public bool FollowTracksProgressWhilePlaying = false;

        public bool IsPlayerPlaying = false;

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

            DrawingArea.BackColor = Color.FromArgb(255, 49, 56, 64);
            MouseWheel += DrawingArea_MouseWheel;
            DrawingArea.MouseWheel += DrawingArea_MouseWheel;

            SetStyle(ControlStyles.Selectable, true);
        }

        public void SelectAll()
        {
            _selectMode.SelectAll();
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
            if (e.KeyChar == Convert.ToChar(Keys.Enter))
            {
                ActiveControl = null;
                UndoManager.ExecAction(new RenameTrackAction(_mSelectedTrack, _mTextBox.Text));
                e.Handled = true;
            }
        }

        private void TextBoxLostFocus(object sender, EventArgs e)
        {
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

            var screenPoint = Cursor.Position;
            var pictureBoxPoint = contextMenu.SourceControl.PointToClient(screenPoint);


            var eventData = _selectMode.GetEventAtPos(
                (int)(pictureBoxPoint.X / _zoom) + _viewablePixels.X,
                (int)(pictureBoxPoint.Y / _zoom) + _viewablePixels.Y
            );

            if (eventData != null)
            {
                //m_textBox.Left = pictureBoxPoint.X;
                //m_textBox.Top = pictureBoxPoint.Y;
                //contextMenu.MenuItems.Add("&Setting", new EventHandler(OpenTrackRename));
                return;
            }

            TrackData trackData = _selectMode.GetTrackAtPos(
                (int)(pictureBoxPoint.X / _zoom) + _viewablePixels.X,
                (int)(pictureBoxPoint.Y / _zoom) + _viewablePixels.Y
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

        public void Initialize(PlayerData playerData) 
        {
            hScrollBar.Visible = true;
            vScrollBar.Visible = true;

            hScrollBar.Value = 0;
            vScrollBar.Value = 0;

            DrawingArea.Width = Math.Max((int)playerData.MaxTick, _drawableZone.Width);

            _playerData = playerData;

            var tracks = playerData.Tracks;
            tracks.EventAdded += NoteAddedOrRemoved;
            tracks.EventRemoved += NoteAddedOrRemoved;

            UpdateDrawableZone();
            
            _selectMode = new EventSelectMode(this, _playerData, EventsRenderer);

            _selectMode.OnSelectEvents += selectMode_OnSelect;

            _selectMode.OnChangeEventPosition += selectMode_OnChangePosition;
            _selectMode.OnDeleteEvent += SelectMode_OnDeleteEvent;

            UndoManager.OnUndoRedo += UndoManager_OnUndoRedo;

            _ready = true;

            SetZoom(_zoom);
        }

        public void SetZoom(float nZoom) 
        {
            // Make the top corner stay the same between zooms
            hScrollBar.Value = Math.Max(hScrollBar.Minimum, Math.Min(hScrollBar.Maximum, (int)(hScrollBar.Value * (nZoom / _zoom))));
            vScrollBar.Value = Math.Max(vScrollBar.Minimum, Math.Min(vScrollBar.Maximum, (int)(vScrollBar.Value * (nZoom / _zoom))));

            this._zoom = nZoom;

            UpdateScrollbars();

            UpdateBlockSize();

            this.Redraw();
        }

        public float GetZoom()
        {
            return _zoom;
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

        private Rectangle _viewablePixels = new Rectangle();

        private Rectangle _drawableZone = new Rectangle();

        private PlayerData _playerData;

        private bool _ignoreMouse = false;

        private float _zoom = 0.5f;

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
            }
        }

        private void EditorControl_SizeChanged(object sender, EventArgs e) 
        {
            //DrawingArea.Invalidate();
            //updateTextBoxPosition();
        }

        private void UpdateBlockSize() 
        {
            if (!_ready) { return; }
            _selectMode.BlockSize.X = (int)((_playerData.TickPerMinute / _noteValue) * EventData.VirtualTickSize);
            _selectMode.BlockSize.Y = EventsRenderer.VirtualTrackheight;
        }

        private void selectMode_OnChangePosition(List<EventData> eventData, int trackDelta, int positionDelta) 
        {
            if (positionDelta != 0 || trackDelta != 0)
            {
                UndoManager.ExecAction(new MoveEventAction(_playerData, eventData, trackDelta, positionDelta));
            }
        }

        private void SelectMode_OnDeleteEvent(object sender, EventData[] events)
        {
            UndoManager.ExecAction(new RemoveEventAction(_playerData, events));
        }

        private void selectMode_OnSelect(object sender, EventData[] events) 
        {
            OnSelectItem?.Invoke(this, events);
        }

        private void DrawingArea_MouseWheel(object sender, MouseEventArgs e)
        {
            switch (Control.ModifierKeys)
            {
                case Keys.Alt:
                    var oldZoom = _zoom;

                    if (e.Delta > 0) {
                        oldZoom = oldZoom + 0.1f;
                    } else if (e.Delta < 0) {
                        oldZoom = oldZoom - 0.1f;
                    }

                    oldZoom = Math.Min(oldZoom, MaxZoom);
                    oldZoom = Math.Max(oldZoom, MinZoom);

                    SetZoom(oldZoom);
                    break;
                case Keys.Control:
                    ScrollEditorPixel((int)(_viewablePixels.X * _zoom - e.Delta / 4), null);
                    break;
                default:
                    ScrollEditorPixel(null, (int)(_viewablePixels.Y * _zoom - e.Delta / 4));
                    break;
            }
        }

        private GraphicsWrapper m_gw = new GraphicsWrapper();

        private bool IsFollowing => FollowTracksProgressWhilePlaying && IsPlayerPlaying && (_playerData.CurrentTick < _playerData.MaxTick);

        private FrameRateLimiter m_rameRateLimiter = new FrameRateLimiter(60);

        private void DrawToBuffer(Graphics g) 
        {
            if (!_ready) { return; }

            if (!m_rameRateLimiter.ShouldAnimateNextFrame())
            {
                return;
            }

            var gw = m_gw;
            gw.UpdateGraphics(g);

            // If checked, follow playing track progression
            if (IsFollowing) {

                const int spacing = 150;
                var pos = _playerData.VirtualCurrentTick > spacing ? _playerData.VirtualCurrentTick - spacing : _playerData.VirtualCurrentTick;

                ScrollTo((int)(pos * _zoom), -1);
                UpdateScrollbars();
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.ScaleTransform(_zoom, _zoom, MatrixOrder.Prepend);

            g.TranslateTransform(-_viewablePixels.X, -_viewablePixels.Y);

            if (_playerData == null) {
                return;
            }

            var beatSize = EventData.VirtualTickSize * _playerData.TickPerMinute;
            var blockSize = beatSize / _noteValue;

            TracksRenderer.RenderTracskList(gw, _playerData.Tracks, _viewablePixels, beatSize, blockSize, _playerData.VirtualMaxTick, _drawableZone);
            
            _progress.Position = _playerData.VirtualCurrentTick;
            _progress.Render(gw, _viewablePixels);

            _selectMode?.Render(gw);
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
                (int)(mouseEvent.Y / _zoom) + _viewablePixels.Y,                 
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
            DrawToBuffer(e.Graphics);
        }

        public void UpdateScrollbars() 
        {
            hScrollBar.Maximum = (int)Math.Floor(_drawableZone.Width * _zoom);
            hScrollBar.LargeChange = DrawingArea.Width;
            hScrollBar.Value = Math.Min(hScrollBar.Value, Math.Max(hScrollBar.Minimum, hScrollBar.Maximum - hScrollBar.LargeChange));
            hScrollBar.Enabled = hScrollBar.Maximum > hScrollBar.LargeChange;

            vScrollBar.Maximum = (int)Math.Floor(_drawableZone.Height * _zoom);
            vScrollBar.LargeChange = DrawingArea.Height;
            vScrollBar.Value = Math.Min(vScrollBar.Value, Math.Max(vScrollBar.Minimum, vScrollBar.Maximum - vScrollBar.LargeChange));
            vScrollBar.Enabled = vScrollBar.Maximum > vScrollBar.LargeChange;

            _viewablePixels.X = (int)(hScrollBar.Value / _zoom);
            _viewablePixels.Y = (int)(vScrollBar.Value / _zoom);
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

            // alt + left click || middle click, enter in screen move
            if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Alt)
            {
                _drag.Start(e.X, e.Y, 0, 0);
                return;
            }

            bool leftAndControlPressed = e.Button == MouseButtons.Left && Control.ModifierKeys == Keys.Control;

            if (_selectMode != null)
            {
                bool res = _selectMode.MouseDown(
                    (int)(e.X / _zoom) + _viewablePixels.X,
                    (int)(e.Y / _zoom) + _viewablePixels.Y,
                    e.Button,
                    leftAndControlPressed
                );

                if (res)
                {
                    return;
                }                
            }

            if (leftAndControlPressed) {

                if (TemplateEvent == null)
                {
                    return;
                }

                var newEvent = TemplateEvent.Clone() as EventData;

                var trackIndex = (_selectMode.EvaluateBlock((int)(e.Y / _zoom) + _viewablePixels.Y, false)) / EventsRenderer.VirtualTrackheight;

                var newPos = _selectMode.EvaluateBlock((int)(e.X / _zoom) + _viewablePixels.X, true);

                newEvent.VirtualTick = newPos;
                newEvent.TrackId = (uint)trackIndex;

                UndoManager.ExecAction(new AddEventAction(_playerData, new List<EventData>() { newEvent }));

                return;
            }

            Focus();
        }

        private void DrawingArea_SizeChanged(object sender, EventArgs e) 
        {
            _ignoreMouse = true;
            hScrollBar.LargeChange = DrawingArea.Width + 16;
            vScrollBar.LargeChange = DrawingArea.Height + 16;
            hScrollBar.Value = Math.Max(0, Math.Min(hScrollBar.Value, hScrollBar.Maximum - hScrollBar.LargeChange));
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
            var yy = (int)(e.Y / _zoom) + _viewablePixels.Y;

            var shouldReDraw = false;

            if (_drag.Active)
            {
                var newX = e.X;
                var newY = e.Y;
                var xDelta = newX - _drag.X;
                var yDelta = newY - _drag.Y;
                ScrollEditorPixel(hScrollBar.Value - xDelta, vScrollBar.Value - yDelta);
                _drag.X = newX;
                _drag.Y = newY;
                shouldReDraw = true;
            }
            else if ((e.Button == MouseButtons.Left || e.Button == MouseButtons.Right) && _selectMode != null)
            {
                if (_selectMode != null)
                {
                    _selectMode.MouseDrag(xx, yy);
                    shouldReDraw = true;
                }
            }
            else
            {
                _selectMode?.MouseMove(xx, yy);
            }

            if (shouldReDraw && !IsPlayerPlaying)
            {
                Redraw();
            }
        }

        private void DrawingArea_MouseUp(object sender, MouseEventArgs e) 
        {
            _drag.Stop();

            _selectMode?.MouseUp();

            if (!IsPlayerPlaying)
            {
                Redraw();
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) 
        {
            if (keyData != Keys.Delete)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }
            _selectMode.DeleteSelection();
            return true;
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
