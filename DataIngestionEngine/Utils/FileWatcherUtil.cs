using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataIngestionEngine.Utils
{
    public class FileWatcherUtil
    {
        private static IConfiguration _config;
        private static ILogger _logger;
        public static string _IngestedPath;
        public FileWatcherUtil(IConfiguration config, ILogger logger)
        {
            _config = config;
            _logger = logger;
        }

        public void ProcessStart(string watchPath)
        {            
            FileSystemWatcher watcher = new FileSystemWatcher();
            try
            {
                string[] filters = _config["Configurations:ProductFilter"].Split(";");
                string filePath = watchPath;
                watcher.Path = filePath;
                watcher.EnableRaisingEvents = true;
                watcher.NotifyFilter = NotifyFilters.FileName;
                foreach (var filter in filters)
                {
                    watcher.Filters.Add(filter);
                }
                watcher.Created += Watcher_Created;

                // wait - not to end
                new System.Threading.AutoResetEvent(false).WaitOne();
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e)
        {
            _logger.Info(e.FullPath);
            try
            {
                _IngestedPath = new FileInfo(e.FullPath).DirectoryName + Path.DirectorySeparatorChar + "Ingested";
                if (Directory.Exists(_IngestedPath) == false)
                    Directory.CreateDirectory(_IngestedPath);
                string failedPath = new FileInfo(e.FullPath).DirectoryName + Path.DirectorySeparatorChar + "Failed";
                if (Directory.Exists(failedPath) == false)
                    Directory.CreateDirectory(failedPath);

                var isMove = _config["Configurations:IsMoveToIngestedFolder"];
                //if (AVI2IngestionUtil.ProcessStartAVI2(e))
                //{
                //    if (isMove == "Y")
                //    {
                //        File.Copy(e.FullPath, _IngestedPath + Path.DirectorySeparatorChar + e.Name, true);
                //        File.Delete(e.FullPath);
                //        _logger.Info("File ingest completed! Move to folder: " + _IngestedPath);
                //    }
                //    else
                //        _logger.Info("File ingest completed! ");
                //}
                //else
                //{
                //    _logger.Info("File ingest Failed! File Name: " + e.FullPath);
                //    if (isMove == "Y")
                //    {
                //        File.Copy(e.FullPath, failedPath + Path.DirectorySeparatorChar + e.Name);
                //        File.Delete(e.FullPath);
                //        _logger.Info("File ingest failed! Move to folder: " + failedPath);
                //    }
                //    else
                //        _logger.Info("File ingest failed! ");
                //}
            }
            catch (Exception)
            {
                throw;
            }

        }
    }
}
