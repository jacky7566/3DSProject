using Dapper;
using NLog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenLib.Utils
{
    public class LogHelper
    {
        private static ILogger _logger;
        private static readonly IDbConnection _sqlConn;
        public LogHelper(ILogger logger)
        {
            //this._sqlConn = sqlConn;
            _logger = logger;
        }

        public void LogInformation(string message)
        {
            _logger.Info(message);
        }

        public static void WriteLine(string message)
        {
            _logger.Info(message);
        }
    }
}
