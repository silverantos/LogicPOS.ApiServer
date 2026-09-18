namespace LogicPOS.ApiServer.Data.Entities;

public sealed class ApiCurrency
{
    public Guid Id { get; set; }
    public uint Order { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string? Acronym { get; set; }
    public string Symbol { get; set; } = "??";
    public string? Entity { get; set; }
    public decimal ExchangeRate { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
