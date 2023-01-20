using Dapper;
using eDocGenAPI.Classes;
using eDocGenLib.Classes;
using eDocGenLib.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenAPI.Utils
{
    public class TraceabilityUtil
    {
        const int BATCH_SIZE = 5000;
        private IDbConnection _sqlConn;
        private ILogger _logger;

        public TraceabilityUtil(IDbConnection sqlConn, ILogger logger)
        {
            _sqlConn = sqlConn;
            _logger = logger;
        }

        #region UMC
        public List<RWDimensionClass> ProcessUMCFile(UMCInputClass inputUMC)
        {
            List<RWDimensionClass> dimensionList = new List<RWDimensionClass>();
            RWDimensionClass rWDimension;
            List<string> l_vals = new List<string>();
            int x = 0;
            bool b_START = false;
            try
            {
                using (var sr = new StreamReader(inputUMC.FormFile.OpenReadStream()))
                {
                    string line = string.Empty;
                    do
                    {
                        line = sr.ReadLine().Trim();
                        #region remove header
                        if (line.Trim().ToUpper().StartsWith("NODI"))
                        {
                            b_START = true;
                            continue;
                        }
                        if (!b_START)
                        {
                            continue;
                        }
                        #endregion

                        char[] aLine = line.ToCharArray();
                        if (string.IsNullOrEmpty(line.Trim()) == false)
                        {
                            #region replace X(no die) to 0
                            for (int i_c = 0; i_c < aLine.Count(); i_c++)
                            {
                                string tVal = aLine[i_c].ToString();
                                if (tVal.ToUpper().Equals("X"))
                                {
                                    aLine[i_c] = '0';
                                }
                                else
                                {
                                    break;
                                }
                            }

                            for (int i_c = aLine.Count() - 1; i_c >= 0; i_c--)
                            {
                                string tVal = aLine[i_c].ToString();
                                if (tVal.ToUpper().Equals("X"))
                                {
                                    aLine[i_c] = '0';
                                }
                                else
                                {
                                    break;
                                }
                            }
                            #endregion

                            x++;
                            int i = 0;
                            //For No need rotate purpose: int y = 0; y < aLine.Count(); y++
                            for (int y = aLine.Count(); y > 0; y--)
                            {
                                rWDimension = new RWDimensionClass();
                                i++;
                                rWDimension.No = i;
                                rWDimension.Product = inputUMC.Product;
                                rWDimension.ProductType = inputUMC.ProductType;
                                rWDimension.OGx = x + inputUMC.XShift;
                                rWDimension.OGy = y + inputUMC.YShift;
                                rWDimension.EDocSpecId = Guid.Parse(inputUMC.Id);
                                string s_val = aLine[i - 1].ToString();
                                if (s_val.ToUpper().Equals("1"))
                                {
                                    rWDimension.Device = "POR";
                                    //string map_line = string.Format(@"{0},{1},{2}", x - rx, y - ry, "POR");
                                    //l_vals.Add(map_line);
                                }
                                else if (s_val.ToUpper().Equals("X"))
                                {
                                    rWDimension.Device = "Fiducial";
                                    //string map_line = string.Format(@"{0},{1},{2}", x - rx, y - ry, "Fiducial");
                                    //l_vals.Add(map_line);
                                }
                                else continue;

                                dimensionList.Add(rWDimension);
                            }
                        }
                    }
                    while (sr.Peek() > -1);
                    sr.Close();
                }
            }
            catch (Exception)
            {

                throw;
            }


            return dimensionList;
        }

        public bool InsertUMCData(List<RWDimensionClass> list)
        {
            try
            {
                List<SqlParameter> dynamicList = new List<SqlParameter>();
                //Remove exists UMC data
                string sql = string.Format("DELETE TBL_RW_Dimension WHERE Product = '{0}' AND ProductType = '{1}'",
                    list.FirstOrDefault().Product, list.FirstOrDefault().ProductType);

                this._sqlConn.Execute(sql);

                //Inser Data
                //Get Column Names
                var cols = typeof(RWDimensionClass).GetProperties().Select(r => r.Name).Aggregate((res, next) => res + ", " + next);
                //Get Parameter Columns
                var pCols = "@" + typeof(RWDimensionClass).GetProperties().Select(r => r.Name).Aggregate((res, next) => res + @", @" + next);

                sql = string.Format(@"INSERT INTO TBL_RW_Dimension ({0}) VALUES ({1})", cols, pCols);

                this._sqlConn.Open();
                //var count = 0;
                _logger.LogInformation("BeginTransaction - Count: " + list.Count());
                using (var tran = this._sqlConn.BeginTransaction())
                {
                    try
                    {
                        this._sqlConn.Execute(sql, list, transaction: tran);
                        tran.Commit();
                        return true;
                        //foreach (var batchData in SplitBatch<RWDimensionClass>(list, BATCH_SIZE))
                        //{
                        //    count += batchData.Length;
                        //    this._sqlConn.Execute(sql, batchData, transaction: tran);
                        //    _logger.LogInformation($"\r{count}/{dynamicList.Count()}({count * 1.0 / dynamicList.Count():p0})");
                        //}
                        //_logger.LogInformation("EndTransaction");
                        //tran.Commit();
                        //return true;
                    }
                    catch (Exception)
                    {
                        tran.Rollback();
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }
            return false;
        }
        public async Task BulkInsertUMCData(List<RWDimensionClass> list)
        {
            try
            {
                //Remove exists UMC data
                string sql = string.Format("DELETE TBL_RW_Dimension WHERE Product = '{0}' AND ProductType = '{1}'",
                    list.FirstOrDefault().Product, list.FirstOrDefault().ProductType);

                this._sqlConn.Execute(sql);

                this._logger.LogInformation("Start BulkInsertUMCData, total count: " + list.Count);
                var res = await this._sqlConn.BulkInsert<RWDimensionClass>(
                    "TBL_RW_Dimension",
                    list,
                    new Dictionary<string, Func<RWDimensionClass, object>>
                        {
                            { "No", u => u.No },
                            { "OGx", u => u.OGx },
                            { "OGy", u => u.OGy },
                            { "Device", u => u.Device },
                            { "Product", u => u.Product },
                            { "ProductType", u => u.ProductType },
                            { "EDocSpecId", u => u.EDocSpecId }
                        });
                this._logger.LogInformation("End BulkInsertUMCData, total count: " + list.Count);
            }
            catch (Exception)
            {
                throw;
            }
        }

        public bool BulkInsertUMCData2(List<RWDimensionClass> list, string table_name)
        {
            try
            {
                string sql = string.Format("DELETE TBL_RW_Dimension WHERE Product = '{0}' AND ProductType = '{1}'",
                    list.FirstOrDefault().Product, list.FirstOrDefault().ProductType);

                this._sqlConn.Execute(sql);

                var dt = IOHelper.ConvertToDataTableWithType(list);

                this._logger.LogInformation(string.Format("Start BatchInsert to {0}, total count: {1}", table_name, dt.Rows.Count));
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
                _logger.LogInformation(string.Format("End BatchInsert to {0}, total count: {1}", table_name, dt.Rows.Count));
                return true;
            }
            catch (Exception ex)
            {
                this._logger.LogDebug(ex.Message);
                return false;
            }

        }
        #endregion

        #region Fennec
        public bool ProcessFennecFile(FennecAVI2InputClass inputAVI2)
        {
            try
            {
                var dicList = ExtractTraceabilityFile(inputAVI2);

                //Check Duplicate
                var duplicateList = CheckInputDuplication(dicList);

                if (duplicateList.Count() == 0)
                {
                    if (string.IsNullOrEmpty(inputAVI2.OutputFileName))
                        inputAVI2.OutputFileName = string.Format("{0}_dummy_AVI2_{1}.txt", inputAVI2.RW_Wafer_Id, DateTime.Now.ToString("MMddyyyy_HHmmss"));

                    inputAVI2.OutputFileName = Path.Combine(inputAVI2.OutputFilePath, inputAVI2.OutputFileName);

                    // Check if file already exists. If yes, delete it.     
                    if (File.Exists(inputAVI2.OutputFileName))
                    {
                        File.Delete(inputAVI2.OutputFileName);
                    }

                    // Overwriting to the above existing file 
                    using (StreamWriter sw = File.CreateText(inputAVI2.OutputFileName))
                    {
                        sw.WriteLine("Wafer ID," + inputAVI2.RW_Wafer_Id);
                        sw.WriteLine("Start time," + DateTime.Now.ToString("MM/dd/yyyy HH:mm"));
                        sw.WriteLine("End time," + DateTime.Now.ToString("MM/dd/yyyy HH:mm"));
                        sw.WriteLine("CostTime,00:00:00");
                        sw.WriteLine("Recipe,Dummy");
                        sw.WriteLine("Machine,Dummy");
                        sw.WriteLine(dicList.First().Keys.Aggregate((res, next) => res + ";" + next));
                        foreach (var dic in dicList)
                        {
                            sw.WriteLine(dic.Values.Aggregate((res, next) => res + ";" + next));
                        }
                        return true;
                    }
                }
                else
                {
                    foreach (var item in duplicateList)
                    {
                        this._logger.LogInformation("Duplicate: " + item);
                    }
                    return false;
                }                
            }
            catch (Exception)
            {
                throw;
            }
        }

        private List<Dictionary<string, string>> ExtractTraceabilityFile(FennecAVI2InputClass inputAVI2)
        {
            List<Dictionary<string, string>> avi2List = new List<Dictionary<string, string>>();
            List<string> headerList = new List<string>();
            Dictionary<string, string> avi2Dic;
            try
            {
                using (var sr = new StreamReader(inputAVI2.FormFile.OpenReadStream()))
                {
                    string line = string.Empty;
                    do
                    {
                        line = sr.ReadLine().Trim();
                        if (line.Trim().ToUpper().StartsWith("NO"))
                        {
                            headerList = line.Split(',', StringSplitOptions.TrimEntries).ToList();
                        }
                        else
                        {
                            if (headerList.Count() > 0)
                            {
                                avi2Dic = new Dictionary<string, string>();
                                var contentList = line.Split(',', StringSplitOptions.TrimEntries).ToList();
                                for (int i = 0; i < contentList.Count; i++)
                                {
                                    switch (headerList[i])
                                    {
                                        case "InputWafer":
                                            avi2Dic.Add("InputWafer", contentList[4]);
                                            break;
                                        case "Bar ID":
                                            avi2Dic.Add("IGx", contentList[i]);
                                            break;
                                        case "Chip ID":
                                            avi2Dic.Add("IGy", contentList[i]);
                                            break;
                                        case "OutputWafer":
                                            if (string.IsNullOrEmpty(inputAVI2.RW_Wafer_Id))
                                            {
                                                inputAVI2.RW_Wafer_Id = contentList[i];
                                            }
                                            var value = contentList[i - 1];
                                            if (value.Equals("X") == false) value = "1";
                                            avi2Dic.Add("PNP Bin", value);
                                            avi2Dic.Add("Bin AOI1", value);
                                            break;
                                        case "Output X":
                                            avi2Dic.Add("Ogx", contentList[i]);
                                            break;
                                        case "Output Y":
                                            avi2Dic.Add("Ogy", contentList[i]);
                                            break;
                                        case "BinCode":
                                            avi2Dic.Add("Bin AOI2", contentList[i]);
                                            break;
                                        default:
                                            avi2Dic.Add(headerList[i], contentList[i]);
                                            break;
                                    }                                    
                                }
                                avi2List.Add(avi2Dic);
                            }
                            else break;
                        }
                    }
                    while (sr.Peek() > -1);
                    sr.Close();
                }
            }
            catch (Exception)
            {
                throw;
            }
            return avi2List;
        }
        #endregion

        private List<string> CheckInputDuplication(List<Dictionary<string, string>> dicList)
        {
            List<string> checkList = new List<string>();
            List<string> duplicateList = new List<string>();
            string checkStr = string.Empty;
            try
            {
                foreach (var dic in dicList)
                {
                    checkStr = string.Format("InputWafer:{0},Bar ID:{1},Chip ID:{2}", dic["InputWafer"], dic["IGx"], dic["IGy"]);
                    if (checkList.Contains(checkStr) == false)
                    {
                        checkList.Add(checkStr);
                    }
                    else
                    {
                        if (dic["IGx"] != "X" || dic["IGy"] != "X")
                            duplicateList.Add(checkStr);
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            return duplicateList;
        }

        #region Common
        static IEnumerable<T[]> SplitBatch<T>(IEnumerable<T> items, int batchSize)
        {
            return items.Select((item, idx) => new { item, idx })
                .GroupBy(o => o.idx / batchSize)
                .Select(o => o.Select(p => p.item).ToArray());
        }
        #endregion
    }
}
