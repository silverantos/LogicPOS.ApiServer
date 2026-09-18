using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using LogicPOS.ApiServer.Endpoints;

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

// ==========================================
// 4. ENDPOINTS CRÍTICOS DO SISTEMA (LogicPOS)
// ==========================================

// Endpoint de Versão real esperado pelo cliente POS:
app.MapGet("/system/api-version", () => "1.5.2 retail");

// Informações básicas do sistema
app.MapGet("/system/information", () => new
{
    Culture = "pt-PT",
    Country = "pt",
    DatabaseModule = "SQLite",
    BootstrapInfo = "OK"
});

// Autenticação (Login por PIN ou Utilizador)
app.MapPost("/auth/login", () => Results.Ok("token-jwt-simulado-logicpos"));
app.MapPost("/auth/sign-in", () => Results.Ok("token-jwt-simulado-logicpos"));

// Licensing e Hardware
app.MapGet("/licensing/hardware-id", () => new { hardwareId = "LINUX-DEBIAN-LOCAL" });
app.MapGet("/licensing/data", () => new { status = "Active", edition = "Retail", seats = 1 });

// ==========================================
// 5. EXEMPLOS DE OUTRAS ROTAS DA TUA API
// ==========================================
app.MapGet("/users", async (AppDbContext db) => Results.Ok(await db.Users.ToListAsync()));
app.MapGet("/terminals", async (AppDbContext db) => Results.Ok(await db.Terminals.ToListAsync()));
app.MapGet("/vat-rates", async (AppDbContext db) => Results.Ok(await db.VatRates.ToListAsync()));
app.MapSystemEndpoints();

app.Run();

// --- DbContext e Modelos Genéricos ---
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
