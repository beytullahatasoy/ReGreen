using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ReGreen.Api.ExceptionHandling;
using Xunit;

namespace ReGreen.Api.Tests.Unit;

/// <summary>
/// api-contract.md §5: "tüm hatalarda code alanı bulunur". Gerçek uçtan uca bir
/// beklenmeyen istisna tetiklemek kırılgan/gereksiz karmaşık olacağından, ASP.NET
/// Core'un genel 500 fallback'ine eklenen bu davranış doğrudan test ediliyor.
/// </summary>
public class ProblemDetailsCustomizationTests
{
    [Fact]
    public void EnsureCodeExtension_NoCodeSet_AddsUnexpectedErrorCode()
    {
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = new ProblemDetails(),
        };

        ProblemDetailsCustomization.EnsureCodeExtension(context);

        Assert.Equal("UNEXPECTED_ERROR", context.ProblemDetails.Extensions["code"]);
    }

    [Fact]
    public void EnsureCodeExtension_CodeAlreadySet_DoesNotOverwrite()
    {
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = new ProblemDetails { Extensions = { ["code"] = "FIRE_NOT_FOUND" } },
        };

        ProblemDetailsCustomization.EnsureCodeExtension(context);

        Assert.Equal("FIRE_NOT_FOUND", context.ProblemDetails.Extensions["code"]);
    }
}
