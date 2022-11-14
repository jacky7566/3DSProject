
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this._esUplBtn = new System.Windows.Forms.Button();
            this._esReUplLbl = new System.Windows.Forms.Label();
            this._esUplCmb = new System.Windows.Forms.ComboBox();
            this._esUpdTime = new System.Windows.Forms.Label();
            this._esRefreshCkb = new System.Windows.Forms.CheckBox();
            this._esNumUpDown = new System.Windows.Forms.NumericUpDown();
            this._drLbl = new System.Windows.Forms.Label();
            this._esMaskCmb = new System.Windows.Forms.ComboBox();
            this._esMaskLbl = new System.Windows.Forms.Label();
            this._esMaskGrpLbl = new System.Windows.Forms.Label();
            this._esMaskGrpCmb = new System.Windows.Forms.ComboBox();
            this.panel2 = new System.Windows.Forms.Panel();
            this._flLsl = new System.Windows.Forms.Label();
            this._esFailGdv = new System.Windows.Forms.DataGridView();
            this.panel1 = new System.Windows.Forms.Panel();
            this._psLbl = new System.Windows.Forms.Label();
            this._passDgv = new System.Windows.Forms.DataGridView();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.maskGrpLbl = new System.Windows.Forms.Label();
            this.maskGrpCmb = new System.Windows.Forms.ComboBox();
            this._uploadUMCBtn = new System.Windows.Forms.Button();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this._esTimer = new System.Windows.Forms.Timer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._esNumUpDown)).BeginInit();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._esFailGdv)).BeginInit();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this._passDgv)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // splitContainer1
            // 
            this.splitContainer1.Cursor = System.Windows.Forms.Cursors.VSplit;
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this._esUplBtn);
            this.splitContainer1.Panel1.Controls.Add(this._esReUplLbl);
            this.splitContainer1.Panel1.Controls.Add(this._esUplCmb);
            this.splitContainer1.Panel1.Controls.Add(this._esUpdTime);
            this.splitContainer1.Panel1.Controls.Add(this._esRefreshCkb);
            this.splitContainer1.Panel1.Controls.Add(this._esNumUpDown);
            this.splitContainer1.Panel1.Controls.Add(this._drLbl);
            this.splitContainer1.Panel1.Controls.Add(this._esMaskCmb);
            this.splitContainer1.Panel1.Controls.Add(this._esMaskLbl);
            this.splitContainer1.Panel1.Controls.Add(this._esMaskGrpLbl);
            this.splitContainer1.Panel1.Controls.Add(this._esMaskGrpCmb);
            this.splitContainer1.Panel1.Controls.Add(this.panel2);
            this.splitContainer1.Panel1.Controls.Add(this.panel1);
            this.splitContainer1.Panel1.Cursor = System.Windows.Forms.Cursors.Arrow;
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.progressBar1);
            this.splitContainer1.Panel2.Controls.Add(this.maskGrpLbl);
            this.splitContainer1.Panel2.Controls.Add(this.maskGrpCmb);
            this.splitContainer1.Panel2.Controls.Add(this._uploadUMCBtn);
            this.splitContainer1.Panel2.Controls.Add(this.dataGridView1);
            this.splitContainer1.Panel2.Cursor = System.Windows.Forms.Cursors.Default;
            this.splitContainer1.Size = new System.Drawing.Size(1508, 668);
            this.splitContainer1.SplitterDistance = 621;
            this.splitContainer1.TabIndex = 0;
            // 
            // _esUplBtn
            // 
            this._esUplBtn.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this._esUplBtn.Location = new System.Drawing.Point(516, 609);
            this._esUplBtn.Name = "_esUplBtn";
            this._esUplBtn.Size = new System.Drawing.Size(75, 23);
            this._esUplBtn.TabIndex = 15;
            this._esUplBtn.Text = "Upload";
            this._esUplBtn.UseVisualStyleBackColor = true;
            this._esUplBtn.Click += new System.EventHandler(this._esUplBtn_Click);
            // 
            // _esReUplLbl
            // 
            this._esReUplLbl.AutoSize = true;
            this._esReUplLbl.Location = new System.Drawing.Point(14, 612);
            this._esReUplLbl.Name = "_esReUplLbl";
            this._esReUplLbl.Size = new System.Drawing.Size(98, 15);
            this._esReUplLbl.TabIndex = 14;
            this._esReUplLbl.Text = "Upload AVI2 Path";
            // 
            // _esUplCmb
            // 
            this._esUplCmb.FormattingEnabled = true;
            this._esUplCmb.Items.AddRange(new object[] {
            "\\\\tw2smbl1.lumentuminc.net\\Data\\Data\\eDocTest\\AVI2\\vi2",
            "\\\\tw2smbl1.lumentuminc.net\\Data\\Data\\RW_Map\\AVI2",
            "\\\\tw2smbl1.lumentuminc.net\\Data\\Data\\RW_Map\\AVI2_SPECIAL"});
            this._esUplCmb.Location = new System.Drawing.Point(118, 609);
            this._esUplCmb.Name = "_esUplCmb";
            this._esUplCmb.Size = new System.Drawing.Size(382, 23);
            this._esUplCmb.TabIndex = 13;
            this._esUplCmb.SelectedValueChanged += new System.EventHandler(this._esUplCmb_SelectedValueChanged);
            // 
            // _esUpdTime
            // 
            this._esUpdTime.AutoSize = true;
            this._esUpdTime.Location = new System.Drawing.Point(0, 651);
            this._esUpdTime.Name = "_esUpdTime";
            this._esUpdTime.Size = new System.Drawing.Size(131, 15);
            this._esUpdTime.TabIndex = 12;
            this._esUpdTime.Text = "Last Updated Time:  NA";
            // 
            // _esRefreshCkb
            // 
            this._esRefreshCkb.AutoSize = true;
            this._esRefreshCkb.Location = new System.Drawing.Point(208, 46);
            this._esRefreshCkb.Name = "_esRefreshCkb";
            this._esRefreshCkb.Size = new System.Drawing.Size(166, 19);
            this._esRefreshCkb.TabIndex = 11;
            this._esRefreshCkb.Text = "Auto Refresh (every 1 Min)";
            this._esRefreshCkb.UseVisualStyleBackColor = true;
            this._esRefreshCkb.CheckedChanged += new System.EventHandler(this._esRefreshCkb_CheckedChanged);
            // 
            // _esNumUpDown
            // 
            this._esNumUpDown.Increment = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this._esNumUpDown.Location = new System.Drawing.Point(89, 45);
            this._esNumUpDown.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this._esNumUpDown.Minimum = new decimal(new int[] {
            50,
            0,
            0,
            0});
            this._esNumUpDown.Name = "_esNumUpDown";
            this._esNumUpDown.Size = new System.Drawing.Size(93, 23);
            this._esNumUpDown.TabIndex = 10;
            this._esNumUpDown.Value = new decimal(new int[] {
            50,
            0,
            0,
            0});
            // 
            // _drLbl
            // 
            this._drLbl.AutoSize = true;
            this._drLbl.Location = new System.Drawing.Point(12, 50);
            this._drLbl.Name = "_drLbl";
            this._drLbl.Size = new System.Drawing.Size(76, 15);
            this._drLbl.TabIndex = 9;
            this._drLbl.Text = "Display Rows";
            // 
            // _esMaskCmb
            // 
            this._esMaskCmb.FormattingEnabled = true;
            this._esMaskCmb.Location = new System.Drawing.Point(325, 17);
            this._esMaskCmb.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._esMaskCmb.Name = "_esMaskCmb";
            this._esMaskCmb.Size = new System.Drawing.Size(142, 23);
            this._esMaskCmb.TabIndex = 8;
            this._esMaskCmb.SelectedValueChanged += new System.EventHandler(this._esMaskCmb_SelectedValueChanged);
            // 
            // _esMaskLbl
            // 
            this._esMaskLbl.AutoSize = true;
            this._esMaskLbl.Location = new System.Drawing.Point(284, 21);
            this._esMaskLbl.Name = "_esMaskLbl";
            this._esMaskLbl.Size = new System.Drawing.Size(35, 15);
            this._esMaskLbl.TabIndex = 7;
            this._esMaskLbl.Text = "Mask";
            // 
            // _esMaskGrpLbl
            // 
            this._esMaskGrpLbl.AutoSize = true;
            this._esMaskGrpLbl.Location = new System.Drawing.Point(12, 20);
            this._esMaskGrpLbl.Name = "_esMaskGrpLbl";
            this._esMaskGrpLbl.Size = new System.Drawing.Size(71, 15);
            this._esMaskGrpLbl.TabIndex = 6;
            this._esMaskGrpLbl.Text = "Mask Group";
            // 
            // _esMaskGrpCmb
            // 
            this._esMaskGrpCmb.FormattingEnabled = true;
            this._esMaskGrpCmb.Items.AddRange(new object[] {
            "Shasta",
            "Non-Shasta",
            "Turbo"});
            this._esMaskGrpCmb.Location = new System.Drawing.Point(89, 17);
            this._esMaskGrpCmb.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._esMaskGrpCmb.Name = "_esMaskGrpCmb";
            this._esMaskGrpCmb.Size = new System.Drawing.Size(189, 23);
            this._esMaskGrpCmb.TabIndex = 5;
            this._esMaskGrpCmb.SelectedValueChanged += new System.EventHandler(this._esMaskGrpCmb_SelectedValueChanged);
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this._flLsl);
            this.panel2.Controls.Add(this._esFailGdv);
            this.panel2.Location = new System.Drawing.Point(4, 336);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(615, 250);
            this.panel2.TabIndex = 1;
            // 
            // _flLsl
            // 
            this._flLsl.AutoSize = true;
            this._flLsl.Location = new System.Drawing.Point(5, 15);
            this._flLsl.Name = "_flLsl";
            this._flLsl.Size = new System.Drawing.Size(92, 15);
            this._flLsl.TabIndex = 2;
            this._flLsl.Text = "Failed eDoc List:";
            // 
            // _esFailGdv
            // 
            this._esFailGdv.AllowUserToDeleteRows = false;
            this._esFailGdv.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._esFailGdv.Location = new System.Drawing.Point(5, 33);
            this._esFailGdv.Name = "_esFailGdv";
            this._esFailGdv.ReadOnly = true;
            this._esFailGdv.RowTemplate.Height = 25;
            this._esFailGdv.Size = new System.Drawing.Size(607, 214);
            this._esFailGdv.TabIndex = 2;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this._psLbl);
            this.panel1.Controls.Add(this._passDgv);
            this.panel1.Location = new System.Drawing.Point(4, 74);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(618, 256);
            this.panel1.TabIndex = 0;
            // 
            // _psLbl
            // 
            this._psLbl.AutoSize = true;
            this._psLbl.Location = new System.Drawing.Point(5, 15);
            this._psLbl.Name = "_psLbl";
            this._psLbl.Size = new System.Drawing.Size(84, 15);
            this._psLbl.TabIndex = 1;
            this._psLbl.Text = "Pass eDoc List:";
            // 
            // _passDgv
            // 
            this._passDgv.AllowUserToDeleteRows = false;
            this._passDgv.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this._passDgv.Location = new System.Drawing.Point(5, 33);
            this._passDgv.Name = "_passDgv";
            this._passDgv.ReadOnly = true;
            this._passDgv.RowTemplate.Height = 25;
            this._passDgv.Size = new System.Drawing.Size(610, 214);
            this._passDgv.TabIndex = 0;
            // 
            // progressBar1
            // 
            this.progressBar1.Location = new System.Drawing.Point(435, 15);
            this.progressBar1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(288, 22);
            this.progressBar1.TabIndex = 4;
            this.progressBar1.Visible = false;
            // 
            // maskGrpLbl
            // 
            this.maskGrpLbl.AutoSize = true;
            this.maskGrpLbl.Location = new System.Drawing.Point(33, 15);
            this.maskGrpLbl.Name = "maskGrpLbl";
            this.maskGrpLbl.Size = new System.Drawing.Size(71, 15);
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
            this.maskGrpCmb.Location = new System.Drawing.Point(128, 13);
            this.maskGrpCmb.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.maskGrpCmb.Name = "maskGrpCmb";
            this.maskGrpCmb.Size = new System.Drawing.Size(177, 23);
            this.maskGrpCmb.TabIndex = 2;
            this.maskGrpCmb.SelectedValueChanged += new System.EventHandler(this.maskGrpCmb_SelectedValueChanged);
            // 
            // _uploadUMCBtn
            // 
            this._uploadUMCBtn.Location = new System.Drawing.Point(782, 13);
            this._uploadUMCBtn.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this._uploadUMCBtn.Name = "_uploadUMCBtn";
            this._uploadUMCBtn.Size = new System.Drawing.Size(94, 26);
            this._uploadUMCBtn.TabIndex = 1;
            this._uploadUMCBtn.Text = "UMC Upload";
            this._uploadUMCBtn.UseVisualStyleBackColor = true;
            this._uploadUMCBtn.Click += new System.EventHandler(this._uploadUMCBtn_Click);
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(4, 45);
            this.dataGridView1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            this.dataGridView1.RowHeadersWidth = 51;
            this.dataGridView1.RowTemplate.Height = 29;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView1.Size = new System.Drawing.Size(872, 612);
            this.dataGridView1.TabIndex = 0;
            this.dataGridView1.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellDoubleClick);
            this.dataGridView1.DataBindingComplete += new System.Windows.Forms.DataGridViewBindingCompleteEventHandler(this.dataGridView1_DataBindingComplete);
            // 
            // _esTimer
            // 
            this._esTimer.Interval = 10000;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(1508, 668);
            this.Controls.Add(this.splitContainer1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "Form1";
            this.Text = "eDoc Generator UI";
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel1.PerformLayout();
            this.splitContainer1.Panel2.ResumeLayout(false);
            this.splitContainer1.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this._esNumUpDown)).EndInit();
            this.panel2.ResumeLayout(false);
            this.panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._esFailGdv)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this._passDgv)).EndInit();
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
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Label _esMaskGrpLbl;
        private System.Windows.Forms.ComboBox _esMaskGrpCmb;
        private System.Windows.Forms.Label _psLbl;
        private System.Windows.Forms.DataGridView _passDgv;
        private System.Windows.Forms.ComboBox _esMaskCmb;
        private System.Windows.Forms.Label _esMaskLbl;
        private System.Windows.Forms.Label _flLsl;
        private System.Windows.Forms.DataGridView _esFailGdv;
        private System.Windows.Forms.Label _drLbl;
        private System.Windows.Forms.NumericUpDown _esNumUpDown;
        private System.Windows.Forms.CheckBox _esRefreshCkb;
        private System.Windows.Forms.Timer _esTimer;
        private System.Windows.Forms.Label _esUpdTime;
        private System.Windows.Forms.Label _esReUplLbl;
        private System.Windows.Forms.ComboBox _esUplCmb;
        private System.Windows.Forms.Button _esUplBtn;
    }
}

