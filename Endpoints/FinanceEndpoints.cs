using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace LogicPOS.ApiServer.Endpoints;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        var fiscalYears = app.MapGroup("/fiscal-years")
            .WithTags("Fiscal Years");

        fiscalYears.MapGet("/current", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await ReadTableAsync(db, "FiscalYears", cancellationToken);
                var current = rows.FirstOrDefault(IsCurrentFiscalYear) ?? rows.FirstOrDefault();

                return current is null ? Results.NotFound() : Results.Ok(current);
            })
            .WithName("GetCurrentFiscalYear");

        fiscalYears.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await ReadTableAsync(db, "FiscalYears", cancellationToken)))
            .WithName("GetFiscalYears");

        app.MapGet("/vat-rates", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await db.VatRates
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)))
            .WithTags("Vat Rates")
            .WithName("GetVatRates");

        var documents = app.MapGroup("/documents");

        documents.MapGet("/types", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await ReadTableAsync(db, "DocumentTypes", cancellationToken)))
            .WithTags("Document Series")
            .WithName("GetDocumentTypes");

        documents.MapGet("/series/active", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await ReadTableAsync(db, "DocumentSeries", cancellationToken);
                var activeSeries = rows.Where(IsActive).ToList();

                return Results.Ok(activeSeries);
            })
            .WithTags("Document Series")
            .WithName("GetActiveDocumentSeries");
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

    private static bool IsCurrentFiscalYear(Dictionary<string, object?> row)
    {
        return IsTruthy(row, "IsCurrent") ||
               IsTruthy(row, "Current") ||
               IsTruthy(row, "IsCurrentFiscalYear");
    }

    private static bool IsActive(Dictionary<string, object?> row)
    {
        return IsTruthy(row, "IsActive") ||
               IsTruthy(row, "Active") ||
               IsTruthy(row, "Enabled");
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
            byte byteValue => byteValue != 0,
            short shortValue => shortValue != 0,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            string stringValue when int.TryParse(stringValue, out var number) => number != 0,
            _ => bool.TryParse(Convert.ToString(value), out var boolValue) && boolValue
        };
    }

    private static string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier.Replace("\"", "\"\"")}\"";
    }
}
