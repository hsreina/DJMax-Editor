namespace DJMaxEditor {
    partial class DebugOutput {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DebugOutput));
            this.DebugConsole = new System.Windows.Forms.RichTextBox();
            this.ConsoleMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.copyToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ConsoleMenu.SuspendLayout();
            this.SuspendLayout();
            // 
            // DebugConsole
            // 
            this.DebugConsole.BackColor = System.Drawing.SystemColors.Window;
            this.DebugConsole.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.DebugConsole.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.DebugConsole.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DebugConsole.Location = new System.Drawing.Point(0, 0);
            this.DebugConsole.Name = "DebugConsole";
            this.DebugConsole.ReadOnly = true;
            this.DebugConsole.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.ForcedVertical;
            this.DebugConsole.Size = new System.Drawing.Size(500, 159);
            this.DebugConsole.TabIndex = 1;
            this.DebugConsole.Text = "== DJMax Editor ==\n";
            // 
            // ConsoleMenu
            // 
            this.ConsoleMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.copyToolStripMenuItem});
            this.ConsoleMenu.Name = "ConsoleMenu";
            this.ConsoleMenu.Size = new System.Drawing.Size(103, 26);
            // 
            // copyToolStripMenuItem
            // 
            this.copyToolStripMenuItem.Name = "copyToolStripMenuItem";
            this.copyToolStripMenuItem.Size = new System.Drawing.Size(102, 22);
            this.copyToolStripMenuItem.Text = "Copy";
            this.copyToolStripMenuItem.Click += new System.EventHandler(this.copyToolStripMenuItem_Click);
            // 
            // DebugOutput
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(500, 159);
            this.ControlBox = false;
            this.Controls.Add(this.DebugConsole);
            this.DockAreas = WeifenLuo.WinFormsUI.Docking.DockAreas.DockBottom;
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.Name = "DebugOutput";
            this.TabText = "DebugOutput";
            this.Text = "DebugOutput";
            this.ConsoleMenu.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox DebugConsole;
        private System.Windows.Forms.ContextMenuStrip ConsoleMenu;
        private System.Windows.Forms.ToolStripMenuItem copyToolStripMenuItem;
    }
}