using Dapper;
using Dapper.Contrib.Extensions;
using eDocGenAPI.Classes;
using eDocGenAPI.Utils;
using eDocGenLib.Classes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenAPI.Controllers
{
    public class eDocGenController : Controller
    {
        private readonly IDbConnection _sqlConn;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public eDocGenController(IDbConnection sqlConn, ILogger<eDocGenController> logger, IConfiguration config)
        {
            this._sqlConn = sqlConn;
            this._logger = logger;
            this._config = config;
        }

        [HttpGet]
        [Route("GetSpecInfo")]
        public IActionResult GetSpecInfo(string groupName)
        {
            try
            {
                var sql = string.Format(@"Select sp.spec_id, gm.mask, sp.spec_filename, sp.spec_version,
                  es.Id, es.UMCFileName, es.EMapVersion 
                  from [centralize_prod].[dbo].tbl_group_mask_map gm
                  inner join [centralize_prod].[dbo].tbl_spec sp on gm.mask = SUBSTRING(sp.spec_filename, 1, 5)
                  left join [eDoc_Prod].[dbo].TBL_eDoc_Spec es on gm.mask = es.Mask
                  where sp.status_id = 2 and gm.group_id = '{0}'
                  order by spec_filename desc ", groupName);

                var res = this._sqlConn.Query<GradingSpecClass>(sql);
                var json = JsonConvert.SerializeObject(res);
                return StatusCode(StatusCodes.Status200OK, json);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status303SeeOther, ex.Message);
            }
            //return StatusCode(StatusCodes.Status200OK, null);
        }

        #region UMC Section
        /// <summary>
        /// ProcessAndUploadUMCFile
        /// </summary>
        /// <param name="inputUMC"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("ProcessAndUploadUMCFile")]
        public async Task<ActionResult> ProcessAndUploadUMCFileAsync([FromBody] UMCInputClass inputUMC)
        {
            var rtnMsg = string.Empty;
            try
            {
                TraceabilityUtil traceabilityUtil = new TraceabilityUtil(this._sqlConn, this._logger);
                var folder = _config["Folder:UMC_File"].ToString();

                if (inputUMC.FormFile != null && inputUMC.FormFile.Length > 0)
                {
                    //Setup eDoc Spec Info
                    eDocGenUtil util = new eDocGenUtil(this._sqlConn, this._logger);
                    EDocSpecClass eDocSpec = new EDocSpecClass()
                    {
                        Id = Guid.Parse(inputUMC.Id),
                        UMCFile = inputUMC.FormFile,
                        UMCFileName = inputUMC.FileName,
                        CreatedBy = inputUMC.CreatedBy,
                        Mask = inputUMC.Product,
                        ProductType = inputUMC.ProductType,
                        EMapVersion = inputUMC.EMapVersion,
                        Status = 1
                    };
                    var isSetHeader = util.SetEDocSpecInfo(ref eDocSpec, false);

                    if (isSetHeader)
                    {
                        //var path = $@"{folder}\{inputUMC.formFile.FileName}";
                        inputUMC.Id = eDocSpec.Id.ToString(); //Set header Id
                        var res = traceabilityUtil.ProcessUMCFile(inputUMC);
                        if (res.Count() > 0)
                        {
                            await traceabilityUtil.BulkInsertUMCData(res);
                            rtnMsg = string.Format("InsertUMCData Succeed!", inputUMC.FileName);
                        }
                        else
                        {
                            rtnMsg = string.Format("ProcessUMCFile fail! File name: {0}", inputUMC.FileName);
                        }
                    }
                    else
                    {
                        rtnMsg = string.Format("Insert/Update eDoc Spec Header fail! File name: {0}", inputUMC.FileName);
                    }
                }
                else
                {
                    rtnMsg = string.Format("Input file can not be empty! File name: {0}", inputUMC.FileName);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status303SeeOther, ex.Message);
            }

            return StatusCode(StatusCodes.Status200OK, rtnMsg);
        }
        [HttpGet]
        [Route("DownloadUMCFile")]
        public ActionResult DownloadUMCFile(string eDocSpecId)
        {
            FileContentResult result;
            try
            {
                var eDocSpec = _sqlConn.Query<EDocSpecClass>(string.Format("SELECT TOP 1 * FROM TBL_eDoc_Spec Where Id = '{0}' ", eDocSpecId));

                if (eDocSpec != null && eDocSpec.Count() > 0)
                {
                    Stream stream = new MemoryStream(eDocSpec.FirstOrDefault().UMCFile);

                    HttpContext.Response.ContentType = "application/octet-stream";
                    result = new FileContentResult(eDocSpec.FirstOrDefault().UMCFile, "application/octet-stream")
                    {
                        FileDownloadName = eDocSpec.FirstOrDefault().UMCFileName
                    };
                    return result;
                }
            }
            catch (Exception)
            {
                throw;
            }
            return Content("No file found!");
        }
        #endregion

        [HttpPost]
        [Route("ConvertFennecFormat")]
        public IActionResult ConvertFennecFormat(FennecAVI2InputClass inputAVI2)
        {
            try
            {
                if (string.IsNullOrEmpty(inputAVI2.OutputFilePath))
                    inputAVI2.OutputFilePath = _config["Folder:AVI2_File"].ToString();

                TraceabilityUtil traceabilityUtil = new TraceabilityUtil(this._sqlConn, this._logger);
                traceabilityUtil.ProcessFennecFile(inputAVI2);
            }
            catch (Exception)
            {
                throw;
            }


            return StatusCode(StatusCodes.Status200OK, null);
        }

        [HttpPost]
        [Route("SeteDocSpecInfo")]
        public IActionResult SeteDocSpecInfo([FromBody] EDocSpecClass eDocSpec)
        {
            var rtnMsg = string.Empty;
            var now = DateTime.Now;
            try
            {
                eDocGenUtil util = new eDocGenUtil(this._sqlConn, this._logger);
                var res = util.SetEDocSpecInfo(ref eDocSpec, true);
                if (res)
                    rtnMsg = "Insert/Update EDocSpecInfo Succeed!";
                else
                    rtnMsg = "Insert/Update EDocSpecInfo Failed!";
            }
            catch (Exception)
            {

                throw;
            }
            return StatusCode(StatusCodes.Status200OK, rtnMsg);
        }

        #region Query RW Status
        [HttpGet]
        [Route("GetWaferIdByMaskGroup")]
        public IActionResult GetWaferIdByMaskGroup(string maskGroup)
        {
            try
            {
                List<string> resultList = new List<string>();
                var sql = string.Format(@"select Mask from [centralize_prod].[dbo].tbl_group_mask_map where group_id = '{0}' ", maskGroup);

                resultList = this._sqlConn.Query<string>(sql).ToList();
                var json = JsonConvert.SerializeObject(resultList);
                return StatusCode(StatusCodes.Status200OK, json);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status303SeeOther, ex.Message);
            }
        }
        [HttpGet]
        [Route("QueryRWStatus")]
        public IActionResult QueryRWStatus(string rowNum, string sqlCond, string maskGroup, string maskId, string eDocStatus = "Success")
        {
            try
            {
                List<TraceabilityStatusClass> resultList = new List<TraceabilityStatusClass>();

                var sql = string.Format(@"Select top {0} [Wafer_Id], [RW_Wafer_Id], [Good_Die_Qty], [Status] 'eDocStatus', [Error_Message], [grade_spec_version],
                        [eDoc_spec_version], [Latest AVI2 FileName] 'Latest_AVI2_FileName', [Creation_Date] 'eDoc_CreatedDate', [Last_Updated_Date] 'eDoc_LastUpdatedDate'
                        FROM [grading_dev].[dbo].[tbl_wafer_resume_result_view] 
                        Where [Status] = '{1}' {2} {3} Order By Last_Updated_Date Desc ", rowNum, eDocStatus,
                        string.IsNullOrEmpty(maskGroup) == false ? string.Format("and MaskGroup = '{0}' ", maskGroup) : string.Empty,
                        string.IsNullOrEmpty(maskId) == false ? string.Format("and SUBSTRING(Wafer_Id, 1, 5) = '{0}' ", maskId) : string.Empty);

                var gradingDevConn = _config["ConnectionStrings:GradingDevConnection"].ToString();
                using (var gDevConn = new SqlConnection(gradingDevConn))
                {
                    var gDevList = gDevConn.Query<TraceabilityStatusClass>(sql);

                    if (gDevList.Any())
                    {
                        var rwList = gDevList.Select(r => r.RW_Wafer_Id).ToList();

                        sql = string.Format(@"select Wafer_Id, RW_Wafer_Id, FilePath, Status 'IngestionStatus',
                                CreatedDate 'Ingestion_CreatedDate', LastUpdatedDate 'Ingestion_LastUpdatedDate'
                                from [dbo].[TBL_Traceability_Info] where status != 0 and rw_wafer_id in ('{0}') {1} 
                                Order By CreatedDate Desc ", string.Join("','", rwList).TrimEnd(','), sqlCond);

                        var traceList = this._sqlConn.Query<TraceabilityStatusClass>(sql);

                        resultList = (from rw in gDevList
                                      join trace in traceList
                                      on rw.RW_Wafer_Id equals trace.RW_Wafer_Id into tp
                                      from trace in tp.DefaultIfEmpty()
                                      select new TraceabilityStatusClass()
                                      {
                                          Wafer_Id = rw.Wafer_Id,
                                          RW_Wafer_Id = rw.RW_Wafer_Id,
                                          FilePath = trace != null ? trace.FilePath : string.Empty,
                                          IngestionStatus = GetIngestionStatus(trace != null ? trace.IngestionStatus : "99"),
                                          Ingestion_CreatedDate = trace != null ? trace.Ingestion_CreatedDate: DateTime.Now,
                                          Ingestion_LastUpdatedDate = trace != null ? trace.Ingestion_LastUpdatedDate : DateTime.Now,
                                          Good_Die_Qty = rw.Good_Die_Qty,
                                          eDocStatus = rw.eDocStatus,
                                          Error_Message = rw.Error_Message,
                                          eDoc_Spec_Version = rw.eDoc_Spec_Version,
                                          Grade_Spec_Version = rw.Grade_Spec_Version,
                                          Latest_AVI2_FileName = rw.Latest_AVI2_FileName,
                                          eDoc_CreatedDate = rw.eDoc_CreatedDate,
                                          eDoc_LastUpdatedDate = rw.eDoc_LastUpdatedDate
                                      }).ToList();
                    }
                }

                var json = JsonConvert.SerializeObject(resultList);
                return StatusCode(StatusCodes.Status200OK, json);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status303SeeOther, ex.Message);
            }

        }

        [HttpGet]
        [Route("QueryRWStatus_Bak")]
        public IActionResult QueryRWStatus_Bak (string rowNum, string sqlCond, string maskGroup, string maskId, string eDocStatus = "Success")
        {
            try
            {
                List<TraceabilityStatusClass> resultList = new List<TraceabilityStatusClass>();
                var sql = string.Format(@"select top {0} Wafer_Id, RW_Wafer_Id, FilePath, Status 'IngestionStatus',
                                CreatedDate 'Ingestion_CreatedDate', LastUpdatedDate 'Ingestion_LastUpdatedDate'
                                from [dbo].[TBL_Traceability_Info] where status != 0 {1} Order By CreatedDate Desc ", rowNum, sqlCond);

                var traceList = this._sqlConn.Query<TraceabilityStatusClass>(sql);
                if (traceList.Any())
                {
                    var rwList = traceList.Select(r => r.RW_Wafer_Id).ToList();
                    if (rwList.Any())
                    {
                        sql = string.Format(@"Select [Wafer_Id], [RW_Wafer_Id], [Good_Die_Qty], [Status] 'eDocStatus', [Error_Message], [grade_spec_version],
                        [eDoc_spec_version], [Latest AVI2 FileName] 'Latest_AVI2_FileName', [Creation_Date] 'eDoc_CreatedDate', [Last_Updated_Date] 'eDoc_LastUpdatedDate'
                        FROM [grading_dev].[dbo].[tbl_wafer_resume_result_view] 
                        Where rw_wafer_id in ('{0}') and [Status] = '{1}' {2} {3} ",
                        string.Join("','", rwList).TrimEnd(','), eDocStatus,
                        string.IsNullOrEmpty(maskGroup) == false ? string.Format("and MaskGroup = '{0}' ", maskGroup) : string.Empty,
                        string.IsNullOrEmpty(maskId) == false ? string.Format("and SUBSTRING(Wafer_Id, 1, 5) = '{0}' ", maskId) : string.Empty);
                        var gradingDevConn = _config["ConnectionStrings:GradingDevConnection"].ToString();
                        using (var gDevConn = new SqlConnection(gradingDevConn))
                        {
                            var gDevList = gDevConn.Query<TraceabilityStatusClass>(sql);

                            if (gDevList.Any())
                            {
                                resultList = (from trace in traceList
                                              join rw in gDevList
                                              on trace.RW_Wafer_Id equals rw.RW_Wafer_Id
                                              select new TraceabilityStatusClass()
                                              {
                                                  Wafer_Id = rw.Wafer_Id,
                                                  RW_Wafer_Id = rw.RW_Wafer_Id,
                                                  FilePath = trace.FilePath,
                                                  IngestionStatus = GetIngestionStatus(trace.IngestionStatus),
                                                  Ingestion_CreatedDate = trace.Ingestion_CreatedDate,
                                                  Ingestion_LastUpdatedDate = trace.Ingestion_LastUpdatedDate,
                                                  Good_Die_Qty = rw.Good_Die_Qty,
                                                  eDocStatus = rw.eDocStatus,
                                                  Error_Message = rw.Error_Message,
                                                  eDoc_Spec_Version = rw.eDoc_Spec_Version,
                                                  Grade_Spec_Version = rw.Grade_Spec_Version,
                                                  Latest_AVI2_FileName = rw.Latest_AVI2_FileName,
                                                  eDoc_CreatedDate = rw.eDoc_CreatedDate,
                                                  eDoc_LastUpdatedDate = rw.eDoc_LastUpdatedDate
                                              }).ToList();
                            }
                        }
                    }
                }

                var json = JsonConvert.SerializeObject(resultList);
                return StatusCode(StatusCodes.Status200OK, json);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status303SeeOther, ex.Message);
            }

        }

        private string GetIngestionStatus(string ingStatus)
        {
            switch (ingStatus)
            {
                case "1":
                    return "In-Progress";
                case "2":
                    return "Done";
                case "9":
                    return "Failed";
                case "99":
                    return "Ingestion Error";
                default:
                    return string.Empty;
            }
        }
        #endregion

        #region Upload Files
        [HttpPost]
        [Route("OnPostUploadAsync")]
        public async Task<IActionResult> OnPostUploadAsync(List<IFormFile> files, string folder)
        {
            try
            {
                long size = files.Sum(f => f.Length);

                foreach (var formFile in files)
                {
                    if (formFile.Length > 0)
                    {
                        var filePath = Path.Combine(folder,
                            formFile.FileName);

                        using (var stream = System.IO.File.Create(filePath))
                        {
                            await formFile.CopyToAsync(stream);
                        }
                    }
                }

                // Process uploaded files
                // Don't rely on or trust the FileName property without validation.

                return Ok(new { count = files.Count, size });
            }
            catch (Exception ex)
            {
                return NotFound(new { Error = ex.Message });
            }            
        }
        #endregion
    }
}
