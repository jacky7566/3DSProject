using Dapper;
using eDocGenLib.Classes.eDocGenEngine;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenLib.Utils
{
    public class APIHelper
    {
        public static eDocGenParaClass _EDocGlobVar;
        private static IConfiguration _config;
        private static ILogger _logger;       

        public APIHelper(IConfiguration config, ILogger logger, eDocGenParaClass eDocGlobVar)
        {
            _config = config;
            _logger = logger;
            _EDocGlobVar = eDocGlobVar;
            GetEDocConfigList();
        }

        public void SendEDocAPI(string eMapStatus, string errorMessage, string machineName)
        {
            try
            {
                string apiUrl = GetAPIConfig();
                TBL_WAFER_RESUME wafer_header = new TBL_WAFER_RESUME()
                {
                    Level = "RW",
                    Type = "eDoc",
                    RW_Wafer_Id = _EDocGlobVar.HeaderInfo.RW_Wafer_Id,
                    Wafer_Id = _EDocGlobVar.HeaderInfo.Wafer_Id.Substring(0, 12),
                    Status = 1,
                    Created_By = machineName
                };

                List<TBL_WAFER_RESUME_ITEM> wafer_item_list = new List<TBL_WAFER_RESUME_ITEM>();
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "rw_map_filename", Value = Path.GetFileName(_EDocGlobVar.AVI2FilePath) });
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "rw_map_file_datetime", Value = new FileInfo(_EDocGlobVar.AVI2FilePath).CreationTime.ToString("yyyy-MM-dd HH:mm:ss") });
                for (int i = 1; i <= _EDocGlobVar.GradingFileList.Count(); i++)
                {
                    wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "grading_file" + i.ToString(), Value = _EDocGlobVar.GradingFileList[i - 1] });
                }
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "software_version", Value = Assembly.GetExecutingAssembly().GetName().Name + " V" + Assembly.GetExecutingAssembly().GetName().Version });
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "good_die_qty", Value = _EDocGlobVar.GoodDieQty.ToString() });
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "status", Value = eMapStatus });
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "error_message", Value = errorMessage });
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "spec_file_path", Value = File.Exists(_EDocGlobVar.GradingSpecFilePath) ? new FileInfo(_EDocGlobVar.GradingSpecFilePath).Name : _EDocGlobVar.GradingSpecFilePath });
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "cbp_version", Value = _EDocGlobVar.WaferTestHeader.cbp_version });
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "grade_spec_version", Value = _EDocGlobVar.WaferTestHeader.spec_version });

                var obj = new { wafer_header, wafer_item_list };
                _logger.Info(Newtonsoft.Json.JsonConvert.SerializeObject(obj));
                var result = IOHelper.SendJsonAPIHttp(obj, apiUrl);
                _logger.Info("SendEDocAPI... Result: " + result);
            }
            catch (Exception)
            {
                throw;
            }
        }

        private string GetAPIConfig()
        {
            string apiURL = "http://10.21.68.71/LumMVC_WebAPI/api/WaferResume/";
            try
            {
                bool isDebug = false;
                string apiConfigKey = "APIURL_Prod";
                bool.TryParse(_config["Configurations:IsDebug"].ToString(), out isDebug);
                if (isDebug)
                    apiConfigKey = "APIURL_Test";
                var apiConfig = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Connections" && r.ConfigKey == apiConfigKey);
                if (apiConfig != null && apiConfig.Count() > 0)
                    apiURL = apiConfig.FirstOrDefault().ConfigValue;
            }
            catch (Exception ex)
            {
                _logger.Error(ex.StackTrace);
            }
            return apiURL;
        }

        private bool GetEDocConfigList()
        {
            var machineName = Environment.MachineName;
            if (_config["ServerName"] != null && string.IsNullOrEmpty(_config["ServerName"].ToString()) == false)
            {
                machineName = _config["ServerName"].ToString();
            }

            var sql = string.Format(@"select * from [dbo].[TBL_eDoc_Config] where ServerName = '{0}' ", machineName);
            try
            {
                using (var sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]))
                {
                    _EDocGlobVar.EDocConfigList = sqlConn.Query<eDocConfigClass>(sql).ToList();
                }

                if (_EDocGlobVar.EDocConfigList.Count == 0)
                {
                    _EDocGlobVar.MailInfo.Content = string.Format("GetEDocConfigList - No data found! Server Name: {0}", machineName);
                    _EDocGlobVar.MailInfo.Level = 1;
                }
                else return true;
            }
            catch (Exception)
            {
                throw;
            }
            return false;
        }
    }
}
