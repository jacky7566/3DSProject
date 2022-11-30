using Dapper;
using Dapper.Contrib;
using eDocGenLib.Classes;
using eDocGenLib.Classes.eDocGenEngine;
using eDocGenLib.Utils;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace DataIngestionEngine.Utils
{
    public class AVI2IngestionUtil
    {
        private static eDocGenParaClass _eDocGenParaClass;
        //public static string RW_Wafer_Id;
        //public static string _Wafer_Id;
        private static Guid _headerId;
        private static IConfiguration _config;
        private static ILogger _logger;
        private static string _inputFilePath;
        private static IDbConnection _sqlConn;
        public static string _FailedOutputPath;
        private static List<eDocConfigClass> _eDocConfigList;
        public AVI2IngestionUtil(IConfiguration config, ILogger logger)
        {
            _config = config;
            _logger = logger;
            _eDocGenParaClass = new eDocGenParaClass();
            _eDocGenParaClass.HeaderInfo = new Traceability_InfoClass();
        }
        public static bool ProcessStartAVI2(FileInfo file)
        {
            _sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]);
            _inputFilePath = file.FullName;
            _logger.Info(string.Format("Import file to Raw Data Table: {0}", file.Name));
            return ExtractTextFileAsync().Result;
        }

        public static bool GetEDocConfigList()
        {
            _eDocConfigList = new List<eDocConfigClass>();
            try
            {
                _eDocConfigList = ConnectionHelper.GetEDocConfigList(_config, Program._MachineName);

                if (_eDocConfigList.Count == 0)
                {
                    _logger.Warn(string.Format("GetEDocConfigList - No data found! Server Name: {0}", Program._MachineName));
                }
                else return true;
            }
            catch (Exception)
            {
                throw;
            }
            return false;
        }

        private static string GetEDocConfigValue(string configType, string configKey)
        {
            string res = string.Empty;
            try
            {
                var config = _eDocConfigList.Where(r=> r.ConfigType == configType && r.ConfigKey == configKey).ToList();
                if (config != null && config.Any())
                {
                    res = config.FirstOrDefault().ConfigValue;
                }
            }
            catch (Exception)
            {

                throw;
            }
            return res;
        }

        private static async Task<bool> ExtractTextFileAsync()
        {
            List<Traceability_InfoClass> headerList = new List<Traceability_InfoClass>();
            List<AVI2_RawDataClass> bodyList = new List<AVI2_RawDataClass>();
            StringBuilder errorSB = new StringBuilder();

            try
            {
                using (FileStream fs = new FileStream(_inputFilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite))
                {
                    StreamReader sr = new System.IO.StreamReader(fs);
                    List<string> lines = new List<string>();
                    while (!sr.EndOfStream)
                        lines.Add(sr.ReadLine());
                    //var lines = System.IO.File.ReadAllLines(_filePath);
                    string[] values;

                    for (int i = 0; i < lines.Count(); i++)
                    {
                        values = lines[i].Replace(";", ",").Split(",");
                        if (values.Length > 0)
                        {
                            if (i == 0 && values[0] == "Wafer ID")
                            {
                                //Create First HeaderInfo                                
                                BuildAndAddNewHeaderInfo(ref headerList, string.Empty);
                                headerList.LastOrDefault().RW_Wafer_Id = values[1].ToString();
                                _eDocGenParaClass.HeaderInfo.RW_Wafer_Id = values[1].ToString(); //20220913 Jacky added For API usage
                            }
                            else if (i == 2 && values[0] == "End time")
                            {
                                headerList.LastOrDefault().Sort_Date = DateTime.Parse(values[1].ToString());
                            }
                            else if (i == 4 && values[0] == "Recipe")
                            {
                                headerList.LastOrDefault().Recipe = values[1].ToString();
                            }
                            else if (i >= 7)
                            {
                                //Add WaferId to first Header Info
                                if (string.IsNullOrEmpty(headerList.LastOrDefault().Wafer_Id) == true)
                                {
                                    headerList.LastOrDefault().Wafer_Id = values[1].ToString(); //InputWafer
                                    _eDocGenParaClass.HeaderInfo.Wafer_Id = values[1].ToString(); //20220913 Jacky added For API usage
                                }
                                else //Has value but different with previous row
                                {
                                    if (values[1].ToString() != "Fiducial") //Ignore Fiducial
                                    {
                                        if (headerList.LastOrDefault().Wafer_Id.Equals(values[1].ToString()) == false)
                                        {
                                            BuildAndAddNewHeaderInfo(ref headerList, values[1].ToString());
                                            headerList.LastOrDefault().RW_Wafer_Id = headerList.FirstOrDefault().RW_Wafer_Id;
                                        }
                                    }
                                }

                                BuildAndAddNewBody(ref bodyList, headerList.LastOrDefault(), values);
                                //Console.WriteLine(String.Format("Count: {0}/{1}", i, lines.Length));
                            }
                        }
                    }

                    bool isCleaned = await CleanUpOldData(headerList);
                    if (isCleaned && headerList != null && headerList.Count() > 0)
                    {
                        //Check Coordinate duplication
                        errorSB.Append(CheckiGxiGyIsDuplicate(bodyList));
                        if (errorSB.Length > 0) errorSB.Append("<BR>");
                        errorSB.Append(CheckoGxoGyIsDuplicate(bodyList));
                        //Check 2DBC Control
                        if (errorSB.Length > 0) errorSB.Append("<BR>");
                        errorSB.Append(Check2DBCControl(bodyList, new FileInfo(_inputFilePath).Name));
                        //20221107 Detections of horizontal and vertical lines on AOI2 bin maps
                        if (errorSB.Length > 0) errorSB.Append("<BR>");
                        errorSB.Append(CheckAOI2ContBincode(bodyList));

                        if (errorSB.Length > 0)
                        {
                            MailHelper mail = new MailHelper(_config);
                            var rwId = headerList.FirstOrDefault().RW_Wafer_Id;
                            var subject = string.Format("{0} - RW Wafer Id: {1}", _config["MailSettings:mailTitle"].ToString(), rwId);
                            var content = string.Format("RW_Wafer_Id: {0} <BR> Error Message:<BR><BR>{1}", rwId, errorSB.ToString()).Replace("\n\r", "<BR>");
                            var receivers = _config["MailSettings:receiveMails"].ToString().Split(",").ToList();
                            mail.SendMail(string.Empty, receivers, subject, content, true);
                            _eDocGenParaClass.MailInfo = new eDocAlertClass();
                            _eDocGenParaClass.MailInfo.Subject = subject; //20220913 Jacky added For API usage
                            _eDocGenParaClass.MailInfo.Content = content; //20220913 Jacky added For API usage
                            CallAPI(errorSB.ToString());
                            _FailedOutputPath = GetEDocConfigValue("Output", "Error"); //2DBC Failed Path
                            return false;
                        }
                        if (await ImportToHeaderInfoAsync(headerList))
                        {
                            //return await ImportToBodyAsync(bodyList);                            
                            var ingested = BatchInsert(bodyList, "Tbl_AVI2_RawData");
                            if (ingested)
                                _FailedOutputPath = "";
                            return true;
                        };
                    }
                }
                //FileStream fs = new System.IO.FileStream(_filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);                
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
                return false;
            }
            return false;
        }


        //2022/10/28 Added function to update failed copy file
        public static void UpdateFailedHeader(string filePath)
        {
            try
            {
                string sql = string.Format(@"Update TBL_Traceability_Info Set Status = 0, LastUpdatedDate = getdate(), FilePath = '{0}'
                                        WHERE Id = '{1}' ", filePath, _headerId.ToString());

                _logger.Error("UpdateFailedHeader - SQL: " + sql);
                _sqlConn.Execute(sql);
            }
            catch (Exception)
            {
                throw;
            }
        }
        private static void BuildAndAddNewHeaderInfo(ref List<Traceability_InfoClass> headerList, string waferId)
        {
            var filePath = FileDetectUtil._IngestedPath + Path.DirectorySeparatorChar + new FileInfo(_inputFilePath).Name;
            var isMove = _config["Configurations:IsMoveToIngestedFolder"];
            if (isMove == "N")
                filePath = _inputFilePath;

            _headerId = Guid.NewGuid();
            headerList.Add(new Traceability_InfoClass()
            {
                Wafer_Id = waferId,
                FilePath = filePath,
                FileType = "AVI2",
                Status = 1,
                Id = _headerId,
                CreatedBy = Program._MachineName,
                LastUpdatedBy = Program._MachineName,
                CreatedDate = DateTime.Now,
                LastUpdatedDate = DateTime.Now,
                RetryCount = 0
            });
        }
        private static void BuildAndAddNewBody(ref List<AVI2_RawDataClass> bodyList, Traceability_InfoClass header, string[] values)
        {
            try
            {
                bodyList.Add(new AVI2_RawDataClass()
                {
                    Id = Guid.NewGuid(),
                    No = int.Parse(values[0]),
                    Wafer_Id = values[1].ToString(),
                    RW_Wafer_Id = header.RW_Wafer_Id,
                    IGx = values[2].ToString(),
                    IGy = values[3].ToString(),
                    Bin_AOI1 = values[5].ToString(),
                    OGx = values[6].ToString(),
                    OGy = values[7].ToString(),
                    Bin_AOI2 = values[8].ToString(),
                    CreatedBy = Program._MachineName,
                    LastUpdatedBy = Program._MachineName,
                    CreatedDate = DateTime.Now,
                    LastUpdatedDate = DateTime.Now,
                    Traceability_Id = header.Id,
                    Line_Data = values.Aggregate((cur, next) => cur + "," + next)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }

        }
        
        private static async Task<bool> CleanUpOldData(List<Traceability_InfoClass> headerList)
        {
            bool isCleaned = false;
            try
            {
                var commandTimeout = int.Parse(_config["Configurations:SQL_CommandTimeout"].ToString());
                //Remove exists Header data
                string sql = string.Format("Update TBL_Traceability_Info Set Status = 0, LastUpdatedDate = getdate() WHERE RW_Wafer_Id in ('{0}')",
                    string.Join("','", headerList.Select(r => r.RW_Wafer_Id)));

                _sqlConn.Execute(sql, null, null, commandTimeout);

                //Remove old avi2 data
                sql = string.Format("Delete From Tbl_AVI2_RawData WHERE RW_Wafer_Id = '{0}' ",
                    headerList.FirstOrDefault().RW_Wafer_Id);

                _sqlConn.Execute(sql, null, null, commandTimeout);

                isCleaned = true;
            }
            catch (Exception)
            {

                throw;
            }
            return isCleaned;
        }

        private static async Task<bool> ImportToHeaderInfoAsync(List<Traceability_InfoClass> headerList)
        {
            //Insert Header Info by List
            try
            {
                var commandTimeout = int.Parse(_config["Configurations:SQL_CommandTimeout"].ToString());
                //Remove exists Header data
                //string sql = string.Format("Update TBL_Traceability_Info Set Status = 0, LastUpdatedDate = getdate() WHERE RW_Wafer_Id in ('{0}')",
                //    String.Join("','", headerList.Select(r => r.RW_Wafer_Id)));

                //_sqlConn.Execute(sql, null, null, commandTimeout);

                _logger.Info("Start BulkInsert Traceability Info, total count: " + headerList.Count);
                var res = await _sqlConn.BulkInsert(
                    "TBL_Traceability_Info", headerList,
                    new Dictionary<string, Func<Traceability_InfoClass, object>>
                        {
                            { "Id", u => u.Id },
                            { "Wafer_Id", u => u.Wafer_Id },
                            { "RW_Wafer_Id", u => u.RW_Wafer_Id },
                            { "FileType", u => u.FileType },
                            { "FilePath", u => u.FilePath },
                            { "Status", u => u.Status },
                            { "CreatedDate", u => u.CreatedDate },
                            { "CreatedBy", u => u.CreatedBy },
                            { "LastUpdatedDate", u => u.LastUpdatedDate },
                            { "LastUpdatedBy", u => u.LastUpdatedBy },
                            { "Recipe", u => u.Recipe },
                            { "Sort_Date", u => u.Sort_Date },
                            { "RetryCount", u => u.RetryCount }
                        });
                _logger.Info("End BulkInsert Traceability Info, total count: " + headerList.Count);
                if (res > 0) return true;
            }
            catch (Exception)
            {
                throw;
            }

            return false;

        }

        private static async Task<bool> ImportToBodyAsync(List<AVI2_RawDataClass> bodyList)
        {
            //Insert Header Info by List
            try
            {
                var commandTimeout = int.Parse(_config["Configurations:SQL_CommandTimeout"].ToString());
                //Remove exists UMC data
                //string sql = string.Format("Delete From Tbl_AVI2_RawData WHERE RW_Wafer_Id = '{0}' ",
                //    bodyList.FirstOrDefault().RW_Wafer_Id);

                //_sqlConn.Execute(sql, null, null, commandTimeout);

                _logger.Info("Start BulkInsert Tbl_AVI2_RawData, total count: " + bodyList.Count);
                var res = await _sqlConn.BulkInsert(
                    "Tbl_AVI2_RawData", bodyList,
                    new Dictionary<string, Func<AVI2_RawDataClass, object>>
                        {
                            { "Id", u => u.Id },
                            { "Traceability_Id", u => u.Traceability_Id },
                            { "Wafer_Id", u => u.Wafer_Id },
                            { "RW_Wafer_Id", u => u.RW_Wafer_Id },
                            { "No", u => u.No },
                            { "IGx", u => u.IGx },
                            { "IGy", u => u.IGy },
                            { "OGx", u => u.OGx },
                            { "OGy", u => u.OGy },
                            { "Bin_AOI1", u => u.Bin_AOI1 },
                            { "Bin_AOI2", u => u.Bin_AOI2 },
                            { "Line_Data", u => u.Line_Data },
                            { "CreatedDate", u => u.CreatedDate },
                            { "CreatedBy", u => u.CreatedBy },
                            { "LastUpdatedDate", u => u.LastUpdatedDate },
                            { "LastUpdatedBy", u => u.LastUpdatedBy }
                        });
                _logger.Info("End BulkInsert Tbl_AVI2_RawDataClass, total count: " + bodyList.Count);
                if (res > 0) return true;
            }
            catch (Exception)
            {
                throw;
            }

            return false;

        }

        private static async Task<bool> ImportToBodyAsync2(List<AVI2_RawDataClass> bodyList)
        {
            //Insert Header Info by List
            try
            {
                //Remove exists UMC data
                string sql = string.Format("Delete From Tbl_AVI2_RawData WHERE RW_Wafer_Id = '{0}' ",
                    bodyList.FirstOrDefault().RW_Wafer_Id);

                _sqlConn.Execute(sql);

                //using (var scope = new TransactionScope())
                //{
                //    sql = AVI2RWData_Insert();
                //    var exeuteResult = _sqlConn.Execute(sql, bodyList);
                //    _logger.Info("Start BulkInsert Tbl_AVI2_RawData, total count: " + exeuteResult);
                //    scope.Complete();
                //}
                _sqlConn.Open();
                using (var trans = _sqlConn.BeginTransaction())
                {
                    sql = AVI2RWData_Insert();
                    var exeuteResult = _sqlConn.Execute(sql, bodyList, trans);
                    trans.Commit();
                    _logger.Info("Start BulkInsert Tbl_AVI2_RawData, total count: " + exeuteResult);
                    if (exeuteResult > 0) return true;
                }

            }
            catch (Exception)
            {
                throw;
            }

            return false;

        }

        public static bool BatchInsert(List<AVI2_RawDataClass> bodyList, string table_name)
        {
            try
            {
                var commandTimeout = int.Parse(_config["Configurations:SQL_CommandTimeout"].ToString());

                string sql = string.Format("Delete From Tbl_AVI2_RawData WHERE RW_Wafer_Id = '{0}' ",
                    bodyList.FirstOrDefault().RW_Wafer_Id);

                _sqlConn.Execute(sql, null, null, commandTimeout);

                var dt = IOHelper.ConvertToDataTableWithType(bodyList);

                _logger.Info(string.Format("Start BatchInsert to {0}, total count: {1}", table_name, dt.Rows.Count));
                using (var bulkCopy = new SqlBulkCopy(_sqlConn.ConnectionString, SqlBulkCopyOptions.KeepIdentity))
                {
                    //the column name is same between dt and table_name
                    foreach (DataColumn col in dt.Columns)
                    {
                        bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);
                    }
                    bulkCopy.BulkCopyTimeout = 6000;
                    bulkCopy.DestinationTableName = table_name;
                    bulkCopy.WriteToServer(dt);
                }
                _logger.Info(string.Format("End BatchInsert to {0}, total count: {1}", table_name, dt.Rows.Count));
                return true;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex.Message);
                return false;
            }

        }

        private static string AVI2RWData_Insert()
        {
            string sql = @"INSERT INTO [dbo].[TBL_AVI2_RawData]
                           ([No]
                           ,[Wafer_Id]
                           ,[RW_Wafer_Id]
                           ,[IGx]
                           ,[IGy]
                           ,[OGx]
                           ,[OGy]
                           ,[Bin_AOI2]
                           ,[Line_Data]
                           ,[CreatedDate]
                           ,[CreatedBy]
                           ,[LastUpdatedDate]
                           ,[LastUpdatedBy]
                           ,[Traceability_Id]
                           ,[Id])
                           VALUES
                           (@No,
		                    @Wafer_Id    
                           ,@RW_Wafer_Id
                           ,@IGx  
                           ,@IGy       
                           ,@OGx      
                           ,@OGy         
                           ,@Bin_AOI2
                           ,@Line_Data 
                           ,@CreatedDate 
                           ,@CreatedBy     
                           ,@LastUpdatedDate
                           ,@LastUpdatedBy
                           ,@Traceability_Id
                           ,@Id);";

            return sql;
        }

        private static string CheckAOI2ContBincode(List<AVI2_RawDataClass> bodyList)
        {
            string result = string.Empty;
            try
            {
                string avi2FileName = new FileInfo(_inputFilePath).Name;                
                var isContMask = IsAOI2ContMask(bodyList.FirstOrDefault().Wafer_Id.Substring(0, 5));
                if (isContMask == false) //If not in control mask, then return
                    return result;
                var configList = GetEDocConfigList(bodyList.FirstOrDefault().Wafer_Id.Substring(0, 5));
                if (configList != null && configList.Count() > 0)
                {
                    var byPassConfigs = configList.Where(r => r.ConfigKey == "AOI2ContByPassKey").FirstOrDefault();
                    var controlMasks = configList.Where(r => r.ConfigType == "Spec" && r.ConfigKey == "");
                    if (byPassConfigs == null) 
                        return result;
                    if (avi2FileName.ToUpper().Contains(byPassConfigs.ConfigValue.ToUpper()) == false)
                    {
                        result = DetectHorVerContinuousAOIFails(bodyList);
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
            return result;
        }

        private static string DetectHorVerContinuousAOIFails(List<AVI2_RawDataClass> bodyList)
        {
            StringBuilder sb = new StringBuilder();
            string errorContent = string.Empty;            
            
            try
            {
                var greaterCond = _config["AOI2_Detector:HOR_VER_BIN_GREATER"].ToString();
                var excludeCond = _config["AOI2_Detector:HOR_VER_BIN_EXCLUDE"].ToString();                

                //Order By Gy
                var aoi2GyList = (from item in bodyList
                                where item.Wafer_Id != "Fiducial" 
                                && int.Parse(item.Bin_AOI2) >= int.Parse(greaterCond)
                                && excludeCond.Contains(item.Bin_AOI2) == false
                                  select item).ToList().OrderBy(r=> int.Parse(r.IGx)).OrderBy(r => int.Parse(r.IGy)).ToList();

                errorContent = CheckCordinateHasNeighbor(aoi2GyList, "IGy", "IGx");
                if (string.IsNullOrEmpty(errorContent) == false)
                    sb.AppendLine(errorContent);

                //Order By Gx
                var aoi2GxList = (from item in bodyList
                                  where item.Wafer_Id != "Fiducial"
                                && int.Parse(item.Bin_AOI2) >= int.Parse(greaterCond)
                                && excludeCond.Contains(item.Bin_AOI2) == false
                                  select item).ToList().OrderBy(r => int.Parse(r.IGy)).OrderBy(r => int.Parse(r.IGx)).ToList();

                errorContent = CheckCordinateHasNeighbor(aoi2GxList, "IGx", "IGy");
                if (string.IsNullOrEmpty(errorContent) == false)
                    sb.AppendLine(errorContent);

                if (sb.Length > 0)
                {
                    var failedReportPath = Path.Combine(GetEDocConfigValue("Output", "OOS"),
                        "ConsecutiveReport", DateTime.Now.ToString("yyyyMMdd"));
                    sb.Insert(0, "filename,inputwafer,igx,igy,rw_wafer_id,ogx,ogy,binaoi2" + Environment.NewLine);

                    if (CreateConsecutiveReport(sb.ToString(), failedReportPath))
                    {
                        return string.Format(@"<li>The given RWID:{0} consecutive report has succefully been created!<BR>
                        Please check the detail from folder: {1}. 
                        <BR><li>System have detected the bin code greater than {2} and excludes the bin codes {3}",
                            _eDocGenParaClass.HeaderInfo.RW_Wafer_Id, failedReportPath, greaterCond, excludeCond);
                    }
                    else
                        return string.Format(@"<li>The given RWID:{0} consecutive report has some error during creation!<BR>
                        Please check the detail from folder: {1}. 
                        <BR><li>System have detected the bin code greater than {2} and excludes the bin codes {3}",
                            _eDocGenParaClass.HeaderInfo.RW_Wafer_Id, failedReportPath, greaterCond, excludeCond);
                }
            }
            catch (Exception)
            {
                throw;
            }

            return string.Empty;
        }

        private static string CheckCordinateHasNeighbor(List<AVI2_RawDataClass> list, string groupCol, string sortCol)
        {
            List<List<AVI2_RawDataClass>> resultList = new List<List<AVI2_RawDataClass>>();
            List<AVI2_RawDataClass> tempRawList = new List<AVI2_RawDataClass>();
            StringBuilder sb = new StringBuilder();
            try
            {
                string avi2FileName = new FileInfo(_inputFilePath).Name;
                string fileName = new FileInfo(_inputFilePath).Name;
                var limitCnt = int.Parse(_config["AOI2_Detector:LIMIT"].ToString());

                //Check X/Y Consecutive Defect
                var contList = list.GroupBy(r => r.GetType().GetProperty(groupCol).GetValue(r))
                    .ToDictionary(o => o.Key, o => o.ToList())
                    .Where(r => r.Value.Count() > limitCnt).ToList();
                
                foreach (var kvp in contList)
                {
                    //Group by x,y to get consecutive fail bin code
                    var nums = kvp.Value.Select(r => 
                    int.Parse(r.GetType().GetProperty(sortCol).GetValue(r).ToString()))
                        .OrderBy(r => r).ToArray();
                    tempRawList = new List<AVI2_RawDataClass>();
                    for (int i = 0; i < nums.Length; i++)
                    {                        
                        if (i > 0 && nums[i] - nums[i - 1] != 1) //Not consecutive then create new list
                        {
                            if (tempRawList.Count() > limitCnt)
                                resultList.Add(tempRawList);
                            tempRawList = new List<AVI2_RawDataClass>();
                        }
                        //Collect consecutive coordinate
                        tempRawList.Add(kvp.Value[i]);
                    }
                }

                foreach (var rwList in resultList)
                {
                    foreach (var item in rwList)
                    {
                        sb.Append(string.Format("{0},{1},{2},{3},{4},{5},{6},{7}",
                            avi2FileName, item.Wafer_Id, item.IGx, item.IGy, item.RW_Wafer_Id, item.OGx, item.OGy, item.Bin_AOI2));
                        sb.AppendLine();
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
            return sb.ToString();
        }

        private static bool CreateConsecutiveReport(string content, string failedReportPath)
        {
            bool isSuccess = false;
            try
            { 
                var fileName = string.Format(@"{0}_AOI2ConsecutiveResult_{1}.csv",
                    _eDocGenParaClass.HeaderInfo.RW_Wafer_Id, DateTime.Now.ToString("yyyyMMddHHmm"));
                if (Directory.Exists(failedReportPath) == false)
                    Directory.CreateDirectory(failedReportPath);
                failedReportPath = Path.Combine(failedReportPath, fileName);
                File.WriteAllText(failedReportPath, content);
                if (File.Exists(failedReportPath))
                    isSuccess = true;
            }
            catch (Exception)
            {

                throw;
            }
            return isSuccess;
        }

        private static string CheckiGxiGyIsDuplicate(List<AVI2_RawDataClass> bodyList)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                var iXYDupRes = (from item in bodyList
                                 group item by new { item.IGx, item.IGy } into grp
                                 select new { iGx = grp.Key.IGx, iGy = grp.Key.IGy, CNT = grp.Count().ToString() }).ToList()
                                             .Where(r => int.Parse(r.CNT) > 1 & r.iGx != "X" & r.iGy != "X").ToList();

                if (iXYDupRes.Count > 0)
                {
                    sb.AppendFormat("There are {0} duplicate(s) iGX,iGy as below: \n\r", iXYDupRes.Count());
                    foreach (var item in iXYDupRes)
                    {
                        sb.Append(string.Format("iGx: {0}, iGy: {1}, Count: {2} \n\r", item.iGx, item.iGy, item.CNT));
                    }
                    sb.AppendLine();
                    _logger.Warn(sb.ToString());
                }
            }
            catch (Exception)
            {

                throw;
            }

            return sb.ToString();
        }

        private static string CheckoGxoGyIsDuplicate(List<AVI2_RawDataClass> bodyList)
        {
            StringBuilder sb = new StringBuilder();

            try
            {
                var iXYDupRes = (from item in bodyList
                                 group item by new { item.OGx, item.OGy } into grp
                                 select new { oGx = grp.Key.OGx, oGy = grp.Key.OGy, CNT = grp.Count().ToString() }).ToList()
                 .Where(r => int.Parse(r.CNT) > 1).ToList();

                if (iXYDupRes.Count > 0)
                {
                    sb.AppendFormat("There are {0} duplicate(s) oGX,oGy as below: \n\r", iXYDupRes.Count());
                    foreach (var item in iXYDupRes)
                    {
                        sb.Append(string.Format("oGx: {0}, oGy: {1}, Count: {2} \n\r", item.oGx, item.oGy, item.CNT));
                    }
                    sb.AppendLine();
                    _logger.Warn(sb.ToString());
                }
            }
            catch (Exception)
            {
                throw;
            }

            return sb.ToString();
        }

        private static string Check2DBCControl(List<AVI2_RawDataClass> bodyList, string fileName)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                var configList = GetEDocConfigList(bodyList.FirstOrDefault().Wafer_Id.Substring(0, 5));
                if (configList != null && configList.Count() > 0)
                {
                    var byPassConfigs = configList.Where(r => r.ConfigKey == "2DBCByPassKey").FirstOrDefault();
                    if (byPassConfigs == null) return sb.ToString();
                    if (fileName.ToUpper().Contains(byPassConfigs.ConfigValue.ToUpper()))
                    {
                        return sb.ToString(); //By Pass Due to Reupload add by pass information
                    }
                    else
                    {
                        var configs = configList.Where(r => r.ConfigKey == "2DBC_Control").FirstOrDefault().ConfigValue.Split(';');
                        if (configs.Count() > 1)
                        {
                            var bincodes = configs[0].Split(',').ToList();
                            var percentage = configs[1].ToString();
                            var avi2CtrlBins = bodyList.Where(r => bincodes.Contains(r.Bin_AOI2)).Count().ToString();
                            var avi2Bins = bodyList.Where(r => r.Wafer_Id != "Fiducial").Select(r => r.Bin_AOI2).Count().ToString();
                            var curPer = Math.Round((double.Parse(avi2CtrlBins) / double.Parse(avi2Bins)) * 100, 2);
                            var ctrlPer = 0.0;
                            double.TryParse(percentage, out ctrlPer);
                            if (curPer > ctrlPer)
                            {
                                sb.AppendFormat("<li>2DBC OOS. Checking bincode:{0}, spec: {1}%, avi2:{2}% ({3}/{4}) ", configs[0], ctrlPer, curPer, avi2CtrlBins, avi2Bins);
                            }
                        }
                        else
                        {
                            sb.AppendFormat("<li>Mask: {0} 2DBC Configuration failed! Config Value: {1}",
                                bodyList.FirstOrDefault().Wafer_Id.Substring(0, 5), configList.FirstOrDefault().ConfigValue);
                        }
                    }
                }

                if (sb.Length > 0)
                    _logger.Warn(sb.ToString());
            }
            catch (Exception)
            {
                throw;
            }

            return sb.ToString();
        }

        private static List<eDocConfigClass> GetEDocConfigList(string mask)
        {
            List<eDocConfigClass> list = new List<eDocConfigClass>();

            try
            {
                string sql = string.Format(@"select * from [dbo].[TBL_eDoc_Config] 
                                    where ServerName = '{0}' and ConfigType = '{1}_Config' and ProductType = 'MP'",
                        Program._MachineName, mask);

                list = _sqlConn.Query<eDocConfigClass>(sql).ToList();
            }
            catch (Exception)
            {
                throw;
            }

            return list;
        }

        private static bool IsAOI2ContMask(string mask)
        {
            bool flag = false;

            try
            {
                string sql = string.Format(@"select * from [dbo].[TBL_eDoc_Config] 
                                    where ServerName = '{0}' and ConfigType = 'Spec' 
                                    and ConfigKey = 'AOI2ContMasks' and ProductType = 'MP'", Program._MachineName);

                var list = _sqlConn.Query<eDocConfigClass>(sql).ToList();
                if (list.Any())
                {
                    var contMasks = list.FirstOrDefault().ConfigValue.Split(";");
                    if (contMasks.Contains(mask))
                        flag = true;
                }
            }
            catch (Exception)
            {
                throw;
            }

            return flag;
        }

        private static void CallAPI(string errorMessage)
        {
            _eDocGenParaClass.GradingFileList = new List<string>();
            _eDocGenParaClass.AVI2FilePath = _inputFilePath;
            _eDocGenParaClass.GoodDieQty = 0;
            _eDocGenParaClass.GradingSpecFilePath = "";
            _eDocGenParaClass.WaferTestHeader = new eDocWaferTestHeaderClass();
            APIHelper aPIHelper = new APIHelper(_config, _logger, _eDocGenParaClass);
            aPIHelper.SendEDocAPI("Fail", errorMessage, Program._MachineName);
        }
    }
}
