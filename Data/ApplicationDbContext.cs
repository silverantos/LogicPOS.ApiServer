using LogicPOS.ApiServer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace LogicPOS.ApiServer.Data;

public sealed class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ApiUser> ApiUsers => Set<ApiUser>();
    public DbSet<ApiCompanyInfo> ApiCompanyInfos => Set<ApiCompanyInfo>();
    public DbSet<ApiLicense> ApiLicenses => Set<ApiLicense>();
    public DbSet<ApiTerminal> ApiTerminals => Set<ApiTerminal>();
    public DbSet<ApiMovementType> ApiMovementTypes => Set<ApiMovementType>();
    public DbSet<ApiPlace> ApiPlaces => Set<ApiPlace>();
    public DbSet<ApiTable> ApiTables => Set<ApiTable>();
    public DbSet<ApiHoliday> ApiHolidays => Set<ApiHoliday>();
    public DbSet<ApiArticleClass> ApiArticleClasses => Set<ApiArticleClass>();
    public DbSet<ApiArticleFamily> ApiArticleFamilies => Set<ApiArticleFamily>();
    public DbSet<ApiArticleSubfamily> ApiArticleSubfamilies => Set<ApiArticleSubfamily>();
    public DbSet<ApiArticleType> ApiArticleTypes => Set<ApiArticleType>();
    public DbSet<ApiMeasurementUnit> ApiMeasurementUnits => Set<ApiMeasurementUnit>();
    public DbSet<ApiSizeUnit> ApiSizeUnits => Set<ApiSizeUnit>();
    public DbSet<ApiVatRate> ApiVatRates => Set<ApiVatRate>();
    public DbSet<ApiArticle> ApiArticles => Set<ApiArticle>();
    public DbSet<ApiWarehouse> ApiWarehouses => Set<ApiWarehouse>();
    public DbSet<ApiWarehouseLocation> ApiWarehouseLocations => Set<ApiWarehouseLocation>();
    public DbSet<ApiWarehouseArticle> ApiWarehouseArticles => Set<ApiWarehouseArticle>();
    public DbSet<ApiStockMovement> ApiStockMovements => Set<ApiStockMovement>();
    public DbSet<ApiStockMovementItem> ApiStockMovementItems => Set<ApiStockMovementItem>();
    public DbSet<ApiOrder> ApiOrders => Set<ApiOrder>();
    public DbSet<ApiOrderTicket> ApiOrderTickets => Set<ApiOrderTicket>();
    public DbSet<ApiOrderDetail> ApiOrderDetails => Set<ApiOrderDetail>();
    public DbSet<ApiArticleChild> ApiArticleChildren => Set<ApiArticleChild>();
    public DbSet<ApiCountry> ApiCountries => Set<ApiCountry>();
    public DbSet<ApiCurrency> ApiCurrencies => Set<ApiCurrency>();
    public DbSet<ApiWorkSessionPeriod> ApiWorkSessionPeriods => Set<ApiWorkSessionPeriod>();
    public DbSet<ApiWorkSessionMovement> ApiWorkSessionMovements => Set<ApiWorkSessionMovement>();
    public DbSet<ApiPaymentMethod> ApiPaymentMethods => Set<ApiPaymentMethod>();
    public DbSet<ApiFiscalYear> ApiFiscalYears => Set<ApiFiscalYear>();
    public DbSet<ApiDocumentType> ApiDocumentTypes => Set<ApiDocumentType>();
    public DbSet<ApiDocumentSeries> ApiDocumentSeries => Set<ApiDocumentSeries>();
    public DbSet<ApiDocument> ApiDocuments => Set<ApiDocument>();
    public DbSet<ApiDocumentDetail> ApiDocumentDetails => Set<ApiDocumentDetail>();
    public DbSet<ApiDocumentPaymentMethod> ApiDocumentPaymentMethods => Set<ApiDocumentPaymentMethod>();

    public override int SaveChanges()
    {
        ApplySoftDeleteRules();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplySoftDeleteRules();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySoftDeleteRules();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplySoftDeleteRules();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApiUser>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).HasMaxLength(128).IsRequired();
            entity.Property(user => user.PinHash).HasMaxLength(256).IsRequired();
            entity.Property(user => user.PinSalt).HasMaxLength(256).IsRequired();
            entity.Property(user => user.CreatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiCompanyInfo>(entity =>
        {
            entity.ToTable("CompanyInfos");
            entity.HasKey(company => company.Id);
            entity.Property(company => company.Name).HasMaxLength(256).IsRequired();
            entity.Property(company => company.BusinessName).HasMaxLength(256).IsRequired();
            entity.Property(company => company.CommercialName).HasMaxLength(256).IsRequired();
            entity.Property(company => company.LogoPng).IsRequired();
            entity.Property(company => company.LogoBmp).IsRequired();
            entity.Property(company => company.Address).HasMaxLength(512).IsRequired();
            entity.Property(company => company.City).HasMaxLength(128).IsRequired();
            entity.Property(company => company.PostalCode).HasMaxLength(64).IsRequired();
            entity.Property(company => company.CountryCode2).HasMaxLength(8).IsRequired();
            entity.Property(company => company.Phone).HasMaxLength(64).IsRequired();
            entity.Property(company => company.MobilePhone).HasMaxLength(64).IsRequired();
            entity.Property(company => company.Email).HasMaxLength(256).IsRequired();
            entity.Property(company => company.Website).HasMaxLength(256).IsRequired();
            entity.Property(company => company.FiscalNumber).HasMaxLength(64).IsRequired();
            entity.Property(company => company.StockCapital).HasMaxLength(64).IsRequired();
            entity.Property(company => company.DocumentFinalLine1).HasMaxLength(256).IsRequired();
            entity.Property(company => company.DocumentFinalLine2).HasMaxLength(256).IsRequired();
            entity.Property(company => company.TaxEntity).HasMaxLength(128).IsRequired();
            entity.Property(company => company.Fax).HasMaxLength(64).IsRequired();
            entity.Property(company => company.TicketFinalLine1).HasMaxLength(256).IsRequired();
            entity.Property(company => company.TicketFinalLine2).HasMaxLength(256).IsRequired();
            entity.Property(company => company.CurrencyCode).HasMaxLength(16).IsRequired();
            entity.Property(company => company.AgtLogo).IsRequired();
        });


        modelBuilder.Entity<ApiLicense>(entity =>
        {
            entity.ToTable("Licenses");
            entity.HasKey(license => license.Id);
            entity.Property(license => license.Version).HasMaxLength(64).IsRequired();
            entity.Property(license => license.HardwareId).HasMaxLength(64).IsRequired();
            entity.Property(license => license.Name).HasMaxLength(256).IsRequired();
            entity.Property(license => license.Company).HasMaxLength(256).IsRequired();
            entity.Property(license => license.Nif).HasMaxLength(64).IsRequired();
            entity.Property(license => license.Address).HasMaxLength(512).IsRequired();
            entity.Property(license => license.Email).HasMaxLength(256).IsRequired();
            entity.Property(license => license.Phone).HasMaxLength(64).IsRequired();
            entity.Property(license => license.Reseller).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<ApiTerminal>(entity =>
        {
            entity.ToTable("Terminals");
            entity.HasKey(terminal => terminal.Id);
            entity.HasIndex(terminal => terminal.HardwareId).IsUnique();
            entity.Property(terminal => terminal.Code).HasMaxLength(64).IsRequired();
            entity.Property(terminal => terminal.Designation).HasMaxLength(256).IsRequired();
            entity.Property(terminal => terminal.HardwareId).HasMaxLength(64).IsRequired();
            entity.Property(terminal => terminal.CreatedUtc).IsRequired();
            entity.Property(terminal => terminal.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiMovementType>(entity =>
        {
            entity.ToTable("MovementTypes");
            entity.HasKey(movementType => movementType.Id);
            entity.Property(movementType => movementType.Code).HasMaxLength(64).IsRequired();
            entity.Property(movementType => movementType.Designation).HasMaxLength(256).IsRequired();
            entity.Property(movementType => movementType.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(movementType => movementType.CreatedUtc).IsRequired();
            entity.Property(movementType => movementType.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiPlace>(entity =>
        {
            entity.ToTable("Places");
            entity.HasKey(place => place.Id);
            entity.Property(place => place.Code).HasMaxLength(64).IsRequired();
            entity.Property(place => place.Designation).HasMaxLength(256).IsRequired();
            entity.Property(place => place.ButtonImage).HasMaxLength(512);
            entity.Property(place => place.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(place => place.CreatedUtc).IsRequired();
            entity.Property(place => place.UpdatedUtc).IsRequired();
            entity.HasOne<ApiMovementType>()
                .WithMany()
                .HasForeignKey(place => place.MovementTypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiTable>(entity =>
        {
            entity.ToTable("Tables");
            entity.HasKey(table => table.Id);
            entity.Property(table => table.Code).HasMaxLength(64).IsRequired();
            entity.Property(table => table.Designation).HasMaxLength(256).IsRequired();
            entity.Property(table => table.ButtonImage).HasMaxLength(512);
            entity.Property(table => table.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(table => table.CreatedUtc).IsRequired();
            entity.Property(table => table.UpdatedUtc).IsRequired();
            entity.HasOne<ApiPlace>()
                .WithMany()
                .HasForeignKey(table => table.PlaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiHoliday>(entity =>
        {
            entity.ToTable("Holidays");
            entity.HasKey(holiday => holiday.Id);
            entity.Property(holiday => holiday.Code).HasMaxLength(64).IsRequired();
            entity.Property(holiday => holiday.Designation).HasMaxLength(256).IsRequired();
            entity.Property(holiday => holiday.Description).HasMaxLength(512).IsRequired();
            entity.Property(holiday => holiday.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(holiday => holiday.CreatedUtc).IsRequired();
            entity.Property(holiday => holiday.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiArticleClass>(entity =>
        {
            entity.ToTable("ArticleClasses");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Acronym).HasMaxLength(16).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiArticleFamily>(entity =>
        {
            entity.ToTable("ArticleFamilies");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiArticleSubfamily>(entity =>
        {
            entity.ToTable("ArticleSubfamilies");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.HasOne<ApiArticleFamily>()
                .WithMany()
                .HasForeignKey(item => item.FamilyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiArticleType>(entity =>
        {
            entity.ToTable("ArticleTypes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiMeasurementUnit>(entity =>
        {
            entity.ToTable("MeasurementUnits");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Acronym).HasMaxLength(16).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiSizeUnit>(entity =>
        {
            entity.ToTable("SizeUnits");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiVatRate>(entity =>
        {
            entity.ToTable("VatRates");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Value).HasColumnName("Tax_Percentage");
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.ReasonCode).HasMaxLength(64).IsRequired();
            entity.Property(item => item.TaxType).HasMaxLength(64).IsRequired();
            entity.Property(item => item.TaxCode).HasMaxLength(64).IsRequired();
            entity.Property(item => item.CountryRegion).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Description).HasMaxLength(512).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiArticle>(entity =>
        {
            entity.ToTable("Articles");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.HasOne<ApiArticleClass>()
                .WithMany()
                .HasForeignKey(item => item.ClassId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApiArticleSubfamily>()
                .WithMany()
                .HasForeignKey(item => item.SubfamilyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApiArticleType>()
                .WithMany()
                .HasForeignKey(item => item.TypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApiMeasurementUnit>()
                .WithMany()
                .HasForeignKey(item => item.MeasurementUnitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApiSizeUnit>()
                .WithMany()
                .HasForeignKey(item => item.SizeUnitId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApiVatRate>()
                .WithMany()
                .HasForeignKey(item => item.VatDirectSellingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiWarehouse>(entity =>
        {
            entity.ToTable("Warehouses");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiWarehouseLocation>(entity =>
        {
            entity.ToTable("WarehouseLocations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.HasOne<ApiWarehouse>()
                .WithMany()
                .HasForeignKey(item => item.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiWarehouseArticle>(entity =>
        {
            entity.ToTable("WarehouseArticles");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.HasOne<ApiArticle>()
                .WithMany()
                .HasForeignKey(item => item.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApiWarehouseLocation>()
                .WithMany()
                .HasForeignKey(item => item.WarehouseLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiStockMovement>(entity =>
        {
            entity.ToTable("StockMovements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DocumentNumber).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.HasMany(item => item.Items)
                .WithOne()
                .HasForeignKey(item => item.StockMovementId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiStockMovementItem>(entity =>
        {
            entity.ToTable("StockMovementItems");
            entity.HasKey(item => item.Id);
            entity.HasOne<ApiArticle>()
                .WithMany()
                .HasForeignKey(item => item.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiOrder>(entity =>
        {
            entity.ToTable("Orders");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.HasOne<ApiTable>()
                .WithMany()
                .HasForeignKey(item => item.TableId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.Tickets)
                .WithOne()
                .HasForeignKey(item => item.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiOrderTicket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.HasMany(item => item.Details)
                .WithOne()
                .HasForeignKey(item => item.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiOrderDetail>(entity =>
        {
            entity.ToTable("OrderDetails");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.HasOne<ApiArticle>()
                .WithMany()
                .HasForeignKey(item => item.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiArticleChild>(entity =>
        {
            entity.ToTable("ArticleCompositions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.ParentArticleId, item.ChildArticleId }).IsUnique();
            entity.HasOne<ApiArticle>()
                .WithMany()
                .HasForeignKey(item => item.ParentArticleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApiArticle>()
                .WithMany()
                .HasForeignKey(item => item.ChildArticleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiCountry>(entity =>
        {
            entity.ToTable("Countries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiCurrency>(entity =>
        {
            entity.ToTable("Currencies");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Symbol).HasMaxLength(8).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiWorkSessionPeriod>(entity =>
        {
            entity.ToTable("WorkSessionPeriods");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiWorkSessionMovement>(entity =>
        {
            entity.ToTable("WorkSessionMovements");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.HasOne<ApiWorkSessionPeriod>()
                .WithMany()
                .HasForeignKey(item => item.WorkSessionPeriodId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiPaymentMethod>(entity =>
        {
            entity.ToTable("PaymentMethods");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiFiscalYear>(entity =>
        {
            entity.ToTable("FiscalYears");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiDocumentType>(entity =>
        {
            entity.ToTable("DocumentTypes");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.Acronym).IsUnique();
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Acronym).HasMaxLength(16).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
        });

        modelBuilder.Entity<ApiDocumentSeries>(entity =>
        {
            entity.ToTable("DocumentSeries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            entity.HasOne<ApiDocumentType>()
                .WithMany()
                .HasForeignKey(item => item.DocumentTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApiFiscalYear>()
                .WithMany()
                .HasForeignKey(item => item.FiscalYearId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiDocument>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Type).HasMaxLength(16).IsRequired();
            entity.Property(item => item.Number).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Notes).HasMaxLength(1024).IsRequired();
            entity.Property(item => item.CreatedUtc).IsRequired();
            entity.Property(item => item.UpdatedUtc).IsRequired();
            // DocumentSeriesService.IssueNumberAsync increments NextNumber in memory with no row lock, so two
            // concurrent issues on the same series could otherwise both claim the same number. This unique
            // index (drafts excluded — they all share Number = "") turns that race into a clean, visible
            // failure (caught by the DbUpdateException handler in Program.cs) instead of silently producing
            // two fiscal documents with an identical sequential number.
            entity.HasIndex(item => item.Number).IsUnique().HasFilter("Number <> ''");
            entity.HasOne<ApiDocumentSeries>()
                .WithMany()
                .HasForeignKey(item => item.DocumentSeriesId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(item => item.Details)
                .WithOne()
                .HasForeignKey(item => item.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.PaymentMethods)
                .WithOne()
                .HasForeignKey(item => item.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApiDocumentDetail>(entity =>
        {
            entity.ToTable("DocumentDetails");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Code).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Designation).HasMaxLength(256).IsRequired();
            entity.HasOne<ApiArticle>()
                .WithMany()
                .HasForeignKey(item => item.ArticleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApiDocumentPaymentMethod>(entity =>
        {
            entity.ToTable("DocumentPaymentMethods");
            entity.HasKey(item => item.Id);
            entity.HasOne<ApiPaymentMethod>()
                .WithMany()
                .HasForeignKey(item => item.PaymentMethodId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        ApplyLegacyColumnMappings(modelBuilder);
        ApplySoftDeleteQueryFilters(modelBuilder);
    }

    private void ApplySoftDeleteRules()
    {
        var utcNow = DateTime.UtcNow;
        var trackedEntries = ChangeTracker.Entries()
            .Where(entry => entry.State == EntityState.Deleted && entry.Metadata.FindProperty("IsDeleted") is not null);

        foreach (var entry in trackedEntries)
        {
            entry.State = EntityState.Modified;
            entry.CurrentValues["IsDeleted"] = true;

            if (entry.Metadata.FindProperty("UpdatedUtc") is not null)
            {
                entry.CurrentValues["UpdatedUtc"] = utcNow;
            }

            if (entry.Metadata.FindProperty("DeletedAt") is not null)
            {
                entry.CurrentValues["DeletedAt"] = utcNow;
            }
        }
    }

    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isDeletedProperty = entityType.FindProperty("IsDeleted");
            if (isDeletedProperty is null || isDeletedProperty.ClrType != typeof(bool))
            {
                continue;
            }

            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "entity");
            var isDeletedExpression = System.Linq.Expressions.Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                [typeof(bool)],
                parameter,
                System.Linq.Expressions.Expression.Constant("IsDeleted"));
            var filterBody = System.Linq.Expressions.Expression.Equal(
                isDeletedExpression,
                System.Linq.Expressions.Expression.Constant(false));
            var lambda = System.Linq.Expressions.Expression.Lambda(filterBody, parameter);
            entityType.SetQueryFilter(lambda);
        }
    }

    private static void ApplyLegacyColumnMappings(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            MapIfExists(entityType, "CreatedUtc", "CreatedAt");
            MapIfExists(entityType, "UpdatedUtc", "UpdatedAt");
            MapIfExists(entityType, "OpennedAtUtc", "OpennedAt");
            MapIfExists(entityType, "ButtonImage", "Button_Image");
            MapIfExists(entityType, "Price1Value", "Price1_Value");
            MapIfExists(entityType, "Price1PromotionValue", "Price1_PromotionValue");
            MapIfExists(entityType, "Price1UsePromotion", "Price1_UsePromotion");
            MapIfExists(entityType, "Price2Value", "Price2_Value");
            MapIfExists(entityType, "Price2PromotionValue", "Price2_PromotionValue");
            MapIfExists(entityType, "Price2UsePromotion", "Price2_UsePromotion");
            MapIfExists(entityType, "Price3Value", "Price3_Value");
            MapIfExists(entityType, "Price3PromotionValue", "Price3_PromotionValue");
            MapIfExists(entityType, "Price3UsePromotion", "Price3_UsePromotion");
            MapIfExists(entityType, "Price4Value", "Price4_Value");
            MapIfExists(entityType, "Price4PromotionValue", "Price4_PromotionValue");
            MapIfExists(entityType, "Price4UsePromotion", "Price4_UsePromotion");
            MapIfExists(entityType, "Price5Value", "Price5_Value");
            MapIfExists(entityType, "Price5PromotionValue", "Price5_PromotionValue");
            MapIfExists(entityType, "Price5UsePromotion", "Price5_UsePromotion");
            MapIfExists(entityType, "CustomerName", "Customer_Name");
            MapIfExists(entityType, "CustomerFiscalNumber", "Customer_FiscalNumber");
            MapIfExists(entityType, "CustomerAddress", "Customer_Address");
            MapIfExists(entityType, "CustomerLocality", "Customer_Locality");
            MapIfExists(entityType, "CustomerZipCode", "Customer_ZipCode");
            MapIfExists(entityType, "CustomerCity", "Customer_City");
            MapIfExists(entityType, "CustomerEmail", "Customer_Email");
            MapIfExists(entityType, "CustomerPhone", "Customer_Phone");
            MapIfExists(entityType, "VatPercentage", "Tax_Percentage");
        }
    }

    private static void MapIfExists(IMutableEntityType entityType, string propertyName, string columnName)
    {
        var property = entityType.FindProperty(propertyName);
        if (property is not null)
        {
            property.SetColumnName(columnName);
        }
    }
}
