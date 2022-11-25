using Dapper;
using eDocGenLib.Classes;
using eDocGenLib.Classes.eDocGenEngine;
using eDocGenLib.Utils;
using Microsoft.Extensions.Configuration;
using NLog;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Odbc;
//using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace eDocGenEngine.Utils
{
    internal class eDocGenUtil
    {
        private static IConfiguration _config;
        private static ILogger _logger;
        //private static IDbConnection _sqlConn;
        public static eDocGenParaClass _EDocGlobVar;
        public static Dictionary<string, string> _OutputFileDic;

        public eDocGenUtil(IConfiguration config, ILogger logger)
        {            
            _config = config;
            _logger = logger;
            //_sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]);
            _EDocGlobVar = new eDocGenParaClass();
        }

        public List<Traceability_InfoClass> GetProcessList(string retryLimitCount)
        {
            var machineName = Environment.MachineName;
            if (_config["ServerName"] != null && string.IsNullOrEmpty(_config["ServerName"].ToString()) == false)
            {
                machineName = _config["ServerName"].ToString();
            }

            var sql = string.Format(@"select * from [TBL_Traceability_Info] 
                        where status = 1 and retrycount < {0} and CreatedBy = '{1}' order by LastUpdatedDate asc",
                        retryLimitCount, machineName);
            _logger.Info(sql);
            try
            {
                var list = new List<Traceability_InfoClass>();
                using (var sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]))
                {
                    list = sqlConn.Query<Traceability_InfoClass>(sql).ToList();
                    if (list.Count > 0)
                    {
                        //sql = string.Format("Update [TBL_Traceability_Info] set status = 2 where Id in ('{0}')",
                        //    string.Join("','", list.Select(r => r.Id).ToList()));
                        //if (_sqlConn.Execute(sql) != list.Count()) 
                        //    return null;
                    }
                    else
                    {
                        _logger.Info("GetProcessList - No data found!");
                    }

                }

                return list;
            }
            catch (Exception)
            {
                throw;
            }
 
        }

        #region Initial
        private void InitialStatus()
        {
            //Initial Global Parameters
            _EDocGlobVar.GoodDieQty = 0;
            _EDocGlobVar.CreationStartTime = DateTime.Now;
            _EDocGlobVar.MailInfo = new eDocAlertClass();
            _EDocGlobVar.GradingFileList = new List<string>();
            _EDocGlobVar.MailInfo.Content = string.Empty;
            _EDocGlobVar.MailInfo.Subject = string.Empty;
            _EDocGlobVar.MailInfo.Level = 0;
            _EDocGlobVar.WaferTestHeader = new eDocWaferTestHeaderClass();
            _OutputFileDic = new Dictionary<string, string>();
        }
        #endregion

        public bool ProcessStart(Traceability_InfoClass header)
        {
            //Initial All Properties
            InitialStatus();

            _EDocGlobVar.HeaderInfo = header;
            Thread.Sleep(10);
            _logger.Info(string.Format("Start to process RW Wafer Id: {0}", header.RW_Wafer_Id));
            
            _EDocGlobVar.AVI2FilePath = header.FilePath;
            _EDocGlobVar.EDocResultPath = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigKey == "Result" && r.ConfigType == "Output").FirstOrDefault().ConfigValue;
            _EDocGlobVar.EDocResultPath = Path.Combine(_EDocGlobVar.EDocResultPath, header.RW_Wafer_Id);

            //Get Ini File
            SpecUtil specUtil = new SpecUtil(_config, _logger);
            //var specFilePath = specUtil.GetSpecFilePath(header.Wafer_Id.Substring(0, 5));

            try
            {
                //Get eDoc Spec
                specUtil.GetEDocSpec(header.Wafer_Id.Substring(0, 5));
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false)
                    return false;

                //Get MCO Spec
                specUtil.GetMCOSpecInfo(header.Wafer_Id.Substring(0, 5));

                //Get Grading Summary Path By RW Wafer Id
                var gradePathList = QueryGradingPath();
                if ((gradePathList == null || gradePathList.Count() == 0) && _EDocGlobVar.ExcludeGradingResult == false)
                {
                    return false;
                }

                //Get Device Info (_EDocGenGlobalParas.RWMapList)
                if (ProcessDeviceInfo(header) == false) return false;

                //Get POR Version
                _EDocGlobVar.POR_Version = GetPORVersion(header.Wafer_Id);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false) //Alert and send notice once POR not found
                    return false;

                //Get EPI Reactor
                GetEPIReactor(header.Wafer_Id);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false) //Alert and send notice once EPI Reacotr query failed
                    return false;

                //Check ETData Shipment
                //Connect to Oracle Database to get information from ShipmentDetail and Shipment Table

                CheckETDataStatus(header.RW_Wafer_Id);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false) return false;
                //IdentifyAndUpdateUnknowWaferID???

                //Get Wafer Test Header (SYL, CBP Version, Grading Version)
                GetWaferTestHeader(gradePathList);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false) return false;
                //Check SYL abnormal and return Yield rate
                CheckSYLAbnormal(header.Wafer_Id);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false) return false;
                //Import MCO
                GetMCOData(header.Wafer_Id);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false) return false;

                //Import Grading Result Data
                ImportGradingData(gradePathList);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false) return false;

                //Validate AVI and AVI2 Conflict
                CheckBinCodeConflict(header.Wafer_Id);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false) return false;

                //Check Value in Spec
                CheckGradingResultInSpec(header.Wafer_Id);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false)
                {
                    //Need send out the new AVI2 file and notice the OOS info
                    return false;
                }

                //Generate eDoc!!
                if (Directory.Exists(_EDocGlobVar.EDocResultPath) == false)
                    Directory.CreateDirectory(_EDocGlobVar.EDocResultPath);
                GeneratEDocProcess(header.Wafer_Id);
                if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = string.Format("ProcessStart exception: Wafer Id {0}", header.Wafer_Id);
                _EDocGlobVar.MailInfo.Content = string.Format("ProcessStart exception: {0}", ex.StackTrace.ToString());
                _EDocGlobVar.MailInfo.Level = 1;
                return false;
            }            
        }

        #region Pre Process
        private bool ProcessDeviceInfo(Traceability_InfoClass header)
        {
            ConnectionHelper connectionHelper = new ConnectionHelper(_config);
            var sql = string.Format(@"SELECT rw.No, rw.Wafer_Id, rw.RW_Wafer_Id, rw.IGx, rw.IGy, rw.Bin_AOI1, dm.OGx, dm.OGy, rw.Bin_AOI2, dm.Device, rw.Line_Data, '' EMap_BinCode
                                from TBL_RW_Dimension dm
                                left join (SELECT * from TBL_AVI2_RawData Where rw_wafer_id = '{0}') rw on rw.OGx = dm.Ogx and rw.OGy = dm.Ogy
                                Where dm.Product = '{1}'
                                order by rw.No asc ", header.RW_Wafer_Id, header.Wafer_Id.Substring(0, 5));

            try
            {
                using (var sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]))
                {
                    _EDocGlobVar.RWMapList = sqlConn.Query<eDocRWMapClass>(sql).ToList();

                    if (_EDocGlobVar.RWMapList.Count == 0)
                    {
                        _EDocGlobVar.MailInfo.Content = String.Format("ProcessDeviceInfo - RW_WaferId: {0} UMC data not found!", header.RW_Wafer_Id);                        
                        _EDocGlobVar.MailInfo.Level = 1;
                        _logger.Info("ProcessDeviceInfo - UMC data found! SQL: " + sql);
                    }
                    else return true;
                }
            }
            catch (Exception)
            {
                throw;
            }
            return false;
        }
        private List<eDocGradePathClass> QueryGradingPath()
        {
            ConnectionHelper connectionHelper = new ConnectionHelper(_config);
            try
            {
                _EDocGlobVar.ExcludeGradingResult = false;
                var isExcludeGradingConfig = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigKey == "ExcludeGradingResult(Y/N)" 
                && r.ConfigType == _EDocGlobVar.HeaderInfo.Wafer_Id.Substring(0, 5) + "_Config");
                if (isExcludeGradingConfig != null && isExcludeGradingConfig.Any())
                    _EDocGlobVar.ExcludeGradingResult = isExcludeGradingConfig.FirstOrDefault().ConfigValue == "Y" ? true : false;

                if (_EDocGlobVar.ExcludeGradingResult) return null;
                _logger.Info("Query Grading Path...");
                //Get Wafer List from AVI2 Data
                var sql = string.Format(@"select Wafer_Id from 
                                        (select Wafer_Id, Min([No]) as Num from 
                                        TBL_AVI2_RawData where RW_Wafer_Id = '{0}' and Wafer_Id not in ('Fiducial') 
                                        Group By Wafer_Id) t
                                        Order by Num asc ", _EDocGlobVar.HeaderInfo.RW_Wafer_Id);
                var waferList = connectionHelper.QueryDataBySQL(sql);
                if (waferList.Count > 0)
                {
                    var wafers = string.Join("','", waferList.Select(x => x as IDictionary<string, object>).ToList().Select(r => r["Wafer_Id"]).ToList());
                    //Get Grading Summary from TBL_File_Trace
                    //sql = String.Format(@"select h.Wafer_Id, h.Target_path from grading_dev_TEST.dbo.tbl_file_trace h
                    //    inner join (select Wafer_Id, max(InsertTime) MaxInsertTime from grading_dev_TEST.dbo.tbl_file_trace group by Wafer_Id) hm
                    //    on h.Wafer_ID = hm.Wafer_ID and h.InsertTime = hm.MaxInsertTime
                    //    where h.filetype = 'Grade_Summary' and h.wafer_id in ('{0}') ", wafers);
                    sql = String.Format(@"select h.Wafer_Id, h.Target_path from grading_dev.dbo.tbl_file_trace h
                        inner join (select Wafer_Id, max(InsertTime) MaxInsertTime, FileType from grading_dev.dbo.tbl_file_trace group by Wafer_Id, FileType) hm
                        on h.Wafer_ID = hm.Wafer_ID and h.InsertTime = hm.MaxInsertTime and h.FileType = hm.FileType
                        where h.filetype = 'Grade_Summary' and h.wafer_id in ('{0}') ", wafers);
                    var gradeSumList = connectionHelper.QueryDataBySQL(sql);
                    if (gradeSumList.Count > 0)
                    {
                        var res = gradeSumList.Select(x => x as IDictionary<string, object>).ToList()
                            .Select(r => new eDocGradePathClass
                            {
                                Wafer_ID = r["Wafer_Id"].ToString(),
                                GradeSumPath = r["Target_path"].ToString()
                            }).ToList();

                        foreach (var item in res)
                        {
                            if (File.Exists(item.GradeSumPath) == false)
                            {
                                _EDocGlobVar.MailInfo.Subject = string.Format("Wafer_Id: {0} grading summary not found!", _EDocGlobVar.HeaderInfo.Wafer_Id);
                                _EDocGlobVar.MailInfo.Content = string.Format("Wafer_Id: {0} grading summary not found! File path: {1}"
                                    , _EDocGlobVar.HeaderInfo.Wafer_Id, item.GradeSumPath);
                                _EDocGlobVar.MailInfo.Level = 1;
                                return null;
                            }
                        }
                        return res;
                    }
                    else
                    {
                        _EDocGlobVar.MailInfo.Subject = string.Format("Wafer_Id: {0} grading summary not found!",
                            _EDocGlobVar.HeaderInfo.Wafer_Id);
                        _EDocGlobVar.MailInfo.Content = string.Format("Wafer_Id: {0} grading summary not found! SQL: {1}"
                            , _EDocGlobVar.HeaderInfo.Wafer_Id, sql);
                        _EDocGlobVar.MailInfo.Level = 1;
                    }
                }
                else
                {
                    _EDocGlobVar.MailInfo.Subject = "Missing AVI2 Header Info";
                    _EDocGlobVar.MailInfo.Content = "Missing AVI2 Header Info. SQL: " + sql;
                    _EDocGlobVar.MailInfo.Level = 1;
                    _logger.Info(_EDocGlobVar.MailInfo.Content);
                }
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = "Error While Query Grading Path";
                _EDocGlobVar.MailInfo.Content = "Error While Query Grading Path. Exception: " + ex.Message;
                _EDocGlobVar.MailInfo.Level = 1;
                _logger.Info("Exception from Query Grading Path...");
                throw;
            }

            return null;
        }
        private string GetPORVersion(string waferID)
        {
            string por = "POR_0.0";
            var sql = string.Format(@"select por_version from wafertest.tbl_wafer_por_version  where wafer_id = '{0}' ", waferID);

            try
            {
                var porInfo = SpecUtil._eDocInfoList.Where(r => r.Type == "Config");
                if (porInfo != null && porInfo.Count() > 0)
                {
                    var isCheckPOR = porInfo.Where(r => r.Key == "Import POR Version from AWS(Y/N)").FirstOrDefault();
                    if (isCheckPOR.Value1 == "Y")
                    {
                        _logger.Info("Get PORVersion...");
                        var athenaConn = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Connections" && r.ConfigKey == "AthenaConnStr");
                        if (athenaConn != null && athenaConn.Count() > 0)
                        {
                            using (var oraConn = new OdbcConnection(athenaConn.FirstOrDefault().ConfigValue))
                            {
                                var porRes = oraConn.Query<string>(sql);
                                if (porRes != null && porRes.Count() > 0)
                                {
                                    por = oraConn.Query<string>(sql).ToList().FirstOrDefault();
                                }
                                else
                                {
                                    _EDocGlobVar.MailInfo.Subject = "POR version not found for wafer:" + waferID;
                                    _EDocGlobVar.MailInfo.Content = "POR version not found for wafer:" + waferID;
                                    _EDocGlobVar.MailInfo.Level = 2;
                                }
                            }
                        }
                        else
                        {
                            _EDocGlobVar.MailInfo.Content = string.Format("GetPORVersion - Athena Connection Failed! Wafer Id: {0}", waferID);
                            _EDocGlobVar.MailInfo.Level = 1;
                        }
                    }
                }
                else
                {
                    var defaultPOR = porInfo.Where(r => r.Key == "Default POR Version");
                    if (defaultPOR != null && defaultPOR.Count() > 0)
                        por = defaultPOR.FirstOrDefault().Value1;
                }
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = "Exception from Get PORVersion";
                _EDocGlobVar.MailInfo.Content = ex.Message;
                _EDocGlobVar.MailInfo.Level = 1;
            }

            return por;
        }
        private void GetWaferTestHeader(List<eDocGradePathClass> gradePathList)
        {

            string inProcessWaferID = string.Empty;
            if (_EDocGlobVar.ExcludeGradingResult) //No need check if exclude grading result
                return;

            try
            {
                var configInfo = SpecUtil._eDocInfoList.Where(r => r.Type == "Config");
                string sql = string.Empty;
                if (configInfo != null && configInfo.Count() > 0)
                {
                    var athenaConn = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Connections" && r.ConfigKey == "AthenaConnStr");
                    if (athenaConn != null && athenaConn.Count() > 0)
                    {
                        using (var oraConn = new OdbcConnection(athenaConn.FirstOrDefault().ConfigValue))
                        {
                            foreach (var item in gradePathList)
                            {
                                inProcessWaferID = item.Wafer_ID;
                                sql = string.Format(@"select wafer_id, wafer_test_yield, syl, spec_version, cbp_version
                                    from wafertest.tbl_wafer_test_header_final where filename = '{0}' 
                                    order by loaded_date desc limit 1 ", new FileInfo(item.GradeSumPath).Name.Replace("_SUMMARY_", "_HEADER_"));

                                //_logger.Info(sql);
                                var res = oraConn.Query<eDocWaferTestHeaderClass>(sql);
                                if (res != null && res.Count() > 0)
                                {
                                    _EDocGlobVar.WaferTestHeader = res.FirstOrDefault();
                                }
                                else
                                {
                                    _logger.Warn(sql);
                                    _EDocGlobVar.MailInfo.Content = string.Format("Wafer Id: {0} missing Grade_Header file", inProcessWaferID);
                                    _EDocGlobVar.MailInfo.Level = 1;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        _EDocGlobVar.MailInfo.Content = string.Format("GetWaferTestHeader - Athena Connection Failed! Wafer Id: {0}", inProcessWaferID);
                        _EDocGlobVar.MailInfo.Level = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Info("Exceptions from GetWaferTestHeader..." + ex.StackTrace);
                _EDocGlobVar.MailInfo.Subject = "Exceptions from GetWaferTestHeader! Wafer Id: " + inProcessWaferID;
                _EDocGlobVar.MailInfo.Content = String.Format("Exceptions from GetWaferTestHeader! Wafer Id: {0}, Exception: {1}", inProcessWaferID, ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }
        }
        private void CheckSYLAbnormal(string waferID)
        {

            try
            {
                if (_EDocGlobVar.ExcludeGradingResult) //No need check if exclude grading result
                    return;

                var configInfo = SpecUtil._eDocInfoList.Where(r => r.Type == "Config");
                if (configInfo != null && configInfo.Count() > 0)
                {
                    var isValidateSYL = configInfo.Where(r => r.Key == "Validate SYL Limit(Y/N)");
                    if (isValidateSYL.Any() && isValidateSYL.FirstOrDefault().Value1 == "Y")
                    {
                        _logger.Info("Check SYLAbnormal...");
                        if (_EDocGlobVar.WaferTestHeader.syl.Equals("Fail"))
                        {
                            _EDocGlobVar.MailInfo.Subject = string.Format("Wafer Id: {0} SYL abnormal! Yield rate: {1}%", waferID,
                                (Double.Parse(_EDocGlobVar.WaferTestHeader.wafer_test_yield) * 100).ToString("##.##"));
                            _EDocGlobVar.MailInfo.Content = string.Format("Wafer Id: {0} SYL abnormal! Yield rate: {1}%", waferID,
                                (Double.Parse(_EDocGlobVar.WaferTestHeader.wafer_test_yield) * 100).ToString("##.##"));
                            _EDocGlobVar.MailInfo.Level = 2;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Info("Exceptions from Check SYLAbnormal..." + ex.StackTrace);
                _EDocGlobVar.MailInfo.Subject = "Exceptions from Check SYLAbnormal! Wafer Id: " + waferID;
                _EDocGlobVar.MailInfo.Content = String.Format("Exceptions from Check SYLAbnormal! Wafer Id: {0}, Exception: {1}", waferID, ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }
        }
        private void CheckETDataStatus(string rw_wafer_Id)
        {
            var sql = string.Format(@"select s.shipmentid from shipmentdetail sd
                            inner join shipment s on sd.shipmentid = s.shipmentid
                            where sd. lotnumber = '{0}' and s.order_type not in ('Standard-314','Sample-314') and s.status = 'TRANSMITTED' ",
                            rw_wafer_Id);

            try
            {
                var etdataInfo = SpecUtil._eDocInfoList.Where(r => r.Type == "Config");
                if (etdataInfo != null && etdataInfo.Count() > 0)
                {
                    var isETData = etdataInfo.Where(r => r.Key == "Validate RW Map against ETDATA shipment(Y/N)").FirstOrDefault();
                    if (isETData.Value1 == "Y")
                    {
                        _logger.Info("Check ETData Status...");
                        var oraConStr = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Connections" && r.ConfigKey == "OracleConnStr");
                        if (oraConStr != null && oraConStr.Count() > 0)
                        {
                            //_logger.Info("Connecting to... " + oraConStr.FirstOrDefault().ConfigValue);
                            using (var oraConn = new OracleConnection(oraConStr.FirstOrDefault().ConfigValue))
                            {
                                var res = oraConn.Query<string>(sql);
                                if (res != null && res.Count() > 0)
                                {
                                    var shipmentId = res.ToList().FirstOrDefault();
                                    _EDocGlobVar.MailInfo.Subject = string.Format("{0} already been Shipped to customer under shipment id: {1}", rw_wafer_Id, shipmentId);
                                    _EDocGlobVar.MailInfo.Content = string.Format("{0} already been Shipped to customer under shipment id: {1}", rw_wafer_Id, shipmentId);
                                    _EDocGlobVar.MailInfo.Level = 2;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = "Error while connecting and read ETDATA data from Oracle database. RW_Wafer_Id:" + rw_wafer_Id;
                _EDocGlobVar.MailInfo.Content = string.Format("Error while connecting and read ETDATA data from Oracle database. Exception: {0} ", ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }
        }
        private void GetMCOData(string waferID)
        {
            var sql = string.Format(@"select * from win.win_mco_v where orig_wafer_id = '{0}' order by sno asc ", waferID);

            try
            {
                var configInfo = SpecUtil._eDocInfoList.Where(r => r.Type == "Config");
                if (configInfo != null && configInfo.Count() > 0)
                {
                    var isValidate = configInfo.Where(r => r.Key == "Include MCO(Y/N)").FirstOrDefault();
                    if (isValidate.Value1 == "Y")
                    {
                        _logger.Info("Get MCO Data...");
                        var athenaConn = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Connections" && r.ConfigKey == "AthenaConnStr");
                        if (athenaConn != null && athenaConn.Count() > 0)
                        {
                            using (var oraConn = new OdbcConnection(athenaConn.FirstOrDefault().ConfigValue))
                            {
                                var res = oraConn.Query<dynamic>(sql);
                                if (res != null && res.Count() > 0)
                                {
                                    _EDocGlobVar.MCOAthenaList = res.ToList();
                                }
                                else
                                {
                                    _EDocGlobVar.MailInfo.Subject = string.Format("MCO data not found for wafer: {0}", waferID);
                                    _EDocGlobVar.MailInfo.Content = string.Format("MCO data not found for wafer: {0}", waferID);
                                    _EDocGlobVar.MailInfo.Level = 3;
                                }
                            }
                        }
                        else
                        {
                            _EDocGlobVar.MailInfo.Content = string.Format("GetMCOData - Athena Connection Failed! Wafer Id: {0}", waferID);
                            _EDocGlobVar.MailInfo.Level = 1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Content = string.Format("GetMCOData Failed! Wafer Id: {0}", waferID);
                _EDocGlobVar.MailInfo.Content = string.Format("GetMCOData Failed! Wafer Id: {0}, Exception: {1}", waferID, ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }
        }
        private void GetEPIReactor(string waferID)
        {
            var sql = string.Format(@"select epi_reactor reactor
                                    from wafertest_agg.tbl_wafer_process_factor where wafer_id = '{0}' ", waferID);

            try
            {
                _logger.Info("Get EPI Reactor...");
                var athenaConn = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Connections" && r.ConfigKey == "AthenaConnStr");
                if (athenaConn != null && athenaConn.Count() > 0)
                {
                    using (var odbcConn = new OdbcConnection(athenaConn.FirstOrDefault().ConfigValue))
                    {
                        var res = odbcConn.Query<string>(sql);
                        if (res != null && res.Count() > 0)
                        {
                            //Hardcode covert epi code; If epi_code = '2.28V1' then 1 ElseIf epi_code = '2.28v2' then 0
                            var epiReacotr = res.FirstOrDefault();
                            if (epiReacotr == "2.28V1")
                                _EDocGlobVar.EPIReactor = "1";
                            else if (epiReacotr == "2.28v2")
                                _EDocGlobVar.EPIReactor = "0";
                            else
                                _EDocGlobVar.EPIReactor = epiReacotr;
                        }
                        else
                        {
                            _EDocGlobVar.EPIReactor = "NA";
                        }
                    }
                }
                else
                {
                    _EDocGlobVar.MailInfo.Content = string.Format("GetEPIReactor - Athena Connection Failed! Wafer Id: {0}", waferID);
                    _EDocGlobVar.MailInfo.Level = 1;
                }
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Content = string.Format("GetEPIReactor - Exception! Wafer Id: {0}", waferID);
                _EDocGlobVar.MailInfo.Content = ex.Message;
                _EDocGlobVar.MailInfo.Level = 1;
            }
        }
        #endregion

        #region Main Process
        private void ImportGradingData(List<eDocGradePathClass> gradePathList)
        {
            if (_EDocGlobVar.ExcludeGradingResult) //No need check if exclude grading result
                return;

            _logger.Info("Start Import Grading Data...");
            Dictionary<string, string> gradingDics = new Dictionary<string, string>();

            try
            {
                _EDocGlobVar.GradingResultList = new List<List<Dictionary<string, string>>>();
                List<Dictionary<string, string>> tempListDic = new List<Dictionary<string, string>>();                

                foreach (var gp in gradePathList.Distinct())
                {
                    if (File.Exists(gp.GradeSumPath) == false)
                    {
                        _EDocGlobVar.MailInfo.Subject = "Error while import Grading result file. Wafer Id: "
                            + string.Join(",", gradePathList.Select(r => r.Wafer_ID).ToList());
                        _EDocGlobVar.MailInfo.Content = String.Format("Error while import Grading result file: {0}",
                            gradePathList.Select(r => r.GradeSumPath).ToList());
                        _EDocGlobVar.MailInfo.Level = 1;
                        break;
                    }
                    var tmpFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, gp.Wafer_ID);
                    if (Directory.Exists(tmpFolder) == false)
                        Directory.CreateDirectory(tmpFolder);
                    var tmpPath = Path.Combine(tmpFolder, new FileInfo(gp.GradeSumPath).Name);
                    File.Copy(gp.GradeSumPath, tmpPath, true);
                    _logger.Info("Copy to temp: " + tmpPath);
                    tempListDic = ExcelOperator.GetCsvDataToDic(tmpPath, 0, true);
                    _EDocGlobVar.GradingResultList.Add(tempListDic);
                    _logger.Info("Complete GetCsvDataToDic, count: " + tempListDic.Count);
                    Directory.Delete(tmpFolder, true);
                    //Collect Grading File Name
                    _EDocGlobVar.GradingFileList.Add(new FileInfo(gp.GradeSumPath).Name);
                }
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = "Error while import Grading result file. Wafer Id: " 
                    + string.Join(",", gradePathList.Select(r => r.Wafer_ID).ToList());
                _EDocGlobVar.MailInfo.Content = String.Format("Error while import Grading result file: {0}, Exception: {1}", 
                    string.Join(",", gradePathList.Select(r => r.GradeSumPath)), ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }


        }
        private void CheckBinCodeConflict(string waferId)
        {
            if (_EDocGlobVar.ExcludeGradingResult) //No need check if exclude grading result
                return;

            bool isCheckConfilct = true;
            StringBuilder errorMsgSB = new StringBuilder();            
            try
            {
                var conflictConfig = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigKey == "CheckBinCodeConflict" && r.ConfigType == waferId.Substring(0, 5) + "_Config");
                if (conflictConfig != null && conflictConfig.Count() > 0)
                {
                    if (conflictConfig.FirstOrDefault().ConfigValue == "N")
                        isCheckConfilct = false;
                }
                //gooDieBinStr
                var goodDieBins = new List<string>() { "1", "16" }; //Default
                var goodDieBinCfg = SpecUtil._eDocInfoList.Where(r => r.Type == "Config" && r.Key == "Good Die Bin code");
                if (goodDieBinCfg?.Any() ?? false) 
                    goodDieBins = goodDieBinCfg.FirstOrDefault().Value1.Split(",").ToList();

                if (isCheckConfilct)
                {
                    _logger.Info("Start Check BinCode Conflicts...");
                    foreach (var dt in _EDocGlobVar.GradingResultList)
                    {
                        var res = from gd in dt
                                  join rw in _EDocGlobVar.RWMapList on
                                  new { WaferID = gd["WAFER_ID"].ToString(), Gx = gd["GX"].ToString(), Gy = gd["GY"].ToString() }
                                  equals new { WaferID = rw.Wafer_Id, Gx = rw.IGx, Gy = rw.IGy }
                                  where (!goodDieBins.Contains(gd["PRE_AOI_BIN"].ToString()) && goodDieBins.Contains(rw.Bin_AOI2))
                                  select new
                                  {
                                      No = rw.No,
                                      Wafer_ID = rw.Wafer_Id,
                                      Gx = rw.IGx,
                                      Gy = rw.IGx,
                                      AOIBIN = gd["PRE_AOI_BIN"].ToString(),
                                      AOI2BIN = rw.Bin_AOI2,
                                      PARETO_CODE = gd["PARETO_CODE"].ToString(),
                                      Line_Data = rw.Line_Data
                                  };

                        if (res != null && res.Count() > 0)
                        {
                            //Alert conflicts and revise the ingested version
                            var conflictList = res.ToList();
                            string errorMsg = string.Empty;
                            errorMsgSB.AppendFormat("There are {0} die(s) with conflict between pre-aoi and AVI2 bincode as below:", conflictList.Count());
                            errorMsgSB.AppendLine();
                            errorMsgSB.AppendLine("No.;InputWafer;IGx;IGy;PNP Bin;Bin AOI1;Ogx;Ogy;Bin AOI2;GradingResult Bin;GradingResult Desc");
                            errorMsgSB.AppendLine();
                            for (int i = 0; i <= conflictList.Count() - 1; i++)
                            {
                                errorMsg = string.Format("{0};{1};{2}", conflictList[i].Line_Data, conflictList[i].AOIBIN, conflictList[i].PARETO_CODE);
                                errorMsgSB.AppendLine(errorMsg);
                                errorMsgSB.AppendLine();
                                //_logger.Error(errorMsg);
                                if (i > 500)
                                {
                                    errorMsgSB.AppendLine("...");
                                    break;
                                }
                                    
                            }
                            _EDocGlobVar.MailInfo.Subject = string.Format("AOI2 BinCode conflict with Grading result({0})", _EDocGlobVar.HeaderInfo.RW_Wafer_Id);
                            _EDocGlobVar.MailInfo.Content = errorMsgSB.ToString();
                            _EDocGlobVar.MailInfo.Level = 2;

                            //Revise Conflict File
                            if (conflictList.Any())
                            {
                                var noList = conflictList.Select(r => r.No).OrderBy(r => r).ToList();
                                var binList = conflictList.OrderBy(r => r.No).Select(r => r.AOIBIN).ToList();
                                ReviseNewAVI2File(noList, binList);
                            }
                        }
                    }
                    _logger.Info("Complete Check BinCode Conflicts...");
                }
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = string.Format("CheckBinCodeConflict exception! Wafer Id: {0}", waferId);
                _EDocGlobVar.MailInfo.Content = string.Format("CheckBinCodeConflict exception! Wafer Id: {0}, Exception: {1}", waferId, ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }

        }
        private void CheckGradingResultInSpec(string waferId)
        {
            if (_EDocGlobVar.ExcludeGradingResult) //No need check if exclude grading result
                return;

            try
            {
                _logger.Info("Check Grading Result In Spec...");
                //Get Columns from Spec
                var tMapSpecList = SpecUtil._eDocSpecList.Where(r => r.Type == "TMAP" && r.IsValidate == "Y");
                var goodDieBins = GetGoodDieBinCode(waferId.Substring(0, 12)).Select(r => r.AOI_BinCode);
                var waferGxGyList = _EDocGlobVar.RWMapList.Where(r => r.IGx != "X" && r.IGy != "X" && goodDieBins.Contains(r.Bin_AOI2))
                    .Select(r => r.IGx + "_" + r.IGy).Distinct().ToList();
                List<List<Dictionary<string, string>>> oosList = new List<List<Dictionary<string, string>>>();
                double lsl = 0.0;
                double usl = 0.0;
                string paraName;
                if (tMapSpecList.Any())
                {
                    foreach (var tMapSpec in tMapSpecList)
                    {                        
                        double.TryParse(tMapSpec.LSL, out lsl);
                        double.TryParse(tMapSpec.USL, out usl);
                        paraName = tMapSpec.Test_Parameter_Name.ToUpper().Trim();
                        //_logger.Info(string.Format("Check Parameter: {0} in spec, LSL: {1}, USL: {2}", tMapSpec.Parameter_name, lsl, usl));
                        if (string.IsNullOrEmpty(tMapSpec.ValLSL) == false && string.IsNullOrEmpty(tMapSpec.ValUSL) == false)
                        {
                            oosList = _EDocGlobVar.GradingResultList.Select(r => r.Where(s => (s[paraName].TryGetDouble() < lsl
                            || s[paraName].TryGetDouble() > usl) && s["PRE_AOI_PF"] == "P" && 
                            waferGxGyList.Contains(s["GX"] + "_" + s["GY"])).ToList()).ToList();
                        }
                        else if(string.IsNullOrEmpty(tMapSpec.ValLSL) == false && string.IsNullOrEmpty(tMapSpec.ValUSL))
                        {
                            oosList = _EDocGlobVar.GradingResultList.Where(r => r.Where(s => double.Parse(s[paraName]) < lsl && s["PRE_AOI_PF"] == "P" &&
                            waferGxGyList.Contains(s["GX"] + "_" + s["GY"])).Any()).ToList();
                        }
                        else
                        {
                            oosList = _EDocGlobVar.GradingResultList.Where(r => r.Where(s => double.Parse(s[paraName]) > usl && s["PRE_AOI_PF"] == "P" &&
                            waferGxGyList.Contains(s["GX"] + "_" + s["GY"])).Any()).ToList();
                        }

                        if (oosList.FirstOrDefault().Count() > 0)
                        {
                            //var oosItem = oosList.FirstOrDefault();
                            _EDocGlobVar.MailInfo.Subject = string.Format("Spec tightened fail, {0} (LSL={1},USL={2}) ({3})",
                                tMapSpec.Display_Paramenter_Name, tMapSpec.LSL, tMapSpec.USL, _EDocGlobVar.HeaderInfo.RW_Wafer_Id);
                            _EDocGlobVar.MailInfo.Content = ReviseOOSFileAndReturnMsg(oosList, tMapSpec, waferId);
                            _EDocGlobVar.MailInfo.Level = 2;
                            break;
                        }
                    }
                }
                _logger.Info("Complete Check Grading Result In Spec...");
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = string.Format("Check Grading Result In-Spec Failed! Wafer Id: {0}", waferId);
                _EDocGlobVar.MailInfo.Content = string.Format("Check Grading Result In-Spec Failed! Wafer Id: {0}, Exception: {1}", waferId, ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }

        }
        private string ReviseOOSFileAndReturnMsg(List<List<Dictionary<string, string>>> oosList, eDocSpecParameterClass tMapSpecItem, string waferId)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                var oosPath = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Output" && r.ConfigKey == "OOS").FirstOrDefault();
                var errPath = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Output" && r.ConfigKey == "Error").FirstOrDefault();
                var oosMaskPath = Path.Combine(oosPath.ConfigValue, waferId.Substring(0, 5));
                if (Directory.Exists(oosMaskPath) == false) 
                    Directory.CreateDirectory(oosMaskPath);
                if (Directory.Exists(errPath.ConfigValue) == false)
                    Directory.CreateDirectory(errPath.ConfigValue);

                var newFileName = string.Format(@"{0}\{1}\{2}_AUTORMP_{3}.txt",
                    oosPath.ConfigValue, waferId.Substring(0, 5), System.IO.Path.GetFileNameWithoutExtension(_EDocGlobVar.AVI2FilePath),
                    tMapSpecItem.Display_Paramenter_Name, DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                var lines = System.IO.File.ReadAllLines(_EDocGlobVar.AVI2FilePath);
                StringBuilder newLineSB = new StringBuilder();

                string[] values;
                bool isLineStart = false;
                string tmpMsg = string.Empty;                
                for (int i = 0; i < lines.Length; i++)
                {
                    if (isLineStart)
                    {
                        values = lines[i].Split(";");
                        var gx = values[2];
                        var gy = values[3];
                        var n = values.Length;
                        var oosItem = oosList.FirstOrDefault().Where(r => r["GX"] == gx && r["GY"] == gy).FirstOrDefault();
                        if (oosItem != null)
                        {
                            values[n - 1] = tMapSpecItem.Bin_Code;
                            lines[i] = string.Join(";", values);
                            tmpMsg = string.Format("{0}({1}) bincode changed to {2}", lines[i], tMapSpecItem.Test_Parameter_Name, tMapSpecItem.Bin_Code);
                            //_logger.Info(tmpMsg);
                            sb.Append(tmpMsg);
                            //sb.AppendFormat("{0}({1}) bincode changed to {2}", lines[i], tMapSpecItem.Test_Parameter_Name, tMapSpecItem.Bin_Code);
                            sb.AppendLine();
                        }
                    }

                    if (lines[i].ToUpper().IndexOf("INPUTWAFER") > -1)
                    {
                        //newLineSB.Append(lines[i]);
                        isLineStart = true;
                    }
                    
                    newLineSB.AppendLine(lines[i]);
                }
                if (newLineSB.Length > 0)
                {
                    //Copy source file into error folder
                    var fi = new FileInfo(_EDocGlobVar.AVI2FilePath);
                    fi.CopyTo(Path.Combine(errPath.ConfigValue, fi.Name), true);

                    if (File.Exists(newFileName))
                        File.Delete(newFileName);

                    File.WriteAllText(newFileName, newLineSB.ToString(), Encoding.ASCII);
                    _EDocGlobVar.MailInfo.Attachments = new List<string>();
                    _EDocGlobVar.MailInfo.Attachments.Add(newFileName);
                }
            }
            catch (Exception)
            {
                throw;
            }
            return sb.ToString();
        }
        private void GeneratEDocProcess(string waferId)
        {
            try
            {                
                var eDocConfig = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigKey == "EMAP_TMAP_COC_Config" && r.ConfigType == waferId.Substring(0, 5) + "_Config");
                if (!eDocConfig.Any())
                {
                    eDocConfig = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigKey == "EMAP_TMAP_COC_Config" && r.ConfigType == "Default_Config");
                }
                var configArry = eDocConfig.FirstOrDefault().ConfigValue.Split(";");
                //eMap
                if (YNParser(configArry[0]))
                {
                    using (var sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]))
                    {
                        //Get EMapVersion
                        _EDocGlobVar.EMapVersion = sqlConn.Query<string>(string.Format("select EMapVersion from TBL_eDoc_Spec where Mask = '{0}' ",
                        waferId.Substring(0, 5))).FirstOrDefault();
                    }
                    if (string.IsNullOrEmpty(_EDocGlobVar.EMapVersion))
                        _EDocGlobVar.EMapVersion = _config["Configurations:DefaultEMapVersion"];
                    CreateEMAP();
                    //Whether to check good die qty = 0?
                    if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false)
                    {
                        return;
                    }
                }
                //tMap
                if (YNParser(configArry[1]))
                {
                    CreateTMAP(waferId);
                    if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false)
                    {
                        return;
                    }
                }
                //COC
                if (YNParser(configArry[2]))
                {
                    CreateCOC();
                    if (string.IsNullOrEmpty(_EDocGlobVar.MailInfo.Content) == false)
                    {
                        return;
                    }
                }
                _logger.Info("Copy to Output folder: " + _EDocGlobVar.EDocResultPath);
                CopyToOutputFolder();
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void CreateEMAP()
        {
            _logger.Info("Generate EMAP");
            try
            {
                //Update BinCode
                var binCodeMap = UpdateBinCodeAndRtnMap();
                //Get Binning Column

                //Get Total Row and Col
                int max_xx = SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "EMAP Total Row").FirstOrDefault().Value1.TryGetInt().Value;
                _logger.Info("EMAP Total Row: " + max_xx);
                int max_yy = SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "EMAP Total Col").FirstOrDefault().Value1.TryGetInt().Value;
                _logger.Info("EMAP Total Col: " + max_yy);

                //Null Area
                binCodeMap.Add(new eDocBincodeMapClass()
                {
                    EMap_BinCode = "F",
                    BinQuality = "Null",
                    BinDescription = "Null",
                    BinCount = (max_xx * max_yy) - _EDocGlobVar.RWMapList.Count()
                });

                //[BIN] (Bin Code Content)
                string binContent = GetBinCodeContent(binCodeMap);
                //Content
                string content = GetEMapContent(max_xx, max_yy);

                //Whether to check good die qty = 0?
                UpdateEMapTemplate(binContent, content, max_xx.ToString(), max_yy.ToString());
                _logger.Info(string.Format("Generate EMAP Complete! Good Die Qty: {0}", _EDocGlobVar.GoodDieQty));
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = string.Format("Error while creating EMAP! Wafer Id: {0}", _EDocGlobVar.HeaderInfo.Wafer_Id);
                _EDocGlobVar.MailInfo.Content = string.Format("CreateEMAP Failed! Wafer Id: {0}, Exception: {1}", _EDocGlobVar.HeaderInfo.Wafer_Id, ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }
        }
        private void CreateTMAP(string wafer_Id)
        {
            _logger.Info("Generate TMAP");
            try
            {
                StringBuilder sb = new StringBuilder();
                //Get Columns from Spec
                var tMapParaSpecList = SpecUtil._eDocSpecList.Where(r => r.Parameter_name.Contains("TMAP Parameter")).ToList();
                if (tMapParaSpecList == null || tMapParaSpecList.Count == 0)
                {
                    _EDocGlobVar.MailInfo.Content = "Missing TMap Parameter Info!! Wafer Id: " + wafer_Id;
                    _EDocGlobVar.MailInfo.Level = 1;
                    return;
                }
                List<string> tMapSpecHDCols = tMapParaSpecList.Select(r => r.Display_Paramenter_Name).ToList(); //Header for Display usage
                List<string> tMapSpecTPCols = tMapParaSpecList.Select(r => r.Test_Parameter_Name).ToList(); //For getting test parameter from grading usage
                //Get Columns from Database
                var tMapDefCols = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigKey == "TMAP_Parameters" && r.ConfigType == wafer_Id.Substring(0, 5) + "_Config");
                List<string> configColList = tMapDefCols != null && tMapDefCols.Count() > 0
                    ? tMapDefCols.Select(r => r.ConfigValue).FirstOrDefault().Split(',').ToList()
                    : _EDocGlobVar.EDocConfigList.Where(r => r.ConfigKey == "TMAP_Parameters" && r.ConfigType == "Default_Config").Select(r => r.ConfigValue).FirstOrDefault().Split(',').ToList();

                var tMapHeader = string.Join(",", configColList).Replace("{TMAP_Columns}", string.Join(",", tMapSpecHDCols));
                List<string> tMapParaList = new List<string>();
                configColList.Remove("{TMAP_Columns}");
                tMapParaList.AddRange(configColList);
                tMapParaList.AddRange(tMapSpecTPCols);
                sb.AppendLine(tMapHeader);
                foreach (var dt in _EDocGlobVar.GradingResultList)
                {
                    var mergeTMapList = (from rw in _EDocGlobVar.RWMapList
                                         join grr in dt on
                                      new { WaferID = rw.Wafer_Id, Gx = rw.IGx, Gy = rw.IGy }
                                      equals new { WaferID = grr["WAFER_ID"].ToString(), Gx = grr["GX"].ToString(), Gy = grr["GY"].ToString() } into subGrp
                                         from grr in subGrp.DefaultIfEmpty()
                                         where rw.No > 0 && new string[] { "X", "x" }.Contains(rw.IGx) == false
                                         select new eDocTMapMergeClass { GradingItem = grr, RWItem = rw }).ToList();

                    foreach (var item in mergeTMapList)
                    {
                        sb.AppendLine(GetTMapRowString(item, tMapParaList, tMapParaSpecList));
                    }
                }

                string fileName = string.Format("{0}_TMAP.csv", _EDocGlobVar.HeaderInfo.RW_Wafer_Id);
                WriteAllTextToTemp(fileName, sb.ToString());
                _OutputFileDic.Add("TMAP", fileName);
                //File.WriteAllText(newTMapPath, sb.ToString(), Encoding.ASCII);
                _logger.Info("Generate TMAP Complete!");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.StackTrace);
                _EDocGlobVar.MailInfo.Subject = string.Format("Error while creating TMAP! Wafer Id: {0}", _EDocGlobVar.HeaderInfo.Wafer_Id);
                _EDocGlobVar.MailInfo.Content = string.Format("CreateTMAP Failed! Wafer Id: {0}, Exception: {1}", _EDocGlobVar.HeaderInfo.Wafer_Id, ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }
        }
        private void CreateCOC()
        {
            _logger.Info("Generate COC");
            try
            {
                bool isSpecialCoCFormat = IsSpecialCoCFormat();
                StringBuilder sb = new StringBuilder();
                string fileName = string.Format("{0}_COC.csv", _EDocGlobVar.HeaderInfo.RW_Wafer_Id);
                var waferList = _EDocGlobVar.RWMapList.Where(r=> r.Wafer_Id != null && r.Wafer_Id != "Fiducial")
                    .Select(r => new { Wafer_ID = r.Wafer_Id, RW_Wafer_Id = r.RW_Wafer_Id }).Distinct().ToList();
                int waferCount = waferList.Count();

                #region Fixed CoC Header Info  
                sb.AppendLine(string.Format("PRODUCT_NAME,{0}", RepStrSepByComma(GetEDocInfoConfig("CoC", "PRODUCT_NAME"), waferCount)));
                sb.AppendLine(string.Format("PRODUCT_MCO,{0}", RepStrSepByComma(GetEDocInfoConfig("CoC", "PRODUCT_MCO"), waferCount)));
                sb.AppendLine(string.Format("VENDOR_NAME,{0}", RepStrSepByComma(GetEDocInfoConfig("CoC", "VENDOR_NAME"), waferCount)));
                sb.AppendLine(string.Format("VENDOR_MPN,{0}", RepStrSepByComma(GetEDocInfoConfig("CoC", "VENDOR_MPN"), waferCount)));
                sb.AppendLine(string.Format("RW_LOT_ID,{0}", _EDocGlobVar.HeaderInfo.RW_Wafer_Id.Substring(0, 10), waferCount));
                sb.AppendLine(String.Format("RW_LOT_TOTAL_DIE_QTY,{0}", GetTotalDieQty()));
                sb.Append("RW_LOT_GOOD_DIE_QTY");
                foreach (var waferItem in waferList)
                {
                    sb.AppendFormat(",{0}", GetGoodDieQtyByWafer(waferItem.Wafer_ID));
                }
                sb.AppendLine();
                sb.AppendLine("RW_LOT_SHIPPING_DATE,[SHIPPING_DATE]");
                sb.AppendLine("RW_LOT_SHIP_TO_MI_NAME,[SHIPPING_TO]");
                sb.AppendLine(string.Format("RW_LOT_COC_FILENAME,{0}", fileName, waferCount));
                sb.AppendLine(string.Format("RW_LOT_UV_TAPE_PART#,{0}", RepStrSepByComma(GetEDocInfoConfig("CoC", "UV_TAPE_PART#"), waferCount)));
                sb.AppendLine(string.Format("RW_LOT_FINAL_AVI_REV#,{0}", RepStrSepByComma(_EDocGlobVar.HeaderInfo.Recipe, waferCount)));
                sb.AppendLine(string.Format("RW_LOT_PROBE_FLOW_REV#,{0}", RepStrSepByComma(_EDocGlobVar.POR_Version, waferCount)));
                sb.AppendLine(string.Format("RW_WF_ID,{0}", RepStrSepByComma(_EDocGlobVar.HeaderInfo.RW_Wafer_Id, waferCount)));
                sb.AppendLine(string.Format("RW_WF_EMAP_FILENAME,{0}", RepStrSepByComma(_OutputFileDic["EMAP"], waferCount)));
                sb.AppendLine(string.Format("RW_WF_TMAP_FILENAME,{0}", RepStrSepByComma(_OutputFileDic["TMAP"], waferCount)));

                if (isSpecialCoCFormat) //20221019 for Raven new format Usage
                    sb.AppendLine(string.Format("RW_WF_FAB_WF_QTY_USED,{0}", RepStrSepByComma(waferCount.ToString(), waferCount)));
                else
                    sb.AppendLine(string.Format("RW_WF_TOTAL_FAB_WF_QTY_USED,{0}", RepStrSepByComma(waferCount.ToString(), waferCount)));

                sb.AppendLine(string.Format("RW_WF_SORT_DATE,{0}", RepStrSepByComma(_EDocGlobVar.HeaderInfo.Sort_Date.ToString("MM/dd/yyyy"), waferCount)));
                sb.AppendLine(String.Format("RW_WF_TOTAL_DIE_QTY,{0}", GetTotalDieQty()));
                sb.Append("RW_WF_GOOD_DIE_QTY");
                foreach (var waferItem in waferList)
                {
                    sb.AppendFormat(",{0}", GetGoodDieQtyByWafer(waferItem.Wafer_ID));
                }
                sb.AppendLine();
                //20221017 Jacky added for Extra_COC_Parameter (RW_WF_WAIVER_STATUS)                
                var extraCocList = GetExtraCoCParameters(_EDocGlobVar.HeaderInfo.Wafer_Id);
                if (extraCocList.Any())
                {
                    foreach (var para in extraCocList)
                    {
                        sb.AppendLine(string.Format("{0},{1}", para, RepStrSepByComma(GetEDocInfoConfig("CoC", para), waferCount)));
                    }
                }
                #endregion

                #region LoadCOCParameters
                var cocSpecDics = GetSpecCOCParaDics(isSpecialCoCFormat) ;
                foreach (var cocKvp in cocSpecDics)
                {
                    sb.AppendLine(string.Format("{0},{1}", cocKvp.Key, cocKvp.Value));
                }
                #endregion

                //File.WriteAllText(newCOCPath, sb.ToString(), Encoding.ASCII);
                WriteAllTextToTemp(fileName, sb.ToString());
                _OutputFileDic.Add("COC", fileName);
                _logger.Info("Generate COC Complete!");
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = "Error while creating COC! Wafer Id: " + _EDocGlobVar.HeaderInfo.Wafer_Id;
                _EDocGlobVar.MailInfo.Content = String.Format("Error while creating COC! Wafer Id: {0}, Exception: {1}",
                    _EDocGlobVar.HeaderInfo.Wafer_Id, ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }
        }
        #endregion

        #region Utils
        private List<string> GetWaferListByRWID(string RW_Wafer_Id)
        {
            var sql = string.Format(@"select distinct Wafer_Id from 
                    TBL_AVI2_RawData where RW_Wafer_Id = '{0}' and Wafer_Id not in ('Fiducial')", RW_Wafer_Id);
            try
            {
                var list = new List<string>();
                using (var sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]))
                {
                    list = sqlConn.Query<string>(sql).ToList();
                    if (list.Count == 0)
                    {
                        _logger.Info("GetWaferListByRWID - No data found!");
                    }
                }
                return list;
            }
            catch (Exception)
            {
                throw;
            }
        }
        public bool GetEDocConfigList()
        {
            _EDocGlobVar.EDocConfigList = new List<eDocConfigClass>();
            try
            {
                _EDocGlobVar.EDocConfigList = ConnectionHelper.GetEDocConfigList(_config, Program._MachineName);

                if (_EDocGlobVar.EDocConfigList.Count == 0)
                {
                    _EDocGlobVar.MailInfo.Content = string.Format("GetEDocConfigList - No data found! Server Name: {0}", Program._MachineName);
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
        private List<Dictionary<string, object>> GetDics(DataTable dt)
        {
            return dt.AsEnumerable()
                 .Select(row => row.Table.Columns.Cast<DataColumn>()
                 .ToDictionary(col => col.ColumnName, col => row[col])).ToList();
        }
        private string ReviseNewAVI2File(List<int> noList, List<string> binCodeList)
        {
            try
            {
                var conflictPath = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Output" && r.ConfigKey == "BinCodeConfilct").FirstOrDefault();
                if (Directory.Exists(conflictPath.ConfigValue) == false)
                    Directory.CreateDirectory(conflictPath.ConfigValue);
                var newFileName = string.Format(@"{0}\{1}_autoremap_{2}.txt",
                    conflictPath.ConfigValue, System.IO.Path.GetFileNameWithoutExtension(_EDocGlobVar.AVI2FilePath), DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                var lines = System.IO.File.ReadAllLines(_EDocGlobVar.AVI2FilePath);
                StringBuilder newLineSB = new StringBuilder();

                string[] values;
                bool islineStart = false;

                for (int i = 0; i < lines.Length; i++)
                {
                    if (islineStart)
                    {
                        values = lines[i].Split(";");
                        var no = values[0];
                        var n = values.Length;
                        for (int j = 0; j < noList.Count(); j++)
                        {
                            if (noList[j].ToString() == no)
                            {
                                values[n - 1] = binCodeList[j];
                                lines[i] = string.Join(";", values);
                            }
                        }
                    }

                    if (lines[i].ToUpper().IndexOf("INPUTWAFER") > -1)
                    {
                        //newLineSB.Append(lines[i]);
                        islineStart = true;                       
                    }
                    
                    newLineSB.AppendLine(lines[i]);
                }
                if (newLineSB.Length > 0)
                {
                    if (File.Exists(newFileName))
                        File.Delete(newFileName);
                    File.WriteAllText(newFileName, newLineSB.ToString(), Encoding.ASCII);
                    _EDocGlobVar.MailInfo.Attachments = new List<string>();
                    _EDocGlobVar.MailInfo.Attachments.Add(newFileName);
                }
            }
            catch (Exception)
            {

                throw;
            }

            return string.Empty;
        }
        private bool YNParser(string input)
        {
            if (string.IsNullOrEmpty(input) == false)
            {
                if (input == "Y") return true;
            }
            return false;
        }
        private string GetEDocInfoConfig(string type, string key)
        {
            try
            {
                var eDocInfoCfg = SpecUtil._eDocInfoList.Where(r => r.Type == type && r.Key.ToUpper() == key);
                if (eDocInfoCfg != null && eDocInfoCfg.Any())
                {
                    return eDocInfoCfg.FirstOrDefault().Value1;
                }               
            }
            catch (Exception)
            {
                throw;
            }
            return string.Empty;
        }
        private string RepStrSepByComma(string input, int count)
        {
            try
            {
                return string.Join(",", Enumerable.Repeat(input, count));
            }
            catch (Exception)
            {

                throw;
            }
        }
        private void WriteAllTextToTemp(string fileName, string content)
        {
            try
            {
                string tempFolder = Path.Combine(_EDocGlobVar.EDocResultPath, "Temp");
                if (Directory.Exists(tempFolder) == false)
                    Directory.CreateDirectory(tempFolder);
                string fileTempPath = Path.Combine(tempFolder, fileName);
                File.WriteAllText(fileTempPath, content, Encoding.ASCII);
            }
            catch (Exception)
            {
                throw;
            }            
        }
        private void CopyToOutputFolder()
        {
            try
            {
                string dtNow = DateTime.Now.ToString("yyyyMMddHHmmss");
                string reNameFN = string.Empty;
                string outputFN = string.Empty;
                var files = Directory.GetFiles(_EDocGlobVar.EDocResultPath);
                if (files.Any())
                {
                    foreach (var file in files)
                    {
                        if (file.Contains("_OLD_"))
                            continue;                            
                        reNameFN = string.Format("{0}_OLD_{1}", file, dtNow);
                        File.Move(file, reNameFN);
                    }
                }
                string tempFolder = Path.Combine(_EDocGlobVar.EDocResultPath, "Temp");
                files = Directory.GetFiles(tempFolder);
                if (files.Any())
                {
                    foreach (var file in files)
                    {
                        File.Move(file, file.Replace("\\Temp", ""));
                    }
                }
                if (Directory.GetFiles(tempFolder).Count() == 0)
                    Directory.Delete(tempFolder);

                //Copy AVI2 
                File.Copy(_EDocGlobVar.AVI2FilePath, Path.Combine(_EDocGlobVar.EDocResultPath, new FileInfo(_EDocGlobVar.AVI2FilePath).Name));

                //Build ORG_WAFER File
                var waferList = _EDocGlobVar.RWMapList.Where(r => r.Wafer_Id != null && r.Wafer_Id != "Fiducial").Select(r => r.Wafer_Id).Distinct();
                var orgWaferResultFile = Path.Combine(_EDocGlobVar.EDocResultPath, string.Format("{0}_ORG_WAFER.txt", _EDocGlobVar.HeaderInfo.RW_Wafer_Id));
                File.WriteAllText(orgWaferResultFile, string.Join(",", waferList), Encoding.ASCII);
            }
            catch (Exception)
            {
                throw;
            }
        }
        private string GetWaferIdsStr()
        {
            List<string> waferList = new List<string>();
            StringBuilder sb = new StringBuilder();
            try
            {
                if (_EDocGlobVar.RWMapList != null)
                {
                    waferList = _EDocGlobVar.RWMapList.Where(r => r.Wafer_Id != null && r.Wafer_Id != "Fiducial")
                        .Select(r => r.Wafer_Id).Distinct().ToList();
                    foreach (var item in waferList)
                    {
                        sb.Append(item).Append(",");
                    }
                    for (int i = 0; i < 5 - waferList.Count; i++)
                    {
                        sb.Append(",");
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            return sb.ToString();
        }
        private string GetGradingPaths()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                foreach (var item in _EDocGlobVar.GradingFileList)
                {
                    sb.Append(item).Append(",");
                }
                for (int i = 0; i < 5 - _EDocGlobVar.GradingFileList.Count; i++)
                {
                    sb.Append(",");
                }
            }
            catch (Exception)
            {
                throw;
            }
            return sb.ToString();
        }
        #endregion

        #region EMAP Methods
        private List<eDocBincodeMapClass> UpdateBinCodeAndRtnMap()
        {
            List<eDocBincodeMapClass> binCodeList = new List<eDocBincodeMapClass>();
            List<eDocBincodeMapClass> specialBinCodeList = new List<eDocBincodeMapClass>();
            try
            {
                var waferList = _EDocGlobVar.RWMapList.Where(r=> r.Wafer_Id != null && r.Wafer_Id != "Fiducial").Select(r => r.Wafer_Id).Distinct();
                string mask = waferList.FirstOrDefault().ToUpper().Substring(0, 5);

                foreach (var wafer_Id in waferList)
                {
                    //Get GoodDieBin
                    binCodeList = GetGoodDieBinCode(wafer_Id.Substring(0, 12));

                    //IsSpidr (WD001/WD013)
                    //bool isSpidr = (mask == "WD001" || mask == "WD013") ? true : false;

                    //Bincode maps for Special Fail BinCode (For Bin L usage)
                    specialBinCodeList.AddRange(GetBinCodeMaps(wafer_Id.Substring(0, 5)));
                }

                foreach (var item in binCodeList)
                {
                    var res = _EDocGlobVar.RWMapList.Where(r => r.Bin_AOI2 == item.AOI_BinCode && r.Wafer_Id != "Fiducial");
                    if (res != null && res.Count() > 0)
                    {
                        res.ToList().ForEach(r => r.EMap_BinCode = item.EMap_BinCode);
                        item.BinCount = item.BinCount + res.Count();
                        if (item.BinQuality == "Pass") //Collect good die qty
                            _EDocGlobVar.GoodDieQty = _EDocGlobVar.GoodDieQty + res.Count();
                    }                    
                }

                //Update Fiducial to eMap Bin Z
                var fidList = _EDocGlobVar.RWMapList.Where(r => r.Device == "Fiducial").ToList();
                fidList.ForEach(r => r.EMap_BinCode = "Z");
                binCodeList.Add(new eDocBincodeMapClass()
                {
                    EMap_BinCode = "Z",
                    BinQuality = "Fail",
                    BinDescription = "Fiducial",
                    BinCount = fidList.Count()
                });

                //Update Fail Die to eMp Bin X
                var goodDieBins = binCodeList.Select(r => r.AOI_BinCode).Distinct().ToList();
                var specialFailBins = specialBinCodeList.Select(r => r.AOI_BinCode).Distinct().ToList();
                var failDieList = _EDocGlobVar.RWMapList.Where(r => r.Device != "Fiducial" && r.Bin_AOI2 != null
                && goodDieBins.Contains(r.Bin_AOI2) == false && specialFailBins.Contains(r.Bin_AOI2) == false 
                && r.Wafer_Id != null)
                    .ToList();
                failDieList.ForEach(r => r.EMap_BinCode = "X");
                binCodeList.Add(new eDocBincodeMapClass()
                {
                    EMap_BinCode = "X",
                    BinQuality = "Fail",
                    BinDescription = "AVI Fail",
                    BinCount = failDieList.Count()
                });

                //Update No Die Bin to eMap Bin W
                var noDieList = _EDocGlobVar.RWMapList.Where(r => r.Device != "Fiducial" && r.Bin_AOI2 == null).ToList();
                noDieList.ForEach(r => r.EMap_BinCode = "W");
                binCodeList.Add(new eDocBincodeMapClass()
                {
                    EMap_BinCode = "W",
                    BinQuality = "No Die",
                    BinDescription = "No Die",
                    BinCount = noDieList.Count()
                });

                //Add Special Bincode (ex: L)
                //Seperate version 20221117 backup to avoid Shasta CR
                //foreach (var item in specialBinCodeList)
                //{
                //    var specialBinList = _EDocGlobVar.RWMapList.Where(r => r.Bin_AOI2 == item.AOI_BinCode && r.Bin_AOI2 == item.AOI_BinCode);
                //    if (specialBinList != null && specialBinList.Count() > 0)
                //    {
                //        specialBinList.ToList().ForEach(r => r.EMap_BinCode = item.EMap_BinCode);
                //        binCodeList.Add(new eDocBincodeMapClass()
                //        {
                //            EMap_BinCode = item.EMap_BinCode,
                //            BinQuality = item.BinQuality,
                //            BinDescription = item.BinDescription,
                //            BinCount = specialBinList.Count()
                //        });
                //    }
                //}

                //Add Special Bincode (ex: L) for grouped by same eMap Bin Code
                var specialBinGroupList = specialBinCodeList.GroupBy(r => r.EMap_BinCode).ToDictionary(o => o.Key, o => o.ToList());
                foreach (var item in specialBinGroupList)
                {
                    var groupedBins = item.Value.Select(r => r.AOI_BinCode).ToList();
                    var specialBinList = _EDocGlobVar.RWMapList.Where(r => groupedBins.Contains(r.Bin_AOI2));
                    if (specialBinList != null && specialBinList.Count() > 0)
                    {
                        specialBinList.ToList().ForEach(r => r.EMap_BinCode = item.Key); //Updated to grouped eMap bincode(ex: bin L)
                        binCodeList.Add(new eDocBincodeMapClass()
                        {
                            EMap_BinCode = item.Key,
                            BinQuality = item.Value.FirstOrDefault().BinQuality,
                            BinDescription = string.Join(", ", item.Value.OrderBy(r=> r.AOI_BinCode).Select(r=> r.BinDescription).ToList()),
                            BinCount = specialBinList.Count()
                        });
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return binCodeList;
        }
        private void UpdateBinCodeOld(string pass_code)
        {
            //Pass Code
            List<string> passCodeList = new List<string>() { pass_code };

            //gooDieBinStr
            var goodDieBins = new List<string>() { "1", "16" }; //Default
            try
            {
                var goodDieBinCfg = SpecUtil._eDocInfoList.Where(r => r.Type == "Config" && r.Key == "Good Die Bin code");
                if (goodDieBinCfg?.Any() ?? false)
                    goodDieBins = goodDieBinCfg.FirstOrDefault().Value1.Split(",").ToList();

                List<string> waferList = new List<string>();
                if (pass_code == "16")
                {
                    waferList = _EDocGlobVar.RWMapList.Where(r => r.Wafer_Id != "fiducial" && passCodeList.Contains(r.Bin_AOI1)
                                        && r.Bin_AOI2 == "1").Select(r => r.Wafer_Id).Distinct().ToList();
                }
                else
                {
                    waferList = _EDocGlobVar.RWMapList.Where(r => r.Wafer_Id != "fiducial" && goodDieBins.Contains(r.Bin_AOI2))
                                        .Select(r => r.Wafer_Id).Distinct().ToList();
                }

                foreach (var wafer in waferList)
                {
                    
                }
            }
            catch (Exception)
            {

                throw;
            }

        }
        private List<eDocBincodeMapClass> GetGoodDieBinCode(string wafer_Id)
        {
            List<eDocBincodeMapClass> list = new List<eDocBincodeMapClass>();
            string defaultEMapBinCode = "A";
            //goodDieBinStr
            var goodDieBins = new List<string>() { "1" }; //Default

            var goodDieBinCfg = SpecUtil._eDocInfoList.Where(r => r.Type == "Config" && r.Key == "Good Die Bin code");
            if (goodDieBinCfg?.Any() ?? false)
            {
                goodDieBins = goodDieBinCfg.FirstOrDefault().Value1.Split(",").ToList();
            }

            ////Is combine_bin1_and_bin16
            bool isCombineBin16 = false;
            var bin16Cfg = SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP"
                                    && r.Key == "Combine Bin1 and Bin16(Y/N)");
            if (bin16Cfg?.Any() ?? false)
                isCombineBin16 = bin16Cfg.FirstOrDefault().Value1.Trim().ToUpper() == "Y" ? true : false;

            foreach (var bin in goodDieBins)
            {
                if (isCombineBin16 && bin == "16")
                    continue;
                //Default
                list.Add(new eDocBincodeMapClass()
                {
                    AOI_BinCode = bin,
                    EMap_BinCode = defaultEMapBinCode,
                    BinQuality = "Pass",
                    BinDescription = String.Format("Pass Bin{0} from fab wafer {1}", bin, wafer_Id)
                });
            }

            return list;
        }
        private List<eDocBincodeMapClass> GetBinCodeMaps(string wafer_id)
        {
            List<eDocBincodeMapClass> list = new List<eDocBincodeMapClass>();

            ConnectionHelper connectionHelper = new ConnectionHelper(_config);
            var sql = string.Format(@"select * from TBL_BinCode_Mapping Where Mask = '{0}' and Status = 1 ", wafer_id.Substring(0, 5));

            try
            {
                using (var sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]))
                {
                    list = sqlConn.Query<eDocBincodeMapClass>(sql).ToList();
                }
                return list;
            }
            catch (Exception ex)
            {
                _EDocGlobVar.MailInfo.Subject = String.Format("Exception while GetBinCodeMaps! Wafer Id: {0}", wafer_id);
                _EDocGlobVar.MailInfo.Subject = String.Format("Exception while GetBinCodeMaps! Wafer Id: {0}, Exception: {1}", wafer_id, ex.Message);
                _EDocGlobVar.MailInfo.Level = 1;
            }
            return list;
        }
        private string GetEMapContent(int max_xx, int max_yy)
        {
            //Content
            StringBuilder content = new StringBuilder();
            eDocRWMapClass item = null;
            try
            {
                for (int xx = 1; xx <= max_xx; xx++)
                {
                    content.Append("<Row><![CDATA[");
                    for (int yy = 1; yy <= max_yy; yy++)
                    {
                        item = _EDocGlobVar.RWMapList.Where(r => r.OGx == yy.ToString() && r.OGy == xx.ToString()).FirstOrDefault();
                        if (item != null)
                        {
                            content.Append(item.EMap_BinCode);
                        }
                        else
                        {
                            content.Append("F");
                        }
                    }
                    content.AppendLine("]]></Row>");
                }
            }
            catch (Exception)
            {
                throw;
            }

            return content.ToString();
        }
        private string GetFiducialContent()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                var list = SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key.Contains("ReferenceDevice")).OrderBy(r => r.Key);                
                if (list != null && list.Count() > 0)
                {
                    for (int i = 1; i <= list.Count(); i++)
                    {
                        var arry = list.Where(r => r.Key == string.Format("ReferenceDevice{0}_X_Y", i.ToString())).FirstOrDefault().Value1.Split(",");
                        sb.AppendFormat(@"<ReferenceDevice ReferenceDeviceX=""{0}"" ReferenceDeviceY=""{1}""/>", arry[0], arry[1]);
                        sb.AppendLine();
                    }
                }
                else
                {
                    _EDocGlobVar.MailInfo.Content = "Missing Reference Device Information! Wafer Id: " + _EDocGlobVar.HeaderInfo.Wafer_Id;
                    _EDocGlobVar.MailInfo.Level = 1;
                }
            }
            catch (Exception)
            {
                throw;
            }
            return sb.ToString();
        }
        private string GetBinCodeContent(List<eDocBincodeMapClass> binCodeMap)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                foreach (var item in binCodeMap)
                {
                    if (item.BinCount == 0)
                        continue;
                    sb.AppendLine("<Bin");
                    sb.AppendLine(string.Format("\tBinCode=\"{0}\"", item.EMap_BinCode));
                    sb.AppendLine(string.Format("\tBinQuality=\"{0}\"", item.BinQuality));
                    sb.AppendLine(string.Format("\tBinCount=\"{0}\"", item.BinCount.ToString()));
                    sb.AppendLine(string.Format("\tBinDescription=\"{0}\"", item.BinDescription));
                    sb.AppendLine("/>");
                }
            }
            catch (Exception)
            {
                throw;
            }
            return sb.ToString();
        }
        private void UpdateEMapTemplate(string binContent, string content, string row, string col)
        {
            var eMapTemplateStr = File.ReadAllText(SpecUtil._EMapTemplate);
            string fileName = string.Format("{0}.xml", _EDocGlobVar.HeaderInfo.RW_Wafer_Id);

            eMapTemplateStr = eMapTemplateStr.Replace("[WaferId]", _EDocGlobVar.HeaderInfo.RW_Wafer_Id);
            eMapTemplateStr = eMapTemplateStr.Replace("[SubstrateId]", _EDocGlobVar.HeaderInfo.RW_Wafer_Id);
            eMapTemplateStr = eMapTemplateStr.Replace("[EmapSWVer]", _EDocGlobVar.EMapVersion);
            eMapTemplateStr = eMapTemplateStr.Replace("[ProductId]", SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "EMAP Product ID").FirstOrDefault().Value1);
            var rw_lotid = _EDocGlobVar.HeaderInfo.RW_Wafer_Id.Substring(0, 10);
            if (_EDocGlobVar.HeaderInfo.RW_Wafer_Id.IndexOf("-R-") > -1)
                rw_lotid = _EDocGlobVar.HeaderInfo.RW_Wafer_Id.Substring(0, 9); //Old case
            eMapTemplateStr = eMapTemplateStr.Replace("[LotId]", rw_lotid);
            eMapTemplateStr = eMapTemplateStr.Replace("[SPEC_Orientation]", SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "Orientation").FirstOrDefault().Value1);
            eMapTemplateStr = eMapTemplateStr.Replace("[SPEC_wafersize]", SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "Wafersize").FirstOrDefault().Value1);
            eMapTemplateStr = eMapTemplateStr.Replace("[SPEC_DeviceSizeX]", SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "DeviceSizeX").FirstOrDefault().Value1);
            eMapTemplateStr = eMapTemplateStr.Replace("[SPEC_DeviceSizeY]", SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "DeviceSizeY").FirstOrDefault().Value1);
            eMapTemplateStr = eMapTemplateStr.Replace("[SPEC_StepSizeX]", SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "StepSizeX").FirstOrDefault().Value1);
            eMapTemplateStr = eMapTemplateStr.Replace("[SPEC_StepSizeY]", SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "StepSizeY").FirstOrDefault().Value1);
            eMapTemplateStr = eMapTemplateStr.Replace("[Rows]", row);
            eMapTemplateStr = eMapTemplateStr.Replace("[Columns]", col);
            eMapTemplateStr = eMapTemplateStr.Replace("[FrameId]", _EDocGlobVar.HeaderInfo.RW_Wafer_Id);
            eMapTemplateStr = eMapTemplateStr.Replace("[NullBin]", "F");
            var dtStr = DateTime.Now.ToString("dd-MMM-yyyy hh:mm:ss");
            eMapTemplateStr = eMapTemplateStr.Replace("[CreateDate]", dtStr);
            eMapTemplateStr = eMapTemplateStr.Replace("[Status]", SpecUtil._eDocInfoList.Where(r => r.Type == "EMAP" && r.Key == "EMAP Status").FirstOrDefault().Value1);            
            eMapTemplateStr = eMapTemplateStr.Replace("[BIN]", binContent);
            eMapTemplateStr = eMapTemplateStr.Replace("[ReferenceDevice_ReferenceDevice]", GetFiducialContent());
            eMapTemplateStr = eMapTemplateStr.Replace("[MapName]", _EDocGlobVar.HeaderInfo.RW_Wafer_Id);
            eMapTemplateStr = eMapTemplateStr.Replace("[MapVersion]", dtStr);
            eMapTemplateStr = eMapTemplateStr.Replace("[CONTENT]", content);

            WriteAllTextToTemp(fileName, eMapTemplateStr);
            _OutputFileDic.Add("EMAP", fileName);
            //File.WriteAllText(newEMapPath, eMapTemplateStr, Encoding.ASCII);
        }
        #endregion

        #region TMAP Methods
        private string GetTMapRowString(eDocTMapMergeClass item, List<string> tMapColList, List<eDocSpecParameterClass> tMapParaList)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                var rwPis = typeof(eDocRWMapClass).GetProperties();
                var attributeName = string.Empty;
                var testValue = string.Empty;
                var mask = item.RWItem.Wafer_Id.Substring(0, 5);
                foreach (var colName in tMapColList.Select(r=> r.ToUpper()))
                {
                    if (colName == "CHIP_ID")
                        testValue = string.Format("{0}_{1}", item.RWItem.IGx, item.RWItem.IGy);
                    else if (colName == "FAB_LOT_ID")
                        testValue = item.RWItem.Wafer_Id.Substring(0, 9);
                    else if (colName == "RW_LOT_ID")
                        testValue = item.RWItem.RW_Wafer_Id.Substring(0, 10);
                    else if (colName == "OT_TESTED")
                    {
                        testValue = CheckOTTested(item);
                    }
                    else if (colName == "EPI_REACTOR")
                        testValue = _EDocGlobVar.EPIReactor;
                    else
                    {
                        //Check from grading
                        testValue = item.GradingItem.ContainsKey(colName) ? item.GradingItem[colName] : null;
                        if (testValue == null) //Check from RW Info
                        {
                            var pi = rwPis.Where(p => GetPropertyDisplayName(p) == colName).FirstOrDefault();
                            if (pi == null)
                                pi = rwPis.Where(p => GetPropertyDescription(p) == colName).FirstOrDefault();

                            if (pi != null)
                                testValue = item.RWItem.GetType().GetProperty(pi.Name).GetValue(item.RWItem, null).ToString();
                        }
                    }
                    if (string.IsNullOrEmpty(testValue))
                    {
                        var specItem = tMapParaList.Where(r=> r.Test_Parameter_Name.ToUpper() == colName);
                        if (specItem.Any())
                        {
                            testValue = specItem.FirstOrDefault().Default_Value;
                        }                            
                    }
                    sb.AppendFormat("{0},", testValue == null ? string.Empty: testValue.Trim());
                }
            }
            catch (Exception)
            {
                throw;
            }
            if (sb.Length > 0)
                sb.Length--; //Remove last comma
            return sb.ToString();
        }
        public static string GetPropertyDisplayName(PropertyInfo pi)
        {
            var dp = pi.GetCustomAttributes(typeof(DisplayNameAttribute), true).Cast<DisplayNameAttribute>().SingleOrDefault();
            return dp != null ? dp.DisplayName : pi.Name;
        }
        public static string GetPropertyDescription(PropertyInfo pi)
        {
            var dp = pi.GetCustomAttributes(typeof(DescriptionAttribute), true).Cast<DescriptionAttribute>().SingleOrDefault();
            return dp != null ? dp.Description : pi.Name;
        }
        private string CheckOTTested(eDocTMapMergeClass item)
        {
            var otTested = "0";
            try
            {
                var mask = item.RWItem.Wafer_Id.Substring(0, 5);
                var specialGroup = new string[] { "WD001", "WD013", "WD021", "WD051" };
                if (specialGroup.Contains(mask))
                {
                    if (item.RWItem.Bin_AOI1 == "16")
                        otTested = "1";
                }
                else
                {
                    if (item.GradingItem["DEVICE"].Trim().ToUpper() == "SAMPLE" || item.GradingItem["DEVICE"].Trim().ToUpper().Contains("SAMPLE"))
                        otTested = "1";
                }
            }
            catch (Exception)
            {
                throw;
            }
            return otTested;
        }
        #endregion

        #region COC Methods
        /// <summary>
        /// Within MCO function
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, string> GetSpecCOCParaDics(bool isSpecialCoCFormat)
        {
            Dictionary<string, string> cocDics = new Dictionary<string, string>();
            var testValues = new List<string>();
            decimal testAvg = 0;
            double testStd = 0;
            string cocParaName = string.Empty;
            List<eDocBincodeMapClass> goodDieBins = GetGoodDieBinCode(_EDocGlobVar.HeaderInfo.Wafer_Id);
            string tempWaferId = string.Empty;
            try
            {
                var cocParaSpecList = SpecUtil._eDocSpecList.Where(r => r.Parameter_name.Contains("CoC Parameter")).ToList();
                int goodBinQty = 0;
                foreach (var goodDieItem in goodDieBins.Select(r=> r.EMap_BinCode).Distinct())
                {
                    foreach (var dt in _EDocGlobVar.GradingResultList)
                    {
                        goodBinQty = 0;
                        tempWaferId = dt.FirstOrDefault()["WAFER_ID"].ToString();
                        //var mergeTMapList = (from rw in _EDocGlobVar.RWMapList
                        //                     join grr in dt on
                        //                  new { WaferID = rw.Wafer_Id, Gx = rw.IGx, Gy = rw.IGy }
                        //                  equals new { WaferID = grr["WAFER_ID"].ToString(), Gx = grr["GX"].ToString(), Gy = grr["GY"].ToString() } into subGrp
                        //                     from grr in subGrp.DefaultIfEmpty()
                        //                     where rw.No > 0 && new string[] { "X", "x" }.Contains(rw.IGx) == false
                        //                     && grr["PRE_AOI_PF"] == "P" //&& rw.EMap_BinCode == goodDieItem
                        //                     select new eDocTMapMergeClass { GradingItem = grr, RWItem = rw }).ToList();
                        var gradingList = dt.Where(r => r["PRE_AOI_PF"] == "P").ToList();
                        cocDics.Add(string.Format("RW_BIN{0}_FAB_WF_ID", goodDieItem), tempWaferId);  
                        //2022/10/20 Add special CoC Format
                        if (isSpecialCoCFormat)
                            cocDics.Add(string.Format("RW_BIN{0}_FAB_WF_MRB", goodDieItem), "0");
                        cocDics.Add(string.Format("RW_BIN{0}_FAB_WF_TOTAL_DIE_QTY", goodDieItem), GetTotalDieQty().ToString());
                        goodBinQty = _EDocGlobVar.RWMapList.Where(r => r.EMap_BinCode == goodDieItem).Count();
                        cocDics.Add(string.Format("RW_BIN{0}_FAB_WF_GOOD_DIE_QTY", goodDieItem), goodBinQty.ToString());

                        foreach (var cocItem in cocParaSpecList)
                        {
                            //testValues = mergeTMapList.Select(r => r.GradingItem[cocItem.Test_Parameter_Name.ToUpper()].ToString()).ToList();
                            testValues = gradingList.Select(r => r[cocItem.Test_Parameter_Name.ToUpper()].ToString()).ToList();
                            if (testValues.Any())
                            {
                                cocParaName = string.Format("RW_BIN{0}_FAB_WF_{1}", goodDieItem, cocItem.Display_Paramenter_Name);
                                testAvg = Math.Round(testValues.Average(r => r.TryGetDecimal().Value), 6);
                                testStd = Math.Round(testValues.StdDev(r => r.TryGetDouble().Value), 6);
                                if (cocDics.ContainsKey(cocItem.Display_Paramenter_Name) == false)
                                {
                                    cocDics.Add(cocParaName + "_AVG", testAvg.ToString());
                                    cocDics.Add(cocParaName + "_STDEV", testStd.ToString());
                                }
                                else
                                {
                                    cocDics[cocParaName + "_AVG"] = cocDics[cocItem.Display_Paramenter_Name + "_AVG"] + "," + testAvg.ToString();
                                    cocDics[cocParaName + "_STDEV"] = cocDics[cocItem.Display_Paramenter_Name + "_STDEV"] + "," + testStd.ToString();
                                }
                            }
                        }
                        if (_EDocGlobVar.MCOAthenaList != null && _EDocGlobVar.MCOAthenaList.Any()) //20221013 Fixed MCO null issue
                            cocDics.AddRange(GetMCOInfo(goodDieItem));
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
            return cocDics;
        }
        private Dictionary<string, string> GetMCOInfo(string binCode)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string mcoValue = string.Empty;
            try
            {
                foreach (var item in _EDocGlobVar.MCOAthenaList)
                {
                    var dicItem = item as IDictionary<string, object>;
                    if (dicItem.ContainsKey("sno") && dicItem["sno"].ToString() == "Average")
                            continue;
                    foreach (var mcoItem in SpecUtil._eDocMCOSpecList)
                    {
                        //Compare if using TorB(top>bottom) or LorR(left and right)
                        mcoValue = MCODataCompareFunc(item, mcoItem.Athena_MCO_Column);
                        //if (dicItem.ContainsKey(mcoItem.Athena_MCO_Column.ToLower()))
                        //    result.Add(string.Format(mcoItem.Display_MCO_Column, binCode, dicItem["sno"].ToString()),
                        //        dicItem[mcoItem.Athena_MCO_Column.ToLower()].ToString());
                        result.Add(string.Format(mcoItem.Display_MCO_Column, binCode, dicItem["sno"].ToString()), mcoValue);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            return result;
        }
        private string MCODataCompareFunc(IDictionary<string, object> mcoDicItem, string athenaMCOCol)
        {
            string mcoVal = string.Empty;
            double lValue = 0.0, rValue = 0.0;
            try
            {
                if (athenaMCOCol.Contains(">"))
                {
                    var mcoCols = athenaMCOCol.Split(">");
                    if (mcoCols.Length > 1)
                    {
                        if (mcoDicItem.ContainsKey(mcoCols[0].ToLower()))
                            double.TryParse(mcoDicItem[mcoCols[0].ToLower()].ToString(), out lValue); //Get Left Value
                        if (mcoDicItem.ContainsKey(mcoCols[1].ToLower()))
                            double.TryParse(mcoDicItem[mcoCols[1].ToLower()].ToString(), out rValue); //Get Right Value
                        if (lValue > rValue)
                            mcoVal = mcoDicItem[mcoCols[1].ToLower()].ToString();
                        else mcoVal = mcoDicItem[mcoCols[0].ToLower()].ToString();
                    }
                }
                else
                {
                    if (mcoDicItem.ContainsKey(athenaMCOCol.ToLower()))
                    {
                        mcoVal = mcoDicItem[athenaMCOCol.ToLower()].ToString();
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            //var res = decimal.Round(decimal.Parse(mcoVal), 2, MidpointRounding.AwayFromZero);
            return mcoVal;
        }
        private int GetTotalDieQty()
        {
            var totalDieQty = 0;
            try
            {
                totalDieQty = _EDocGlobVar.RWMapList.Where(r => r.Bin_AOI2 != null && r.Bin_AOI2 != "Z" && r.Wafer_Id != null).Count();
            }
            catch (Exception)
            {
                throw;
            }
            return totalDieQty;
        }
        private int GetGoodDieQtyByWafer(string wafer_Id)
        {
            var goodDieQty = 0;
            var goodDieBins = new List<string>() { "1", "16" }; //Default
            try
            {
                var goodDieBinCfg = SpecUtil._eDocInfoList.Where(r => r.Type == "Config" && r.Key == "Good Die Bin code");
                if (goodDieBinCfg?.Any() ?? false)
                    goodDieBins = goodDieBinCfg.FirstOrDefault().Value1.Split(",").ToList();

                goodDieQty = _EDocGlobVar.RWMapList.Where(r => r.Bin_AOI1 != null && goodDieBins.Contains(r.Bin_AOI2) && r.Wafer_Id == wafer_Id).Count();
            }
            catch (Exception)
            {
                throw;
            }
            return goodDieQty;
        }
        private List<string> GetExtraCoCParameters(string waferId)
        {
            List<string> extraCocPara = new List<string>();
            try
            {
                var eDocConfig = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigKey == "Extra_COC_Parameter" && r.ConfigType == waferId.Substring(0, 5) + "_Config");
                if (eDocConfig != null && eDocConfig.Any())
                {
                    extraCocPara = eDocConfig.FirstOrDefault().ConfigValue.Split(',').ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }

            return extraCocPara;
        }
        private bool IsSpecialCoCFormat()
        {
            bool isSpecialCoCFormat = false;
            try
            {
                var config = this.GetEDocInfoConfig("CoC", "NEWATHENSCOCFORMAT(Y/N)");
                if (string.IsNullOrEmpty(config) == false && config == "Y")
                    isSpecialCoCFormat = true;
            }
            catch (Exception)
            {
                throw;
            }

            return isSpecialCoCFormat;
        }
        #endregion

        #region SendAlert
        public static void SendAlertMail(Traceability_InfoClass obj = null)
        {
            try
            {
                MailHelper mailHelper = new MailHelper(_config);
                if (string.IsNullOrEmpty(eDocGenUtil._EDocGlobVar.MailInfo.Subject))
                    eDocGenUtil._EDocGlobVar.MailInfo.Subject = _config["MailSettings:mailTitle"].ToString();
                List<string> receivers = new List<string>();
                string mainReceiver = string.Empty;
                if (Program._isDebug)
                {
                    receivers.AddRange(_config["MailSettings:receiveMails"].ToString().Split(",").ToList());
                }
                else
                {
                    if (_EDocGlobVar.MailInfo.Level == 1)
                        mainReceiver = SpecUtil._eDocInfoList.Where(r => r.Type == "EMAIL" && r.Key == "Lite IT email").FirstOrDefault()?.Value1;
                    else if (_EDocGlobVar.MailInfo.Level == 2)
                        mainReceiver = SpecUtil._eDocInfoList.Where(r => r.Type == "EMAIL" && r.Key == "To").FirstOrDefault()?.Value1;
                    else if (_EDocGlobVar.MailInfo.Level == 3)
                        mainReceiver = SpecUtil._eDocInfoList.Where(r => r.Type == "EMAIL" && r.Key == "Win Engineer email").FirstOrDefault()?.Value1;
                    else
                        mainReceiver = SpecUtil._eDocInfoList.Where(r => r.Type == "EMAIL" && r.Key == "cc").FirstOrDefault()?.Value1;

                    receivers.Add(mainReceiver);
                }
                
                _logger.Info(_EDocGlobVar.MailInfo.Subject);
                mailHelper.SendMail(string.Empty, receivers, string.Format("[eDoc Generator Alert] - {0}", _EDocGlobVar.MailInfo.Subject)
                    , _EDocGlobVar.MailInfo.Content, false,
                    (_EDocGlobVar.MailInfo.Attachments != null && _EDocGlobVar.MailInfo.Attachments.Any()) 
                    ? _EDocGlobVar.MailInfo.Attachments.ToArray() : null, false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex.StackTrace);
                _logger.Error(ex.Message);
            }

        }
        #endregion

        #region Create Completed Info
        private void CopyToS3Folder(string filePath)
        {
            try
            {
                var s3UploadConfig = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Output" && r.ConfigKey == "S3Result");
                if (s3UploadConfig.Any())
                {
                    var s3UploadPath = s3UploadConfig.FirstOrDefault().ConfigValue;
                    File.Copy(filePath, Path.Combine(s3UploadPath, new FileInfo(filePath).Name), true);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public void CreateEMapLog(string eMapStatus, string errorMessage)
        {
            try
            {
                DateTime now = DateTime.Now;
                var rwMapFileDT = new FileInfo(_EDocGlobVar.AVI2FilePath).CreationTime.ToString("yyyy-MM-dd HH:mm:ss");
                StringBuilder sb = new StringBuilder("rw_wafer_id,rw_map_filename,rw_map_file_datetime, wafer_id1,wafer_id2,wafer_id3,wafer_id4,wafer_id5,grading_file1,grading_file2,grading_file3,grading_file4,grading_file5,software_version,creationdate_start,creationdate_end,good_die_qty,status,error_message");
                sb.AppendLine();
                sb.Append(_EDocGlobVar.HeaderInfo.RW_Wafer_Id).Append(",");
                sb.Append(new FileInfo(_EDocGlobVar.AVI2FilePath).Name).Append(",");
                sb.Append(rwMapFileDT).Append(",");
                sb.Append(GetWaferIdsStr());
                sb.Append(GetGradingPaths());
                sb.Append(Assembly.GetExecutingAssembly().GetName().Name + " V" + Assembly.GetExecutingAssembly().GetName().Version).Append(";");
                sb.Append(_EDocGlobVar.EMapVersion).Append(",");
                sb.Append(_EDocGlobVar.CreationStartTime.ToString("yyyy-MM-dd HH:mm:ss")).Append(",");
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(",");
                sb.Append(_EDocGlobVar.GoodDieQty).Append(",");
                sb.Append(eMapStatus).Append(",");
                sb.Append(errorMessage).Append(",");

                //header_filename
                if (Directory.Exists(_EDocGlobVar.EDocResultPath) == false)
                    Directory.CreateDirectory(_EDocGlobVar.EDocResultPath);
                string headerFN = Path.Combine(_EDocGlobVar.EDocResultPath,
                    string.Format("{0}_EMAP_HEADER_{1}.csv", _EDocGlobVar.HeaderInfo.RW_Wafer_Id, now.ToString("yyyyMMdd_HHmmss")));
                File.WriteAllText(headerFN, sb.ToString(), Encoding.ASCII);
                if (eMapStatus == "Success")
                {
                    CopyToS3Folder(headerFN);
                    //Check if exist tMap
                    if (_OutputFileDic.ContainsKey("TMAP"))
                        CopyToS3Folder(Path.Combine(_EDocGlobVar.EDocResultPath, _OutputFileDic["TMAP"]));
                }
                    
            }
            catch (Exception ex)
            {
                _logger.Error(ex.StackTrace);
            }
        }
        public void SendEDocAPI(string eMapStatus, string errorMessage)
        {
            try
            {
                string apiUrl = this.GetAPIConfig();
                TBL_WAFER_RESUME wafer_header = new TBL_WAFER_RESUME()
                {
                    Level = "RW", Type = "eDoc", RW_Wafer_Id = _EDocGlobVar.HeaderInfo.RW_Wafer_Id,
                    Wafer_Id = _EDocGlobVar.HeaderInfo.Wafer_Id.Substring(0, 12), Status = 1,
                    Created_By = Program._MachineName
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
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "spec_file_path", Value = File.Exists(_EDocGlobVar.GradingSpecFilePath) ? new FileInfo(_EDocGlobVar.GradingSpecFilePath).Name: _EDocGlobVar.GradingSpecFilePath });
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "cbp_version", Value = _EDocGlobVar.WaferTestHeader.cbp_version });
                wafer_item_list.Add(new TBL_WAFER_RESUME_ITEM() { Type = "eDoc", Key = "grade_spec_version", Value = _EDocGlobVar.WaferTestHeader.spec_version });

                var obj = new { wafer_header, wafer_item_list };
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
                string apiConfigKey = "APIURL_Prod";
                if (Program._isDebug)
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
        public int UpdateTraceabilityInfo(string eMapStatus, Traceability_InfoClass item, string retryLimitCount)
        {
            try
            {
                bool isCopyFile = true;
                _logger.Info("eMap Status: " + eMapStatus);
                var completePath = _EDocGlobVar.EDocConfigList.Where(r => r.ConfigType == "Output" 
                && r.ConfigKey == (eMapStatus == "Success" ? "VI2" : "Error")).FirstOrDefault();
                if (Directory.Exists(completePath.ConfigValue) == false)
                    Directory.CreateDirectory(completePath.ConfigValue);

                var copyFilePath = Path.Combine(completePath.ConfigValue, new FileInfo(item.FilePath).Name);
                //Success SQL
                var sql = string.Format(@"Update [TBL_Traceability_Info] set status = 2, FilePath = '{0}',
                                        LastUpdatedBy = '{1}', LastUpdatedDate = GETDATE() where Id = '{2}'",
                                        copyFilePath, Program._MachineName, item.Id);
                if (eMapStatus == "Fail")
                {
                    if (item.RetryCount == int.Parse(retryLimitCount) - 1)
                    {
                        //Success SQL Failed Over Limit
                        sql = string.Format(@"Update [TBL_Traceability_Info] set RetryCount = {0} + 1, status = 9, FilePath = '{1}',
                                            LastUpdatedBy = '{2}', LastUpdatedDate = GETDATE() where Id = '{3}'",
                                            item.RetryCount, copyFilePath, Program._MachineName, item.Id);
                    }
                    else
                    {
                        //Success SQL Failed
                        sql = string.Format(@"Update [TBL_Traceability_Info] set RetryCount = {0} + 1, LastUpdatedBy = '{1}',
                                            LastUpdatedDate = GETDATE() where Id = '{2}'", item.RetryCount, Program._MachineName, item.Id);
                        isCopyFile = false;
                    }
                }

                using (var sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]))
                {
                    var res = sqlConn.Execute(sql);
                    if (res > 0 && isCopyFile)
                    {
                        File.Copy(item.FilePath, copyFilePath, true);
                        File.Delete(item.FilePath);
                    }    
                    return res;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.StackTrace);
                return 0;
            }            
        }

        public int DeleteTBL_AVI2_RAW(Traceability_InfoClass item)
        {
            try
            {
                _logger.Info("DeleteTBL_AVI2_RAW - RW_Wafer_Id: " + item.RW_Wafer_Id);

                //Delete SQL
                string sql = string.Format("Delete From Tbl_AVI2_RawData WHERE RW_Wafer_Id = '{0}' ", item.RW_Wafer_Id);

                using (var sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]))
                {
                    var res = sqlConn.Execute(sql);
                    if (res > 0) _logger.Info("Success!");
                    else _logger.Info("Failed");
                    return res;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.StackTrace);
                return 0;
            }
        }
        #endregion
    }
}
