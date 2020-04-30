using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DJMaxEditor.Undo.Action;
using DJMaxEditor.Controls.Editor.Renderers;
using DJMaxEditor.DJMax;
using DJMaxEditor.Controls.Editor;
using DJMaxEditor.Controls.Editor.Handlers;

namespace DJMaxEditor 
{
    class EventSelectMode
    {
        public Point BlockSize = new Point(1, 1);

        public event EventDataHandler OnSelectEvents;

        public event ChangeEventDataPositionHandler OnChangeEventPosition;

        public event EventDataHandler OnDeleteEvent;

        public EventSelectMode(EditorControl editor, PlayerData playerData, EventsRenderer eventsRenderer)
        {
            m_playerData = playerData;
            m_editor = editor;
            m_eventsRenderer = eventsRenderer;
        }

        /// <summary>
        /// Delete all the current selection items
        /// </summary>
        public void DeleteSelection()
        {
            OnDeleteEvent?.Invoke(this, m_selectedObjects.ToArray());            
            m_selectedObjects.Clear();
            m_editor.Repaint();
        }

        /// <summary>
        /// Select all event in a track
        /// </summary>
        /// <param name="trackData"></param>
        public void SelectTrack(TrackData trackData)
        {
            if (trackData == null)
            {
                return;
            }

            m_selectedObjects.Clear();

            foreach (EventData eventData in trackData.Events)
            {
                m_selectedObjects.Add(eventData);
            }
        }

        /// <summary>
        /// Select all events
        /// </summary>
        public void SelectAll()
        {
            m_selectedObjects.Clear();

            foreach (EventData eventData in m_playerData.Tracks.Events)
            {
                m_selectedObjects.Add(eventData);
            }

            m_editor.Redraw();
        }

        public void InvertSelection()
        {
            var currentSelection = new List<EventData>(m_selectedObjects.ToArray());

            m_selectedObjects.Clear();

            foreach (EventData eventData in m_playerData.Tracks.Events)
            {
                if (currentSelection.Contains(eventData))
                {
                    continue;
                }

                m_selectedObjects.Add(eventData);
            }
        }

        public bool ToggleSelectionAtPos(int x1, int y1, int x2, int y2, bool singleFile = false, bool tootle = false)
        {
            if (false == tootle)
            {
                m_selectedObjects.Clear();
            }

            if (x1 > x2)
            {
                int aux = x1;
                x1 = x2;
                x2 = aux;
            }
            if (y1 > y2)
            {
                int aux = y1;
                y1 = y2;
                y2 = aux;
            }

            Rectangle r = new Rectangle(x1, y1, x2 - x1, y2 - y1);
            m_selectionRectangle = r;

            bool didSelect = false;
            foreach (EventData o in m_playerData.Tracks.Events)
            {
                bool res = selectIfInside(o, r);
                didSelect |= res;
                if (singleFile && res)
                {
                    return didSelect;
                }
            }

            return didSelect;
        }

        public EventData GetEventAtPos(int x, int y)
        {
            Rectangle rect = new Rectangle(x, y, 1, 1);
            foreach (EventData o in m_playerData.Tracks.Events)
            {
                Rectangle eventRectangle = m_eventsRenderer.GetEventRectangle(o, (int)o.TrackId * EventsRenderer.VirtualTrackheight);
                if (eventRectangle.IntersectsWith(rect))
                {
                    return o;
                }
            }

            return null;
        }

        public void Render(GraphicsWrapper g)
        {
            if (m_drag.Active && m_selectMode)
            {
                g.FillRectangle(tbrush, m_selectionRectangle);
                g.DrawRectangle(Pens.LightBlue, m_selectionRectangle);
            }

            var eventsRenderer = m_eventsRenderer;
            foreach (EventData o in m_selectedObjects)
            {
                Rectangle rec = eventsRenderer.GetEventRectangle(o, (int)o.TrackId * EventsRenderer.VirtualTrackheight);
                g.FillRectangle(tbrush, rec);
                g.DrawRectangle(selection_border, rec);
            }
        }

        public int EvaluateBlock(int value, bool x)
        {
            int divBy = Math.Max(1, x ? BlockSize.X : BlockSize.Y);
            return (int)Math.Truncate((decimal)((value + (x ? (divBy / 2) : 0) ) / divBy) * divBy);        
        }

        public void MouseDoubleClick(int x, int y, MouseButtons buttons)
        {
            var trackData = GetTrackAtPos(x, y);
            SelectTrack(trackData);
        }

        public TrackData GetTrackAtPos(int x, int y)
        {
            Rectangle rect = new Rectangle(x, y, 1, 1);
            foreach (TrackData trackData in m_playerData.Tracks)
            {
                Rectangle rectangle = m_eventsRenderer.GetTrackRectangle(trackData, m_playerData.VirtualMaxTick);
                if (rect.IntersectsWith(rectangle))
                {
                    return trackData;
                }
            }
            return null;
        }

