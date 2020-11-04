using System.Windows.Forms;

namespace DJMaxEditor.Files.bytes
{
    public partial class OpenSettingsDialog : Form
    {
        public OpenSettingsDialog()
        {
            InitializeComponent();
        }

        public bool RenameInst
        {
            get
            {
                return checkBox1.Checked;
            }
            set
            {
                checkBox1.Checked = value;
            }
        }

        private void button2_Click(object sender, System.EventArgs e)
        {
            CloseWithResult(DialogResult.OK);
        }

        private void button1_Click(object sender, System.EventArgs e)
        {
            CloseWithResult(DialogResult.Cancel);
        }

        private void CloseWithResult(DialogResult dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }
    }
}
