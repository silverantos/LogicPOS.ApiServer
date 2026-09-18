using LogicPOS.ApiServer.Data;
using LogicPOS.ApiServer.Data.Entities;
using LogicPOS.ApiServer.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogicPOS.ApiServer.Services;

public sealed class CountryService
{
    private readonly ApplicationDbContext _dbContext;

    public CountryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CountryResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.ApiCountries.AsNoTracking().Where(item => !item.IsDeleted).OrderBy(item => item.Order).ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<CountryResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ApiCountries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<AddEntityIdResponse> CreateAsync(AddCountryRequest request, CancellationToken cancellationToken = default)
    {
        var count = await _dbContext.ApiCountries.CountAsync(cancellationToken);
        var item = new ApiCountry
        {
            Id = Guid.NewGuid(),
            Order = (uint)(count + 1),
            Code = $"C{count + 1}",
            Designation = request.Designation.Trim(),
            Code2 = request.Code2,
            Code3 = request.Code3,
            Capital = request.Capital,
            TLD = request.TLD,
            Currency = request.Currency,
            CurrencyCode = request.CurrencyCode,
            FiscalNumberRegex = request.FiscalNumberRegex,
            ZipCodeRegex = request.ZipCodeRegex,
            Notes = request.Notes ?? string.Empty,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _dbContext.ApiCountries.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new AddEntityIdResponse { Id = item.Id };
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateCountryRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ApiCountries.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.Order = request.Order;
        item.Code = string.IsNullOrWhiteSpace(request.Code) ? item.Code : request.Code;
        item.Designation = request.Designation.Trim();
        item.Code2 = request.Code2;
        item.Code3 = request.Code3;
        item.Capital = request.Capital;
        item.TLD = request.TLD;
        item.Currency = request.Currency;
        item.CurrencyCode = request.CurrencyCode;
        item.FiscalNumberRegex = request.FiscalNumberRegex;
        item.ZipCodeRegex = request.ZipCodeRegex;
        item.Notes = request.Notes ?? string.Empty;
        item.IsDeleted = request.IsDeleted;
        item.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ApiCountries.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        _dbContext.ApiCountries.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static CountryResponse Map(ApiCountry item)
    {
        return new CountryResponse
        {
            Id = item.Id,
            Notes = item.Notes,
            CreatedAt = item.CreatedUtc,
            UpdatedAt = item.UpdatedUtc,
            UpdatedBy = Guid.Empty,
            IsDeleted = item.IsDeleted,
            Order = item.Order,
            Code = item.Code,
            Designation = item.Designation,
            Code2 = item.Code2,
            Code3 = item.Code3,
            Capital = item.Capital,
            TLD = item.TLD,
            Currency = item.Currency,
            CurrencyCode = item.CurrencyCode,
            FiscalNumberRegex = item.FiscalNumberRegex,
            ZipCodeRegex = item.ZipCodeRegex
        };
    }
}
