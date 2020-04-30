using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace DJMaxEditor 
{
    public partial class EditorForm : DockContent 
    {
        public string Title {
            get {
                return this.Text;
            }
            set {
                this.Text = value;
            }
        }

        public EditorForm() 
        {
            InitializeComponent();
        }

        public EditorControl Editor 
        {
            get
            {
                return editorControl1;
            }
        }

        private void EditorForm_Resize(object sender, EventArgs e) 
        {
            editorControl1.Invalidate();
        }

        public void SelectAll()
        {
            editorControl1.SelectAll();
        }

        public void Deselect()
        {
            editorControl1.Deselect();
        }

        public void InverseSelection()
        {
            editorControl1.InverseSelection();
        }
    }
}
