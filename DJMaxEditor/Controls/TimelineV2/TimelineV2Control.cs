using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DJMaxEditor.Controls.Editor.Renderers.Events;
using DJMaxEditor.Controls.Editor.Renderers.Zones;
using DJMaxEditor.Controls.TimelineV2.Projection;
using DJMaxEditor.Controls.TimelineV2.Renderers;
using DJMaxEditor.DJMax;
using DJMaxEditor.Editor;

namespace DJMaxEditor.Controls.TimelineV2
{
    /// <summary>
    /// Feature-flagged read-only timeline prototype. It deliberately registers no editing gesture,
    /// mutation command, undo action, file operation, or audio operation.
    /// </summary>
    public sealed class TimelineV2Control : UserControl, IEditorSurface
    {
        private const int DefaultRowHeight = 28;
        private const int DefaultHeaderWidth = 180;
        private const int DefaultRulerHeight = 42;
        private const int MinimapBucketCount = 256;
        private const double MinNoteVisualScale = 0.75;
        private const double MaxNoteVisualScale = 2.0;

        private readonly TimelineRenderer _renderer = new TimelineRenderer();
        private readonly RawTrackProjection _projection = new RawTrackProjection();
        // Timeline V2's static layer is comparatively expensive to rebuild while
        // following playback. Coalesce the UI-side redraws to the 30 Hz budget
        // instead of competing with audio for a 60 Hz WinForms paint loop.
        private readonly PlaybackFrameScheduler _playbackFrames =
            new PlaybackFrameScheduler(33);
        private readonly Stopwatch _playbackClock = Stopwatch.StartNew();
        private TimelineProjectionResult _projectionResult;
        private TimelineEventIndex _index;
        private TimelineCoordinateSystem _coordinates;
        private TimelineViewport _viewport;
        private int[] _minimapDensity = new int[0];
        private int _firstVisibleRow;
        private int _playheadVirtualTick;
        private bool _isPanning;
        private bool _hHeld;
        private bool _isPlaybackActive;
        private double _noteVisualScale = 1.0;
        private Point _lastPointer;

        public TimelineV2Control()
        {
            DoubleBuffered = true;
            BackColor = TimelineRenderTheme.Canvas;
            TabStop = true;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable |
                ControlStyles.UserPaint,
                true);
            _coordinates = CreateCoordinates(1f, TimelineViewport.DefaultPixelsPerTick);
        }

        public Control View
        {
            get { return this; }
        }

        public bool SupportsEditing
        {
            get { return false; }
        }

        public EditorDocumentContext Document { get; private set; }

        public TimelinePerformanceMonitor Performance { get; } = new TimelinePerformanceMonitor();

        public int IndexedItemCount
        {
            get { return _index == null ? 0 : _index.ItemCount; }
        }

        public string StatusText { get; private set; } = "NO DOCUMENT | RAW TRACKS";

        public int QuantizeDivision { get; set; } = 8;

        public IEventRenderer EventTheme { get; set; }

        public IZoneRenderer ZoneTheme { get; set; }

        public EventDisplayMode EventDisplayMode { get; set; } = EventDisplayMode.Attribute;

        public bool FollowPlayback { get; set; }

        public bool IsPlaybackActive
        {
            get { return _isPlaybackActive; }
            set { _isPlaybackActive = value; }
        }

        public int PlayheadVirtualTick
        {
            get { return _playheadVirtualTick; }
            set
            {
                int next = Math.Max(0, value);
                if (_playheadVirtualTick == next)
                {
                    return;
                }
                _playheadVirtualTick = next;
                FollowPlayhead();
                RequestRepaint();
            }
        }

        /// <summary>
        /// Sub-tick playhead position. The V2 timeline is a read-only prototype and keeps
        /// its integer playhead; it rounds the smooth position and behaves as before.
        /// </summary>
        public double PlayheadPositionVirtualTick
        {
            get { return _playheadVirtualTick; }
            set { PlayheadVirtualTick = (int)Math.Round(value); }
        }

