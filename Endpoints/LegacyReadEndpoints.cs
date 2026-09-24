using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LogicPOS.ApiServer.Endpoints;

public static class LegacyReadEndpoints
{
    public static void MapLegacyReadEndpoints(this IEndpointRouteBuilder app)
    {
        MapCollection(app, "/users/profiles", "UserProfiles");
        MapCollection(app, "/users/permission-profiles", "PermissionProfiles");
        MapCollection(app, "/users/permission-items", "PermissionItems");
        MapCollection(app, "/users/permission-groups", "PermissionGroups");
        MapCollection(app, "/users/commission-groups", "CommissionGroups");
        MapCollection(app, "/movementtypes", "MovementTypes");
        MapCollection(app, "/holidays", "Holidays");
        MapCollection(app, "/weighingmachines", "WeighingMachines");
        MapCollection(app, "/printers/types", "PrinterTypes");
        MapCollection(app, "/printers", "Printers");
        MapCollection(app, "/poledisplays", "PoleDisplays");
        MapCollection(app, "/inputreaders", "InputReaders");
        MapCollection(app, "/vatrates", "VatRates");
        MapCollection(app, "/vat-exemption-reasons", "VatExemptionReasons");
        MapCollection(app, "/payment/methods", "PaymentMethods");
        MapCollection(app, "/payment/conditions", "PaymentConditions");
        MapCollection(app, "/currencies", "Currencies");
        MapCollection(app, "/articles/sizeunits", "SizeUnits");
        MapCollection(app, "/articles/measurementunits", "MeasurementUnits");
        MapCollection(app, "/articles/types", "ArticleTypes");
        MapCollection(app, "/articles/subfamilies", "ArticleSubfamilies");
        MapCollection(app, "/articles/pricetypes", "PriceTypes");
        MapCollection(app, "/articles/families", "ArticleFamilies");
        MapCollection(app, "/articles/classes", "ArticleClasses");
        MapCollection(app, "/articles", "Articles");
        MapCollection(app, "/customers/types", "CustomerTypes");
        MapCollection(app, "/customers", "Customers");
        MapCollection(app, "/documents", "Documents");
        MapCollection(app, "/receipts", "Receipts");
        MapCollection(app, "/payments", "Payments");
    }

    private static void MapCollection(IEndpointRouteBuilder app, string route, string tableName)
    {
        app.MapGet(route, async (AppDbContext db, CancellationToken cancellationToken) =>
            Results.Ok(await TryReadTableAsync(db, tableName, cancellationToken)));

        app.MapGet($"{route}/{{id:guid}}", async (Guid id, AppDbContext db,
            CancellationToken cancellationToken) =>
        {
            var rows = await TryReadTableAsync(db, tableName, cancellationToken);
            var row = rows.FirstOrDefault(candidate =>
                candidate.TryGetValue("Id", out var value) &&
                Guid.TryParse(Convert.ToString(value), out var candidateId) && candidateId == id);

            return row is null ? Results.NotFound() : Results.Ok(row);
        });
    }

    private static async Task<List<Dictionary<string, object?>>> TryReadTableAsync(
        AppDbContext db, string tableName, CancellationToken cancellationToken)
    {
        try
        {
            return await SqliteTableReader.ReadTableAsync(db, tableName, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return new List<Dictionary<string, object?>>();
        }
    }
}