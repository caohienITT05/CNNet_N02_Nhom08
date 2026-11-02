using Microsoft.Data.SqlClient;
namespace SaigonChe.Web.Services;
public static class DatabaseErrors
{
    public static SqlException? FindSqlException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current is SqlException sql) return sql;
        return null;
    }
}
