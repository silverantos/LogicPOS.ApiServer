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

        terminals.MapGet("/hardwareid/{hardwareId}", async (string hardwareId, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "Terminals", cancellationToken);
                var terminal = rows.FirstOrDefault(row => ValueEquals(row, "HardwareId", hardwareId));

                return terminal is null
                    ? Results.Problem(detail: $"Terminal with HardwareId '{hardwareId}' was not found.", statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(terminal);
            })
            .WithName("GetTerminalByHardwareId");

        terminals.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "Terminals", cancellationToken);
                var terminal = rows.FirstOrDefault(row => ValueEquals(row, "Id", id.ToString()));

                return terminal is null
                    ? Results.Problem(detail: $"Terminal with Id '{id}' was not found.", statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(terminal);
            })
            .WithName("GetTerminalById");

        terminals.MapPut("/{id:guid}", async (Guid id, UpdateTerminalRequest request,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Designation))
                return Results.Problem(title: "Code and designation are required.", statusCode: StatusCodes.Status400BadRequest);

            var connection = db.Database.GetDbConnection();
            var shouldClose = await OpenConnectionAsync(connection, cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE "Terminals"
                    SET "Order" = @order, "Code" = @code, "Designation" = @designation,
                        "PrinterId" = @printerId, "WeighingMachineId" = @weighingMachineId,
                        "PlaceId" = @placeId, "ThermalPrinterId" = @thermalPrinterId,
                        "BarcodeReaderId" = @barcodeReaderId, "CardReaderId" = @cardReaderId,
                        "PoleDisplayId" = @poleDisplayId, "TimerInterval" = @timerInterval,
                        "Notes" = @notes, "IsDefault" = @isDefault,
                        "UpdatedAt" = @updatedAt, "UpdatedBy" = @updatedBy, "UpdatedWhere" = @updatedWhere
                    WHERE "Id" = @id AND "IsDeleted" = 0
                    """;
                AddParameters(command, new Dictionary<string, object?>
                {
                    ["@id"] = id.ToString(), ["@order"] = request.Order, ["@code"] = request.Code,
                    ["@designation"] = request.Designation, ["@printerId"] = request.PrinterId,
                    ["@weighingMachineId"] = request.WeighingMachineId, ["@placeId"] = request.PlaceId,
                    ["@thermalPrinterId"] = request.ThermalPrinterId, ["@barcodeReaderId"] = request.BarcodeReaderId,
                    ["@cardReaderId"] = request.CardReaderId, ["@poleDisplayId"] = request.PoleDisplayId,
                    ["@timerInterval"] = request.TimerInterval, ["@notes"] = request.Notes,
                    ["@isDefault"] = request.IsDefault ? 1 : 0, ["@updatedAt"] = DateTime.UtcNow.ToString("O"),
                    ["@updatedBy"] = "api", ["@updatedWhere"] = "api"
                });
                return await command.ExecuteNonQueryAsync(cancellationToken) == 0
                    ? Results.NotFound() : Results.Ok();
            }
            finally { CloseConnection(connection, shouldClose); }
        }).WithName("UpdateTerminal");

        terminals.MapPost("/create", async (CreateTerminalRequest request, AppDbContext db, CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.HardwareId))
                {
                    return Results.Problem(title: "HardwareId is required.", statusCode: StatusCodes.Status400BadRequest);
                }

                var existingRows = await SqliteTableReader.ReadTableAsync(db, "Terminals", cancellationToken);
                var existingTerminal = existingRows.FirstOrDefault(row => ValueEquals(row, "HardwareId", request.HardwareId));

                if (existingTerminal is not null && existingTerminal.TryGetValue("Id", out var existingIdValue) &&
                    Guid.TryParse(Convert.ToString(existingIdValue), out var existingId))
                {
                    return Results.Ok(new { id = existingId });
                }

                var connection = db.Database.GetDbConnection();
                var shouldClose = connection.State == System.Data.ConnectionState.Closed;
                if (shouldClose) await connection.OpenAsync(cancellationToken);

                try
                {
                    var newId = Guid.NewGuid();
                    await using var command = connection.CreateCommand();
                    command.CommandText = $"""
                        INSERT INTO {SqliteTableReader.QuoteIdentifier("Terminals")}
                            ("Id", "HardwareId", "Designation", "Code", "IsDefault", "Order", "TimerInterval",
                             "CreatedAt", "CreatedBy", "CreatedWhere", "UpdatedAt", "UpdatedBy", "UpdatedWhere",
                             "IsDeleted", "DeletedAt")
                        VALUES (@id, @hardwareId, @designation, @code, @isDefault, @order, @timerInterval,
                                @createdAt, @createdBy, @createdWhere, @updatedAt, @updatedBy, @updatedWhere,
                                @isDeleted, @deletedAt)
                        """;

                    AddParameter(command, "@id", newId.ToString());
                    AddParameter(command, "@hardwareId", request.HardwareId);
                    AddParameter(command, "@designation", $"Terminal {request.HardwareId}");
                    AddParameter(command, "@code", request.HardwareId);
                    AddParameter(command, "@isDefault", existingRows.Count == 0 ? 1 : 0);
                    AddParameter(command, "@order", existingRows.Count);
                    AddParameter(command, "@timerInterval", 0);
                    AddParameter(command, "@createdAt", DateTime.UtcNow.ToString("O"));
                    AddParameter(command, "@createdBy", "api");
                    AddParameter(command, "@createdWhere", "api");
                    AddParameter(command, "@updatedAt", DateTime.UtcNow.ToString("O"));
                    AddParameter(command, "@updatedBy", "api");
                    AddParameter(command, "@updatedWhere", "api");
                    AddParameter(command, "@isDeleted", 0);
                    AddParameter(command, "@deletedAt", string.Empty);
                    await command.ExecuteNonQueryAsync(cancellationToken);
                    return Results.Created($"/terminals/{newId}", new { id = newId });
                }
                finally
                {
                    if (shouldClose) await connection.CloseAsync();
                }
            })
            .WithName("CreateTerminal");

        var places = app.MapGroup("/places").WithTags("Places");
        places.MapGet("", async (Guid? placeId, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "Places", cancellationToken);
                return Results.Ok(placeId is null
                    ? rows
                    : rows.Where(row => ValueEquals(row, "Id", placeId.Value.ToString())).ToList());
            })
            .WithName("GetPlaces");

        places.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "Places", cancellationToken);
                var place = rows.FirstOrDefault(row => ValueEquals(row, "Id", id.ToString()));
                return place is null ? Results.NotFound() : Results.Ok(place);
            })
            .WithName("GetPlaceById");

        places.MapPost("", async (CreatePlaceRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (request.PriceTypeId == Guid.Empty || string.IsNullOrWhiteSpace(request.Designation))
                return Results.Problem(title: "PriceTypeId and designation are required.", statusCode: StatusCodes.Status400BadRequest);
            var id = Guid.NewGuid();
            var code = request.Designation.Trim();
            var order = await NextOrderAsync(db, "Places", cancellationToken);
            var affected = await ExecuteAsync(db, """
                INSERT INTO "Places" ("Id", "PriceTypeId", "MovementTypeId", "ButtonImage", "TypeSubtotal", "AccountType", "OrderPrintMode", "Notes", "CreatedAt", "CreatedBy", "CreatedWhere", "UpdatedAt", "UpdatedBy", "UpdatedWhere", "IsDeleted", "DeletedAt", "Code", "Order", "Designation")
                VALUES (@id, @priceTypeId, @movementTypeId, @buttonImage, @typeSubtotal, @accountType, @orderPrintMode, @notes, @createdAt, @createdBy, @createdWhere, @updatedAt, @updatedBy, @updatedWhere, 0, @deletedAt, @code, @order, @designation)
                """, new Dictionary<string, object?>
            {
                ["@id"] = id.ToString(), ["@priceTypeId"] = request.PriceTypeId.ToString(), ["@movementTypeId"] = request.MovementTypeId?.ToString(),
                ["@buttonImage"] = request.ButtonImage, ["@typeSubtotal"] = request.TypeSubtotal, ["@accountType"] = request.AccountType,
                ["@orderPrintMode"] = request.OrderPrintMode, ["@notes"] = request.Notes, ["@createdAt"] = DateTime.UtcNow.ToString("O"),
                ["@createdBy"] = "api", ["@createdWhere"] = "api", ["@updatedAt"] = DateTime.UtcNow.ToString("O"),
                ["@updatedBy"] = "api", ["@updatedWhere"] = "api", ["@deletedAt"] = string.Empty, ["@code"] = code,
                ["@order"] = order, ["@designation"] = request.Designation.Trim()
            }, cancellationToken);
            return affected == 0 ? Results.Problem(statusCode: StatusCodes.Status400BadRequest) : Results.Created($"/places/{id}", new { id });
        }).WithName("AddPlace");

        places.MapPut("/{id:guid}", async (Guid id, UpdatePlaceRequest request, AppDbContext db, CancellationToken cancellationToken) =>
            await UpdatePlaceAsync(id, request, db, cancellationToken)).WithName("UpdatePlace");

        places.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
            await SoftDeleteAsync(db, "Places", id, cancellationToken) ? Results.Ok() : Results.NotFound()).WithName("DeletePlace");

        var tables = app.MapGroup("/tables").WithTags("Tables");
        tables.MapGet("", async (int? status, Guid? placeId, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "Tables", cancellationToken);
                var filtered = rows.Where(row =>
                    (!status.HasValue || Convert.ToInt32(row.GetValueOrDefault("Status") ?? -1) == status.Value) &&
                    (!placeId.HasValue || ValueEquals(row, "PlaceId", placeId.Value.ToString()))).ToList();
                return Results.Ok(filtered);
            })
            .WithName("GetTables");

        tables.MapGet("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "Tables", cancellationToken);
                var table = rows.FirstOrDefault(row => ValueEquals(row, "Id", id.ToString()));
                return table is null ? Results.NotFound() : Results.Ok(table);
            })
            .WithName("GetTableById");

        tables.MapPost("", async (CreateTableRequest request, AppDbContext db, CancellationToken cancellationToken) =>
        {
            if (request.PlaceId == Guid.Empty || string.IsNullOrWhiteSpace(request.Designation))
                return Results.Problem(title: "PlaceId and designation are required.", statusCode: StatusCodes.Status400BadRequest);
            var id = Guid.NewGuid();
            var order = await NextOrderAsync(db, "Tables", cancellationToken);
            var code = request.Designation.Trim();
            var affected = await ExecuteAsync(db, """
                INSERT INTO "Tables" ("Id", "ButtonImage", "ClosedAt", "Code", "CreatedAt", "CreatedBy", "CreatedWhere", "DeletedAt", "Designation", "Discount", "IsDeleted", "Notes", "OpennedAt", "Order", "PlaceId", "Status", "UpdatedAt", "UpdatedBy", "UpdatedWhere")
                VALUES (@id, @buttonImage, @closedAt, @code, @createdAt, @createdBy, @createdWhere, @deletedAt, @designation, @discount, 0, @notes, @opennedAt, @order, @placeId, 0, @updatedAt, @updatedBy, @updatedWhere)
                """, new Dictionary<string, object?>
            {
                ["@id"] = id.ToString(), ["@buttonImage"] = request.ButtonImage, ["@closedAt"] = DBNull.Value, ["@code"] = code,
                ["@createdAt"] = DateTime.UtcNow.ToString("O"), ["@createdBy"] = "api", ["@createdWhere"] = "api", ["@deletedAt"] = string.Empty,
                ["@designation"] = request.Designation.Trim(), ["@discount"] = request.Discount, ["@notes"] = request.Notes, ["@opennedAt"] = DBNull.Value,
                ["@order"] = order, ["@placeId"] = request.PlaceId.ToString(), ["@updatedAt"] = DateTime.UtcNow.ToString("O"),
                ["@updatedBy"] = "api", ["@updatedWhere"] = "api"
            }, cancellationToken);
            return affected == 0 ? Results.Problem(statusCode: StatusCodes.Status400BadRequest) : Results.Created($"/tables/{id}", new { id });
        }).WithName("AddTable");

        tables.MapPut("/{id:guid}", async (Guid id, UpdateTableRequest request, AppDbContext db, CancellationToken cancellationToken) =>
            await UpdateTableAsync(id, request, db, cancellationToken)).WithName("UpdateTable");

        tables.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
            await SoftDeleteAsync(db, "Tables", id, cancellationToken) ? Results.Ok() : Results.NotFound()).WithName("DeleteTable");

        tables.MapGet("/default", async (AppDbContext db, CancellationToken cancellationToken) =>
            {
                var rows = await SqliteTableReader.ReadTableAsync(db, "Tables", cancellationToken);
                var defaultTable = rows.FirstOrDefault(IsDefaultRow) ?? rows.FirstOrDefault();
                return defaultTable is null
                    ? Results.Problem(detail: "No default table was found.", statusCode: StatusCodes.Status404NotFound)
                    : Results.Ok(defaultTable);
            })
            .WithName("GetDefaultTable");
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddParameters(System.Data.Common.DbCommand command, Dictionary<string, object?> values)
    {
        foreach (var pair in values) AddParameter(command, pair.Key, pair.Value ?? DBNull.Value);
    }

    private static async Task<bool> OpenConnectionAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        return shouldClose;
    }

    private static void CloseConnection(System.Data.Common.DbConnection connection, bool shouldClose)
    {
        if (shouldClose) connection.Close();
    }

    private static async Task<int> ExecuteAsync(AppDbContext db, string sql, Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = await OpenConnectionAsync(connection, cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameters(command, values);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally { CloseConnection(connection, shouldClose); }
    }

    private static async Task<int> NextOrderAsync(AppDbContext db, string table, CancellationToken cancellationToken)
    {
        var rows = await SqliteTableReader.ReadTableAsync(db, table, cancellationToken);
        return rows.Count == 0 ? 0 : rows.Max(row => Convert.ToInt32(row.GetValueOrDefault("Order") ?? -1)) + 1;
    }

    private static async Task<bool> SoftDeleteAsync(AppDbContext db, string table, Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(db, $"UPDATE {SqliteTableReader.QuoteIdentifier(table)} SET \"IsDeleted\" = 1, \"DeletedAt\" = @deletedAt, \"UpdatedAt\" = @updatedAt, \"UpdatedBy\" = @updatedBy, \"UpdatedWhere\" = @updatedWhere WHERE \"Id\" = @id AND \"IsDeleted\" = 0", new Dictionary<string, object?>
        {
            ["@id"] = id.ToString(), ["@deletedAt"] = DateTime.UtcNow.ToString("O"), ["@updatedAt"] = DateTime.UtcNow.ToString("O"), ["@updatedBy"] = "api", ["@updatedWhere"] = "api"
        }, cancellationToken) > 0;

    private static async Task<IResult> UpdatePlaceAsync(Guid id, UpdatePlaceRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var affected = await ExecuteAsync(db, "UPDATE \"Places\" SET \"Order\"=@order, \"Code\"=@code, \"Designation\"=@designation, \"PriceTypeId\"=@priceTypeId, \"MovementTypeId\"=@movementTypeId, \"ButtonImage\"=@buttonImage, \"TypeSubtotal\"=@typeSubtotal, \"AccountType\"=@accountType, \"OrderPrintMode\"=@orderPrintMode, \"Notes\"=@notes, \"IsDeleted\"=@isDeleted, \"UpdatedAt\"=@updatedAt, \"UpdatedBy\"=@updatedBy, \"UpdatedWhere\"=@updatedWhere WHERE \"Id\"=@id", new Dictionary<string, object?>
        {
            ["@id"] = id.ToString(), ["@order"] = request.Order, ["@code"] = request.Code, ["@designation"] = request.Designation, ["@priceTypeId"] = request.PriceTypeId.ToString(), ["@movementTypeId"] = request.MovementTypeId?.ToString(), ["@buttonImage"] = request.ButtonImage, ["@typeSubtotal"] = request.TypeSubtotal, ["@accountType"] = request.AccountType, ["@orderPrintMode"] = request.OrderPrintMode, ["@notes"] = request.Notes, ["@isDeleted"] = request.IsDeleted ? 1 : 0, ["@updatedAt"] = DateTime.UtcNow.ToString("O"), ["@updatedBy"] = "api", ["@updatedWhere"] = "api"
        }, cancellationToken);
        return affected == 0 ? Results.NotFound() : Results.Ok();
    }

    private static async Task<IResult> UpdateTableAsync(Guid id, UpdateTableRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var affected = await ExecuteAsync(db, "UPDATE \"Tables\" SET \"Order\"=@order, \"Code\"=@code, \"PlaceId\"=@placeId, \"Designation\"=@designation, \"Notes\"=@notes, \"IsDeleted\"=@isDeleted, \"UpdatedAt\"=@updatedAt, \"UpdatedBy\"=@updatedBy, \"UpdatedWhere\"=@updatedWhere WHERE \"Id\"=@id", new Dictionary<string, object?>
        {
            ["@id"] = id.ToString(), ["@order"] = request.Order, ["@code"] = request.Code, ["@placeId"] = request.PlaceId.ToString(), ["@designation"] = request.Designation, ["@notes"] = request.Notes, ["@isDeleted"] = request.IsDeleted ? 1 : 0, ["@updatedAt"] = DateTime.UtcNow.ToString("O"), ["@updatedBy"] = "api", ["@updatedWhere"] = "api"
        }, cancellationToken);
        return affected == 0 ? Results.NotFound() : Results.Ok();
    }

    private static bool ValueEquals(Dictionary<string, object?> row, string key, string value) =>
        row.TryGetValue(key, out var rowValue) &&
        string.Equals(Convert.ToString(rowValue), value, StringComparison.OrdinalIgnoreCase);

    private static bool IsDefaultRow(Dictionary<string, object?> row) =>
        IsTruthy(row, "IsDefault") || IsTruthy(row, "Default") || IsTruthy(row, "IsDefaultTable");

    private static bool IsTruthy(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null) return false;
        return value switch
        {
            bool boolValue => boolValue,
            int intValue => intValue != 0,
            long longValue => longValue != 0,
            _ => bool.TryParse(Convert.ToString(value), out var boolValue) && boolValue
        };
    }
}

public sealed class CreateTerminalRequest
{
    public string HardwareId { get; set; } = string.Empty;
}

public sealed class UpdateTerminalRequest
{
    public int Order { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public Guid? PrinterId { get; set; }
    public Guid? WeighingMachineId { get; set; }
    public Guid? PlaceId { get; set; }
    public Guid? ThermalPrinterId { get; set; }
    public Guid? BarcodeReaderId { get; set; }
    public Guid? CardReaderId { get; set; }
    public Guid? PoleDisplayId { get; set; }
    public int TimerInterval { get; set; }
    public bool IsDefault { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreatePlaceRequest
{
    public string Designation { get; set; } = string.Empty;
    public Guid PriceTypeId { get; set; }
    public Guid? MovementTypeId { get; set; }
    public string? ButtonImage { get; set; }
    public string? TypeSubtotal { get; set; }
    public string? AccountType { get; set; }
    public int? OrderPrintMode { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdatePlaceRequest
{
    public int Order { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public Guid PriceTypeId { get; set; }
    public Guid? MovementTypeId { get; set; }
    public string? ButtonImage { get; set; }
    public string? TypeSubtotal { get; set; }
    public string? AccountType { get; set; }
    public int? OrderPrintMode { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed class CreateTableRequest
{
    public string Designation { get; set; } = string.Empty;
    public Guid PlaceId { get; set; }
    public string? ButtonImage { get; set; }
    public decimal Discount { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateTableRequest
{
    public int Order { get; set; }
    public string Code { get; set; } = string.Empty;
    public Guid PlaceId { get; set; }
    public string Designation { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
}
