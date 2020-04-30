using System.Windows.Forms;

namespace DJMaxEditor.Files.pt
{
    public partial class SaveSettingsDialog : Form
    {
        public bool EncryptFile
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

        public SaveSettingsDialog()
        {
            InitializeComponent();
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
