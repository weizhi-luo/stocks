using Dapper;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace Argus
{
    public static class DatabaseHelper
    {
        public static List<T> DatabaseQuery<T>(string connectionString, string query, int commandTimeout)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                return connection.Query<T>(query, commandTimeout: commandTimeout).ToList();
            }
        }
    }
}
