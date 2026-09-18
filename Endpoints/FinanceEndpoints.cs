using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace LogicPOS.ApiServer.Endpoints;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        var fiscalYears = app.MapGroup("/fiscal-years")
            .WithTags("Fiscal Years");

        fiscalYears.MapGet("/current", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "FiscalYears", cancellationToken);
                var current = rows.FirstOrDefault(IsCurrentFiscalYear) ?? rows.FirstOrDefault();

                return current is null ? Results.NotFound() : Results.Ok(current);
            })
            .WithName("GetCurrentFiscalYear");

        fiscalYears.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await SqliteTableReader.ReadTableAsync(db, "FiscalYears", cancellationToken)))
            .WithName("GetFiscalYears");

        app.MapGet("/vat-rates", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await db.VatRates
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)))
            .WithTags("Vat Rates")
            .WithName("GetVatRates");

        var documents = app.MapGroup("/documents");

        documents.MapGet("/types", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await SqliteTableReader.ReadTableAsync(db, "DocumentTypes", cancellationToken)))
            .WithTags("Document Series")
            .WithName("GetDocumentTypes");

        documents.MapGet("/series/active", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "DocumentSeries", cancellationToken);
                var activeSeries = rows.Where(IsActive).ToList();

                return Results.Ok(activeSeries);
            })
            .WithTags("Document Series")
            .WithName("GetActiveDocumentSeries");
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
}
