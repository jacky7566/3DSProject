using eDocGenLib.Utils;
using eDocGenUI.Classes;
using eDocGenUI.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace eDocGenUI
{
    public partial class Form1 : Form
    {
        private static DataTable _HeaderDataTable;
        public Form1()
        {
            InitializeComponent();
            _HeaderDataTable = new DataTable();
            InitialTimer();
        }

        private void FileUpload()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select file";
            openFileDialog.InitialDirectory = ".\\";
            openFileDialog.Filter = "UMC files (*.*)|*.UMC";
        }

        private void InitialTimer()
        {
            _esTimer.Interval = 60000;
            _esTimer.Tick += _esTimer_Tick;
        }


        #region Event Handler
        private void _esRefreshCkb_CheckedChanged(object sender, EventArgs e)
        {
            if (_esRefreshCkb.Checked)
                _esTimer.Enabled = true;
            else _esTimer.Enabled = false;
        }
        private void _esTimer_Tick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_esMaskGrpCmb.Text) == false || string.IsNullOrEmpty(_esMaskCmb.Text) == false)
                _ = this.RefreshSuccessStatusGVAsync();
        }

        private async void _uploadUMCBtn_Click(object sender, EventArgs e)
        {
            try
            {
                var umcXShift = ConfigurationManager.AppSettings["UMCXShift"].ToString();
                var umcYShift = ConfigurationManager.AppSettings["UMCYShift"].ToString();
                var row = this.dataGridView1.Rows[this.dataGridView1.SelectedCells[0].RowIndex];
                if (row.Selected)
                {
                    var specId = row.Cells["Id"].Value.ToString();
                    var product = row.Cells["Mask"].Value.ToString();
                    var eMapVersion = row.Cells["EMapVersion"].Value.ToString();

                    var dialog = new OpenFileDialog();
                    dialog.Filter = "Archive (*.UMC)|*.UMC";
                    var result = dialog.ShowDialog(); //shows save file dialog
                    if (result == DialogResult.OK)
                    {
                        
                        // strong typed instance
                        var values = new JObject();
                        values.Add("Id", specId);
                        values.Add("FileName", new FileInfo(dialog.FileName).Name);
                        values.Add("FormFile", File.ReadAllBytes(dialog.FileName));
                        values.Add("Product", product);
                        values.Add("ProductType", "NA");
                        values.Add("XShift", int.Parse(umcXShift));
                        values.Add("YShift", int.Parse(umcYShift));
                        values.Add("EMapVersion", eMapVersion);
                        this.progressBar1.Visible = true;
                        this.progressBar1.Style = ProgressBarStyle.Marquee;
                        var resMsg = await UI_APIHelper.PostAPIAsync("ProcessAndUploadUMCFile", values);
                        this.progressBar1.Visible = false;                     
                        MessageBox.Show(resMsg);                        
                    }
                }
                else
                {
                    MessageBox.Show("Please select at least one row!");
                }
            }
            catch (Exception ex)
            {               
                MessageBox.Show(ex.Message);
            }
            finally
            {
                await this.RefreshSpecInfoGridViewAsync();
            }
        }
        private void DataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 7)
            {
                MessageBox.Show("Test");
            }
        }
        private async void dataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == -1) return;
            try
            {
                UIEventHelper uIEventHelper = new UIEventHelper(this);
                var cell = this.dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex];

                switch (cell.OwningColumn.Name)
                {
                    case "UMCFileName":
                        uIEventHelper.UMCFileEvent(sender, e);
                        break;
                    case "EMapVersion":
                        var dtr = _HeaderDataTable.Rows[e.RowIndex];
                        await uIEventHelper.EMapVersionEventAsync(dtr);
                        await this.RefreshSpecInfoGridViewAsync();
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }
        private void dataGridView1_DataBindingComplete(object sender, DataGridViewBindingCompleteEventArgs e)
        {
            foreach (DataGridViewRow r in dataGridView1.Rows)
            {
                if (r.Cells["UMCFileName"].Value != null &&
                    !System.Uri.IsWellFormedUriString(r.Cells["UMCFileName"].Value.ToString(), UriKind.Absolute))
                {
                    r.Cells["UMCFileName"] = new DataGridViewLinkCell();
                    // Note that if I want a different link color for example it must go here
                    DataGridViewLinkCell c = r.Cells["UMCFileName"] as DataGridViewLinkCell;
                    c.ToolTipText = "Double Click to Download";
                    c.LinkColor = Color.Blue;
                }
            }
        }
        private async void maskGrpCmb_SelectedValueChanged(object sender, EventArgs e)
        {
            try
            {
                await this.RefreshSpecInfoGridViewAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
        }
        private async void _esMaskGrpCmb_SelectedValueChanged(object sender, EventArgs e)
        {
            try
            {
                var list = await UI_APIHelper.GetMaskListByAPI(LookupHelper.ConvertMaskGroupName(this._esMaskGrpCmb.Text));
                this._esMaskCmb.DataSource = list;
                this._esMaskCmb.Refresh();

                await this.RefreshSuccessStatusGVAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void _esMaskCmb_SelectedValueChanged(object sender, EventArgs e)
        {
            try
            {
                await this.RefreshSuccessStatusGVAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void _esUplBtn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(this._esUplCmb.Text))
            {
                MessageBox.Show("Please select file upload path!");
                return;
            }    
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = true;
            openFileDialog.AddExtension = true;
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "txt files (*.txt)|*.txt";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var res = await UI_APIHelper.UploadFilesAsync(this._esUplCmb.Text, openFileDialog.FileNames);
                if (res)
                    MessageBox.Show("Uploaded files successfully!");
                else
                    MessageBox.Show("Uploaded files failed!");
            }
        }

        private void _esUplCmb_SelectedValueChanged(object sender, EventArgs e)
        {
            //if (IsDirectoryWritable(this._esUplCmb.Text) == false)
            //    MessageBox.Show("No permission to upload file, please contact IT owner!");
        }
        #endregion

        #region Utils
        private void ShowWaitCursor()
        {
            Application.UseWaitCursor = true; //keeps waitcursor even when the thread ends.
            System.Windows.Forms.Cursor.Current = Cursors.WaitCursor; //Normal mode of setting waitcursor
        }

        private void ShowNormalCursor()
        {
            Application.UseWaitCursor = false;
            System.Windows.Forms.Cursor.Current = Cursors.Default;
        }

        private async Task RefreshSpecInfoGridViewAsync()
        {
            _HeaderDataTable = await UI_APIHelper.GetSpecInfoByAPI(LookupHelper.ConvertMaskGroupName(this.maskGrpCmb.Text));

            this.dataGridView1.DataSource = _HeaderDataTable;
            this.dataGridView1.Columns["Spec_Id"].Visible = false;
            this.dataGridView1.Columns["Id"].Visible = false;
            //this.dataGridView1.Columns["UMCFileName"].CellType = new DataGridViewColumn()
            //if (this.dataGridView1.Columns.Contains("Upload UMC") == false)
            //{
            //    //add update button
            //    DataGridViewButtonColumn b_update = new DataGridViewButtonColumn();
            //    b_update.Name = "Upload UMC";
            //    b_update.HeaderText = "Upload UMC";
            //    b_update.DefaultCellStyle.NullValue = "Upload UMC";
            //    this.dataGridView1.Columns.Add(b_update);
            //    this.dataGridView1.Columns["Upload UMC"].DisplayIndex = dt.Columns.Count;
            //    this.dataGridView1.CellContentClick += DataGridView1_CellContentClick;
            //}

            this.dataGridView1.Refresh();
        }

        private async Task RefreshSuccessStatusGVAsync()
        {
            var successDt = await UI_APIHelper.GeteDocListByAPI(LookupHelper.ConvertMaskGroupName(this._esMaskGrpCmb.Text),
                this._esMaskCmb.Text, "Success", this._esNumUpDown.Value.ToString());

            this._passDgv.DataSource = successDt;
            this._passDgv.Refresh();

            var failDt = await UI_APIHelper.GeteDocListByAPI(LookupHelper.ConvertMaskGroupName(this._esMaskGrpCmb.Text),
                this._esMaskCmb.Text, "Fail", this._esNumUpDown.Value.ToString());
            this._esFailGdv.DataSource = failDt;
            this._esFailGdv.Refresh();

            _esUpdTime.Text = "Last Updated Time: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }

        private bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
        {
            try
            {
                using (FileStream fs = File.Create(
                    Path.Combine(
                        dirPath,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose)
                )
                { }
                return true;
            }
            catch
            {
                if (throwIfFails)
                    throw;
                else
                    return false;
            }
        }

        #endregion


    }
}
