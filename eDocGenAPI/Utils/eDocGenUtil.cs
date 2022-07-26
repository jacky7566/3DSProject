using Dapper;
using Dapper.Contrib.Extensions;
using eDocGenAPI.Classes;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenAPI.Utils
{
    public class eDocGenUtil
    {
        private IDbConnection _sqlConn;
        private ILogger _logger;
        public eDocGenUtil(IDbConnection sqlConn, ILogger logger)
        {
            _sqlConn = sqlConn;
            _logger = logger;
        }
        public bool SetEDocSpecInfo(ref EDocSpecClass inputeDocSpec, bool isEmapVerUpdate)
        {
            var now = DateTime.Now;
            try
            {
                if ((inputeDocSpec.Id == Guid.Empty) == false)
                {
                    var dbEDocSpec = _sqlConn.QueryFirst<EDocSpecClass>(string.Format("SELECT TOP 1 * FROM TBL_eDoc_Spec Where Id = '{0}' "
                        , inputeDocSpec.Id));

                    var pis = dbEDocSpec.GetType().GetProperties();
                    foreach (var pi in pis)
                    {
                        if (isEmapVerUpdate == false && pi.Name == "EMapVersion") continue;
                        if (isEmapVerUpdate && pi.Name == "UMCFile") continue;

                        var oldValue = pi.GetValue(dbEDocSpec);
                        var newValue = pi.GetValue(inputeDocSpec);
                        if (oldValue != newValue)
                        {
                            if (pi.PropertyType == typeof(DateTime))
                                continue;
                            pi.SetValue(dbEDocSpec, newValue, null);
                        }
                        if (pi.PropertyType == typeof(DateTime))
                            continue;
                        pi.SetValue(dbEDocSpec, pi.GetValue(inputeDocSpec), null);
                    }
                    dbEDocSpec.LastUpdatedDate = now;
                    dbEDocSpec.LastUpdatedBy = inputeDocSpec.CreatedBy;
                    return _sqlConn.Update(dbEDocSpec);
                }
                else
                {
                    inputeDocSpec.Id = Guid.NewGuid();
                    inputeDocSpec.CreatedDate = now;
                    inputeDocSpec.LastUpdatedDate = now;
                    return _sqlConn.Insert(inputeDocSpec) == 0 ? true : false;
                }
            }
            catch (Exception)
            {
                throw;
            }            
        }
    }
}
