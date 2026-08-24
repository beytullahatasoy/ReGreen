using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ReGreen.Api.ExceptionHandling;

/// <summary>
/// Minimal API'nin kendi query binding'i, sayısal olmayan bir değer verildiğinde
/// (ör. `?recovery=abc`) `BadHttpRequestException` fırlatır — bu, `AddProblemDetails()`
/// fallback'ine düşerse `500 UNEXPECTED_ERROR` olur (gerçek bug, düzeltildi). Burada
/// yakalanıp `BadHttpRequestException.StatusCode` (binding hataları için 400) ile
/// `INVALID_QUERY_PARAMETER` olarak dönülür — api-contract.md'deki mevcut kodla tutarlı.
/// </summary>
public sealed class InvalidQueryParameterExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequest)
            return false;

        httpContext.Response.StatusCode = badRequest.StatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Type = "https://regreen/errors/invalid-query-parameter",
                Title = "Geçersiz sorgu parametresi",
                Status = badRequest.StatusCode,
                Detail = "Bir veya daha fazla query parametresi beklenen türde değil.",
                Extensions = { ["code"] = "INVALID_QUERY_PARAMETER" },
            },
        });
    }
}
