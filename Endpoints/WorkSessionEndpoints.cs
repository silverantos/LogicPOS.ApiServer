using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace LogicPOS.ApiServer.Endpoints;

public static class WorkSessionEndpoints
{
    public static void MapWorkSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var worksessions = app.MapGroup("/worksessions").WithTags("Work Sessions");
        var worksession = app.MapGroup("/worksession").WithTags("Work Sessions");

        worksessions.MapPost("/periods/open-session", async (OpenSessionRequest request,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            var day = (await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken))
                .FirstOrDefault(row => ToInt(row, "Type") == 0 && ToInt(row, "Status") == 0);
            if (day is null) return Results.Problem(title: "An open day is required.", statusCode: StatusCodes.Status400BadRequest);

            var terminalId = await GetDefaultTerminalIdAsync(db, cancellationToken);
            if (terminalId is null) return Results.Problem(title: "No terminal is configured.", statusCode: StatusCodes.Status400BadRequest);

            var sessions = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            if (sessions.Any(row => ToInt(row, "Type") == 1 && ToInt(row, "Status") == 0 && GuidEquals(row.GetValueOrDefault("CreatedWhere"), terminalId.Value)))
                return Results.Problem(title: "The terminal already has an open session.", statusCode: StatusCodes.Status400BadRequest);

            var id = Guid.NewGuid();
            var now = DateTime.UtcNow.ToString("O");
            var affected = await ExecuteAsync(db, """
                INSERT INTO "WorkSessionPeriods" ("Id", "Type", "Status", "Designation", "StartDate", "EndDate", "ParentId", "Notes", "CreatedAt", "CreatedBy", "CreatedWhere", "UpdatedAt", "UpdatedBy", "UpdatedWhere", "IsDeleted", "DeletedAt")
                VALUES (@id, 1, 0, @designation, @startDate, NULL, @parentId, @notes, @createdAt, 'api', @createdWhere, @updatedAt, 'api', 'api', 0, '')
                """, new Dictionary<string, object?>
            {
                ["@id"] = id.ToString(), ["@designation"] = "Terminal session", ["@startDate"] = now,
                ["@parentId"] = day["Id"], ["@notes"] = request.Notes, ["@createdAt"] = now,
                ["@createdWhere"] = terminalId.Value.ToString(), ["@updatedAt"] = now
            }, cancellationToken);

            if (affected == 0) return Results.Problem(statusCode: StatusCodes.Status400BadRequest);
            if (request.Amount.HasValue)
                await InsertMovementAsync(db, id, request.Amount.Value, 1, cancellationToken);
            return Results.Created($"/worksession/period/terminal/{terminalId}", new { id });
        }).WithName("OpenTerminalSession");

        worksession.MapPost("/periods/open-day", async (OpenDayRequest request,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            var periods = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            if (periods.Any(row => ToInt(row, "Type") == 0 && ToInt(row, "Status") == 0))
                return Results.Problem(title: "An open day already exists.", statusCode: StatusCodes.Status400BadRequest);
            var id = Guid.NewGuid();
            var now = DateTime.UtcNow.ToString("O");
            var affected = await ExecuteAsync(db, """
                INSERT INTO "WorkSessionPeriods" ("Id", "Type", "Status", "Designation", "StartDate", "EndDate", "ParentId", "Notes", "CreatedAt", "CreatedBy", "CreatedWhere", "UpdatedAt", "UpdatedBy", "UpdatedWhere", "IsDeleted", "DeletedAt")
                VALUES (@id, 0, 0, 'Day', @startDate, NULL, NULL, @notes, @createdAt, 'api', 'api', @updatedAt, 'api', 'api', 0, '')
                """, new Dictionary<string, object?>
            {
                ["@id"] = id.ToString(), ["@startDate"] = now, ["@notes"] = request.Notes,
                ["@createdAt"] = now, ["@updatedAt"] = now
            }, cancellationToken);
            return affected == 0 ? Results.Problem(statusCode: StatusCodes.Status400BadRequest) : Results.Created($"/worksessions/periods/lastday", new { id });
        }).WithName("OpenDay");

        worksessions.MapGet("/periods/day-is-open", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            var isOpen = rows.Any(r => Convert.ToInt32(r.GetValueOrDefault("Type") ?? -1) == 0 &&
                                       Convert.ToInt32(r.GetValueOrDefault("Status") ?? -1) == 0);
            return Results.Ok(isOpen);
        })
        .WithName("DayIsOpen");

        worksessions.MapGet("/periods/terminal-is-open", async (Guid? terminalId, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            var isOpen = rows.Any(r => Convert.ToInt32(r.GetValueOrDefault("Type") ?? -1) == 1 &&
                                       Convert.ToInt32(r.GetValueOrDefault("Status") ?? -1) == 0 &&
                                       (!terminalId.HasValue || GuidEquals(r.GetValueOrDefault("CreatedWhere"), terminalId.Value)));
            return Results.Ok(isOpen);
        })
        .WithName("TerminalIsOpen");

        worksessions.MapGet("/periods/open-terminal-sessions", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            var sessions = rows.Where(r => Convert.ToInt32(r.GetValueOrDefault("Type") ?? -1) == 1 &&
                                           Convert.ToInt32(r.GetValueOrDefault("Status") ?? -1) == 0).ToList();
            return Results.Ok(sessions);
        })
        .WithName("GetOpenTerminalSessions");

        worksessions.MapGet("/periods/lastday", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            var lastClosedDay = rows.Where(r => Convert.ToInt32(r.GetValueOrDefault("Type") ?? -1) == 0 &&
                                                Convert.ToInt32(r.GetValueOrDefault("Status") ?? -1) == 1)
                                    .OrderByDescending(r => r.GetValueOrDefault("EndDate"))
                                    .FirstOrDefault();

            return lastClosedDay is null ? Results.NotFound() : Results.Ok(lastClosedDay);
        })
        .WithName("GetLastClosedDay");

        worksessions.MapGet("/periods/alldays", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            var closedDays = rows.Where(r => Convert.ToInt32(r.GetValueOrDefault("Type") ?? -1) == 0 &&
                                             Convert.ToInt32(r.GetValueOrDefault("Status") ?? -1) == 1)
                                 .OrderByDescending(r => r.GetValueOrDefault("EndDate"))
                                 .ToList();
            return Results.Ok(closedDays);
        })
        .WithName("GetAllClosedDays");

        worksession.MapGet("/period/terminal/{id:guid}", async (Guid id, AppDbContext db, CancellationToken cancellationToken) =>
        {
            var rows = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            var session = rows.Where(r => Convert.ToInt32(r.GetValueOrDefault("Type") ?? -1) == 1 &&
                                          GuidEquals(r.GetValueOrDefault("CreatedWhere"), id))
                              .OrderByDescending(r => r.GetValueOrDefault("StartDate"))
                              .FirstOrDefault();

            return session is null ? Results.NotFound() : Results.Ok(session);
        })
        .WithName("GetLastWorkSessionByTerminalId");

        worksessions.MapGet("/movements/total-cash-in-drawer/{terminalId:guid}", async (Guid terminalId,
            AppDbContext db, CancellationToken cancellationToken) =>
        {
            var periods = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            var terminalPeriodIds = periods
                .Where(row => Convert.ToInt32(row.GetValueOrDefault("Type") ?? -1) == 1 &&
                              GuidEquals(row.GetValueOrDefault("CreatedWhere"), terminalId))
                .Select(row => Convert.ToString(row.GetValueOrDefault("Id")))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var movements = await TryReadTableAsync(db, "WorkSessionMovements", cancellationToken);
            var total = movements
                .Where(row => terminalPeriodIds.Contains(Convert.ToString(row.GetValueOrDefault("PeriodId")) ?? string.Empty) &&
                              !IsTruthy(row, "IsDeleted"))
                .Sum(row => Convert.ToDecimal(row.GetValueOrDefault("Amount") ?? 0));

            return Results.Ok(total);
        })
        .WithName("GetTotalCashInDrawer");

        worksessions.MapPut("/periods/close-session", async (CloseSessionRequest request,
            AppDbContext db, CancellationToken cancellationToken) =>
            await CloseTerminalSessionAsync(request, db, cancellationToken)).WithName("CloseTerminalSession");

        worksession.MapPut("/periods/close-day", async (CloseDayRequest request,
            AppDbContext db, CancellationToken cancellationToken) =>
            await CloseDayAsync(request.Notes, db, cancellationToken)).WithName("CloseDay");

        worksessions.MapPut("/periods/close-all-sessions", async (AppDbContext db, CancellationToken cancellationToken) =>
        {
            var sessions = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
            var open = sessions.Where(row => ToInt(row, "Type") == 1 && ToInt(row, "Status") == 0).ToList();
            foreach (var session in open.Where(row => Guid.TryParse(Convert.ToString(row.GetValueOrDefault("Id")), out _)))
                await ClosePeriodAsync(Guid.Parse(Convert.ToString(session["Id"])!), null, db, cancellationToken);
            return Results.Ok();
        }).WithName("CloseAllTerminalSessions");
    }

    private static bool GuidEquals(object? value, Guid target) =>
        value != null && Guid.TryParse(Convert.ToString(value), out var parsed) && parsed == target;

    private static bool IsTruthy(Dictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null) return false;
        return value is bool booleanValue
            ? booleanValue
            : int.TryParse(Convert.ToString(value), out var integerValue) && integerValue != 0;
    }

    private static int ToInt(Dictionary<string, object?> row, string key) =>
        int.TryParse(Convert.ToString(row.GetValueOrDefault(key)), out var value) ? value : -1;

    private static async Task<Guid?> GetDefaultTerminalIdAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var rows = await TryReadTableAsync(db, "Terminals", cancellationToken);
        var row = rows.FirstOrDefault(candidate => IsTruthy(candidate, "IsDefault")) ?? rows.FirstOrDefault();
        return row is not null && Guid.TryParse(Convert.ToString(row.GetValueOrDefault("Id")), out var id) ? id : null;
    }

    private static async Task<IResult> CloseTerminalSessionAsync(CloseSessionRequest request, AppDbContext db, CancellationToken cancellationToken)
    {
        var terminalId = await GetDefaultTerminalIdAsync(db, cancellationToken);
        if (terminalId is null) return Results.Problem(title: "No terminal is configured.", statusCode: StatusCodes.Status400BadRequest);
        var sessions = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
        var session = sessions.Where(row => ToInt(row, "Type") == 1 && ToInt(row, "Status") == 0 && GuidEquals(row.GetValueOrDefault("CreatedWhere"), terminalId.Value)).OrderByDescending(row => row.GetValueOrDefault("StartDate")).FirstOrDefault();
        if (session is null || !Guid.TryParse(Convert.ToString(session["Id"]), out var id)) return Results.Problem(title: "No open terminal session exists.", statusCode: StatusCodes.Status400BadRequest);
        await ClosePeriodAsync(id, request.Notes, db, cancellationToken);
        if (request.Amount.HasValue) await InsertMovementAsync(db, id, request.Amount.Value, 2, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> CloseDayAsync(string? notes, AppDbContext db, CancellationToken cancellationToken)
    {
        var periods = await TryReadTableAsync(db, "WorkSessionPeriods", cancellationToken);
        var day = periods.Where(row => ToInt(row, "Type") == 0 && ToInt(row, "Status") == 0).OrderByDescending(row => row.GetValueOrDefault("StartDate")).FirstOrDefault();
        if (day is null || !Guid.TryParse(Convert.ToString(day["Id"]), out var id)) return Results.Problem(title: "No open day exists.", statusCode: StatusCodes.Status400BadRequest);
        await ClosePeriodAsync(id, notes, db, cancellationToken);
        return Results.Ok();
    }

    private static async Task ClosePeriodAsync(Guid id, string? notes, AppDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow.ToString("O");
        await ExecuteAsync(db, "UPDATE \"WorkSessionPeriods\" SET \"Status\"=1, \"EndDate\"=@endDate, \"Notes\"=COALESCE(@notes, \"Notes\"), \"UpdatedAt\"=@updatedAt, \"UpdatedBy\"='api', \"UpdatedWhere\"='api' WHERE \"Id\"=@id AND \"Status\"=0", new Dictionary<string, object?> { ["@id"] = id.ToString(), ["@endDate"] = now, ["@notes"] = notes, ["@updatedAt"] = now }, cancellationToken);
    }

    private static Task<int> InsertMovementAsync(AppDbContext db, Guid periodId, decimal amount, int type, CancellationToken cancellationToken) =>
        ExecuteAsync(db, "INSERT INTO \"WorkSessionMovements\" (\"Id\", \"Amount\", \"CreatedAt\", \"CreatedBy\", \"CreatedWhere\", \"DeletedAt\", \"DocumentId\", \"IsDeleted\", \"Notes\", \"PaymentId\", \"PeriodId\", \"Type\", \"UpdatedAt\", \"UpdatedBy\", \"UpdatedWhere\") VALUES (@id, @amount, @createdAt, 'api', 'api', '', NULL, 0, NULL, NULL, @periodId, @type, @updatedAt, 'api', 'api')", new Dictionary<string, object?> { ["@id"] = Guid.NewGuid().ToString(), ["@amount"] = amount, ["@createdAt"] = DateTime.UtcNow.ToString("O"), ["@periodId"] = periodId.ToString(), ["@type"] = type, ["@updatedAt"] = DateTime.UtcNow.ToString("O") }, cancellationToken);

    private static async Task<int> ExecuteAsync(AppDbContext db, string sql, Dictionary<string, object?> values, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State == System.Data.ConnectionState.Closed;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var pair in values)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = pair.Key;
                parameter.Value = pair.Value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

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
}

public sealed class OpenSessionRequest { public decimal? Amount { get; set; } public string? Notes { get; set; } }
public sealed class OpenDayRequest { public string? Notes { get; set; } }
public sealed class CloseSessionRequest { public decimal? Amount { get; set; } public string? Notes { get; set; } }
public sealed class CloseDayRequest { public string? Notes { get; set; } }
