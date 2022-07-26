using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;


namespace eDocGenLib.Utils
{
    public class ExcelHelper<T>
    {
        public void Create(IList<T> dataList, string sheetName, MemoryStream outputStream)
        {
            using (ExcelPackage pck = new ExcelPackage())
            {
                //MemoryStream stream = new MemoryStream();
                ExcelWorkbook workBook = pck.Workbook;
                if (workBook != null)
                {
                    ExcelWorksheet currentWorksheet = workBook.Worksheets.Add(sheetName);

                    //----------------------Header
                    int ihc = 1;
                    Type classType = typeof(T);
                    PropertyInfo[] properties = typeof(T).GetProperties().ToArray();
                    //string className = typeof(T).FullName;
                    List<string> columns = NHibernateHelper.GetColumnNames(typeof(T));
                    //foreach (var property in properties)
                    foreach (var column in columns)
                    {
                        //currentWorksheet.Cells[1, ihc].Value = NHibernateHelper.GetColumnName(className, property.Name).ToUpper();
                        currentWorksheet.Cells[1, ihc].Value = column.ToUpper();
                        currentWorksheet.Cells[1, ihc].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        currentWorksheet.Cells[1, ihc].Style.Fill.BackgroundColor.SetColor(Color.DarkSlateGray);
                        currentWorksheet.Cells[1, ihc].Style.Font.Color.SetColor(Color.White);
                        currentWorksheet.Cells[1, ihc].AutoFitColumns();
                        ihc = ihc + 1;
                    }

                    //----------------------Content
                    object value = new object();
                    int rowNum = 0;
                    PropertyInfo statusPro = properties.Where(p => p.Name == "EngDispositionStatus").FirstOrDefault();
                    PropertyInfo isFirstPassPro = properties.Where(p => p.Name == "IsFirstPass").FirstOrDefault();
                    foreach (var obj in dataList)
                    {
                        int colNum = 0;
                        string status = statusPro == null ? "" : (string)statusPro.GetValue(obj);
                        string isFirstPass = isFirstPassPro == null ? "" : (string)isFirstPassPro.GetValue(obj);

                        //foreach (var property in properties)
                        foreach (var column in columns)
                        {
                            var propertyName = NHibernateHelper.GetPropertyName(classType, column);
                            var property = properties.Where(p => p.Name == propertyName).First();
                            value = property.GetValue(obj);
                            if (value != null)
                            {
                                currentWorksheet.Cells[rowNum + 2, colNum + 1].Value = value.ToString();
                            }

                            Color backgroundColor = Color.White;

                            currentWorksheet.Cells[rowNum + 2, colNum + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;

                            if (status == "Pass")
                            {
                                backgroundColor = Color.OliveDrab;
                            }
                            else if (status == "Scrap")
                            {
                                backgroundColor = Color.LightCoral;
                            }
                            //else if (status == "Retest")
                            //{
                            //    backgroundColor = Color.Khaki;
                            //}
                            if (isFirstPass == "Y")
                            {
                                backgroundColor = Color.PaleGreen;
                            }

                            currentWorksheet.Cells[rowNum + 2, colNum + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                            currentWorksheet.Cells[rowNum + 2, colNum + 1].Style.Fill.BackgroundColor.SetColor(backgroundColor);
                            currentWorksheet.Cells[rowNum + 2, colNum + 1].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                            currentWorksheet.Cells[rowNum + 2, colNum + 1].AutoFitColumns();
                            colNum++;
                        }
                        rowNum++;
                    }
                }
                pck.SaveAs(outputStream);
            }
        }

        public List<T> ToObjList(Stream stream) //, string sheetName
        {
            var list = new List<T>();
            var column = new List<string>();

            Type type = typeof(T);
            var propertys = type.GetProperties();
            var propertyNames = propertys.Select(p => p.Name);

            using (var pck = new ExcelPackage())
            {
                pck.Load(stream);

                var ws = pck.Workbook.Worksheets.First();//.Where(x => x.Name == sheetName).First();

                foreach (var firstRowCell in ws.Cells[1, 1, 1, ws.Dimension.End.Column])
                {
                    column.Add(firstRowCell.Text);
                }

                var startRow = 3;
                for (var rowNum = startRow; rowNum <= ws.Dimension.End.Row; rowNum++)
                {
                    var obj = (T)Assembly.GetAssembly(type).CreateInstance(type.FullName);
                    //int i = 0;
                    for (var colNum = 0; colNum < column.Count; colNum++)
                    {                        
                        object value = new object();
                        string key = column[colNum];
                        if (string.IsNullOrEmpty(key))
                            continue;
                        //var property = propertys.Where(p => p.Name == column[colNum]).FirstOrDefault();
                        var property = propertys.Where(p => (p.GetCustomAttributes(typeof(DisplayAttribute), true).FirstOrDefault() as DisplayAttribute).Name == column[colNum]).FirstOrDefault();
                        if (property == null)
                        {
                            throw new Exception(" column " + (colNum + 1) + " (" + column[colNum] + ") display name is wrong");
                        }


                        var proTypeFullName = property.PropertyType.FullName;
                        var tempValue = property.GetValue(obj);
                        var cell = ws.Cells[rowNum, colNum + 1];

                        try
                        {
                            if (cell.Style.Numberformat.Format.Equals("_-* #,##0.00_-;\\-* #,##0.00_-;_-* \"-\"??_-;_-@_-"))
                                value = ParseValue(cell.Value, proTypeFullName);
                            else
                                value = ParseValue(cell.Text, proTypeFullName);

                            if (tempValue == null ? true : string.IsNullOrEmpty(tempValue.ToString()) || proTypeFullName.Contains("Boolean"))
                                property.SetValue(obj, value);
                            else
                                property.SetValue(obj, (tempValue.ToString().Replace("NA", "") + "," + value).TrimStart(','));
                        }
                        catch (Exception e)
                        {
                            string msg = e.Message;
                            //throw;
                        }

                        //i++;
                    }
                    list.Add(obj);
                }

            }
            return list;
        }

        private object ParseValue(object value, string proTypeFullName)
        {
            if (value != null)
            {
                if (proTypeFullName.Contains("DateTime"))
                {
                    DateTime time;
                    if (DateTime.TryParse(value.ToString(), out time) == true)
                        value = time;
                    else
                        value = null;
                }
                else if (proTypeFullName.Contains("Int32"))
                {
                    value = int.Parse(value.ToString());
                }
                else if (proTypeFullName.Contains("Decimal"))
                {
                    value = Decimal.Parse(value.ToString());
                }
                else if (proTypeFullName.Contains("String"))
                {
                    value = value.ToString().Trim();
                }
                else if (proTypeFullName.Contains("Boolean"))
                {
                    value = value.ToString() == "1";
                }
            }

            return value;
        }
		
		public DataTable ReadExcelBySheetForWindows(string filePath, string sheetName, bool hasHeader)
        {
            OleDbDataAdapter da = new OleDbDataAdapter();
            DataTable dt = new DataTable();
            OleDbCommand cmd = new OleDbCommand();
            var hdr = hasHeader ? "YES" : "NO";
            OleDbConnection xlsConn = new OleDbConnection(@"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" +
                filePath + ";Mode=Read;Extended Properties=\"Excel 12.0 Xml;HDR=" + hdr + ";IMEX=1;\" ");

            try
            {
                xlsConn.Open();
                cmd.Connection = xlsConn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = string.Format("SELECT * FROM [{0}] ", sheetName);
                da.SelectCommand = cmd;
                da.Fill(dt);
                xlsConn.Close();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                xlsConn.Dispose();
            }

            return dt;
        }

        public DataTable ReadCSVForWindowsBySQL(string filePath, string sql)
        {
            OleDbDataAdapter da = new OleDbDataAdapter();
            DataTable dt = new DataTable();
            OleDbCommand cmd = new OleDbCommand();

            var connString = string.Format(@"Provider=Microsoft.Jet.OleDb.4.0; Data Source={0};Extended Properties=""Text;HDR=YES;FMT=Delimited""", Path.GetDirectoryName(filePath));
            OleDbConnection xlsConn = new OleDbConnection(connString);

            try
            {
                xlsConn.Open();
                cmd.Connection = xlsConn;
                cmd.CommandType = CommandType.Text;
                cmd.CommandText = sql;
                da.SelectCommand = cmd;
                da.Fill(dt);
                xlsConn.Close();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                xlsConn.Dispose();
            }

            return dt;
        }
    }
}