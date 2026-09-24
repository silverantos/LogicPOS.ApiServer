using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Data.Common;

namespace LogicPOS.ApiServer.Endpoints;

public static class CompanyEndpoints
{
    private static readonly ConcurrentDictionary<string, string?> InvertedMemoryFallback = new(StringComparer.OrdinalIgnoreCase);

    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        var company = app.MapGroup("/company").WithTags("Company");

        company.MapGet("/info", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var parameters = await LoadCompanyParametersAsync(db, cancellationToken);

            string? GetVal(string token, string? fallback = null) =>
                parameters.TryGetValue(token, out var val) && !string.IsNullOrWhiteSpace(val)
                    ? val
                    : fallback;

            var countryCode = GetVal("COMPANY_COUNTRY_CODE2", "pt")!;

            return Results.Ok(new
            {
                name = GetVal("COMPANY_NAME", string.Empty)!,
                businessName = GetVal("COMPANY_BUSINESS_NAME", string.Empty)!,
                commercialName = GetVal("COMPANY_NAME", string.Empty)!,
                logoPng = (string?)null,
                logoBmp = (string?)null,
                address = GetVal("COMPANY_ADDRESS", string.Empty)!,
                city = GetVal("COMPANY_CITY", string.Empty)!,
                postalCode = GetVal("COMPANY_POSTALCODE", string.Empty)!,
                countryCode2 = countryCode,
                phone = GetVal("COMPANY_TELEPHONE"),
                mobilePhone = GetVal("COMPANY_MOBILEPHONE"),
                email = GetVal("COMPANY_EMAIL"),
                website = GetVal("COMPANY_WEBSITE"),
                fiscalNumber = GetVal("COMPANY_FISCALNUMBER"),
                stockCapital = GetVal("COMPANY_STOCK_CAPITAL"),
                documentFinalLine1 = (string?)null,
                documentFinalLine2 = (string?)null,
                taxEntity = GetVal("COMPANY_TAX_ENTITY", "Global"),
                fax = GetVal("COMPANY_FAX"),
                ticketFinalLine1 = GetVal("TICKET_FOOTER_LINE1", "Obrigado pela sua visita"),
                ticketFinalLine2 = GetVal("TICKET_FOOTER_LINE2", "Volte sempre"),
                currencyCode = GetVal("SYSTEM_CURRENCY", "EUR")!,
                agtLogo = (string?)null,
                isPortugal = string.Equals(countryCode, "pt", StringComparison.OrdinalIgnoreCase)
            });
        })
        .WithName("GetCompanyInformation");

        company.MapPut("/details", async (UpdateCompanyDetailsRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var updates = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["COMPANY_NAME"] = request.CompanyName,
                ["COMPANY_BUSINESS_NAME"] = request.BusinessName,
                ["COMPANY_FISCALNUMBER"] = request.FiscalNumber,
                ["COMPANY_COUNTRY_CODE2"] = request.CountryCode2,
                ["COMPANY_COUNTRY"] = request.Country,
                ["COMPANY_TAX_ENTITY"] = request.TaxEntity,
                ["COMPANY_CITY"] = request.City,
                ["COMPANY_ADDRESS"] = request.Address,
                ["COMPANY_STOCK_CAPITAL"] = request.StockCapital,
                ["COMPANY_POSTALCODE"] = request.PostalCode,
                ["COMPANY_EMAIL"] = request.Email,
                ["COMPANY_TELEPHONE"] = request.Phone,
                ["COMPANY_MOBILEPHONE"] = request.MobilePhone,
                ["COMPANY_WEBSITE"] = request.Website,
                ["COMPANY_FAX"] = request.Fax
            };

            await SaveCompanyParametersAsync(db, updates, cancellationToken);

            return Results.NoContent();
        })
        .WithName("UpdateCompanyDetails");

        company.MapGet("/currency", () => Results.Ok(new
        {
            code = "EUR",
            designation = "Euro",
            isoCode = "EUR",
            symbol = "€"
        }))
        .WithName("GetCompanyCurrency");
    }

    private static async Task<Dictionary<string, string?>> LoadCompanyParametersAsync(
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string?>(InvertedMemoryFallback, StringComparer.OrdinalIgnoreCase);

        try
        {
            var rows = await SqliteTableReader.ReadTableAsync(db, "PreferenceParameters", cancellationToken);
            foreach (var row in rows)
            {
                if (row.TryGetValue("Token", out var tok) && tok is string tokenStr &&
                    row.TryGetValue("Value", out var val))
                {
                    result[tokenStr] = val?.ToString();
                }
            }
        }
        catch
        {
            // Fallback to in-memory parameters if database is not available
        }

        return result;
    }

    private static async Task SaveCompanyParametersAsync(
        AppDbContext db,
        Dictionary<string, string?> updates,
        CancellationToken cancellationToken)
    {
        foreach (var (k, v) in updates)
        {
            InvertedMemoryFallback[k] = v;
        }

        try
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State == System.Data.ConnectionState.Closed;

            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                foreach (var (token, value) in updates)
                {
                    await using var cmd = connection.CreateCommand();
                    cmd.CommandText = "UPDATE PreferenceParameters SET Value = @val WHERE Token = @tok";

                    var pVal = cmd.CreateParameter();
                    pVal.ParameterName = "@val";
                    pVal.Value = (object?)value ?? DBNull.Value;
                    cmd.Parameters.Add(pVal);

                    var pTok = cmd.CreateParameter();
                    pTok.ParameterName = "@tok";
                    pTok.Value = token;
                    cmd.Parameters.Add(pTok);

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch
        {
            // Changes remain saved in-memory if the database table cannot be updated
        }
    }
}

public sealed record UpdateCompanyDetailsRequest(
    string? CompanyName,
    string? BusinessName,
    string? FiscalNumber,
    string? CountryCode2,
    string? TaxEntity,
    string? City,
    string? Address,
    string? StockCapital,
    string? PostalCode,
    string? Email,
    string? Phone,
    string? MobilePhone,
    string? Website,
    string? Fax,
    string? Country
);