        public void Bind(EditorDocumentContext document)
        {
            if (document == null) throw new ArgumentNullException("document");

            Document = document;
            var stopwatch = Stopwatch.StartNew();
            _projectionResult = _projection.Build(document);
            int documentEnd = Math.Max(
                1,
                _projectionResult.Items.Count == 0
                    ? 1
                    : _projectionResult.Items.Max(item => item.EndTick));
            int ticksPerMeasure = TicksPerMeasure;
            _index = new TimelineEventIndex(
                _projectionResult.Items,
                Math.Max(96, ticksPerMeasure));
            _viewport = new TimelineViewport(
                0,
                documentEnd,
                Math.Max(1, ClientSize.Width),
                _coordinates.HeaderWidth);
            _viewport.PixelsPerTick = _coordinates.PixelsPerTick;
            _minimapDensity = BuildMinimapDensity(_projectionResult, documentEnd);
            _firstVisibleRow = 0;
            _playheadVirtualTick = document.Model.VirtualCurrentTick;
            StatusText = BuildStatusText(document);
            stopwatch.Stop();

            Performance.LastIndexBuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            Performance.IndexedItemCount = _index.ItemCount;
            Performance.FullRebuildCount++;
            Invalidate();
        }

        public void InvalidateView()
        {
            RequestRepaint();
        }

