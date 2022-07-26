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
                  left join [eDoc_Dev].[dbo].TBL_eDoc_Spec es on gm.mask = es.Mask
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
    }
}
