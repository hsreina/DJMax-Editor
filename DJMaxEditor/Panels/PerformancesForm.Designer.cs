namespace DJMaxEditor.Panels
{
    partial class PerformancesForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.DebugInfoList = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.DebugInfoList)).BeginInit();
            this.SuspendLayout();
            // 
            // DebugInfoList
            // 
            this.DebugInfoList.AllowUserToAddRows = false;
            this.DebugInfoList.AllowUserToDeleteRows = false;
            this.DebugInfoList.AllowUserToResizeRows = false;
            this.DebugInfoList.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.DebugInfoList.BackgroundColor = System.Drawing.SystemColors.ButtonHighlight;
            this.DebugInfoList.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.DebugInfoList.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.DebugInfoList.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.DebugInfoList.ColumnHeadersVisible = false;
            this.DebugInfoList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.DebugInfoList.EditMode = System.Windows.Forms.DataGridViewEditMode.EditProgrammatically;
            this.DebugInfoList.Location = new System.Drawing.Point(0, 0);
            this.DebugInfoList.Name = "DebugInfoList";
            this.DebugInfoList.ReadOnly = true;
            this.DebugInfoList.RowHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            this.DebugInfoList.RowHeadersVisible = false;
            this.DebugInfoList.RowTemplate.Height = 28;
            this.DebugInfoList.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.DebugInfoList.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.DebugInfoList.Size = new System.Drawing.Size(263, 331);
            this.DebugInfoList.TabIndex = 27;
            // 
            // PerformancesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(263, 331);
            this.Controls.Add(this.DebugInfoList);
            this.Name = "PerformancesForm";
            this.Text = "PerformancesForm";
            ((System.ComponentModel.ISupportInitialize)(this.DebugInfoList)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView DebugInfoList;
    }
}