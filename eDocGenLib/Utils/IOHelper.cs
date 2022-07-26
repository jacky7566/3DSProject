using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Utils
{
    public class IOHelper
    {
        public static DataTable ConvertToDataTable<T>(IList<T> data)
        {
            var json = JsonConvert.SerializeObject(data);
            DataTable dataTable = (DataTable)JsonConvert.DeserializeObject(json, (typeof(DataTable)));
            return dataTable;
        }

        public static DataTable ConvertToDataTableWithType<T>(IList<T> data)
        {
            PropertyDescriptorCollection properties =
               TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable();
            foreach (PropertyDescriptor prop in properties)
                table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
            foreach (T item in data)
            {
                DataRow row = table.NewRow();
                foreach (PropertyDescriptor prop in properties)
                    row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                table.Rows.Add(row);
            }
            return table;

        }

        public static string SendJsonAPI(object input, string url)
        {
            string result = string.Empty;
            try
            {
                // 建立 WebClient
                using (WebClient webClient = new WebClient())
                {
                    // 指定 WebClient 編碼
                    webClient.Encoding = Encoding.UTF8;
                    // 指定 WebClient 的 Content-Type header
                    webClient.Headers.Add(HttpRequestHeader.ContentType, "application/json");
                    // 指定 WebClient 的 authorization header
                    //webClient.Headers.Add("authorization", "token {apitoken}");

                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(input, Formatting.Indented);
                    // 執行 post 動作
                    result = webClient.UploadString(url, json);
                }
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }

        public static string SendJsonAPIHttp(object input, string url)
        {
            string result = string.Empty;
            try
            {
                //建立 HttpClient
                using (HttpClient client = new HttpClient() { BaseAddress = new Uri(url) })
                {
                    // 將 data 轉為 json
                    string json = JsonConvert.SerializeObject(input, Formatting.Indented);
                    // 將轉為 string 的 json 依編碼並指定 content type 存為 httpcontent
                    HttpContent contentPost = new StringContent(json, Encoding.UTF8, "application/json");
                    // 發出 post 並取得結果
                    HttpResponseMessage response = client.PostAsync(url, contentPost).GetAwaiter().GetResult();
                    result = response.Content.ReadAsStringAsync().Result;
                }
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }
    }
}
