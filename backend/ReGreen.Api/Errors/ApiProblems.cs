namespace ReGreen.Api.Errors;

/// <summary>
/// RFC 7807 ProblemDetails yanıtları için tek merkez — bkz. docs/api-contract.md
/// hata kodları tablosu. Her endpoint kendi 404/400'lerini burada üretir; 503/500
/// (DB'ye ulaşılamıyor / beklenmeyen hata) ReGreen.Api/ExceptionHandling üzerinden gelir.
/// </summary>
internal static class ApiProblems
{
    public static IResult FireNotFound(string fireId) => Problem(
        StatusCodes.Status404NotFound, "FIRE_NOT_FOUND", "Yangın bulunamadı",
        $"'{fireId}' kimlikli yangın bulunamadı.", "fire-not-found");

    public static IResult ModelRunNotFound(string fireId) => Problem(
        StatusCodes.Status404NotFound, "MODEL_RUN_NOT_FOUND", "Model run bulunamadı",
        $"'{fireId}' için henüz içe aktarılmış bir model run yok.", "model-run-not-found");

    public static IResult CellNotFound(string fireId, string cellId) => Problem(
        StatusCodes.Status404NotFound, "CELL_NOT_FOUND", "Hücre bulunamadı",
        $"'{fireId}' yangınında '{cellId}' kimlikli hücre bulunamadı.", "cell-not-found");

    public static IResult CellVerdictNotFound(string cellId) => Problem(
        StatusCodes.Status404NotFound, "CELL_VERDICT_NOT_FOUND", "Hüküm bulunamadı",
        $"'{cellId}' hücresi için henüz içe aktarılmış bir hüküm yok.", "cell-verdict-not-found");

    public static IResult FireNarrativeNotFound(string fireId) => Problem(
        StatusCodes.Status404NotFound, "FIRE_NARRATIVE_NOT_FOUND", "Yangın özeti bulunamadı",
        $"'{fireId}' için henüz içe aktarılmış bir yangın özeti (anlatı) yok.", "fire-narrative-not-found");

    public static IResult HukumSozluguNotFound() => Problem(
        StatusCodes.Status404NotFound, "HUKUM_SOZLUGU_NOT_FOUND", "Hüküm sözlüğü bulunamadı",
        "Hüküm sözlüğü henüz içe aktarılmamış.", "hukum-sozlugu-not-found");

    public static IResult InvalidPriorityWeights(string detail) => Problem(
        StatusCodes.Status400BadRequest, "INVALID_PRIORITY_WEIGHTS", "Geçersiz öncelik ağırlıkları",
        detail, "invalid-priority-weights");

    public static IResult InvalidBoundingBox(string detail) => Problem(
        StatusCodes.Status400BadRequest, "INVALID_BOUNDING_BOX", "Geçersiz sınırlayıcı kutu",
        detail, "invalid-bounding-box");

    public static IResult InvalidQueryParameter(string detail) => Problem(
        StatusCodes.Status400BadRequest, "INVALID_QUERY_PARAMETER", "Geçersiz sorgu parametresi",
        detail, "invalid-query-parameter");

    public static IResult PerimeterDataCorrupt(string fireId) => Problem(
        StatusCodes.Status500InternalServerError, "PERIMETER_DATA_CORRUPT", "Yangın sınırı verisi bozuk",
        $"'{fireId}' için saklanan sınır GeoJSON'u ayrıştırılamadı.", "perimeter-data-corrupt");

    private static IResult Problem(int status, string code, string title, string detail, string typeSlug) =>
        Results.Problem(
            statusCode: status,
            title: title,
            detail: detail,
            type: $"https://regreen/errors/{typeSlug}",
            extensions: new Dictionary<string, object?> { ["code"] = code });
}
