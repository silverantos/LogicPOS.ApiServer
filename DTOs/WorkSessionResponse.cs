namespace LogicPOS.ApiServer.DTOs;

public sealed class WorkSessionPeriodResponse
{
    public Guid Id { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public int Type { get; set; }
    public int Status { get; set; }
    public string Designation { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public WorkSessionPeriodResponse? Parent { get; set; }
    public Guid? ParentId { get; set; }
}

public sealed class OpenTerminalSessionRequest
{
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed class CloseTerminalSessionRequest
{
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed class CashDrawerMovementRequest
{
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}

public sealed class OpenWorkSessionDayRequest
{
    public string? Notes { get; set; }
}

public sealed class CloseWorkSessionDayRequest
{
    public string? Notes { get; set; }
}
