namespace DJMaxEditor.Panels {
    partial class EventsListForm {
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
            this.EventsList = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.EventsList)).BeginInit();
            this.SuspendLayout();
            // 
            // EventsList
            // 
            this.EventsList.AllowUserToAddRows = false;
            this.EventsList.AllowUserToDeleteRows = false;
            this.EventsList.AllowUserToResizeRows = false;
            this.EventsList.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.EventsList.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.EventsList.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleVertical;
            this.EventsList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.EventsList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.EventsList.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.EventsList.Location = new System.Drawing.Point(0, 0);
            this.EventsList.Margin = new System.Windows.Forms.Padding(2);
            this.EventsList.MultiSelect = false;
            this.EventsList.Name = "EventsList";
            this.EventsList.ReadOnly = true;
            this.EventsList.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            this.EventsList.RowHeadersVisible = false;
            this.EventsList.RowTemplate.Height = 28;
            this.EventsList.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.EventsList.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.EventsList.Size = new System.Drawing.Size(238, 397);
            this.EventsList.TabIndex = 26;
            // 
            // EventsListForm
            // 
            this.AllowEndUserDocking = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(238, 397);
            this.Controls.Add(this.EventsList);
            this.DockAreas = ((WeifenLuo.WinFormsUI.Docking.DockAreas)((((((WeifenLuo.WinFormsUI.Docking.DockAreas.Float | WeifenLuo.WinFormsUI.Docking.DockAreas.DockLeft) 
            | WeifenLuo.WinFormsUI.Docking.DockAreas.DockRight) 
            | WeifenLuo.WinFormsUI.Docking.DockAreas.DockTop) 
            | WeifenLuo.WinFormsUI.Docking.DockAreas.DockBottom) 
            | WeifenLuo.WinFormsUI.Docking.DockAreas.Document)));
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.HideOnClose = true;
            this.Name = "EventsListForm";
            this.TabText = "Events list";
            this.Text = "EventsListForm";
            ((System.ComponentModel.ISupportInitialize)(this.EventsList)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView EventsList;
    }
}