using Microsoft.EntityFrameworkCore;
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

public static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // 1. Configurar Kestrel para http://127.0.0.1:5001 (exatamente como na API original)
        builder.WebHost.UseUrls("http://127.0.0.1:5001");

        // 2. Configurar a Base de Dados SQLite
        builder.Configuration["ConnectionStrings:DefaultConnection"] = "Data Source=logicpos.db";

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // 3. Ativar Scalar na Raiz (Interface visual igual à original)
        app.UseSwagger(options =>
        {
            options.RouteTemplate = "openapi/{documentName}/swagger.json";
        });

        app.MapScalarApiReference(options =>
        {
            options.Title = "LogicPOS API";
            options.Theme = ScalarTheme.Purple;
        });

        app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();

        app.MapSystemEndpoints();
        app.MapAuthEndpoints();
        app.MapPosEndpoints();
        app.MapFinanceEndpoints();

        app.Run();
    }
}
