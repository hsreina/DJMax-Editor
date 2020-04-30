using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DJMaxEditor {
    public partial class LoadingForm : Form {

        public LoadingForm() {
            InitializeComponent();
        }


        public string DisplayedMessage {

            set {
                label1.Text = value;
            }

            get {
                return label1.Text;
            }
        }

    }
}
