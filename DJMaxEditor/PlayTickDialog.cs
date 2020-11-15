using System;
using System.Windows.Forms;

namespace DJMaxEditor
{
    public partial class PlayTickDialog : Form
    {
        public string SetTick
        {
            get
            {
                return textBox1.Text;
            }
            set
            {
                textBox1.Text = value;
            }
        }
        public PlayTickDialog(int tick)
        {
            InitializeComponent();
            SetTick = tick.ToString();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            CloseWithResult(DialogResult.Cancel);
        }

        private void CloseWithResult(DialogResult dialogResult)
        {
            DialogResult = dialogResult;
            Close();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            CloseWithResult(DialogResult.OK);
        }
    }
}
