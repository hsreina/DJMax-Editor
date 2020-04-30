namespace DJMaxEditor {
    partial class EditorForm {
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
            this.editorControl1 = new DJMaxEditor.EditorControl();
            this.SuspendLayout();
            // 
            // editorControl1
            // 
            this.editorControl1.BackColor = System.Drawing.SystemColors.ControlDark;
            this.editorControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.editorControl1.Location = new System.Drawing.Point(0, 0);
            this.editorControl1.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
            this.editorControl1.Name = "editorControl1";
            this.editorControl1.NoteValue = 8;
            this.editorControl1.Size = new System.Drawing.Size(496, 421);
            this.editorControl1.TabIndex = 0;
            // 
            // EditorForm
            // 
            this.AllowEndUserDocking = false;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(496, 421);
            this.CloseButton = false;
            this.CloseButtonVisible = false;
            this.Controls.Add(this.editorControl1);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Name = "EditorForm";
            this.Text = "New Document";
            this.Resize += new System.EventHandler(this.EditorForm_Resize);
            this.ResumeLayout(false);

        }

        #endregion

        private EditorControl editorControl1;
    }
}