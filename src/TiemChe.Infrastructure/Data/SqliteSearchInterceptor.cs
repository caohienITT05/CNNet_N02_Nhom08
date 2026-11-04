using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TiemChe.Application.Services;

namespace TiemChe.Infrastructure.Data;

public sealed class SqliteSearchInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
        Register(connection);

    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Register(connection);
        return Task.CompletedTask;
    }

    private static void Register(DbConnection connection) =>
        ((SqliteConnection)connection).CreateFunction<string, string, bool>(
            VietnameseSearch.SqlName, VietnameseSearch.Contains, isDeterministic: true);
}
