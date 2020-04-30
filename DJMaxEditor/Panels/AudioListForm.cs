using DJMaxEditor.DJMax;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DJMaxEditor 
{
    public partial class AudioListForm : ToolWindow 
    {
        public delegate void ButtonClickedDelegate(object sender);

        public delegate void InstrumentChangedDelegate(object sender, InstrumentData instrumentData, string filename);

        public ButtonClickedDelegate OnPlayPressed { get; set; }

        public ButtonClickedDelegate OnStopPressed { get; set; }

        public InstrumentChangedDelegate OnInstrumentChanged { get; set; }

        public DataGridView List {
            get {
                return SoundList;
            }
            set{
                SoundList = value;
            }
        }

        public AudioListForm() 
        {
            InitializeComponent();
        }

        public InstrumentData GetCurrentSelection()
        {
            DataGridViewSelectedRowCollection rows = SoundList.SelectedRows;

            if (rows == null || rows.Count == 0)
            {
                return null;
            }

            DataGridViewRow selectedRow = rows[0];

            if (selectedRow == null)
            {
                return null;
            }

            return selectedRow.DataBoundItem as InstrumentData;
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            TriggerPlayPressed();
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            OnStopPressed?.Invoke(this);
        }

        private void SoundList_DoubleClick(object sender, EventArgs e)
        {
            TriggerPlayPressed();
        }

        private void TriggerPlayPressed() 
        {
            OnPlayPressed?.Invoke(this);
        }

        private void SoundList_MouseDown(object sender, MouseEventArgs e)
        {
            DataGridView dataGridView = sender as DataGridView;
            if (null == dataGridView)
            {
                return;
            }

            if (e.Button == MouseButtons.Right)
            {
                var hti = dataGridView.HitTest(e.X, e.Y);
                dataGridView.ClearSelection();

                int rowIndex = hti.RowIndex;
                var rows = dataGridView.Rows;
                if (rowIndex > -1 && rowIndex < rows.Count)
                {
                    rows[rowIndex].Selected = true;
                }
            }
        }

        private void changeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var currentSelection = GetCurrentSelection();

            if (null == currentSelection)
            {
                return;
            }

            if (currentSelection.Name == "none")
            {
                return;
            }

            DialogResult result = openFileDialog1.ShowDialog();
            if (result == DialogResult.OK)
            {
                OnInstrumentChanged.Invoke(this, currentSelection, openFileDialog1.FileName);
            }
        }
    }
}
