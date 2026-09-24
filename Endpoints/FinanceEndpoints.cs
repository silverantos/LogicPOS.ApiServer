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

                return current is null
                    ? Results.Problem(
                        detail: "No current fiscal year was found.",
                        statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(current);
            })
            .WithName("GetCurrentFiscalYear");

        fiscalYears.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
                Results.Ok(await SqliteTableReader.ReadTableAsync(db, "FiscalYears", cancellationToken)))
            .WithName("GetFiscalYears");

        fiscalYears.MapGet("/creation-data", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await TryReadTableAsync(db, "FiscalYears", cancellationToken);
            var currentYear = DateTime.UtcNow.Year;
            var currentYearCount = rows.Count(r => Convert.ToInt32(r.GetValueOrDefault("Year") ?? 0) == currentYear);
            return Results.Ok(new
            {
                currentYear,
                currentYearCount
            });
        })
        .WithName("GetFiscalYearCreationData");

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

        documents.MapGet("/types/active", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "DocumentTypes", cancellationToken);
                return Results.Ok(rows);
            })
            .WithTags("Document Series")
            .WithName("GetActiveDocumentTypes");

        documents.MapGet("/series/active", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var seriesRows = await SqliteTableReader.ReadTableAsync(db, "DocumentSeries", cancellationToken);
                var typeRows = await TryReadTableAsync(db, "DocumentTypes", cancellationToken);
                var fiscalYearRows = await TryReadTableAsync(db, "FiscalYears", cancellationToken);
                var terminalRows = await TryReadTableAsync(db, "Terminals", cancellationToken);

                var activeSeries = seriesRows
                    .Where(row => !IsTruthy(row, "IsDeleted"))
                    .Select(row => CreateDocumentSeriesView(row, typeRows, fiscalYearRows, terminalRows))
                    .ToList();

                return Results.Ok(activeSeries);
            })
            .WithTags("Document Series")
            .WithName("GetActiveDocumentSeries");

        documents.MapGet("/series/has-active", async (string? documentType, Guid? terminalId, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var seriesRows = await TryReadTableAsync(db, "DocumentSeries", cancellationToken);
                var typeRows = await TryReadTableAsync(db, "DocumentTypes", cancellationToken);
                var fiscalRows = await TryReadTableAsync(db, "FiscalYears", cancellationToken);

                bool hasActive = false;
                bool seriesForEachTerminal = false;

                if (!string.IsNullOrWhiteSpace(documentType))
                {
                    var matchingType = typeRows.FirstOrDefault(t =>
                        ValueEquals(t, "Acronym", documentType) ||
                        ValueEquals(t, "Code", documentType));

                    if (matchingType != null && matchingType.TryGetValue("Id", out var typeId))
                    {
                        var matchingSeries = seriesRows.Where(s =>
                            s.TryGetValue("DocumentTypeId", out var dtId) &&
                            string.Equals(Convert.ToString(dtId), Convert.ToString(typeId), StringComparison.OrdinalIgnoreCase)).ToList();

                        hasActive = matchingSeries.Count > 0;
                    }
                }
                else
                {
                    hasActive = seriesRows.Count > 0;
                }

                seriesForEachTerminal = fiscalRows.Any(f => IsTruthy(f, "SeriesForEachTerminal"));

                return Results.Ok(new
                {
                    hasActiveSeries = hasActive,
                    seriesForEachTerminal
                });
            })
            .WithTags("Document Series")
            .WithName("HasActiveDocumentSeries");

        var countries = app.MapGroup("/countries").WithTags("Countries");

        countries.MapGet("", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await TryReadTableAsync(db, "Countries", cancellationToken);
                return Results.Ok(rows.Count == 0 ? CreateDefaultCountries() : rows);
            })
            .WithName("GetCountries");

        countries.MapGet("/country", async (Guid? id, string? code2, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await TryReadTableAsync(db, "Countries", cancellationToken);
                var allRows = rows.Count == 0 ? CreateDefaultCountries() : rows;

                Dictionary<string, object?>? country = null;

                if (id is Guid countryId)
                {
                    country = allRows.FirstOrDefault(row => row.TryGetValue("Id", out var idValue) &&
                        Guid.TryParse(Convert.ToString(idValue), out var rowId) && rowId == countryId);
                }

                if (country is null && !string.IsNullOrWhiteSpace(code2))
                {
                    country = allRows.FirstOrDefault(row => ValueEquals(row, "Code2", code2) ||
                        ValueEquals(row, "CountryCode2", code2) ||
                        ValueEquals(row, "Code", code2));
                }

                return country is null
                    ? Results.NotFound()
                    : Results.Ok(country);
            })
            .WithName("GetCountryByCodeOrId");

        countries.MapGet("/search/{designation}", async (string designation, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await TryReadTableAsync(db, "Countries", cancellationToken);
                var allRows = rows.Count == 0 ? CreateDefaultCountries() : rows;
                var matches = allRows.Where(row =>
                    row.TryGetValue("Designation", out var designationValue) &&
                    Convert.ToString(designationValue) is { Length: > 0 } displayName &&
                    displayName.Contains(designation, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                return Results.Ok(matches);
            })
            .WithName("SearchCountries");
    }

    private static bool IsCurrentFiscalYear(Dictionary<string, object?> row)
    {
        return IsTruthy(row, "IsCurrent") ||
               IsTruthy(row, "Current") ||
               IsTruthy(row, "IsCurrentFiscalYear");
    }

    private static Dictionary<string, object?> CreateDocumentSeriesView(
        Dictionary<string, object?> series,
        List<Dictionary<string, object?>> documentTypes,
        List<Dictionary<string, object?>> fiscalYears,
        List<Dictionary<string, object?>> terminals)
    {
        var view = new Dictionary<string, object?>(series, StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentType"] = FindRelated(documentTypes, series.GetValueOrDefault("DocumentTypeId"), "DocumentType"),
            ["FiscalYear"] = FindRelated(fiscalYears, series.GetValueOrDefault("FiscalYearId"), "FiscalYear")
        };

        var terminalId = series.GetValueOrDefault("TerminalId");
        view["Terminal"] = terminalId is null || string.IsNullOrWhiteSpace(Convert.ToString(terminalId))
            ? null
            : FindRelated(terminals, terminalId, "Terminal");
        return view;
    }

    private static Dictionary<string, object?> FindRelated(
        IEnumerable<Dictionary<string, object?>> rows,
        object? id,
        string entityName)
    {
        var related = rows.FirstOrDefault(row => string.Equals(
            Convert.ToString(row.GetValueOrDefault("Id")),
            Convert.ToString(id),
            StringComparison.OrdinalIgnoreCase));

        if (related is not null) return related;

        return new Dictionary<string, object?>
        {
            ["Id"] = id,
            ["Code"] = string.Empty,
            ["Designation"] = "-"
        };
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

    private static bool ValueEquals(Dictionary<string, object?> row, string key, string value) =>
        row.TryGetValue(key, out var rowValue) &&
        string.Equals(Convert.ToString(rowValue), value, StringComparison.OrdinalIgnoreCase);

    private static async Task<List<Dictionary<string, object?>>> TryReadTableAsync(
        AppDbContext db,
        string tableName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await SqliteTableReader.ReadTableAsync(db, tableName, cancellationToken);
        }
        catch (Exception)
        {
            return new List<Dictionary<string, object?>>();
        }
    }

    private static List<Dictionary<string, object?>> CreateDefaultCountries()
    {
        return new List<Dictionary<string, object?>>
        {
            CreateCountryRow("Portugal", "PT", "PRT", "Lisboa", "EUR"),
            CreateCountryRow("Spain", "ES", "ESP", "Madrid", "EUR"),
            CreateCountryRow("France", "FR", "FRA", "Paris", "EUR"),
            CreateCountryRow("Germany", "DE", "DEU", "Berlin", "EUR"),
            CreateCountryRow("United Kingdom", "GB", "GBR", "London", "GBP"),
            CreateCountryRow("Brazil", "BR", "BRA", "Brasília", "BRL")
        };
    }

    private static Dictionary<string, object?> CreateCountryRow(string designation, string code2, string code3, string capital, string currencyCode)
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = Guid.NewGuid(),
            ["Code"] = code2,
            ["Code2"] = code2,
            ["Code3"] = code3,
            ["Designation"] = designation,
            ["Capital"] = capital,
            ["CurrencyCode"] = currencyCode,
            ["Order"] = 0,
            ["IsDeleted"] = false,
            ["CreatedAt"] = DateTime.UtcNow,
            ["UpdatedAt"] = DateTime.UtcNow
        };
    }
}
