using System;
using System.Windows.Forms;

namespace DJMaxEditor.Files.bms
{
    public partial class SaveSettingsDialog : Form
    {
        public SaveSettingsDialog()
        {
            InitializeComponent();
        }

        private void CloseWithResult(DialogResult dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            CloseWithResult(DialogResult.OK);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            CloseWithResult(DialogResult.Cancel);
        }
    }
}
