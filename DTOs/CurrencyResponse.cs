namespace LogicPOS.ApiServer.DTOs;

public sealed class CurrencyResponse
{
    public Guid Id { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public uint Order { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string? Acronym { get; set; }
    public string Symbol { get; set; } = "??";
    public string? Entity { get; set; }
    public decimal ExchangeRate { get; set; }
}

public sealed class AddCurrencyRequest
{
    public string Designation { get; set; } = string.Empty;
    public string? Acronym { get; set; }
    public string Symbol { get; set; } = "??";
    public string? Entity { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateCurrencyRequest
{
    public uint Order { get; set; }
    public string? Code { get; set; }
    public string Designation { get; set; } = string.Empty;
    public string? Acronym { get; set; }
    public string Symbol { get; set; } = "??";
    public string? Entity { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
}
