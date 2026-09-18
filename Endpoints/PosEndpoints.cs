using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace LogicPOS.ApiServer.Endpoints;

public static class PosEndpoints
{
    public static void MapPosEndpoints(this IEndpointRouteBuilder app)
    {
        var terminals = app.MapGroup("/terminals")
            .WithTags("Terminals");

        terminals.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await SqliteTableReader.ReadTableAsync(db, "Terminals", cancellationToken)))
            .WithName("GetTerminals");

        terminals.MapGet("/hardwareid/{id}", async (string id, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "Terminals", cancellationToken);
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
                Results.Ok(await SqliteTableReader.ReadTableAsync(db, "Places", cancellationToken)))
            .WithName("GetPlaces");

        var tables = app.MapGroup("/tables")
            .WithTags("Tables");

        tables.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await SqliteTableReader.ReadTableAsync(db, "Tables", cancellationToken)))
            .WithName("GetTables");

        tables.MapGet("/default", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "Tables", cancellationToken);
                var defaultTable = rows.FirstOrDefault(IsDefaultRow) ?? rows.FirstOrDefault();

                return defaultTable is null ? Results.NotFound() : Results.Ok(defaultTable);
            })
            .WithName("GetDefaultTable");
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
}
