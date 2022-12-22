using eDocGenEngine.Utils;
using eDocGenLib.Classes;
using eDocGenLib.Utils;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Extensions.Logging;
using System;
using System.Linq;

namespace eDocGenEngine
{
    class Program
    {
        static ConnectionHelper _connHelper;
        static IConfiguration _config;
        static ILogger _logger;
        static string _retryLimitCount;
        public static bool _isDebug;
        public static string _MachineName;

        static void Main(string[] args)
        {
            try
            {
                Initial();

                eDocGenUtil eDocGenUtil = new eDocGenUtil(_config, _logger);

                //Get eDocConfigList
                if (eDocGenUtil.GetEDocConfigList() == false)
                {
                    eDocGenUtil._EDocGlobVar.MailInfo.Content = "Missing eDoc configurations!";
                    eDocGenUtil._EDocGlobVar.MailInfo.Level = 1;
                    eDocGenUtil.SendAlertMail();
                }
                else
                {
                    //Get Retry Limit Count
                    GetRetryLimitCount();
                    //Get Traceability Info ; select * from [TBL_Traceability_Info] where status = 1
                    var traceabilityList = eDocGenUtil.GetProcessList(_retryLimitCount);

                    string eMapStatus = string.Empty;
                    foreach (var item in traceabilityList)
                    {
                        eMapStatus = "Success";
                        if (eDocGenUtil.ProcessStart(item) == false)
                        {
                            _logger.Warn(eDocGenUtil._EDocGlobVar.MailInfo.Content);
                            //Send Alert Mail
                            eDocGenUtil.SendAlertMail(item);
                            eMapStatus = "Fail";
                        }
                        eDocGenUtil.CreateEMapLog(eMapStatus, eDocGenUtil._EDocGlobVar.MailInfo.Subject);                        
                        eDocGenUtil.SendEDocAPI(eMapStatus,
                            string.IsNullOrEmpty(eDocGenUtil._EDocGlobVar.MailInfo.Subject) 
                            ? eDocGenUtil._EDocGlobVar.MailInfo.Content : eDocGenUtil._EDocGlobVar.MailInfo.Subject);
                        if (eDocGenUtil.UpdateTraceabilityInfo(eMapStatus, item, _retryLimitCount) > 0 && eMapStatus == "Success")
                        {
                            eDocGenUtil.DeleteTBL_AVI2_RAW(item);
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.StackTrace);
                _logger.Warn(ex.InnerException);
            }
            
        }

        static void Initial()
        {
            Console.WriteLine("Initial...");
            IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

            try
            {
                // NLog configuration with appsettings.json
                // https://github.com/NLog/NLog.Extensions.Logging/wiki/NLog-configuration-with-appsettings.json
                // 從組態設定檔載入NLog設定
                NLog.LogManager.Configuration = new NLogLoggingConfiguration(config.GetSection("NLog"));
                _logger = LogManager.GetCurrentClassLogger();

                _config = config;
                _connHelper = new ConnectionHelper(config);

                _MachineName = Environment.MachineName;
                if (_config["ServerName"] != null && string.IsNullOrEmpty(_config["ServerName"].ToString()) == false)
                {
                    _MachineName = _config["ServerName"].ToString();
                }

                _isDebug = false;
                bool.TryParse(_config["Configurations:IsDebug"].ToString(), out _isDebug);
            }
            catch (Exception)
            {
                throw;
            }
        }

        static void GetRetryLimitCount()
        {
            try
            {
                _retryLimitCount = "3";
                var res = eDocGenUtil._EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Output" && r.ConfigKey == "RetryLimitCount");
                if (res.Any())
                {
                    _retryLimitCount = res.FirstOrDefault().ConfigValue;
                }
                _logger.Info("GetRetryLimitCount: " + _retryLimitCount);
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
