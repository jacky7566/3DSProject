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
    public class FileDetectUtil
    {
        private static IConfiguration _config;
        private static ILogger _logger;
        public static string _IngestedPath;

        public FileDetectUtil(IConfiguration config, ILogger logger)
        {
            _config = config;
            _logger = logger;
        }

        public void ProcessStart(string filePath)
        {
            try
            {
                string filters = _config["Configurations:ProductFilter"];
                var files = this.GetFiles(filePath, filters, SearchOption.TopDirectoryOnly);
                _logger.Info(string.Format("Detected file count: {0}", files.Count()));

                if (AVI2IngestionUtil.GetEDocConfigList())
                {
                    foreach (string file in files)
                    {
                        ProcessFile(new FileInfo(file));
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private string[] GetFiles(string sourceFolder, string filters, System.IO.SearchOption searchOption)
        {
            return filters.Split('|').SelectMany(filter => System.IO.Directory.GetFiles(sourceFolder, filter, searchOption)).ToArray();
        }

        public void ProcessFile(FileInfo fi)
        {
            _logger.Info(fi.FullName);
            try
            {
                _IngestedPath = fi.DirectoryName + Path.DirectorySeparatorChar + "Ingested";
                if (Directory.Exists(_IngestedPath) == false)
                    Directory.CreateDirectory(_IngestedPath);
                AVI2IngestionUtil._FailedOutputPath = fi.DirectoryName + Path.DirectorySeparatorChar + "Failed";

                var isMove = _config["Configurations:IsMoveToIngestedFolder"];
                if (AVI2IngestionUtil.ProcessStartAVI2(fi))
                {
                    if (isMove == "Y")
                    {
                        File.Copy(fi.FullName, _IngestedPath + Path.DirectorySeparatorChar + fi.Name, true);
                        File.Delete(fi.FullName);
                        _logger.Info("File ingest completed! Move to folder: " + _IngestedPath);
                    }
                    else
                        _logger.Info("File ingest completed! ");
                }
                else
                {
                    _logger.Info("File ingest Failed! File Name: " + fi.FullName);
                    if (Directory.Exists(AVI2IngestionUtil._FailedOutputPath) == false)
                        Directory.CreateDirectory(AVI2IngestionUtil._FailedOutputPath);

                    if (isMove == "Y")
                    {
                        var failedTarget = AVI2IngestionUtil._FailedOutputPath + Path.DirectorySeparatorChar + fi.Name;
                        AVI2IngestionUtil.UpdateFailedHeader(failedTarget);
                        File.Copy(fi.FullName, failedTarget, true);
                        File.Delete(fi.FullName);
                        _logger.Info("File ingest failed! Move to folder: " + AVI2IngestionUtil._FailedOutputPath);
                    }
                    else
                        _logger.Info("File ingest failed! ");
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
