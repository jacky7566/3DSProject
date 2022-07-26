using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;
using System.IO;
using OfficeOpenXml;

namespace eDocGenLib.Utils
{
    public class ExcelOperator
    {
        public static List<Dictionary<string, string>> GetCsvDataToDic(string filePath, int startIdx, bool isToUpper, string defaultValue = "")
        {
            var file = new FileInfo(filePath);
            if (!file.Exists)
            {
                return null;
            }

            List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
            string[] keys = { };
            string[] vals = { };
            string tempFile = filePath + "temp";
            File.Copy(filePath, tempFile, true);
            StreamReader sr = new StreamReader(tempFile, Encoding.Default);
            try
            {
                int idx = 0;
                bool hasHeader = false;
                do
                {                                       
                    String line = sr.ReadLine();
                    vals = line.Split(',');
                    if (!hasHeader)
                    {
                        if (idx >= startIdx)
                        {                            
                            keys = vals.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                            hasHeader = true;
                        }
                    }
                    else
                    {
                        Dictionary<string, string> dic = new Dictionary<string, string>();
                        for (var i = 0; i < keys.Length; i++)
                        {
                            if (i > vals.Length - 1)
                            {
                                dic.Add(isToUpper ? keys[i].ToUpper() : keys[i], defaultValue);
                            }
                            else
                            {
                                dic.Add(isToUpper ? keys[i].ToUpper() : keys[i], vals[i]);
                            }
                        }
                        list.Add(dic);
                    }
                    idx++;

                } while (sr.Peek() > -1);
            }
            catch (Exception ex)
            {
                string msg = ex.StackTrace;
                throw;
            }
            finally
            {
                sr.Close();
                File.Delete(tempFile);
            }

            return list;
        }

        public static List<Dictionary<string, string>> GetCsvDataToDic(string filePath)
        {
            return GetCsvDataToDic(filePath, 0, false, string.Empty);
        }

        public static List<Dictionary<string, string>> GetCsvDataToDic(string filePath, bool isToUpper)
        {
            return GetCsvDataToDic(filePath, 0, isToUpper, string.Empty);
        }

        public static List<Dictionary<string, string>> GetCsvDataToDic(string filePath, int startIdx)
        {
            return GetCsvDataToDic(filePath, startIdx, false, string.Empty);
        }

        public static List<Dictionary<string, string>> GetCsvDataToDic(string filePath, string defaultValue)
        {
            return GetCsvDataToDic(filePath, 0, false, defaultValue);
        }

        public static List<Dictionary<string, string>> GetCsvDataToDicReverse(string filePath)
        {
            var file = new FileInfo(filePath);
            if (!file.Exists)
            {
                return null;
            }
            
            List<Dictionary<string, string>> list = new List<Dictionary<string, string>>();
            string tempFile = filePath + "temp";
            File.Copy(filePath, tempFile, true);
            StreamReader sr = new StreamReader(tempFile, Encoding.Default);
            string[] stringSeparators = new string[] { "\r\n" };
            Dictionary<string, string> dic = new Dictionary<string, string>();
            try
            {
                string rawData = sr.ReadToEnd();
                string[] rows = rawData.Split(stringSeparators, StringSplitOptions.RemoveEmptyEntries);
                //Rebuild Data
                int colCnt = 0;
                if (rows.Count() > 0)
                    colCnt = rows[0].Split(',').Count(); //Filter Header -1

                for (int j = 0; j < colCnt; j++)
                {
                    if (j == colCnt - 1) break;
                    dic = new Dictionary<string, string>();
                    for (int i = 0; i < rows.Count(); i++)
                    {
                        var rowAry1 = rows[i].Split(',');                        
                        dic.Add(rowAry1[0].ToString(), rowAry1[j + 1].ToString());
                    }
                    list.Add(dic);
                }
            }
            catch (Exception ex)
            {
                string msg = ex.StackTrace;
                throw;
            }
            finally
            {
                sr.Close();
                File.Delete(tempFile);
            }

            return list;
        }

            public static void CsvToExcel(string csvFilePath, string excelFilePath, bool needStyle, bool deleteCsv)
        {
            //string csvFilePath = @"D:\sample.csv";
            //string excelFilePath = @"D:\sample.xls";

            //string worksheetsName = "TEST";
            bool firstRowIsHeader = true;

            var excelTextFormat = new ExcelTextFormat();
            excelTextFormat.Delimiter = ',';
            excelTextFormat.EOL = "\r";

            var excelFileInfo = new FileInfo(excelFilePath);
            var csvFileInfo = new FileInfo(csvFilePath);
            string worksheetsName = csvFileInfo.Name.Replace(".csv", "");

            try
            {
                using (ExcelPackage package = new ExcelPackage(excelFileInfo))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets.Add(worksheetsName);

                    var tableStyle = needStyle ? OfficeOpenXml.Table.TableStyles.Medium25 : OfficeOpenXml.Table.TableStyles.None;
                    worksheet.Cells["A1"].LoadFromText(csvFileInfo, excelTextFormat, tableStyle, firstRowIsHeader);
                    //worksheet.Cells["A1"].LoadFromText(csvFileInfo, excelTextFormat);
                    package.Save();
                }

                if (deleteCsv)
                    csvFileInfo.Delete();
            }
            catch (Exception e)
            {
                LogHelper.WriteLine("cannot read " + excelFileInfo.Name);
                LogHelper.WriteLine(csvFileInfo.Name + "cannot convert to excel!!");
                LogHelper.WriteLine("error message:" + e.Message + "\r\n" + e.StackTrace);
                //Console.ReadLine();
            }
        }

        //public static void DataTableToCsv(string filePath, string fileName, DataTable dataTable)
        //{
        //    var columnNames = dataTable.Columns.Cast<DataColumn>()
        //        .Select(x => x.ColumnName).ToList();

        //    var values = dataTable.AsEnumerable()
        //        .Select(row => string.Join(",", row.ItemArray.Select(value => value.ToString()))).ToList();

        //    var csv = new StringBuilder(string.Join(",", columnNames) + "\r\n" + string.Join("\r\n", values));
        //    FileHelper.WriteAllText(filePath, fileName, csv.ToString());
        //}

        public static DataTable GetDataTableFromExcel(string path, string sheetName, bool hasHeader = true)
        {
            using (var pck = new OfficeOpenXml.ExcelPackage())
            {
                using (var stream = File.OpenRead(path))
                {
                    pck.Load(stream);
                }
                var ws = pck.Workbook.Worksheets[sheetName];
                DataTable tbl = new DataTable();
                foreach (var firstRowCell in ws.Cells[1, 1, 1, ws.Dimension.End.Column])
                {
                    tbl.Columns.Add(hasHeader ? firstRowCell.Text : string.Format("Column {0}", firstRowCell.Start.Column));
                }
                var startRow = hasHeader ? 2 : 1;
                for (int rowNum = startRow; rowNum <= ws.Dimension.End.Row; rowNum++)
                {
                    var wsRow = ws.Cells[rowNum, 1, rowNum, ws.Dimension.End.Column];
                    DataRow row = tbl.Rows.Add();
                    foreach (var cell in wsRow)
                    {
                        row[cell.Start.Column - 1] = cell.Text;
                    }
                }
                return tbl;
            }
        }

        public static void DicsToCsv(List<Dictionary<string, string>> list, string filePath)
        {
            var sb = new StringBuilder(string.Join(",", list[0].Keys) + "\r\n");
            foreach (var dic in list)
            {
                sb.AppendLine(string.Join(",", dic.Values));
            }

            File.WriteAllText(filePath, sb.ToString());
        }
    }
}
