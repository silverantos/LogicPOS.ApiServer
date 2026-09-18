using LogicPOS.ApiServer.Data;
using LogicPOS.ApiServer.Data.Entities;
using LogicPOS.ApiServer.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogicPOS.ApiServer.Services;

public sealed class WorkSessionService
{
    private readonly ApplicationDbContext _dbContext;

    public WorkSessionService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // --- Day period ---

    public async Task<bool> DayIsOpenAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiWorkSessionPeriods.AnyAsync(
            p => p.Type == ApiWorkSessionPeriodType.Day && p.Status == ApiWorkSessionPeriodStatus.Open, cancellationToken);
    }

    public async Task<(bool Success, string? Error, AddEntityIdResponse? Response)> OpenDayAsync(OpenWorkSessionDayRequest request, CancellationToken cancellationToken = default)
    {
        if (await DayIsOpenAsync(cancellationToken))
        {
            return (false, "O dia já está aberto.", null);
        }

        var day = new ApiWorkSessionPeriod
        {
            Id = Guid.NewGuid(),
            Type = ApiWorkSessionPeriodType.Day,
            Status = ApiWorkSessionPeriodStatus.Open,
            Designation = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            StartDate = DateTime.UtcNow,
            Notes = request.Notes ?? string.Empty,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _dbContext.ApiWorkSessionPeriods.Add(day);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, new AddEntityIdResponse { Id = day.Id });
    }

    public async Task<(bool Success, string? Error)> CloseDayAsync(CloseWorkSessionDayRequest request, CancellationToken cancellationToken = default)
    {
        var day = await _dbContext.ApiWorkSessionPeriods.SingleOrDefaultAsync(
            p => p.Type == ApiWorkSessionPeriodType.Day && p.Status == ApiWorkSessionPeriodStatus.Open, cancellationToken);

        if (day is null)
        {
            return (false, "O dia não está aberto.");
        }

        var openTerminalSessions = await _dbContext.ApiWorkSessionPeriods.CountAsync(
            p => p.Type == ApiWorkSessionPeriodType.Terminal && p.Status == ApiWorkSessionPeriodStatus.Open && p.ParentId == day.Id, cancellationToken);

        if (openTerminalSessions > 0)
        {
            return (false, "Existem sessões de terminal ainda abertas; feche-as primeiro (ou use close-all-sessions).");
        }

        day.Status = ApiWorkSessionPeriodStatus.Closed;
        day.EndDate = DateTime.UtcNow;
        day.Notes = request.Notes ?? day.Notes;
        day.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task CloseAllSessionsAsync(CancellationToken cancellationToken = default)
    {
        var openSessions = await _dbContext.ApiWorkSessionPeriods
            .Where(p => p.Type == ApiWorkSessionPeriodType.Terminal && p.Status == ApiWorkSessionPeriodStatus.Open)
            .ToListAsync(cancellationToken);

        foreach (var session in openSessions)
        {
            session.Status = ApiWorkSessionPeriodStatus.Closed;
            session.EndDate = DateTime.UtcNow;
            session.UpdatedUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WorkSessionPeriodResponse?> GetLastClosedDayAsync(CancellationToken cancellationToken = default)
    {
        var day = await _dbContext.ApiWorkSessionPeriods.AsNoTracking()
            .Where(p => p.Type == ApiWorkSessionPeriodType.Day && p.Status == ApiWorkSessionPeriodStatus.Closed)
            .OrderByDescending(p => p.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        return day is null ? null : Map(day, null);
    }

    public async Task<IReadOnlyList<WorkSessionPeriodResponse>> GetAllClosedDaysAsync(CancellationToken cancellationToken = default)
    {
        var days = await _dbContext.ApiWorkSessionPeriods.AsNoTracking()
            .Where(p => p.Type == ApiWorkSessionPeriodType.Day && p.Status == ApiWorkSessionPeriodStatus.Closed)
            .OrderByDescending(p => p.EndDate)
            .ToListAsync(cancellationToken);

        return days.Select(day => Map(day, null)).ToList();
    }

    // --- Terminal session ---

    public async Task<bool> TerminalIsOpenAsync(Guid terminalId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.ApiWorkSessionPeriods.AnyAsync(
            p => p.Type == ApiWorkSessionPeriodType.Terminal && p.Status == ApiWorkSessionPeriodStatus.Open && p.TerminalId == terminalId, cancellationToken);
    }

    public async Task<(bool Success, string? Error, AddEntityIdResponse? Response)> OpenTerminalSessionAsync(Guid terminalId, OpenTerminalSessionRequest request, CancellationToken cancellationToken = default)
    {
        var day = await _dbContext.ApiWorkSessionPeriods.SingleOrDefaultAsync(
            p => p.Type == ApiWorkSessionPeriodType.Day && p.Status == ApiWorkSessionPeriodStatus.Open, cancellationToken);

        if (day is null)
        {
            return (false, "O dia não está aberto; abra o dia primeiro.", null);
        }

        if (await TerminalIsOpenAsync(terminalId, cancellationToken))
        {
            return (false, "Este terminal já tem uma sessão aberta.", null);
        }

        var session = new ApiWorkSessionPeriod
        {
            Id = Guid.NewGuid(),
            Type = ApiWorkSessionPeriodType.Terminal,
            Status = ApiWorkSessionPeriodStatus.Open,
            Designation = $"Sessão {DateTime.UtcNow:yyyy-MM-dd HH:mm}",
            StartDate = DateTime.UtcNow,
            ParentId = day.Id,
            TerminalId = terminalId,
            Notes = request.Notes ?? string.Empty,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _dbContext.ApiWorkSessionPeriods.Add(session);
        _dbContext.ApiWorkSessionMovements.Add(new ApiWorkSessionMovement
        {
            Id = Guid.NewGuid(),
            WorkSessionPeriodId = session.Id,
            Type = ApiWorkSessionMovementType.CashDrawerOpen,
            Amount = request.Amount,
            Notes = request.Notes ?? string.Empty,
            CreatedUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, new AddEntityIdResponse { Id = session.Id });
    }

    public async Task<(bool Success, string? Error)> CloseTerminalSessionAsync(Guid terminalId, CloseTerminalSessionRequest request, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ApiWorkSessionPeriods.SingleOrDefaultAsync(
            p => p.Type == ApiWorkSessionPeriodType.Terminal && p.Status == ApiWorkSessionPeriodStatus.Open && p.TerminalId == terminalId, cancellationToken);

        if (session is null)
        {
            return (false, "Este terminal não tem uma sessão aberta.");
        }

        _dbContext.ApiWorkSessionMovements.Add(new ApiWorkSessionMovement
        {
            Id = Guid.NewGuid(),
            WorkSessionPeriodId = session.Id,
            Type = ApiWorkSessionMovementType.CashDrawerClose,
            Amount = request.Amount,
            Notes = request.Notes ?? string.Empty,
            CreatedUtc = DateTime.UtcNow
        });

        session.Status = ApiWorkSessionPeriodStatus.Closed;
        session.EndDate = DateTime.UtcNow;
        session.Notes = request.Notes ?? session.Notes;
        session.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<IReadOnlyList<WorkSessionPeriodResponse>> GetOpenTerminalSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _dbContext.ApiWorkSessionPeriods.AsNoTracking()
            .Where(p => p.Type == ApiWorkSessionPeriodType.Terminal && p.Status == ApiWorkSessionPeriodStatus.Open)
            .ToListAsync(cancellationToken);

        if (sessions.Count == 0)
        {
            return [];
        }

        var parentIds = sessions.Where(s => s.ParentId.HasValue).Select(s => s.ParentId!.Value).Distinct().ToList();
        var parents = await _dbContext.ApiWorkSessionPeriods.AsNoTracking().Where(p => parentIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);

        return sessions.Select(session => Map(session, session.ParentId.HasValue ? parents.GetValueOrDefault(session.ParentId.Value) : null)).ToList();
    }

    public async Task<WorkSessionPeriodResponse?> GetLastWorkSessionByTerminalIdAsync(Guid terminalId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ApiWorkSessionPeriods.AsNoTracking()
            .Where(p => p.Type == ApiWorkSessionPeriodType.Terminal && p.TerminalId == terminalId)
            .OrderByDescending(p => p.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return null;
        }

        var parent = session.ParentId.HasValue
            ? await _dbContext.ApiWorkSessionPeriods.AsNoTracking().SingleOrDefaultAsync(p => p.Id == session.ParentId.Value, cancellationToken)
            : null;

        return Map(session, parent);
    }

    // --- Cash drawer movements ---

    public async Task<(bool Success, string? Error, AddEntityIdResponse? Response)> AddCashDrawerMovementAsync(Guid terminalId, ApiWorkSessionMovementType type, CashDrawerMovementRequest request, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ApiWorkSessionPeriods.SingleOrDefaultAsync(
            p => p.Type == ApiWorkSessionPeriodType.Terminal && p.Status == ApiWorkSessionPeriodStatus.Open && p.TerminalId == terminalId, cancellationToken);

        if (session is null)
        {
            return (false, "Este terminal não tem uma sessão aberta.", null);
        }

        var movement = new ApiWorkSessionMovement
        {
            Id = Guid.NewGuid(),
            WorkSessionPeriodId = session.Id,
            Type = type,
            Amount = request.Amount,
            Notes = request.Notes ?? string.Empty,
            CreatedUtc = DateTime.UtcNow
        };

        _dbContext.ApiWorkSessionMovements.Add(movement);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (true, null, new AddEntityIdResponse { Id = movement.Id });
    }

    public async Task<decimal?> GetTotalCashInDrawerAsync(Guid terminalId, CancellationToken cancellationToken = default)
    {
        var session = await _dbContext.ApiWorkSessionPeriods.AsNoTracking().SingleOrDefaultAsync(
            p => p.Type == ApiWorkSessionPeriodType.Terminal && p.Status == ApiWorkSessionPeriodStatus.Open && p.TerminalId == terminalId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        var movements = await _dbContext.ApiWorkSessionMovements.AsNoTracking()
            .Where(m => m.WorkSessionPeriodId == session.Id)
            .ToListAsync(cancellationToken);

        // Running cash total = opening float + cash added in - cash taken out. Document/Payment movement
        // types exist in the client's enum for when a sale is paid in cash during the session, but nothing
        // in this API auto-records those yet (that would mean wiring PayDocuments to emit a movement here,
        // which hasn't been connected) — so this total reflects manual drawer movements only, not sales.
        return movements.Sum(m => m.Type switch
        {
            ApiWorkSessionMovementType.CashDrawerOpen => m.Amount,
            ApiWorkSessionMovementType.CashDrawerIn => m.Amount,
            ApiWorkSessionMovementType.Payment => m.Amount,
            ApiWorkSessionMovementType.CashDrawerOut => -m.Amount,
            ApiWorkSessionMovementType.CashDrawerMoneyOut => -m.Amount,
            ApiWorkSessionMovementType.CashDrawerClose => 0,
            _ => 0
        });
    }

    private static WorkSessionPeriodResponse Map(ApiWorkSessionPeriod period, ApiWorkSessionPeriod? parent)
    {
        return new WorkSessionPeriodResponse
        {
            Id = period.Id,
            Notes = period.Notes,
            CreatedAt = period.CreatedUtc,
            UpdatedAt = period.UpdatedUtc,
            UpdatedBy = Guid.Empty,
            IsDeleted = false,
            Type = (int)period.Type,
            Status = (int)period.Status,
            Designation = period.Designation,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            Parent = parent is null ? null : Map(parent, null),
            ParentId = period.ParentId
        };
    }
}
