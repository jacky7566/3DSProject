using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Data;
//using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eDocGenLib.Classes.eDocGenEngine;
using System.IO;

namespace eDocGenEngine.Utils
{
    internal class SpecUtil
    {
        private static IConfiguration _config;
        private static ILogger _logger;
        private static IDbConnection _sqlConn;
        private static string _Mask;
        public static List<eDocInfoClass> _eDocInfoList;
        public static List<eDocSpecParameterClass> _eDocSpecList;
        public static List<eDocMCOSpecClass> _eDocMCOSpecList;
        public eMapTemplateClass _eMapTemplateInfo;
        public static string _EMapTemplate;
        public static string _SpecFilePath;

        public SpecUtil(IConfiguration config, ILogger logger)
        {
            _config = config;
            _logger = logger;
            _sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]);
        }
        public void GetEDocSpec(string maskIDPrefix)
        {
            _Mask = maskIDPrefix;
            try
            {
                _SpecFilePath = GetSpecFilePath(maskIDPrefix);
                //_SpecFilePath = @"C:\SourceControl\Bitbucket\eDoc\eDocTestData\S_Drive\3DS_Document\eDocSpec\WD092\WD092_V2.12.xlsx";
                _logger.Info("Start to process eDoc spec: " + _SpecFilePath);
                if (File.Exists(_SpecFilePath))
                {
                    GeteDocSpecInfo();
                    if (_eDocInfoList.Count() > 0)
                    {
                        GeteDocSpecParameter(maskIDPrefix);
                        if (_eDocSpecList.Count() == 0)
                        {
                            eDocGenUtil._EDocGlobVar.MailInfo.Subject = string.Format("Missing {0} eDoc Spec Setup", maskIDPrefix);
                            eDocGenUtil._EDocGlobVar.MailInfo.Content = string.Format("Missing [eDoc Spec](tMap/CoC) setup in spec: {0}", _SpecFilePath);
                            eDocGenUtil._EDocGlobVar.MailInfo.Level = 2;
                        }
                    }
                    else
                    {
                        eDocGenUtil._EDocGlobVar.MailInfo.Subject = string.Format("Missing {0} eDoc Spec - [eDoc Info]", maskIDPrefix);
                        eDocGenUtil._EDocGlobVar.MailInfo.Content = string.Format("Missing [eDoc Info] setup in spec: {0}", _SpecFilePath);
                        eDocGenUtil._EDocGlobVar.MailInfo.Level = 2;
                    }
                }
                else
                {
                    eDocGenUtil._EDocGlobVar.MailInfo.Subject = string.Format("Missing {0} eDoc Spec", maskIDPrefix);
                    eDocGenUtil._EDocGlobVar.MailInfo.Content = string.Format("Missing Spec File: {0}", _SpecFilePath);
                    eDocGenUtil._EDocGlobVar.MailInfo.Level = 2;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void GeteDocSpecParameter(string maskIDPrefix)
        {
            _eDocSpecList = new List<eDocSpecParameterClass>();
            eDocSpecParameterClass item = new eDocSpecParameterClass();
            var pis = typeof(eDocSpecParameterClass).GetProperties();

            try
            {
                var specSheetName = "eDocs Spec$";
                //Special Old product case
                var noSpecMasks = GetNoSpecMaskList();
                if (noSpecMasks != null && noSpecMasks.Any())
                {
                    if (noSpecMasks.Contains(maskIDPrefix))
                    {
                        specSheetName = maskIDPrefix + " eDocs Spec$";
                    }
                }
                    
                var dt = ExcelUtil.ReadExcelBySheetForWindows(_SpecFilePath, "eDocs Spec$", true, "Type");
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow dtr in dt.Rows)
                    {
                        item = new eDocSpecParameterClass();
                        if (string.IsNullOrEmpty(dtr["Type"].ToString()) == false
                            || string.IsNullOrEmpty(dtr["Display_Paramenter_Name"].ToString()) == false
                            || string.IsNullOrEmpty(dtr["Parameter_name"].ToString()) == false)
                        {
                            foreach (var pi in pis)
                            {
                                pi.SetValue(item, dtr[pi.Name].ToString().Trim());
                            }
                            _eDocSpecList.Add(item);
                        }
                        else
                        {
                            eDocGenUtil._EDocGlobVar.MailInfo.Content = "Missing Type/Display_Paramenter_Name/Parameter_name information. Mask: " + maskIDPrefix;
                            eDocGenUtil._EDocGlobVar.MailInfo.Level = 2;
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void GeteDocSpecInfo()
        {
            _eDocInfoList = new List<eDocInfoClass>();
            _eMapTemplateInfo = new eMapTemplateClass();
            _eMapTemplateInfo.FiducialList = new List<string[]>();
            eDocInfoClass item;
            var pis = typeof(eDocInfoClass).GetProperties();
            var epis = typeof(eMapTemplateClass).GetProperties();

            try
            {
                var dt = ExcelUtil.ReadExcelBySheetForWindows(_SpecFilePath, "eDocs Info$", true, "Type");
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow dtr in dt.Rows)
                    {
                        item = new eDocInfoClass();
                        foreach (var pi in pis)
                        {
                            pi.SetValue(item, dtr[pi.Name].ToString().Trim());
                        }
                        _eDocInfoList.Add(item);
                        //Get eMap Template Fiducial Info
                        GetEMapTemplateInfo(dtr);
                    }
                    //Get eMap Template Header
                    foreach (var infoItem in _eDocInfoList.Where(r => r.Type == "EMAP").ToList())
                    {
                        foreach (var epi in epis)
                        {
                            if (infoItem.Key.Equals(epi.Name))
                            {
                                epi.SetValue(_eMapTemplateInfo, infoItem.Value1);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

        }
        private void GetEMapTemplateInfo(DataRow dtr)
        {                                   
            string[] xyArry = new string[] { };
            try
            {
                if (dtr["Key"].ToString().Contains("ReferenceDevice")
                        && string.IsNullOrEmpty(dtr["Value1"].ToString()) == false)
                {
                    xyArry = dtr["Value1"].ToString().Split(",");
                    if (xyArry.Length > 0)
                    {
                        _eMapTemplateInfo.FiducialList.Add(xyArry);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public void GetMCOSpecInfo(string maskIDPrefix)
        {
            _eDocMCOSpecList = new List<eDocMCOSpecClass>();
            eDocMCOSpecClass item = null;
            var pis = typeof(eDocMCOSpecClass).GetProperties();
            try
            {
                //Get MCO Spec Path
                var mcoSpecPath = eDocGenUtil._EDocGlobVar.EDocConfigList
                    .Where(r=> r.ConfigType == "Spec" && r.ConfigKey == "MCO_SpecPath").FirstOrDefault().ConfigValue;
                if (string.IsNullOrEmpty(mcoSpecPath) == false)
                {
                    var dt = ExcelUtil.ReadExcelBySheetForWindows(mcoSpecPath, string.Format("{0}_MCO$", maskIDPrefix), true, "Sequence");
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        foreach (DataRow dtr in dt.Rows)
                        {
                            item = new eDocMCOSpecClass();
                            foreach (var pi in pis)
                            {
                                pi.SetValue(item, dtr[pi.Name].ToString().Trim());
                            }
                            _eDocMCOSpecList.Add(item);
                        }
                    }
                    else
                    {
                        eDocGenUtil._EDocGlobVar.MailInfo.Subject = string.Format("Missing {0} MCO Spec settings.", maskIDPrefix);
                        eDocGenUtil._EDocGlobVar.MailInfo.Content = string.Format("Missing MCO Spec: {0}", mcoSpecPath);
                        eDocGenUtil._EDocGlobVar.MailInfo.Level = 1;
                    }
                }
                else
                {
                    eDocGenUtil._EDocGlobVar.MailInfo.Subject = string.Format("Missing {0} MCO Spec In-Spec.", maskIDPrefix);
                    eDocGenUtil._EDocGlobVar.MailInfo.Content = string.Format("Missing MCO Spec: {0}", mcoSpecPath);
                    eDocGenUtil._EDocGlobVar.MailInfo.Level = 1;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public string GetSpecFilePath(string maskIDPrefix)
        {
            var noSpecMasks = GetNoSpecMaskList();
            //Read INI
            IniFileUtil iniFileUtil = new IniFileUtil(_config["Configurations:SpecMasterIni"]);
            _EMapTemplate = iniFileUtil.Read("EMAP_TEMPLATE", maskIDPrefix);
            if (noSpecMasks != null && noSpecMasks.Any() && noSpecMasks.Contains(maskIDPrefix))
            {
                return _config["Configurations:NoSpecConfigFile"];
            }
            else
            {
                return iniFileUtil.Read("Product Spec", maskIDPrefix);
            }
        }
        public List<string> GetNoSpecMaskList()
        {
            var noSpecMasks = eDocGenUtil._EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Spec" && r.ConfigKey == "NoSpecMasks");
            if (noSpecMasks != null && noSpecMasks.Any())
            {
                var noSpecMaskList = noSpecMasks.FirstOrDefault().ConfigValue.Split(";").ToList();
                return noSpecMaskList;
            }
            return null;
        }
    }
}
