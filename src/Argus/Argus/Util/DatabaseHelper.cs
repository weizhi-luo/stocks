using Dapper;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Argus
{
    public static class DatabaseHelper
    {
        public static List<T> Query<T>(string connectionString, string query, int commandTimeout)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                return connection.Query<T>(query, commandTimeout: commandTimeout).ToList();
            }
        }

        public static int ExecuteStoredProcedure(string connectionString, string storedProcedure, object parameters, int commandTimeout)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                return connection.Execute(storedProcedure, param: parameters,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: commandTimeout);
            }
        }
    }
}
