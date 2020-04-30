using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DJMaxEditor 
{
    public partial class DebugOutput : ToolWindow 
    {
        public void Log(string message) 
        {
            DebugConsole.AppendText(message + "\n");
            DebugConsole.ScrollToCaret();
        }

        public DebugOutput() 
        {
            InitializeComponent();
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e) 
        {
            Clipboard.SetText(DebugConsole.SelectedText);
        }
    }
}