        public bool MouseDown(int x, int y, MouseButtons buttons, bool toogle = false)
        {
            m_drag.Start(x, y, EvaluateBlock(x, true), EvaluateBlock(y, false));

            bool didToggledAtPos = false;

            if (m_selectMode || toogle)
            {
                didToggledAtPos = ToggleSelectionAtPos(x, y, x, y, true, toogle);
            }

            m_selectMode = m_selectedObjects.Count == 0;

            m_editor.Cursor = GetCursorAtPos(x, y);

            return didToggledAtPos;
        }

        public void MouseUp()
        {
            m_drag.Stop();
            OnSelectEvents?.Invoke(this, m_selectedObjects.ToArray());
        }

        public void MouseMove(int x, int y)
        {
            m_editor.Cursor = GetCursorAtPos(x, y);
            m_selectMode = GetActionAtPos(x, y) == 0;
        }

        public void ClearSelection()
        {
            m_selectedObjects.Clear();
        }

        public void MouseDrag(int x, int y)
        {
            if (m_drag.Active && m_selectMode)
            {

                DoSelection(m_drag.X, x, m_drag.Y, y);

            }
            else if (m_drag.Active)
            {

                //if (_selectedObjects.Count == 1) {

                int vx = EvaluateBlock(x, true);
                int vy = EvaluateBlock(y, false);

                int xDelta = vx - m_drag.LX;
                int yDelta = vy - m_drag.LY;

                if (xDelta != 0 || yDelta != 0)
                {
                    int position = xDelta;
                    int track = 0;
                    if (yDelta != 0)
                    {
                        track = yDelta > 0 ? 1 : -1;
                    }
                    OnChangeEventPosition(m_selectedObjects, track, position);
                }
                
                m_drag.LX = EvaluateBlock(x, true);
                m_drag.LY = EvaluateBlock(y, false);
            }
        }

        /// <summary>
        /// Select mode
        /// </summary>
        private bool m_selectMode = false;

        /// <summary>
        /// instance of editor control
        /// </summary>
        private EditorControl m_editor;

        /// <summary>
        /// Instance of PlayerData
        /// </summary>
        private PlayerData m_playerData;

        /// <summary>
        /// Selection Rectangle
        /// </summary>
        private Rectangle m_selectionRectangle = new Rectangle();

        /// <summary>
        /// Drag n Drop element
        /// </summary>
        private Drag m_drag = new Drag();

        /// <summary>
        /// List of selected objects
        /// </summary>
        private List<EventData> m_selectedObjects = new List<EventData>();

        private Brush tbrush = new SolidBrush(Color.FromArgb(60, 183, 209, 238));

        private Pen selection_border = new Pen(Color.FromArgb(255, 51, 153, 255));

        private void RenderSelectedObject(EventData o, Graphics g) { }

        private EventsRenderer m_eventsRenderer;

        private bool selectIfInside(EventData it, Rectangle r)
        {
            if (it == null)
            {
                return false;
            }

            Rectangle eventRectangle = this.m_eventsRenderer.GetEventRectangle(it, (int)it.TrackId * EventsRenderer.VirtualTrackheight);

            if (r.IntersectsWith(eventRectangle))
            {
                if (m_selectedObjects.Contains(it))
                {
                    m_selectedObjects.Remove(it);
                }
                else
                {
                    m_selectedObjects.Add(it);
                }
                
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Get
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private int GetActionAtPos(int x, int y)
        {
            foreach (EventData o in m_selectedObjects)
            {
                Rectangle r = new Rectangle(x, y, 0, 0);

                Rectangle eventRectangle = this.m_eventsRenderer.GetEventRectangle(o, (int)o.TrackId * EventsRenderer.VirtualTrackheight);
                if (r.IntersectsWith(eventRectangle)) {
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// Return cursor at pos x, y
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private Cursor GetCursorAtPos(int x, int y)
        {
            foreach (EventData o in m_selectedObjects)
            {
                Rectangle r = new Rectangle(x, y, 0, 0);

                Rectangle eventRectangle = this.m_eventsRenderer.GetEventRectangle(o, (int)o.TrackId * EventsRenderer.VirtualTrackheight);
                

                if (r.IntersectsWith(eventRectangle))
                {
                    return Cursors.SizeAll;
                }
            }

            return Cursors.Default;
        }
        
        /// <summary>
        /// Do selection from position x1, x2, y1, y2
        /// </summary>
        /// <param name="x1"></param>
        /// <param name="x2"></param>
        /// <param name="y1"></param>
        /// <param name="y2"></param>
        private void DoSelection(int x1, int x2, int y1, int y2)
        {
            ToggleSelectionAtPos(x1, y1, x2, y2);
        }
    }
}
