
namespace eDocGenUI
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.maskGrpLbl = new System.Windows.Forms.Label();
            this.maskGrpCmb = new System.Windows.Forms.ComboBox();
            this._uploadUMCBtn = new System.Windows.Forms.Button();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.progressBar1);
            this.splitContainer1.Panel2.Controls.Add(this.maskGrpLbl);
            this.splitContainer1.Panel2.Controls.Add(this.maskGrpCmb);
            this.splitContainer1.Panel2.Controls.Add(this._uploadUMCBtn);
            this.splitContainer1.Panel2.Controls.Add(this.dataGridView1);
            this.splitContainer1.Size = new System.Drawing.Size(1734, 891);
            this.splitContainer1.SplitterDistance = 578;
            this.splitContainer1.TabIndex = 0;
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(697, 17);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(329, 29);
            this.progressBar1.TabIndex = 4;
            this.progressBar1.Visible = false;
            // 
            // maskGrpLbl
            // 
            this.maskGrpLbl.AutoSize = true;
            this.maskGrpLbl.Location = new System.Drawing.Point(38, 20);
            this.maskGrpLbl.Name = "maskGrpLbl";
            this.maskGrpLbl.Size = new System.Drawing.Size(88, 20);
            this.maskGrpLbl.TabIndex = 3;
            this.maskGrpLbl.Text = "Mask Group";
            // 
            // maskGrpCmb
            // 
            this.maskGrpCmb.FormattingEnabled = true;
            this.maskGrpCmb.Items.AddRange(new object[] {
            "Shasta",
            "Non-Shasta",
            "Turbo"});
            this.maskGrpCmb.Location = new System.Drawing.Point(146, 17);
            this.maskGrpCmb.Name = "maskGrpCmb";
            this.maskGrpCmb.Size = new System.Drawing.Size(202, 28);
            this.maskGrpCmb.TabIndex = 2;
            this.maskGrpCmb.SelectedValueChanged += new System.EventHandler(this.maskGrpCmb_SelectedValueChanged);
            // 
            // _uploadUMCBtn
            // 
            this._uploadUMCBtn.Location = new System.Drawing.Point(1032, 13);
            this._uploadUMCBtn.Name = "_uploadUMCBtn";
            this._uploadUMCBtn.Size = new System.Drawing.Size(108, 34);
            this._uploadUMCBtn.TabIndex = 1;
            this._uploadUMCBtn.Text = "UMC Upload";
            this._uploadUMCBtn.UseVisualStyleBackColor = true;
            this._uploadUMCBtn.Click += new System.EventHandler(this._uploadUMCBtn_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(3, 67);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowHeadersWidth = 51;
            this.dataGridView1.RowTemplate.Height = 29;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView1.Size = new System.Drawing.Size(1146, 821);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellDoubleClick);
            this.dataGridView1.DataBindingComplete += new System.Windows.Forms.DataGridViewBindingCompleteEventHandler(this.dataGridView1_DataBindingComplete);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1734, 891);
            this.Controls.Add(this.splitContainer1);
            this.Name = "Form1";
            this.Text = "eDoc Generator UI";
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        public System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.Label maskGrpLbl;
        private System.Windows.Forms.ComboBox maskGrpCmb;
        private System.Windows.Forms.Button _uploadUMCBtn;
        private System.Windows.Forms.ProgressBar progressBar1;
    }
}

