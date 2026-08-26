using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using ReGreen.Api.Endpoints;
using ReGreen.Api.ExceptionHandling;
using ReGreen.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// /cells en büyük yangında (~9048 hücre) sıkıştırılmamış ~4 MB JSON döner — sıkıştırma
// bunu kalıcı olarak küçültür (model/frontend değişse de geçerliliğini korur). BREACH/CRIME
// riski burada YOK: API'de auth/secret yok, response gövdesine yansıyan gizli bir değer
// bulunmuyor (salt-okunur, herkese açık yangın verisi) — HTTPS için de güvenle açılabilir.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/problem+json"]);
});
// Brotli'nin varsayılan seviyesi (Optimal) ~4 MB'lık dinamik JSON'da saniyeler sürüyor —
// ölçüldü: response compression ile /cells gecikmesi ~100ms'den ~4200ms'ye çıktı. Fastest,
// boyut kazancının büyük kısmını korurken gecikmeyi normale döndürüyor (bkz. backend/scripts/perf-check.ps1).
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

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

// Yerel geliştirme sözleşmesi bilinçli olarak http://localhost:5066 kullanır. HTTP-only
// launch profilinde HTTPS portu olmadığı için middleware her istekte yanıltıcı bir uyarı
// üretiyordu. Production ortamında yönlendirme korunur.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseResponseCompression();
app.UseCors("Default");

app.MapFireEndpoints();
app.MapHealthEndpoints();

app.Run();

public partial class Program;
