using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace LogicPOS.ApiServer.Endpoints;

internal static class SqliteTableReader
{
    internal static async Task<List<Dictionary<string, object?>>> ReadTableAsync(
        AppDbContext db,
        string tableName,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM {QuoteIdentifier(tableName)}";

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await ReadRowsAsync(reader, cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    internal static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }
}
