using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ReGreen.Api.ExceptionHandling;

/// <summary>
/// DB'ye ulaşılamadığında (bağlantı koptu/kurulamadı) 503 + ProblemDetails döner.
/// `IProblemDetailsService` üzerinden yazılır — böylece `Content-Type: application/problem+json`
/// ve genel `CustomizeProblemDetails` hook'u (bkz. Program.cs) otomatik uygulanır.
/// Diğer beklenmeyen istisnalar burada YAKALANMAZ (false döner) — onlar için
/// `AddProblemDetails()` zaten 500 fallback'i üretir.
/// </summary>
public sealed class DbUnavailableExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbException)
            return false;

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://regreen/errors/db-unavailable",
                Title = "Veritabanına ulaşılamıyor",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "Veritabanı bağlantısı kurulamadı veya sorgu sırasında kesildi.",
                Extensions = { ["code"] = "DB_UNAVAILABLE" },
            },
        });
    }
}
