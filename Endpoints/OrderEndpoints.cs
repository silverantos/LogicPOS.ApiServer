using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace LogicPOS.ApiServer.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/orders").WithTags("Orders");

        orders.MapGet("", async (Guid? tableId, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await ReadAsync(db, "Orders", cancellationToken);
            var result = rows.Where(row => !Truthy(row, "IsDeleted") &&
                (!tableId.HasValue || GuidEquals(row.GetValueOrDefault("TableId"), tableId.Value))).ToList();
            return Results.Ok(result);
        }).WithName("GetOpenOrders");

        orders.MapGet("/{id:guid}", async (Guid id, Guid? tableId, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var order = (await ReadAsync(db, "Orders", cancellationToken)).FirstOrDefault(row =>
                GuidEquals(row.GetValueOrDefault("Id"), id) && !Truthy(row, "IsDeleted") &&
                (!tableId.HasValue || GuidEquals(row.GetValueOrDefault("TableId"), tableId.Value)));
            if (order is null) return Results.NotFound();
            var tickets = (await ReadAsync(db, "Tickets", cancellationToken)).Where(row => GuidEquals(row.GetValueOrDefault("OrderId"), id) && !Truthy(row, "IsDeleted")).ToList();
            var ticketIds = tickets.Select(row => Convert.ToString(row.GetValueOrDefault("Id"))).Where(value => value is not null).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var details = (await ReadAsync(db, "OrderDetails", cancellationToken)).Where(row => ticketIds.Contains(Convert.ToString(row.GetValueOrDefault("TicketId")) ?? string.Empty) && !Truthy(row, "IsDeleted")).ToList();
            return Results.Ok(new { order, tickets, details });
        }).WithName("GetOrderById");

        orders.MapPost("", async (CreateOrderRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (request.TableId == Guid.Empty) return Results.Problem(title: "TableId is required.", statusCode: StatusCodes.Status400BadRequest);
            var orderId = Guid.NewGuid();
            var now = DateTime.UtcNow.ToString("O");
            await ExecuteAsync(db, "INSERT INTO \"Orders\" (\"Id\",\"TableId\",\"OrderStatus\",\"Notes\",\"CreatedAt\",\"CreatedBy\",\"CreatedWhere\",\"UpdatedAt\",\"UpdatedBy\",\"UpdatedWhere\",\"IsDeleted\",\"DeletedAt\") VALUES (@id,@tableId,1,NULL,@now,'api','api',@now,'api','api',0,'')", new Dictionary<string, object?> { ["@id"] = orderId.ToString(), ["@tableId"] = request.TableId.ToString(), ["@now"] = now }, cancellationToken);
            foreach (var ticket in request.Tickets ?? []) await AddTicketAsync(orderId, ticket.Details ?? [], db, cancellationToken);
            return Results.Created($"/orders/{orderId}", new { id = orderId });
        }).WithName("CreateOrder");

        orders.MapPost("/{orderId:guid}/tickets", async (Guid orderId, AddTicketRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (!(await ReadAsync(db, "Orders", cancellationToken)).Any(row => GuidEquals(row.GetValueOrDefault("Id"), orderId) && !Truthy(row, "IsDeleted"))) return Results.NotFound();
            if (request.Details is null || request.Details.Count == 0) return Results.Problem(title: "At least one detail is required.", statusCode: StatusCodes.Status400BadRequest);
            var ticketId = await AddTicketAsync(orderId, request.Details, db, cancellationToken);
            return Results.Created($"/orders/{orderId}/tickets/{ticketId}", new { id = ticketId });
        }).WithName("AddTicket");
    }

    private static async Task<Guid> AddTicketAsync(Guid orderId, List<CreateOrderDetailRequest> details, AppDbContext db, CancellationToken cancellationToken)
    {
        var ticketId = Guid.NewGuid();
        var now = DateTime.UtcNow.ToString("O");
        var numericTicket = (await ReadAsync(db, "Tickets", cancellationToken)).Select(row => Convert.ToInt32(row.GetValueOrDefault("TicketId") ?? 0)).DefaultIfEmpty(0).Max() + 1;
        await ExecuteAsync(db, "INSERT INTO \"Tickets\" (\"Id\",\"CreatedAt\",\"CreatedBy\",\"CreatedWhere\",\"DeletedAt\",\"Discount\",\"IsDeleted\",\"Notes\",\"OrderId\",\"PriceType\",\"TicketId\",\"UpdatedAt\",\"UpdatedBy\",\"UpdatedWhere\") VALUES (@id,@now,'api','api','',0,0,NULL,@orderId,0,@ticketNumber,@now,'api','api')", new Dictionary<string, object?> { ["@id"] = ticketId.ToString(), ["@now"] = now, ["@orderId"] = orderId.ToString(), ["@ticketNumber"] = numericTicket }, cancellationToken);
        var order = 0;
        foreach (var detail in details)
        {
            var lineId = Guid.NewGuid(); var total = detail.Quantity * detail.UnitPrice;
            await ExecuteAsync(db, "INSERT INTO \"OrderDetails\" (\"Id\",\"ArticleId\",\"Code\",\"CreatedAt\",\"CreatedBy\",\"CreatedWhere\",\"DeletedAt\",\"Designation\",\"Discount\",\"IsDeleted\",\"Notes\",\"Order\",\"Price\",\"Quantity\",\"TicketId\",\"TotalDiscount\",\"TotalFinal\",\"TotalTax\",\"Unit\",\"UpdatedAt\",\"UpdatedBy\",\"UpdatedWhere\",\"Vat\",\"VatExemptionReasonId\") VALUES (@id,@articleId,'',@now,'api','api','',@designation,0,0,NULL,@order,@price,@quantity,@ticketId,0,@total,0,'Un',@now,'api','api',0,NULL)", new Dictionary<string, object?> { ["@id"] = lineId.ToString(), ["@articleId"] = detail.ArticleId.ToString(), ["@now"] = now, ["@designation"] = detail.ArticleId.ToString(), ["@order"] = order++, ["@price"] = detail.UnitPrice, ["@quantity"] = detail.Quantity, ["@ticketId"] = ticketId.ToString(), ["@total"] = total }, cancellationToken);
        }
        return ticketId;
    }

    private static bool GuidEquals(object? value, Guid target) => Guid.TryParse(Convert.ToString(value), out var parsed) && parsed == target;
    private static bool Truthy(Dictionary<string, object?> row, string key) => int.TryParse(Convert.ToString(row.GetValueOrDefault(key)), out var value) && value != 0;
    private static async Task<List<Dictionary<string, object?>>> ReadAsync(AppDbContext db, string table, CancellationToken cancellationToken) => await SqliteTableReader.ReadTableAsync(db, table, cancellationToken);

    private static async Task<int> ExecuteAsync(AppDbContext db, string sql, Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection(); var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try { await using var command = connection.CreateCommand(); command.CommandText = sql; foreach (var pair in values) { var parameter = command.CreateParameter(); parameter.ParameterName = pair.Key; parameter.Value = pair.Value ?? DBNull.Value; command.Parameters.Add(parameter); } return await command.ExecuteNonQueryAsync(cancellationToken); }
        finally { if (shouldClose) await connection.CloseAsync(); }
    }
}

public sealed class CreateOrderRequest { public Guid TableId { get; set; } public List<CreateTicketRequest>? Tickets { get; set; } }
public sealed class CreateTicketRequest { public Guid? OrderId { get; set; } public List<CreateOrderDetailRequest>? Details { get; set; } }
public sealed class AddTicketRequest { public List<CreateOrderDetailRequest>? Details { get; set; } }
public sealed class CreateOrderDetailRequest { public Guid ArticleId { get; set; } public decimal Quantity { get; set; } public decimal UnitPrice { get; set; } }