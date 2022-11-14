using Dapper;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
//using System.Data.SqlClient;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eDocGenLib.Classes.eDocGenEngine;

namespace eDocGenLib.Utils
{
    public class ConnectionHelper
    {
        public static IConfiguration _config;
        private static string connStr;
        public ConnectionHelper(IConfiguration config)
        {
            _config = config;
            connStr = config["ConnectionStrings:DefaultConnection"];
        }
        public ConnectionHelper(IConfiguration config, string sDBAliasName)
        {
            _config = config;
            connStr = _config[string.Format("ConnectionStrings:{0}", sDBAliasName)];
        }
        public DbConnection GetDBConn(string sDBAliasName)
        {
            DbProviderFactory dbpf = null;
            DbConnection dbcn = null;

            try
            {
                string providerName = _config[string.Format("ConnectionStrings:{0}", sDBAliasName)];
                dbpf = DbProviderFactories.GetFactory(providerName);
                dbcn = dbpf.CreateConnection();

                string connString = _config[string.Format("ConnectionStrings:{0}", sDBAliasName)].ToString();
                //LogHelper.WriteLine("connect:" + connString);
                dbcn.ConnectionString = connString;
                if (dbcn.State == ConnectionState.Closed)
                {
                    try
                    {
                        dbcn.Open();
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
            return dbcn;
        }
        public List<dynamic> QueryDataBySQL(string sql)
        {            
            List<dynamic> list = new List<dynamic>();
            try
            {
                var commandTimeout = int.Parse(_config["Configurations:SQL_CommandTimeout"].ToString());
                using (var sqlConn = new SqlConnection(connStr))
                {                    
                    list = sqlConn.Query<dynamic>(sql, null, null, true, commandTimeout, null).ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
            return list;
        }
        public DataTable GetDataTable(DbConnection dbcn, DbTransaction tran, string sql)
        {
            DataTable dt = new DataTable();
            try
            {
                using (DbCommand dbcm = dbcn.CreateCommand())
                {
                    dbcm.CommandText = sql;
                    dbcm.CommandType = CommandType.Text;
                    dbcm.CommandTimeout = 5000;
                    if (tran != null)
                        dbcm.Transaction = tran;
                    //using SqlCeCommand
                    using (DbDataReader Rdr = dbcm.ExecuteReader())
                    {
                        //Create datatable to hold schema and data seperately
                        //Get schema of our actual table
                        DataTable DTSchema = Rdr.GetSchemaTable();
                        //DataTable DT = new DataTable();
                        if (DTSchema != null)
                        {
                            if (DTSchema.Rows.Count > 0)
                            {
                                for (int i = 0; i < DTSchema.Rows.Count; i++)
                                {
                                    //Create new column for each row in schema table
                                    //Set properties that are causing errors and add it to our datatable
                                    //Rows in schema table are filled with information of columns in our actual table
                                    DataColumn Col = new DataColumn(DTSchema.Rows[i]["ColumnName"].ToString(), (Type)DTSchema.Rows[i]["DataType"]);
                                    Col.AllowDBNull = true;
                                    Col.Unique = false;
                                    Col.AutoIncrement = false;
                                    dt.Columns.Add(Col);
                                }
                            }
                        }

                        while (Rdr.Read())
                        {
                            //Read data and fill it to our datatable
                            DataRow Row = dt.NewRow();
                            for (int i = 0; i < dt.Columns.Count; i++)
                            {
                                Row[i] = Rdr[i];
                            }
                            dt.Rows.Add(Row);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
            //This is our datatable filled with data
            return dt;
        }

        public static List<eDocConfigClass> GetEDocConfigList(IConfiguration _config)
        {
            var list = new List<eDocConfigClass>();
            var sql = string.Format(@"select * from [dbo].[TBL_eDoc_Config] where ServerName = '{0}' ", Environment.MachineName);
            try
            {
                using (var sqlConn = new SqlConnection(_config["ConnectionStrings:DefaultConnection"]))
                {
                    list = sqlConn.Query<eDocConfigClass>(sql).ToList();
                }              
            }
            catch (Exception)
            {
                throw;
            }
            return list;
        }
    }
}
