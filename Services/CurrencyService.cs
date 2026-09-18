using LogicPOS.ApiServer.Data;
using LogicPOS.ApiServer.Data.Entities;
using LogicPOS.ApiServer.DTOs;
using Microsoft.EntityFrameworkCore;

namespace LogicPOS.ApiServer.Services;

public sealed class CurrencyService
{
    private readonly ApplicationDbContext _dbContext;

    public CurrencyService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<CurrencyResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.ApiCurrencies.AsNoTracking().Where(item => !item.IsDeleted).OrderBy(item => item.Order).ToListAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<AddEntityIdResponse> CreateAsync(AddCurrencyRequest request, CancellationToken cancellationToken = default)
    {
        var count = await _dbContext.ApiCurrencies.CountAsync(cancellationToken);
        var item = new ApiCurrency
        {
            Id = Guid.NewGuid(),
            Order = (uint)(count + 1),
            Code = $"CUR{count + 1}",
            Designation = request.Designation.Trim(),
            Acronym = request.Acronym,
            Symbol = string.IsNullOrWhiteSpace(request.Symbol) ? "??" : request.Symbol,
            Entity = request.Entity,
            ExchangeRate = request.ExchangeRate,
            Notes = request.Notes ?? string.Empty,
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };

        _dbContext.ApiCurrencies.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new AddEntityIdResponse { Id = item.Id };
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateCurrencyRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ApiCurrencies.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        item.Order = request.Order;
        item.Code = string.IsNullOrWhiteSpace(request.Code) ? item.Code : request.Code;
        item.Designation = request.Designation.Trim();
        item.Acronym = request.Acronym;
        item.Symbol = string.IsNullOrWhiteSpace(request.Symbol) ? item.Symbol : request.Symbol;
        item.Entity = request.Entity;
        item.ExchangeRate = request.ExchangeRate;
        item.Notes = request.Notes ?? string.Empty;
        item.IsDeleted = request.IsDeleted;
        item.UpdatedUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.ApiCurrencies.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        _dbContext.ApiCurrencies.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static CurrencyResponse Map(ApiCurrency item)
    {
        return new CurrencyResponse
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
            Acronym = item.Acronym,
            Symbol = item.Symbol,
            Entity = item.Entity,
            ExchangeRate = item.ExchangeRate
        };
    }
}
