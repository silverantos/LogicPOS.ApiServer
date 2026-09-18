using LogicPOS.ApiServer.Data;
using LogicPOS.ApiServer.Data.Entities;
using LogicPOS.ApiServer.DTOs;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace LogicPOS.ApiServer.Services;

public sealed class LicensingService
{
    private const int SingletonLicenseId = 1;
    private readonly ApplicationDbContext _dbContext;
    private readonly SystemVersionService _systemVersionService;

    public LicensingService(ApplicationDbContext dbContext, SystemVersionService systemVersionService)
    {
        _dbContext = dbContext;
        _systemVersionService = systemVersionService;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var license = await GetOrCreateLicenseAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(license.Version))
        {
            license.Version = _systemVersionService.GetApiVersion();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<LicenseResponse> GetDataAsync(CancellationToken cancellationToken = default)
    {
        var license = await GetOrCreateLicenseAsync(cancellationToken);
        return new LicenseResponse
        {
            Data = Map(license)
        };
    }

    public async Task<HardwareIdResponse> GetHardwareIdAsync(CancellationToken cancellationToken = default)
    {
        var license = await GetOrCreateLicenseAsync(cancellationToken);
        return new HardwareIdResponse
        {
            HardwareId = license.HardwareId
        };
    }

    public ConnectResponse GetConnectionStatus()
    {
        return new ConnectResponse
        {
            Connected = true
        };
    }

    public IReadOnlyList<string> GetCountries()
    {
        return new[] { "Portugal", "Angola" };
    }

    public async Task<ActivateLicenseResponseDto> ActivateAsync(ActivateLicenseRequest request, CancellationToken cancellationToken = default)
    {
        var license = await GetOrCreateLicenseAsync(cancellationToken);
        license.IsLicensed = true;
        license.Version = string.IsNullOrWhiteSpace(request.AssemblyVersion) ? _systemVersionService.GetApiVersion() : request.AssemblyVersion;
        license.HardwareId = string.IsNullOrWhiteSpace(request.HardwareId) ? license.HardwareId : request.HardwareId;
        license.Status = 1;
        license.Date = DateTime.UtcNow;
        license.Name = request.Name ?? string.Empty;
        license.Company = request.Company ?? string.Empty;
        license.Nif = request.FiscalNumber ?? string.Empty;
        license.Address = request.Address ?? string.Empty;
        license.Email = request.Email ?? string.Empty;
        license.Phone = request.Phone ?? string.Empty;
        license.IsValid = true;
        license.HasExpired = false;
        if (license.AllUpdateExpirationDate is null)
        {
            license.AllUpdateExpirationDate = DateTime.UtcNow.AddYears(1);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ActivateLicenseResponseDto
        {
            Success = true,
            LicenseData = string.Empty
        };
    }

    private async Task<ApiLicense> GetOrCreateLicenseAsync(CancellationToken cancellationToken)
    {
        await EnsureLicensesTableAsync(cancellationToken);

        var license = await _dbContext.ApiLicenses.SingleOrDefaultAsync(item => item.Id == SingletonLicenseId, cancellationToken);
        if (license is not null)
        {
            return license;
        }

        license = new ApiLicense
        {
            Id = SingletonLicenseId,
            IsLicensed = false,
            Version = _systemVersionService.GetApiVersion(),
            HardwareId = Guid.NewGuid().ToString().ToUpperInvariant(),
            Status = 0,
            Reseller = "LogicPOS",
            IsValid = false,
            HasExpired = false,
            AllNumberOfDevices = 1
        };

        _dbContext.ApiLicenses.Add(license);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return license;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqliteException sqliteException && sqliteException.SqliteErrorCode == 19)
        {
            _dbContext.Entry(license).State = EntityState.Detached;
            return await _dbContext.ApiLicenses.SingleAsync(item => item.Id == SingletonLicenseId, cancellationToken);
        }
    }

    private async Task EnsureLicensesTableAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (await TableExistsAsync(connection, "Licenses", cancellationToken))
            {
                return;
            }

            await using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = """
CREATE TABLE IF NOT EXISTS "Licenses" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Licenses" PRIMARY KEY,
    "IsLicensed" INTEGER NOT NULL DEFAULT 0,
    "Version" TEXT NOT NULL DEFAULT '',
    "HardwareId" TEXT NOT NULL DEFAULT '',
    "Status" INTEGER NOT NULL DEFAULT 0,
    "Date" TEXT NULL,
    "Name" TEXT NOT NULL DEFAULT '',
    "Company" TEXT NOT NULL DEFAULT '',
    "Nif" TEXT NOT NULL DEFAULT '',
    "Address" TEXT NOT NULL DEFAULT '',
    "Email" TEXT NOT NULL DEFAULT '',
    "Phone" TEXT NOT NULL DEFAULT '',
    "Reseller" TEXT NOT NULL DEFAULT '',
    "StocksModule" INTEGER NOT NULL DEFAULT 0,
    "AgtFeModule" INTEGER NOT NULL DEFAULT 0,
    "AllUpdateExpirationDate" TEXT NULL,
    "AllNumberOfDevices" INTEGER NULL,
    "HasExpired" INTEGER NOT NULL DEFAULT 0,
    "IsValid" INTEGER NOT NULL DEFAULT 0
);
""";
            await createTableCommand.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> TableExistsAsync(System.Data.Common.DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tableName;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "$tableName";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private static LicenseDataResponse Map(ApiLicense license)
    {
        return new LicenseDataResponse
        {
            IsLicensed = license.IsLicensed,
            Version = license.Version,
            HardwareId = license.HardwareId,
            Status = license.Status,
            Date = license.Date,
            Name = license.Name,
            Company = license.Company,
            Nif = license.Nif,
            Address = license.Address,
            Email = license.Email,
            Phone = license.Phone,
            Reseller = license.Reseller,
            StocksModule = license.StocksModule,
            AgtFeModule = license.AgtFeModule,
            AllUpdateExpirationDate = license.AllUpdateExpirationDate,
            AllNumberOfDevices = license.AllNumberOfDevices,
            HasExpired = license.HasExpired,
            IsValid = license.IsValid
        };
    }
}
