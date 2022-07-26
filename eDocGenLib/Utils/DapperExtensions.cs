using Dapper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace eDocGenLib.Utils
{
    public static class DapperExtensions
    {
        public static async Task<int> BulkInsert<T>(
        this IDbConnection connection,
        string tableName,
        IReadOnlyCollection<T> items,
        Dictionary<string, Func<T, object>> dataFunc)
        {
            const int MaxBatchSize = 1000;
            const int MaxParameterSize = 2000;

            try
            {
                var batchSize = Math.Min((int)Math.Ceiling((double)MaxParameterSize / dataFunc.Keys.Count), MaxBatchSize);
                var numberOfBatches = (int)Math.Ceiling((double)items.Count / batchSize);
                var columnNames = dataFunc.Keys;
                var insertSql = $"INSERT INTO {tableName} ({string.Join(", ", columnNames.Select(e => $"[{e}]"))}) VALUES ";
                var sqlToExecute = new List<Tuple<string, DynamicParameters>>();

                for (var i = 0; i < numberOfBatches; i++)
                {
                    var dataToInsert = items.Skip(i * batchSize)
                        .Take(batchSize);
                    var valueSql = GetQueries(dataToInsert, dataFunc);

                    sqlToExecute.Add(Tuple.Create($"{insertSql}{string.Join(", ", valueSql.Item1)}", valueSql.Item2));
                }

                foreach (var sql in sqlToExecute)
                {
                    await connection.ExecuteAsync(sql.Item1, sql.Item2, commandTimeout: int.MaxValue);
                }
                return sqlToExecute.Count;
            }
            catch (Exception)
            {
                throw;
            }
            
        }

        private static Tuple<IEnumerable<string>, DynamicParameters> GetQueries<T>(
            IEnumerable<T> dataToInsert,
            Dictionary<string, Func<T, object>> dataFunc)
        {
            var parameters = new DynamicParameters();

            return Tuple.Create(
                dataToInsert.Select(e => $"({string.Join(", ", GenerateQueryAndParameters(e, parameters, dataFunc))})"),
                parameters);
        }

        private static IEnumerable<string> GenerateQueryAndParameters<T>(
            T entity,
            DynamicParameters parameters,
            Dictionary<string, Func<T, object>> dataFunc)
        {
            var paramTemplateFunc = new Func<Guid, string>(guid => $"@p{guid.ToString().Replace("-", "")}");
            var paramList = new List<string>();

            foreach (var key in dataFunc)
            {
                var paramName = paramTemplateFunc(Guid.NewGuid());
                parameters.Add(paramName, key.Value(entity));
                paramList.Add(paramName);
            }

            return paramList;
        }
    }
}
