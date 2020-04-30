using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace DJMaxEditor 
{
    public partial class ToolWindow : DockContent 
    {
        public ToolWindow() {
            InitializeComponent();
        }

        private void ToolWindow_FormClosing(object sender, FormClosingEventArgs e) 
        {
            this.Hide();
            e.Cancel = true; // this cancels the close event.
        }
    }
}