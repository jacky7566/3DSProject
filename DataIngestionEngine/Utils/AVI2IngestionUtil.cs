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
        public static string RW_Wafer_Id;
        public static string Wafer_Id;
        private static IConfiguration _config;
        private static ILogger _logger;
        private static string _inputFilePath;
        private static IDbConnection _sqlConn;
        public AVI2IngestionUtil(IConfiguration config, ILogger logger)
        {
            _config = config;
            _logger = logger;
        }
        public static bool ProcessStartAVI2(FileInfo file)
        {
            _sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]);
            _inputFilePath = file.FullName;
            _logger.Info(string.Format("Import file to Raw Data Table: {0}", file.Name));
            return ExtractTextFileAsync().Result;
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

                    if (headerList != null && headerList.Count() > 0)
                    {
                        //Check Coordinate duplication
                        errorSB.Append(CheckiGxiGyIsDuplicate(bodyList));
                        errorSB.Append(CheckoGxoGyIsDuplicate(bodyList));
                        //Check 2DBC Control
                        errorSB.Append(Check2DBCControl(bodyList, new FileInfo(_inputFilePath).Name));

                        if (errorSB.Length > 0)
                        {
                            MailHelper mail = new MailHelper(_config);
                            var rwId = headerList.FirstOrDefault().RW_Wafer_Id;
                            mail.SendMail(String.Empty, new List<string>() { "jacky.li@lumentum.com" }, "eDoc Ingestion Alert - RW Wafer Id: " + rwId,
                                string.Format("RW_Wafer_Id: {0} <BR> Error Message:<BR><BR>{1}", rwId, errorSB.ToString()).Replace("\n\r", "<BR>"), true);
                            return false;
                        }
                        if (await ImportToHeaderInfoAsync(headerList))
                        {
                            //return await ImportToBodyAsync(bodyList);                            
                            return BatchInsert(bodyList, "Tbl_AVI2_RawData");
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

        private static void BuildAndAddNewHeaderInfo(ref List<Traceability_InfoClass> headerList, string waferId)
        {
            var filePath = FileDetectUtil._IngestedPath + Path.DirectorySeparatorChar + new FileInfo(_inputFilePath).Name;
            var isMove = _config["Configurations:IsMoveToIngestedFolder"];
            if (isMove == "N")
                filePath = _inputFilePath;
            headerList.Add(new Traceability_InfoClass()
            {
                Wafer_Id = waferId,
                FilePath = filePath,
                FileType = "AVI2",
                Status = 1,
                Id = Guid.NewGuid(),
                CreatedBy = Environment.MachineName,
                LastUpdatedBy = Environment.MachineName,
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
                    CreatedBy = Environment.MachineName,
                    LastUpdatedBy = Environment.MachineName,
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

        private static async Task<bool> ImportToHeaderInfoAsync(List<Traceability_InfoClass> headerList) 
        {
            //Insert Header Info by List
            try
            {
                //Remove exists Header data
                string sql = string.Format("Update TBL_Traceability_Info Set Status = 0, LastUpdatedDate = getdate() WHERE RW_Wafer_Id in ('{0}')",
                    String.Join("','", headerList.Select(r => r.RW_Wafer_Id)));

                _sqlConn.Execute(sql);

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
                //Remove exists UMC data
                string sql = string.Format("Delete From Tbl_AVI2_RawData WHERE RW_Wafer_Id = '{0}' ",
                    bodyList.FirstOrDefault().RW_Wafer_Id);

                _sqlConn.Execute(sql);

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
                string sql = string.Format("Delete From Tbl_AVI2_RawData WHERE RW_Wafer_Id = '{0}' ",
                    bodyList.FirstOrDefault().RW_Wafer_Id);

                _sqlConn.Execute(sql);

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
                            var avi2Bins = bodyList.Select(r => r.Bin_AOI2).Count().ToString();
                            var curPer = Math.Round((double.Parse(avi2CtrlBins) / double.Parse(avi2Bins)) * 100, 2);
                            var ctrlPer = 0.0;
                            double.TryParse(percentage, out ctrlPer);
                            if (curPer > ctrlPer)
                            {
                                sb.AppendFormat("2DBC OOS. Checking bincode:{0}, spec: {1}%, avi2:{2}% ({3}/{4})", configs[0], ctrlPer, curPer, avi2CtrlBins, avi2Bins);
                            }
                        }
                        else
                        {
                            sb.AppendFormat("Mask: {0} 2DBC Configuration failed! Config Value: {1}", Wafer_Id.Substring(0, 5), configList.FirstOrDefault().ConfigValue);
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
                        Environment.MachineName, mask);

                list = _sqlConn.Query<eDocConfigClass>(sql).ToList();
            }
            catch (Exception)
            {
                throw;
            }

            return list;
        }
    }
}
