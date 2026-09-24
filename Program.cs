using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using LogicPOS.ApiServer.Endpoints;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Terminal> Terminals => Set<Terminal>();
    public DbSet<VatRate> VatRates => Set<VatRate>();
}

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Terminal
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class VatRate
{
    public Guid Id { get; set; }
    public decimal Rate { get; set; }
}

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 1. Configurar a Base de Dados SQLite
        var dbPath = Path.Combine(builder.Environment.ContentRootPath, "logicpos.db");
        builder.Configuration["ConnectionStrings:DefaultConnection"] = $"Data Source={dbPath}";

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("BasicAuth", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "basic",
                Description = "Basic Authentication (docs / docs)"
            });

            options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("BasicAuth", doc),
                    new List<string>()
                }
            });
        });

        var app = builder.Build();

        // 2. Ativar Scalar na Raiz (Interface visual igual à original)
        app.UseSwagger(options =>
        {
            options.RouteTemplate = "openapi/{documentName}.json";
        });

        app.MapScalarApiReference(options =>
        {
            options.Title = "LogicPOS API";
            options.Theme = ScalarTheme.Purple;
            options.WithPreferredSecurityScheme("BasicAuth");
        });

        app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

        app.MapSystemEndpoints();
        app.MapAuthEndpoints();
        app.MapPosEndpoints();
        app.MapFinanceEndpoints();
        app.MapCompanyEndpoints();
        app.MapReportsEndpoints();
        app.MapWorkSessionEndpoints();
        app.MapLegacyReadEndpoints();
        app.MapOrderEndpoints();

        app.Run();
    }
}

public static class ScalarApiReferenceExtensions
{
    public static ScalarOptions WithPreferredSecurityScheme(this ScalarOptions options, string scheme)
    {
        return options.AddPreferredSecuritySchemes(new[] { scheme });
    }
}
