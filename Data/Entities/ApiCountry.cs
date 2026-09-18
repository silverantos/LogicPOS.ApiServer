namespace LogicPOS.ApiServer.Data.Entities;

public sealed class ApiCountry
{
    public Guid Id { get; set; }
    public uint Order { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string? Code2 { get; set; }
    public string? Code3 { get; set; }
    public string? Capital { get; set; }
    public string? TLD { get; set; }
    public string? Currency { get; set; }
    public string? CurrencyCode { get; set; }
    public string? FiscalNumberRegex { get; set; }
    public string? ZipCodeRegex { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
