using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using DJMaxEditor.Controls.Editor.Renderers;
using DJMaxEditor.Controls.Editor.Renderers.Events;
using DJMaxEditor.DJMax;
using DJMaxEditor.Controls.Editor;

namespace DJMaxEditor.Panels 
{
    public partial class NoteSelectForm : ToolWindow 
    {
        public delegate void OnSelectDataEventDelegate(EventData eventData);

        public event OnSelectDataEventDelegate OnSelectDataEvent;

        private EventsRenderer m_eventsRenderer;

        private GraphicsWrapper m_gw = new GraphicsWrapper();

        private NoteListContent CreateNoteFromEventData(string name, EventData eventData)
        {
            NoteListContent basicNote = new NoteListContent();

            basicNote.Description = name;

            basicNote.NotePreview = new Bitmap(_SupportSize.X, _SupportSize.Y);

            var g = Graphics.FromImage(basicNote.NotePreview);

            var gw = m_gw;
            gw.UpdateGraphics(g);

            g.ScaleTransform(0.5f, 0.5f, MatrixOrder.Prepend);
            m_eventsRenderer.RenderEventDataAtInRect(gw, eventData, new Rectangle(0, 0, 90, 90), 70, 70);
            basicNote.EventData = eventData;

            return basicNote;
        }

        public void ApplyTheme(IEventRenderer theme)
        {
            if (null == theme)
            {
                return;
            }

            m_notes.Clear();

            foreach (var keyvalue in theme.GetTemplates())
            {
                m_notes.Add(this.CreateNoteFromEventData(
                    keyvalue.Key,
                    keyvalue.Value
                ));
            }

            AvailableNotesList.DataSource = m_notes;
        }

        internal NoteSelectForm(EventsRenderer eventsRenderer) 
            : base()
        {
            InitializeComponent();

            m_eventsRenderer = eventsRenderer;
        }

        public void SelectEvent(byte index) 
        {
            AvailableNotesList.Rows[index].Selected = true; ;
        }

        private Rectangle dragBoxFromMouseDown;

        private int rowIndexFromMouseDown;

        private int rowIndexOfItemUnderMouseToDrop;

        private BindingList<NoteListContent> m_notes = new BindingList<NoteListContent>();

        private Rectangle _rect = new Rectangle(0, 0, 0, 5);

        private Point _SupportSize = new Point(70, 70);

        private void AvailableNotesList_SelectionChanged(object sender, EventArgs e) 
        {
            if (sender is DataGridView) {

                DataGridViewSelectedRowCollection selectedRows = (sender as DataGridView).SelectedRows;

                if (selectedRows != null && selectedRows.Count > 0) {

                    DataGridViewRow selectedRow = selectedRows[0];

                    NoteListContent noteListContent = (selectedRow.DataBoundItem as NoteListContent);

                    if (noteListContent != null && OnSelectDataEvent != null) {
                        OnSelectDataEvent(noteListContent.EventData);
                    }
                }
            }
        }

        private void AvailableNotesList_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e) 
        {
            DataGridView datagridView = sender as DataGridView;

            if (datagridView == null) { return; }

            string newLine = Environment.NewLine;
            if (e.RowIndex > -1) {

                DataGridViewRow dataGridViewRow1 = datagridView.Rows[e.RowIndex];

                NoteListContent noteListContent = dataGridViewRow1.DataBoundItem as NoteListContent;

                if (noteListContent != null) {
                    e.ToolTipText = noteListContent.Description;
                }
            }
        }

        private void AvailableNotesList_MouseMove(object sender, MouseEventArgs e) 
        {
            if ((e.Button & MouseButtons.Left) == MouseButtons.Left) {
                // If the mouse moves outside the rectangle, start the drag.
                if (dragBoxFromMouseDown != Rectangle.Empty &&
                !dragBoxFromMouseDown.Contains(e.X, e.Y)) {
                    // Proceed with the drag and drop, passing in the list item.                    
                    DragDropEffects dropEffect = AvailableNotesList.DoDragDrop(
                          AvailableNotesList.Rows[rowIndexFromMouseDown],
                          DragDropEffects.Move);
                }
            }
        }

        private void AvailableNotesList_MouseDown(object sender, MouseEventArgs e) 
        { }

        private void AvailableNotesList_DragOver(object sender, DragEventArgs e) 
        {
            e.Effect = DragDropEffects.Move;
        }

        private void AvailableNotesList_DragDrop(object sender, DragEventArgs e) 
        {
            // The mouse locations are relative to the screen, so they must be 
            // converted to client coordinates.
            Point clientPoint = AvailableNotesList.PointToClient(new Point(e.X, e.Y));

            // Get the row index of the item the mouse is below. 
            rowIndexOfItemUnderMouseToDrop = AvailableNotesList.HitTest(clientPoint.X, clientPoint.Y).RowIndex;

            // If the drag operation was a move then remove and insert the row.
            if (e.Effect == DragDropEffects.Move) {
                DataGridViewRow rowToMove = e.Data.GetData(typeof(DataGridViewRow)) as DataGridViewRow;
                AvailableNotesList.Rows.RemoveAt(rowIndexFromMouseDown);
                AvailableNotesList.Rows.Insert(rowIndexOfItemUnderMouseToDrop, rowToMove);
            }
        }
    }

    public class NoteListContent 
    {
        public Bitmap NotePreview { get; set; }

        [Browsable(false)]
        public EventData EventData { get; set; }

        [Browsable(false)]
        public String Description { get; set; }
    }
}
