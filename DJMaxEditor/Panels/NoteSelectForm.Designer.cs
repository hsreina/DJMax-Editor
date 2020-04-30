namespace DJMaxEditor.Panels {
    partial class NoteSelectForm {
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(NoteSelectForm));
            this.AvailableNotesList = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.AvailableNotesList)).BeginInit();
            this.SuspendLayout();
            // 
            // AvailableNotesList
            // 
            this.AvailableNotesList.AllowUserToAddRows = false;
            this.AvailableNotesList.AllowUserToDeleteRows = false;
            this.AvailableNotesList.AllowUserToResizeColumns = false;
            this.AvailableNotesList.AllowUserToResizeRows = false;
            this.AvailableNotesList.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.AvailableNotesList.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCells;
            this.AvailableNotesList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.AvailableNotesList.ColumnHeadersVisible = false;
            this.AvailableNotesList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.AvailableNotesList.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.AvailableNotesList.Location = new System.Drawing.Point(0, 0);
            this.AvailableNotesList.Margin = new System.Windows.Forms.Padding(2);
            this.AvailableNotesList.MultiSelect = false;
            this.AvailableNotesList.Name = "AvailableNotesList";
            this.AvailableNotesList.ReadOnly = true;
            this.AvailableNotesList.RowHeadersVisible = false;
            this.AvailableNotesList.RowTemplate.Height = 28;
            this.AvailableNotesList.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.AvailableNotesList.Size = new System.Drawing.Size(171, 348);
            this.AvailableNotesList.TabIndex = 0;
            this.AvailableNotesList.CellToolTipTextNeeded += new System.Windows.Forms.DataGridViewCellToolTipTextNeededEventHandler(this.AvailableNotesList_CellToolTipTextNeeded);
            this.AvailableNotesList.SelectionChanged += new System.EventHandler(this.AvailableNotesList_SelectionChanged);
            this.AvailableNotesList.DragDrop += new System.Windows.Forms.DragEventHandler(this.AvailableNotesList_DragDrop);
            this.AvailableNotesList.DragOver += new System.Windows.Forms.DragEventHandler(this.AvailableNotesList_DragOver);
            this.AvailableNotesList.MouseDown += new System.Windows.Forms.MouseEventHandler(this.AvailableNotesList_MouseDown);
            this.AvailableNotesList.MouseMove += new System.Windows.Forms.MouseEventHandler(this.AvailableNotesList_MouseMove);
            // 
            // NoteSelectForm
            // 
            this.AllowEndUserDocking = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(171, 348);
            this.Controls.Add(this.AvailableNotesList);
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)(((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.DockLeft) 
            | WeifenLuo.WinFormsUI.Docking.DockAreas.DockRight)));
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HideOnClose = true;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(6, 8, 6, 8);
            this.Name = "NoteSelectForm";
            this.ShowInTaskbar = false;
            this.TabText = "Notes";
            this.Text = "NoteSelect";
            ((System.ComponentModel.ISupportInitialize)(this.AvailableNotesList)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView AvailableNotesList;
    }
}