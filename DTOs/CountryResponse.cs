namespace LogicPOS.ApiServer.DTOs;

public sealed class CountryResponse
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
    public string? Code2 { get; set; }
    public string? Code3 { get; set; }
    public string? Capital { get; set; }
    public string? TLD { get; set; }
    public string? Currency { get; set; }
    public string? CurrencyCode { get; set; }
    public string? FiscalNumberRegex { get; set; }
    public string? ZipCodeRegex { get; set; }
}

public sealed class AddCountryRequest
{
    public string Designation { get; set; } = string.Empty;
    public string? Code2 { get; set; }
    public string? Code3 { get; set; }
    public string? Capital { get; set; }
    public string? TLD { get; set; }
    public string? Currency { get; set; }
    public string? CurrencyCode { get; set; }
    public string? FiscalNumberRegex { get; set; }
    public string? ZipCodeRegex { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateCountryRequest
{
    public uint Order { get; set; }
    public string? Code { get; set; }
    public string Designation { get; set; } = string.Empty;
    public string? Code2 { get; set; }
    public string? Code3 { get; set; }
    public string? Capital { get; set; }
    public string? Currency { get; set; }
    public string? CurrencyCode { get; set; }
    public string? FiscalNumberRegex { get; set; }
    public string? ZipCodeRegex { get; set; }
    public string? TLD { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
}
