using DataIngestionEngine.Utils;
using eDocGenLib.Utils;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Extensions.Logging;
using System;
using System.Data;

namespace DataIngestionEngine
{
    class Program
    {
        static ConnectionHelper _connHelper;
        static DataTable _configDt;
        static IConfiguration _config;
        static ILogger _logger;

        static void Main(string[] args)
        {
            Initial();
            ProcessStart();

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
            }
            catch (Exception)
            {
                throw;
            }
        }

        static void ProcessStart()
        {
            _logger.Info("Process Start...");
            try
            {
                //Get Configuration Info
                var sql = string.Format("SELECT * FROM [TBL_eDoc_Config] Where ServerName = '{0}' ", Environment.MachineName);
                _logger.Info(sql);

                var list = _connHelper.QueryDataBySQL(sql);
                _configDt = IOHelper.ConvertToDataTable<dynamic>(list);

                //Initial FileWatcher and AVIIngestion
                _logger.Info("Initial File Watcher");
                //FileWatcherUtil fwUtil = new FileWatcherUtil(_config, _logger);
                FileDetectUtil fdUtil = new FileDetectUtil(_config, _logger);
                _logger.Info("Initial AVI2 Ingestion");
                AVI2IngestionUtil aiu = new AVI2IngestionUtil(_config, _logger);

                //Turn on FileWatcher
                string expression = "ConfigKey = 'AVI2File'";
                _logger.Info("Query condition: " + expression);
                var watchPath = _configDt.Select(expression)[0].ItemArray[5].ToString();
                _logger.Info(string.Format("Start File Detect: {0}...", watchPath));
                fdUtil.ProcessStart(watchPath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                Console.ReadLine();
            }

        }

    }
}
