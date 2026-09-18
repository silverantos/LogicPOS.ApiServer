using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace LogicPOS.ApiServer.Endpoints;

public static class PosEndpoints
{
    public static void MapPosEndpoints(this IEndpointRouteBuilder app)
    {
        var terminals = app.MapGroup("/terminals")
            .WithTags("Terminals");

        terminals.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await ReadTableAsync(db, "Terminals", cancellationToken)))
            .WithName("GetTerminals");

        terminals.MapGet("/hardwareid/{id}", async (string id, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await ReadTableAsync(db, "Terminals", cancellationToken);
                var terminal = rows.FirstOrDefault(row =>
                    ValueEquals(row, "HardwareId", id) ||
                    ValueEquals(row, "HardwareID", id) ||
                    ValueEquals(row, "Hardware", id) ||
                    ValueEquals(row, "Id", id));

                return terminal is null ? Results.NotFound() : Results.Ok(terminal);
            })
            .WithName("GetTerminalByHardwareId");

        var places = app.MapGroup("/places")
            .WithTags("Places");

        places.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await ReadTableAsync(db, "Places", cancellationToken)))
            .WithName("GetPlaces");

        var tables = app.MapGroup("/tables")
            .WithTags("Tables");

        app.MapGet("/Tables", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await ReadTableAsync(db, "Tables", cancellationToken)))
            .WithTags("Tables")
            .WithName("GetTablesLegacy");

        tables.MapGet("/default", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await ReadTableAsync(db, "Tables", cancellationToken);
                var defaultTable = rows.FirstOrDefault(IsDefaultRow) ?? rows.FirstOrDefault();

                return defaultTable is null ? Results.NotFound() : Results.Ok(defaultTable);
            })
            .WithName("GetDefaultTable");
    }

    private static async Task<List<Dictionary<string, object?>>> ReadTableAsync(
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

    private static bool ValueEquals(Dictionary<string, object?> row, string key, string value)
    {
        return row.TryGetValue(key, out var rowValue) &&
               string.Equals(Convert.ToString(rowValue), value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDefaultRow(Dictionary<string, object?> row)
    {
        return IsTruthy(row, "IsDefault") ||
               IsTruthy(row, "Default") ||
               IsTruthy(row, "IsDefaultTable");
    }

    private static bool IsTruthy(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            bool boolValue => boolValue,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            _ => bool.TryParse(Convert.ToString(value), out var boolValue) && boolValue
        };
    }

      private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
