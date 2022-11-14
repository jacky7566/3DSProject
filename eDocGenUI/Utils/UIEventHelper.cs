using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Net;
using System.Text;
using System.Windows.Forms;

namespace eDocGenUI.Utils
{
    public class UIEventHelper
    {
        private Form1 _Form;
        public UIEventHelper(Form1 form)
        {            
            _Form = form;
        }
        public void UMCFileEvent(object sender, DataGridViewCellEventArgs e)
        {
            var rootUrl = ConfigurationManager.AppSettings["APIUrl"].ToString();
            var Id = _Form.dataGridView1.Rows[e.RowIndex].Cells["Id"].Value.ToString();
            var umcFileName = _Form.dataGridView1.Rows[e.RowIndex].Cells["UMCFileName"].Value.ToString();

            if (string.IsNullOrEmpty(umcFileName) == false)
            {
                var dialog = new SaveFileDialog();
                dialog.Filter = "WIN UMC File (*.UMC)|*.UMC";
                dialog.FileName = umcFileName;

                var result = dialog.ShowDialog(); //shows save file dialog
                if (result == DialogResult.OK)
                {
                    Console.WriteLine("writing to: " + dialog.FileName); //prints the file to save

                    using (WebClient client = new WebClient())
                    {
                        client.DownloadFile(string.Format("{0}/DownloadUMCFile?Id={1}", rootUrl, Id), dialog.FileName);
                    }
                }
            }
        }
        public async System.Threading.Tasks.Task EMapVersionEventAsync(DataRow dtr)
        {
            var result = ShowDialog(string.Format("{0} - EMap Version", dtr["Mask"].ToString()), "Input EMap Version");
            if (string.IsNullOrEmpty(result) == false)
            {
                JObject json = new JObject();
                for (int i = 0; i < dtr.Table.Columns.Count ; i++)
                {
                    if (dtr.Table.Columns[i].ColumnName == "EMapVersion")
                    {
                        dtr[dtr.Table.Columns[i]] = result;
                    }
                    json.Add(dtr.Table.Columns[i].ColumnName, dtr[dtr.Table.Columns[i]].ToString());
                }
                MessageBox.Show(await UI_APIHelper.PostAPIAsync("SeteDocSpecInfo", json));
            }
            else
            {
                MessageBox.Show("No data input!");
            }
        }

        private string ShowDialog(string text, string caption)
        {
            Form prompt = new Form()
            {
                Width = 500,
                Height = 160,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 50, Top = 20, Text = text };
            TextBox textBox = new TextBox() { Left = 50, Top = 50, Width = 400 };
            Button confirmation = new Button() { Text = "Ok", Left = 350, Width = 100, Top = 80, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) => { prompt.Close(); };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : "";
        }
    }
}