        internal Bitmap RenderSnapshot(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException("width");
            if (height <= 0) throw new ArgumentOutOfRangeException("height");

            var bitmap = new Bitmap(width, height);
            var stopwatch = Stopwatch.StartNew();
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                TimelineFrame frame = CreateFrameForTesting(width, height);
                _renderer.Render(graphics, frame);
            }
            stopwatch.Stop();
            Performance.LastFrameMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            return bitmap;
        }

        public bool TrySetTimeZoom(float zoom)
        {
            if (_viewport == null || zoom <= 0)
            {
                return false;
            }

            _viewport.PixelsPerTick = zoom;
            _coordinates = CreateCoordinates(1f, _viewport.PixelsPerTick);
            Invalidate();
            return true;
        }

        public float TimeZoom
        {
            get
            {
                return (float)(_viewport == null
                    ? _coordinates.PixelsPerTick
                    : _viewport.PixelsPerTick);
            }
        }

        public EditorViewState CaptureViewState()
        {
            return new EditorViewState
            {
                PixelsPerTick = _viewport == null
                    ? _coordinates.PixelsPerTick
                    : _viewport.PixelsPerTick,
                OriginTick = _viewport == null ? 0 : _viewport.OriginTick,
                FirstVisibleRow = _firstVisibleRow,
                PlayheadVirtualTick = PlayheadVirtualTick
            };
        }

        public void RevealPosition(int virtualTick, int trackIndex)
        {
            if (_viewport == null) return;

            _viewport.OriginTick = virtualTick - (_viewport.VisibleTickCount / 2);
            RequestRepaint();
        }

        public void RestoreViewState(EditorViewState state)
        {
            if (state == null) return;

            if (_viewport != null)
            {
                _viewport.PixelsPerTick = state.PixelsPerTick;
                _viewport.OriginTick = state.OriginTick;
                _coordinates = CreateCoordinates(1f, _viewport.PixelsPerTick);
            }
            _firstVisibleRow = ClampFirstRow(state.FirstVisibleRow);
            _playheadVirtualTick = Math.Max(0, state.PlayheadVirtualTick);
            Invalidate();
        }

        internal TimelineFrame CreateFrameForTesting(int width, int height)
        {
            if (Document == null || _projectionResult == null || _index == null || _viewport == null)
            {
                throw new InvalidOperationException("Bind a document before creating a frame.");
            }

            _viewport.ViewportWidth = Math.Max(1, width);
            int visibleRowCount = Math.Max(
                1,
                (Math.Max(1, height - TimelineFrame.MinimapHeight - _coordinates.RulerHeight) /
                    _coordinates.RowHeight) + 1);
            int lastVisibleRow = Math.Max(
                _firstVisibleRow,
                Math.Min(_projectionResult.Rows.Count - 1, _firstVisibleRow + visibleRowCount - 1));

            var queryStopwatch = Stopwatch.StartNew();
            var visibleItems = _index.Query(
                _viewport.VisibleTimeRange,
                new TimelineRowRange(_firstVisibleRow, lastVisibleRow),
                Math.Max(96, TicksPerMeasure),
                1);
            queryStopwatch.Stop();

            Performance.LastQueryMilliseconds = queryStopwatch.Elapsed.TotalMilliseconds;
            Performance.LastVisibleItemCount = visibleItems.Count;
            Performance.LastQueryCandidateCount = _index.LastQueryCandidateCount;

            return new TimelineFrame(
                width,
                height,
                _coordinates,
                _viewport,
                _projectionResult.Rows,
                visibleItems,
                _firstVisibleRow,
                _playheadVirtualTick,
                Document.Capabilities.IsReadOnly || !SupportsEditing,
                Document.Capabilities.IsReadOnly
                    ? Document.Capabilities.StatusLabel
                    : "TIMELINE V2 - VIEW ONLY",
                StatusText,
                TicksPerMeasure,
                TicksPerMeasure > 0 ? 4 : 0,
                _minimapDensity,
                QuantizeDivision,
                EventTheme,
                ZoneTheme,
                EventDisplayMode);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Document == null)
            {
                e.Graphics.Clear(TimelineRenderTheme.Canvas);
                using (var brush = new SolidBrush(TimelineRenderTheme.MutedText))
                using (var font = new Font("Segoe UI", 10f))
                {
                    e.Graphics.DrawString("Open a chart to use Timeline V2", font, brush, 24, 24);
                }
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            TimelineFrame frame = CreateFrameForTesting(ClientSize.Width, ClientSize.Height);
            _renderer.Render(e.Graphics, frame);
            if (Focused)
            {
                ControlPaint.DrawFocusRectangle(
                    e.Graphics,
                    new Rectangle(2, 2, Math.Max(1, Width - 5), Math.Max(1, Height - 5)),
                    TimelineRenderTheme.Text,
                    TimelineRenderTheme.Canvas);
            }
            stopwatch.Stop();
            Performance.LastFrameMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_viewport != null)
            {
                _viewport.ViewportWidth = Math.Max(1, ClientSize.Width);
            }
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_viewport == null) return;

            switch (TimelineInputBindings.ResolveWheelAction(ModifierKeys))
            {
                case TimelineWheelAction.Zoom:
                    if ((ModifierKeys & Keys.Control) == Keys.Control)
                    {
                        ApplyTouchpadZoom(e.X, e.Delta);
                        return;
                    }
                    _viewport.ZoomAt(e.X, WheelZoomFactor(e.Delta));
                    _coordinates = CreateCoordinates(1f, _viewport.PixelsPerTick);
                    break;
                case TimelineWheelAction.HorizontalScroll:
                    _viewport.ScrollByPixels(-e.Delta / 2.0);
                    break;
                default:
                    _firstVisibleRow = ClampFirstRow(
                        _firstVisibleRow - Math.Sign(e.Delta) * 3);
                    break;
            }
            Invalidate();
        }

        internal void ApplyTouchpadZoom(int screenX, int delta)
        {
            if (_viewport == null || delta == 0)
            {
                return;
            }

            double factor = WheelZoomFactor(delta);
            _viewport.ZoomAt(screenX, factor);
            _noteVisualScale = Math.Max(
                MinNoteVisualScale,
                Math.Min(MaxNoteVisualScale, _noteVisualScale * factor));
            _coordinates = CreateCoordinates(1f, _viewport.PixelsPerTick);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_viewport != null && e.Y >= Height - TimelineFrame.MinimapHeight)
            {
                double ratio = Math.Max(0, Math.Min(1, (double)e.X / Math.Max(1, Width)));
                double target = _viewport.DocumentStartTick +
                    ((_viewport.DocumentEndTick - _viewport.DocumentStartTick) * ratio);
                _viewport.OriginTick = target - (_viewport.VisibleTickCount / 2);
                Invalidate();
                return;
            }

            if (TimelineInputBindings.IsPanGesture(e.Button, ModifierKeys, _hHeld))
            {
                _isPanning = true;
                _lastPointer = e.Location;
                Capture = true;
                Cursor = Cursors.SizeAll;
                return;
            }

            // Left click on the lane area parks the playhead for play-from-spot.
            if (e.Button == MouseButtons.Left &&
                _viewport != null &&
                _coordinates != null &&
                e.X >= _coordinates.HeaderWidth)
            {
                double tick = _viewport.OriginTick +
                    ((e.X - _coordinates.HeaderWidth) /
                        Math.Max(0.001, _viewport.PixelsPerTick));
                int virtualTick = Math.Max(0, (int)Math.Round(tick));
                PlayheadVirtualTick = virtualTick;
                if (Document != null && Document.Model != null)
                {
                    Document.Model.CurrentTick = virtualTick / EventData.VirtualTickSize;
                }
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isPanning || _viewport == null) return;

            int deltaX = e.X - _lastPointer.X;
            int deltaY = e.Y - _lastPointer.Y;
            _viewport.PanByPixels(deltaX);
            if (Math.Abs(deltaY) >= _coordinates.RowHeight / 2)
            {
                _firstVisibleRow = ClampFirstRow(
                    _firstVisibleRow - (deltaY / _coordinates.RowHeight));
                _lastPointer.Y = e.Y;
            }
            _lastPointer.X = e.X;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isPanning = false;
            Capture = false;
            Cursor = Cursors.Default;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_viewport == null) return;

            if (e.KeyCode == Keys.H)
            {
                _hHeld = true;
                Cursor = Cursors.Hand;
            }
            else if (e.KeyCode == Keys.Add || e.KeyCode == Keys.Oemplus)
            {
                _viewport.ZoomAt(Width / 2.0, 1.2);
                _coordinates = CreateCoordinates(1f, _viewport.PixelsPerTick);
                Invalidate();
            }
            else if (e.KeyCode == Keys.Subtract || e.KeyCode == Keys.OemMinus)
            {
                _viewport.ZoomAt(Width / 2.0, 1.0 / 1.2);
                _coordinates = CreateCoordinates(1f, _viewport.PixelsPerTick);
                Invalidate();
            }
            else if (e.KeyCode == Keys.Home)
            {
                _viewport.OriginTick = _viewport.DocumentStartTick;
                Invalidate();
            }
            else if (e.KeyCode == Keys.End)
            {
                _viewport.OriginTick = _viewport.DocumentEndTick;
                Invalidate();
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (e.KeyCode == Keys.H)
            {
                _hHeld = false;
                if (!_isPanning) Cursor = Cursors.Default;
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Home || key == Keys.End || key == Keys.Add ||
                key == Keys.Subtract || key == Keys.Oemplus || key == Keys.OemMinus ||
                key == Keys.H || base.IsInputKey(keyData);
        }

        private int TicksPerMeasure
        {
            get
            {
                if (Document == null || Document.Model.TickPerMinute == 0)
                {
                    return 0;
                }
                return Document.Model.TickPerMinute * DJMax.EventData.VirtualTickSize;
            }
        }

        private void FollowPlayhead()
        {
            if (!FollowPlayback || !IsPlaybackActive || _viewport == null)
            {
                return;
            }

            double followedOrigin = Math.Max(
                _viewport.DocumentStartTick,
                _playheadVirtualTick - 150);
            _viewport.OriginTick = followedOrigin;
        }

        private int ClampFirstRow(int row)
        {
            int maximum = _projectionResult == null
                ? 0
                : Math.Max(0, _projectionResult.Rows.Count - 1);
            return Math.Max(0, Math.Min(maximum, row));
        }

        private TimelineCoordinateSystem CreateCoordinates(float dpiScale, double pixelsPerTick)
        {
            return new TimelineCoordinateSystem(
                pixelsPerTick,
                (int)Math.Round(DefaultRowHeight * _noteVisualScale),
                DefaultHeaderWidth,
                DefaultRulerHeight,
                dpiScale);
        }

        private static double WheelZoomFactor(int delta)
        {
            return Math.Pow(1.0015, delta);
        }

        private void RequestRepaint()
        {
            if (IsPlaybackActive &&
                !_playbackFrames.ShouldRenderAt(_playbackClock.ElapsedMilliseconds))
            {
                return;
            }
            Invalidate();
        }

        private static int[] BuildMinimapDensity(
            TimelineProjectionResult projection,
            int documentEnd)
        {
            var density = new int[MinimapBucketCount];
            foreach (TimelineItem item in projection.Items)
            {
                int bucket = Math.Min(
                    density.Length - 1,
                    Math.Max(0, (int)((long)item.StartTick * density.Length / Math.Max(1, documentEnd))));
                density[bucket]++;
            }
            return density;
        }

        private static string BuildStatusText(EditorDocumentContext document)
        {
            string format = document.Capabilities.SourceFormat.HasValue
                ? (document.Capabilities.SourceFormat.Value == DJMaxEditor.Files.FormatDetection.ChartFormat.TrailerRespectV
                    ? "Technika Q"
                    : document.Capabilities.SourceFormat.Value.ToString())
                : "Unknown format";
            string encryption = document.Capabilities.IsEncrypted
                ? " | ENCRYPTED SOURCE / DECRYPTED IN MEMORY"
                : string.Empty;
            string lockState = document.Capabilities.IsReadOnly
                ? " | " + document.Capabilities.StatusLabel
                : (document.Capabilities.IsRespectV
                    ? " | " + document.Capabilities.StatusLabel + " | TIMELINE V2 VIEW ONLY"
                    : " | TIMELINE V2 VIEW ONLY");
            return format + encryption + " | RAW TRACKS" + lockState;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
