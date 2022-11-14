using eDocGenLib.Classes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenUI.Utils
{
    public static class UI_APIHelper
    {
        public static string _APIURL = ConfigurationManager.AppSettings["APIUrl"].ToString();
        private static HttpClient _Client;

        #region Get APIs
        public static async Task<DataTable> GetSpecInfoByAPI(string groupName)
        {
            DataTable dt = new DataTable();
            using (_Client = new HttpClient())
            {
                try
                {
                    _Client.BaseAddress = new Uri(_APIURL);
                    _Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    _Client.Timeout = TimeSpan.FromSeconds(30);

                    var queryDic = new Dictionary<string, string>();
                    queryDic.Add("groupName", groupName);

                    HttpResponseMessage response = await _Client.GetAsync("GetSpecInfo?" + DicToQueryString(queryDic));
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    responseBody = responseBody.TrimStart('\"');
                    responseBody = responseBody.TrimEnd('\"');
                    responseBody = responseBody.Replace("\\", "");
                    var list = JsonConvert.DeserializeObject<List<GradingSpecClass>>(responseBody, new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });
                    dt = ToDataTable<GradingSpecClass>(list);
                }
                catch (Exception)
                {

                    throw;
                }
            }
            return dt;
        }

        public static async Task<DataTable> GeteDocListByAPI(string groupName, string maskName, string eDocStatus, string rowNum)
        {
            DataTable dt = new DataTable();
            using (_Client = new HttpClient())
            {
                try
                {
                    _Client.BaseAddress = new Uri(_APIURL);
                    _Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    _Client.Timeout = TimeSpan.FromSeconds(30);

                    var queryDic = new Dictionary<string, string>();
                    queryDic.Add("rowNum", rowNum);
                    //queryDic.Add("sqlCond", string.Format(""));
                    queryDic.Add("maskGroup", groupName);
                    queryDic.Add("maskId", maskName);
                    queryDic.Add("eDocStatus", eDocStatus);

                    HttpResponseMessage response = await _Client.GetAsync("/QueryRWStatus?" + DicToQueryString(queryDic));
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();                    
                    responseBody = responseBody.Replace("\\\\", ">");
                    responseBody = responseBody.TrimStart('\"');
                    responseBody = responseBody.TrimEnd('\"');
                    responseBody = responseBody.Replace("\\", "");
                    var list = JsonConvert.DeserializeObject<List<TraceabilityStatusClass>>(responseBody, new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });
                    dt = ToDataTable<TraceabilityStatusClass>(list);
                }
                catch (Exception)
                {

                    throw;
                }
            }
            return dt;
        }

        public static async Task<List<string>> GetMaskListByAPI(string groupName)
        {
            List<string> list = new List<string>();
            using (_Client = new HttpClient())
            {
                try
                {
                    _Client.BaseAddress = new Uri(_APIURL);
                    _Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    _Client.Timeout = TimeSpan.FromSeconds(30);

                    var queryDic = new Dictionary<string, string>();
                    queryDic.Add("maskGroup", groupName);

                    HttpResponseMessage response = await _Client.GetAsync("/GetWaferIdByMaskGroup?" + DicToQueryString(queryDic));
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    responseBody = responseBody.TrimStart('\"');
                    responseBody = responseBody.TrimEnd('\"');
                    responseBody = responseBody.Replace("\\", "");
                    list = JsonConvert.DeserializeObject<List<string>>(responseBody, new JsonSerializerSettings()
                    {
                        NullValueHandling = NullValueHandling.Ignore
                    });
                }
                catch (Exception)
                {
                    throw;
                }
            }
            return list;
        }
        #endregion




        public static async Task<string> PostAPIAsync(string apiName, JObject jObject)
        {
            using (_Client = new HttpClient())
            {
                try
                {
                    _Client.BaseAddress = new Uri(_APIURL);
                    _Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    _Client.Timeout = TimeSpan.FromSeconds(3000);

                    using (var umcContent = new StringContent(jObject.ToString(), Encoding.UTF8, "application/json"))
                    {
                        var response = await _Client.PostAsync(apiName, umcContent);
                        if (response != null)
                        {
                            if (response.IsSuccessStatusCode == true)
                            {
                                // 取得呼叫完成 API 後的回報內容
                                String strResult = await response.Content.ReadAsStringAsync();
                                return strResult;
                            }
                            else
                            {
                                return string.Format("Error Code:{0}, Error Message:{1}", response.StatusCode, response.RequestMessage);
                            }
                        }
                        else return "API calling falied!";
                    }
                    //dt = ToDataTable<GradingSpecClass>(list);
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }
        }

        public static async Task<bool> UploadFilesAsync(string folderName, params string[] paths)
        {
            HttpClient client = new HttpClient();
            // we need to send a request with multipart/form-data
            var multiForm = new MultipartFormDataContent();

            foreach (string path in paths)
            {
                // add file and directly upload it
                FileStream fs = File.OpenRead(path);
                var streamContent = new StreamContent(fs);

                //string dd = MimeType(path);
                var fileContent = new ByteArrayContent(await streamContent.ReadAsByteArrayAsync());
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("multipart/form-data");
                multiForm.Add(fileContent, "files", Path.GetFileName(path));
            }
            multiForm.Add(new StringContent(folderName), "folder");

            var apiUrl = _APIURL + "/OnPostUploadAsync";
            // send request to API
            using (var response = await client.PostAsync(apiUrl, multiForm))
            {
                return response.IsSuccessStatusCode;
            }
        }

        public static string DicToQueryString(Dictionary<string, string> dic)
        {
            string queryString = string.Empty;
            foreach (string key in dic.Keys)
            {
                queryString += key + "=" + dic[key] + "&";
            }
            queryString = queryString.Remove(queryString.Length - 1, 1);
            return queryString;
        }

        public static DataTable ToDataTable<T>(this IList<T> data)
        {
            PropertyDescriptorCollection props =
            TypeDescriptor.GetProperties(typeof(T));
            DataTable table = new DataTable();
            for (int i = 0; i < props.Count; i++)
            {
                PropertyDescriptor prop = props[i];
                table.Columns.Add(prop.Name, prop.PropertyType);
            }
            object[] values = new object[props.Count];
            foreach (T item in data)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = props[i].GetValue(item);
                }
                table.Rows.Add(values);
            }
            return table;
        }
    }
}
