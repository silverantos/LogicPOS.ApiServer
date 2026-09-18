namespace LogicPOS.ApiServer.Data.Entities;

public enum ApiWorkSessionPeriodType
{
    Day = 0,
    Terminal = 1
}

public enum ApiWorkSessionPeriodStatus
{
    Open = 0,
    Closed = 1
}

public enum ApiWorkSessionMovementType
{
    CashDrawerOpen = 1,
    CashDrawerClose = 2,
    CashDrawerIn = 3,
    CashDrawerOut = 4,
    CashDrawerMoneyOut = 5,
    Document = 6,
    Payment = 7
}

public sealed class ApiWorkSessionPeriod
{
    public Guid Id { get; set; }
    public ApiWorkSessionPeriodType Type { get; set; }
    public ApiWorkSessionPeriodStatus Status { get; set; } = ApiWorkSessionPeriodStatus.Open;
    public string Designation { get; set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public Guid? ParentId { get; set; }

    // Only set for Type == Terminal; identifies which terminal this session belongs to.
    public Guid? TerminalId { get; set; }

    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ApiWorkSessionMovement
{
    public Guid Id { get; set; }
    public Guid WorkSessionPeriodId { get; set; }
    public ApiWorkSessionMovementType Type { get; set; }
    public decimal Amount { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
