using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace eDocGenEngine.Utils
{
    internal class ExcelUtil
    {
        public static DataTable ReadExcelBySheetForWindows(string filePath, string sheetName, bool hasHeader, string emptyCheckCol)
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
                cmd.CommandText = string.Format("SELECT * FROM [{0}] WHERE [{1}] IS NOT NULL", sheetName, emptyCheckCol);
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

        public static DataTable ReadCSVForWindowsBySQL(string filePath, string sql)
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
