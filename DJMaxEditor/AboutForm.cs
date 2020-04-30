using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Deployment.Application;
using System.Diagnostics;

namespace DJMaxEditor {
    public partial class AboutForm : Form {
        public AboutForm() {
            InitializeComponent();

            //label1.Parent = label2.Parent = pictureBox1;
            //label1.BackColor = label2.BackColor = Color.Transparent;

            var version = Assembly.GetEntryAssembly().GetName().Version;

            var buildDateTime = new DateTime(2000, 1, 1).Add(new TimeSpan(
            TimeSpan.TicksPerDay * version.Build + // days since 1 January 2000
            TimeSpan.TicksPerSecond * 2 * version.Revision)); // seconds since midnight, (multiply by 2 to get original)

            label1.Text = "DJMax Editor - build " + buildDateTime.ToString();

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);

            //this.label3.Text = String.Format("Version {0}", version.ToString());

        }

        private void button1_Click(object sender, EventArgs e) {
            this.Close();
        }

        private void button1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 27) 
            {
                this.Close();
            }
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(@"https://github.com/hsreina/DJMax-editor");
        }
    }
}
