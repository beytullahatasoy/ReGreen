using Microsoft.AspNetCore.Http;

namespace ReGreen.Api.ExceptionHandling;

/// <summary>
/// docs/api-contract.md: "tüm hatalarda code alanı bulunur". `DbUnavailableExceptionHandler`
/// kendi code'unu (DB_UNAVAILABLE) zaten koyuyor; bu SADECE onun dışında kalan, gerçekten
/// beklenmeyen istisnalar için (AddProblemDetails'in kendi 500 fallback'i) bir varsayılan
/// sağlar — var olan bir code ASLA ezilmez. Program.cs'den ayrı bir dosyada, doğrudan
/// unit test edilebilsin diye (bkz. ReGreen.Api.Tests/Unit).
/// </summary>
public static class ProblemDetailsCustomization
{
    public static void EnsureCodeExtension(ProblemDetailsContext context)
    {
        if (!context.ProblemDetails.Extensions.ContainsKey("code"))
            context.ProblemDetails.Extensions["code"] = "UNEXPECTED_ERROR";
    }
}
