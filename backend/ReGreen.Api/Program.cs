using Microsoft.EntityFrameworkCore;
using ReGreen.Api.Endpoints;
using ReGreen.Api.ExceptionHandling;
using ReGreen.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Bağlantı dizesi önceliği (ImportTool ile tutarlı — bkz. ImportTool/Orchestrator.cs):
// REGREEN_CONNECTION_STRING env var -> appsettings ConnectionStrings:Default -> localdb fallback.
var connectionString =
    Environment.GetEnvironmentVariable("REGREEN_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=ReGreen;Trusted_Connection=True;";

// Migration BİLEREK burada çalıştırılmaz — şema `dotnet ef database update` ile elle uygulanır.
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        // Boş liste = hiçbir origin'e izin verme (wildcard KULLANILMAZ, dev dahil —
        // appsettings.Development.json'da izin verilen dev port'ları açıkça listelenir).
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddExceptionHandler<InvalidQueryParameterExceptionHandler>();
builder.Services.AddExceptionHandler<DbUnavailableExceptionHandler>();
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ProblemDetailsCustomization.EnsureCodeExtension);

var app = builder.Build();

// Sıra önemli: özel handler (DB -> 503) önce denenir, aksi halde AddProblemDetails()
// genel istisnalar için 500 ProblemDetails fallback'i üretir.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Default");

app.MapFireEndpoints();
app.MapHealthEndpoints();

app.Run();

public partial class Program;
