using Microsoft.EntityFrameworkCore;
using ReGreen.Api.Dtos;
using ReGreen.Api.Errors;
using ReGreen.Data;
using ReGreen.Data.Entities;

namespace ReGreen.Api.Endpoints;

/// <summary>
/// Organisation ve Community ekranlarının uç noktaları — docs/topluluk_veri_sozlesmesi.md.
/// <see cref="FireEndpoints"/> (model/tahmin) ve <see cref="HukumEndpoints"/> (hüküm)
/// kapsamlarının dışında kaldığı için AYRI dosyada.
///
/// <b>Bu katman modele hiçbir şey yazmaz.</b> Gözlem ve katılım kayıtları
/// Predictions/CellVerdicts tablolarına dokunmaz; yangına yalnızca <c>FireId</c>
/// üzerinden bağlanırlar. Bir gönüllünün yazdığı hiçbir şey öncelik skorunu,
/// hükmü veya tahmini değiştiremez — bu ayrım bilinçlidir ve şema seviyesinde
/// zorlanır (topluluk tablolarında ModelRunId yoktur).
///
/// Kimlik doğrulama YOK: kurum kimliği istekte gelir, gönüllü kimliği ilk kayıtta
/// sunucunun ürettiği GUID'dir. Oturum katmanı eklendiğinde değişecek tek şey bu
/// kimliklerin nereden okunduğudur; tablolar ve uç noktalar aynı kalır.
/// </summary>
public static class CommunityEndpoints
{
    /// <summary>Liste uç noktalarında tek seferde dönebilecek en fazla kayıt.</summary>
    private const int MaxLimit = 200;
    private const int DefaultLimit = 50;

    public static void MapCommunityEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/organisations", GetOrganisations)
            .Produces<OrganisationDto[]>()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapPost("/api/volunteers", CreateVolunteer)
            .Produces<VolunteerDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/volunteers/{id:guid}", GetVolunteer)
            .Produces<VolunteerDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/activities", GetActivities)
            .Produces<FieldActivityDto[]>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapPost("/api/activities", CreateActivity)
            .Produces<FieldActivityDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapPatch("/api/activities/{id:int}", UpdateActivity)
            .Produces<FieldActivityDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapPost("/api/activities/{id:int}/participants", JoinActivity)
            .Produces<FieldActivityDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapDelete("/api/activities/{id:int}/participants/{volunteerId:guid}", LeaveActivity)
            .Produces<FieldActivityDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapGet("/api/observations", GetObservations)
            .Produces<FieldObservationDto[]>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapPost("/api/fires/{fireId}/observations", CreateObservation)
            .Produces<FieldObservationDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        app.MapPatch("/api/observations/{id:int}", ReviewObservation)
            .Produces<FieldObservationDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }

    // ------------------------------------------------------------- kurumlar

    private static async Task<IResult> GetOrganisations(AppDbContext db, CancellationToken ct) =>
        Results.Ok(await db.Organisations.AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new OrganisationDto(o.Id, o.Name, o.ContactEmail, o.Verified))
            .ToArrayAsync(ct));

    // ------------------------------------------------------------ gönüllüler

    private static async Task<IResult> CreateVolunteer(
        CreateVolunteerRequest? request, AppDbContext db, CancellationToken ct)
    {
        var alias = request?.Alias?.Trim();
        if (alias is { Length: > 60 })
            return ApiProblems.InvalidRequestBody("alias en fazla 60 karakter olabilir.");

        var volunteer = new Volunteer
        {
            Id = Guid.NewGuid(),
            // Ad verilmezse okunabilir bir takma ad üretilir: kuyrukta "bilinmeyen"
            // yerine bir şey görünsün, ama kişisel veri istemek zorunda kalmayalım.
            Alias = string.IsNullOrWhiteSpace(alias)
                ? $"Gönüllü {Random.Shared.Next(100, 1000)}"
                : alias,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Volunteers.Add(volunteer);
        await db.SaveChangesAsync(ct);

        var dto = new VolunteerDto(volunteer.Id, volunteer.Alias, volunteer.CreatedAt);
        return Results.Created($"/api/volunteers/{volunteer.Id}", dto);
    }

    private static async Task<IResult> GetVolunteer(Guid id, AppDbContext db, CancellationToken ct)
    {
        var volunteer = await db.Volunteers.AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new VolunteerDto(v.Id, v.Alias, v.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return volunteer is null ? ApiProblems.VolunteerNotFound(id) : Results.Ok(volunteer);
    }

    // ------------------------------------------------------------ etkinlikler

    private static async Task<IResult> GetActivities(
        AppDbContext db, string? fire_id, string? status, Guid? volunteer_id, int? limit,
        CancellationToken ct)
    {
        if (status is not null && !ActivityStatuses.All.Contains(status))
            return ApiProblems.InvalidQueryParameter(
                $"status şu değerlerden biri olmalıdır: {string.Join(", ", ActivityStatuses.All)}.");

        if (!TryGetLimit(limit, out var take, out var limitError))
            return limitError!;

        var query = db.FieldActivities.AsNoTracking();
        if (fire_id is not null) query = query.Where(a => a.FireId == fire_id);
        if (status is not null) query = query.Where(a => a.Status == status);

        var activities = await query
            .OrderBy(a => a.ScheduledFor)
            .ThenBy(a => a.Id)
            .Take(take)
            .Select(ActivityProjection(volunteer_id))
            .ToArrayAsync(ct);

        return Results.Ok(activities);
    }

    private static async Task<IResult> CreateActivity(
        CreateActivityRequest? request, AppDbContext db, CancellationToken ct)
    {
        if (request is null)
            return ApiProblems.InvalidRequestBody("İstek gövdesi boş olamaz.");

        if (string.IsNullOrWhiteSpace(request.FireId))
            return ApiProblems.InvalidRequestBody("fire_id zorunludur.");
        if (request.Kind is null || !ActivityKinds.All.Contains(request.Kind))
            return ApiProblems.InvalidRequestBody(
                $"kind şu değerlerden biri olmalıdır: {string.Join(", ", ActivityKinds.All)}.");
        if (string.IsNullOrWhiteSpace(request.Title))
            return ApiProblems.InvalidRequestBody("title zorunludur.");
        if (string.IsNullOrWhiteSpace(request.MeetingPoint))
            return ApiProblems.InvalidRequestBody("meeting_point zorunludur.");
        if (request.ScheduledFor is null)
            return ApiProblems.InvalidRequestBody("scheduled_for zorunludur.");
        if (request.Capacity is not (> 0 and <= 1000))
            return ApiProblems.InvalidRequestBody("capacity 1 ile 1000 arasında olmalıdır.");

        if (!await db.Fires.AsNoTracking().AnyAsync(f => f.FireId == request.FireId, ct))
            return ApiProblems.FireNotFound(request.FireId);

        // Kurum verilmezse tek kurumlu kurulumda ilk kurum kullanılır — oturum
        // katmanı gelene kadarki bilinçli geçici çözüm (bkz. sınıf açıklaması).
        var organisationId = request.OrganisationId
            ?? await db.Organisations.AsNoTracking().OrderBy(o => o.Id).Select(o => (int?)o.Id).FirstOrDefaultAsync(ct)
            ?? 0;
        if (!await db.Organisations.AsNoTracking().AnyAsync(o => o.Id == organisationId, ct))
            return ApiProblems.OrganisationNotFound(organisationId);

        var activity = new FieldActivity
        {
            FireId = request.FireId,
            OrganisationId = organisationId,
            Kind = request.Kind,
            Title = Cut(request.Title.Trim(), 150),
            Description = Cut(request.Description?.Trim() ?? "", 1000),
            ScheduledFor = request.ScheduledFor.Value,
            MeetingPoint = Cut(request.MeetingPoint.Trim(), 200),
            Capacity = request.Capacity.Value,
            Requirements = JoinList(request.Requirements, 500),
            Status = ActivityStatuses.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.FieldActivities.Add(activity);
        await db.SaveChangesAsync(ct);

        var dto = await LoadActivity(db, activity.Id, ct);
        return Results.Created($"/api/activities/{activity.Id}", dto);
    }

    private static async Task<IResult> UpdateActivity(
        int id, UpdateActivityRequest? request, AppDbContext db, CancellationToken ct)
    {
        if (request is null)
            return ApiProblems.InvalidRequestBody("İstek gövdesi boş olamaz.");

        var activity = await db.FieldActivities.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (activity is null)
            return ApiProblems.ActivityNotFound(id);

        if (request.Status is not null)
        {
            if (!ActivityStatuses.All.Contains(request.Status))
                return ApiProblems.InvalidRequestBody(
                    $"status şu değerlerden biri olmalıdır: {string.Join(", ", ActivityStatuses.All)}.");
            activity.Status = request.Status;
        }

        if (request.Capacity is not null)
        {
            if (request.Capacity is not (> 0 and <= 1000))
                return ApiProblems.InvalidRequestBody("capacity 1 ile 1000 arasında olmalıdır.");

            // Kontenjan mevcut katılımın altına çekilemez: aksi halde kayıtlı
            // gönüllülerin bir kısmı sessizce "fazlalık" olurdu.
            var joined = await db.ActivityParticipants.CountAsync(p => p.ActivityId == id, ct);
            if (request.Capacity < joined)
                return ApiProblems.InvalidRequestBody(
                    $"capacity mevcut katılımcı sayısının ({joined}) altına indirilemez.");
            activity.Capacity = request.Capacity.Value;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await LoadActivity(db, id, ct));
    }

    private static async Task<IResult> JoinActivity(
        int id, JoinActivityRequest? request, AppDbContext db, CancellationToken ct)
    {
        if (request?.VolunteerId is null)
            return ApiProblems.InvalidRequestBody("volunteer_id zorunludur.");
        var volunteerId = request.VolunteerId.Value;

        var activity = await db.FieldActivities.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.Id, a.Status, a.Capacity })
            .FirstOrDefaultAsync(ct);
        if (activity is null)
            return ApiProblems.ActivityNotFound(id);

        if (!await db.Volunteers.AsNoTracking().AnyAsync(v => v.Id == volunteerId, ct))
            return ApiProblems.VolunteerNotFound(volunteerId);

        var already = await db.ActivityParticipants.AsNoTracking()
            .AnyAsync(p => p.ActivityId == id && p.VolunteerId == volunteerId, ct);

        // Zaten kayıtlıysa istek başarılı sayılır: "katıl" düğmesine ikinci kez
        // basmak hata vermemeli, sonuç aynı durumdur (idempotent).
        if (!already)
        {
            if (!ActivityStatuses.AcceptsParticipants(activity.Status))
                return ApiProblems.ActivityNotOpen(id, activity.Status);

            var joined = await db.ActivityParticipants.CountAsync(p => p.ActivityId == id, ct);
            if (joined >= activity.Capacity)
                return ApiProblems.ActivityFull(id, activity.Capacity);

            db.ActivityParticipants.Add(new ActivityParticipant
            {
                ActivityId = id,
                VolunteerId = volunteerId,
                JoinedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(await LoadActivity(db, id, ct, volunteerId));
    }

    private static async Task<IResult> LeaveActivity(
        int id, Guid volunteerId, AppDbContext db, CancellationToken ct)
    {
        if (!await db.FieldActivities.AsNoTracking().AnyAsync(a => a.Id == id, ct))
            return ApiProblems.ActivityNotFound(id);

        var participant = await db.ActivityParticipants
            .FirstOrDefaultAsync(p => p.ActivityId == id && p.VolunteerId == volunteerId, ct);

        // Kayıt yoksa da sonuç aynı: gönüllü artık katılımcı değil (idempotent).
        if (participant is not null)
        {
            db.ActivityParticipants.Remove(participant);
            await db.SaveChangesAsync(ct);
        }

        return Results.Ok(await LoadActivity(db, id, ct));
    }

    // --------------------------------------------------------------- gözlemler

    private static async Task<IResult> GetObservations(
        AppDbContext db, string? status, string? fire_id, Guid? volunteer_id, int? limit, CancellationToken ct)
    {
        if (status is not null && !ObservationStatuses.All.Contains(status))
            return ApiProblems.InvalidQueryParameter(
                $"status şu değerlerden biri olmalıdır: {string.Join(", ", ObservationStatuses.All)}.");

        if (!TryGetLimit(limit, out var take, out var limitError))
            return limitError!;

        var query = db.FieldObservations.AsNoTracking();
        if (status is not null) query = query.Where(o => o.Status == status);
        if (fire_id is not null) query = query.Where(o => o.FireId == fire_id);
        if (volunteer_id is not null) query = query.Where(o => o.VolunteerId == volunteer_id);

        var observations = await query
            // Kurum kuyruğu: karar bekleyenler önce, sonra en yeni.
            .OrderBy(o => o.Status == ObservationStatuses.Pending ? 0 : 1)
            .ThenByDescending(o => o.SubmittedAt)
            .ThenByDescending(o => o.Id)
            .Take(take)
            .Select(ObservationProjection())
            .ToArrayAsync(ct);

        return Results.Ok(observations);
    }

    private static async Task<IResult> CreateObservation(
        string fireId, CreateObservationRequest? request, AppDbContext db, CancellationToken ct)
    {
        if (request?.VolunteerId is null)
            return ApiProblems.InvalidRequestBody("volunteer_id zorunludur.");

        // Cevapsız gözlem kanıt değil, kuyrukta gürültüdür — DB CHECK'i de aynı
        // kuralı uyguluyor, burada kullanıcıya anlaşılır hata olarak dönüyor.
        var answers = (request.Answers ?? [])
            .Select(a => a?.Trim())
            .Where(a => !string.IsNullOrEmpty(a))
            .Select(a => a!.Replace('|', '/'))
            .ToArray();
        if (answers.Length == 0)
            return ApiProblems.InvalidRequestBody("En az bir cevap (answers) verilmelidir.");

        if (string.IsNullOrWhiteSpace(request.Location))
            return ApiProblems.InvalidRequestBody("location zorunludur.");

        if (!await db.Fires.AsNoTracking().AnyAsync(f => f.FireId == fireId, ct))
            return ApiProblems.FireNotFound(fireId);

        var volunteerId = request.VolunteerId.Value;
        if (!await db.Volunteers.AsNoTracking().AnyAsync(v => v.Id == volunteerId, ct))
            return ApiProblems.VolunteerNotFound(volunteerId);

        if (request.ActivityId is not null)
        {
            var activityMatches = await db.FieldActivities.AsNoTracking()
                .AnyAsync(a => a.Id == request.ActivityId && a.FireId == fireId, ct);
            if (!activityMatches)
                return ApiProblems.InvalidRequestBody(
                    $"activity_id {request.ActivityId}, '{fireId}' yangınına ait bir etkinlik değil.");
        }

        var observation = new FieldObservation
        {
            FireId = fireId,
            ActivityId = request.ActivityId,
            VolunteerId = volunteerId,
            Location = Cut(request.Location.Trim(), 200),
            PhotoName = Truncate(request.PhotoName?.Trim(), 260),
            Answers = Cut(string.Join('|', answers), 1000),
            Note = Truncate(request.Note?.Trim(), 1000),
            Status = ObservationStatuses.Pending,
            SubmittedAt = DateTimeOffset.UtcNow,
        };

        db.FieldObservations.Add(observation);
        await db.SaveChangesAsync(ct);

        var dto = await LoadObservation(db, observation.Id, ct);
        return Results.Created($"/api/observations/{observation.Id}", dto);
    }

    private static async Task<IResult> ReviewObservation(
        int id, ReviewObservationRequest? request, AppDbContext db, CancellationToken ct)
    {
        if (request?.Status is null)
            return ApiProblems.InvalidRequestBody("status zorunludur.");
        if (!ObservationStatuses.All.Contains(request.Status))
            return ApiProblems.InvalidRequestBody(
                $"status şu değerlerden biri olmalıdır: {string.Join(", ", ObservationStatuses.All)}.");

        var observation = await db.FieldObservations.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (observation is null)
            return ApiProblems.ObservationNotFound(id);

        if (request.OrganisationId is not null
            && !await db.Organisations.AsNoTracking().AnyAsync(o => o.Id == request.OrganisationId, ct))
            return ApiProblems.OrganisationNotFound(request.OrganisationId.Value);

        observation.Status = request.Status;
        observation.ReviewNote = Truncate(request.ReviewNote?.Trim(), 500);
        observation.ReviewedByOrganisationId = request.OrganisationId;
        // CHECK kısıtı: 'pending' dışındaki her durumun bir karar zamanı olmalı.
        observation.ReviewedAt = request.Status == ObservationStatuses.Pending
            ? null
            : DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Results.Ok(await LoadObservation(db, id, ct));
    }

    // ------------------------------------------------------------ yardımcılar

    /// <summary>
    /// Etkinlik projeksiyonu. <c>Joined</c> ve <c>ObservationCount</c> ilişkili
    /// satırlardan sayılır; tek sorguda dönmesi için ayrı bir tur atılmaz.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<FieldActivity, FieldActivityDto>> ActivityProjection(
        Guid? volunteerId = null) =>
        a => new FieldActivityDto(
            a.Id, a.FireId, a.Fire.Province, a.OrganisationId, a.Organisation.Name,
            a.Organisation.Verified, a.Kind, a.Title, a.Description, a.ScheduledFor,
            a.MeetingPoint, a.Capacity, a.Participants.Count,
            a.Requirements == null || a.Requirements == ""
                ? new string[0]
                : a.Requirements.Split('|', StringSplitOptions.None),
            a.Status, a.Observations.Count,
            volunteerId != null && a.Participants.Any(p => p.VolunteerId == volunteerId));

    private static System.Linq.Expressions.Expression<Func<FieldObservation, FieldObservationDto>> ObservationProjection() =>
        o => new FieldObservationDto(
            o.Id, o.FireId, o.Fire.Province, o.ActivityId,
            o.Activity == null ? null : o.Activity.Title,
            o.VolunteerId, o.Volunteer.Alias, o.Location, o.PhotoName,
            o.Answers.Split('|', StringSplitOptions.None),
            o.Note, o.Status, o.ReviewNote,
            o.ReviewedByOrganisation == null ? null : o.ReviewedByOrganisation.Name,
            o.SubmittedAt, o.ReviewedAt);

    private static Task<FieldActivityDto?> LoadActivity(
        AppDbContext db, int id, CancellationToken ct, Guid? volunteerId = null) =>
        db.FieldActivities.AsNoTracking().Where(a => a.Id == id)
            .Select(ActivityProjection(volunteerId)).FirstOrDefaultAsync(ct);

    private static Task<FieldObservationDto?> LoadObservation(AppDbContext db, int id, CancellationToken ct) =>
        db.FieldObservations.AsNoTracking().Where(o => o.Id == id).Select(ObservationProjection()).FirstOrDefaultAsync(ct);

    private static bool TryGetLimit(int? limit, out int take, out IResult? error)
    {
        take = DefaultLimit;
        error = null;
        if (limit is null) return true;

        if (limit is < 1 or > MaxLimit)
        {
            error = ApiProblems.InvalidQueryParameter($"limit 1 ile {MaxLimit} arasında olmalıdır.");
            return false;
        }

        take = limit.Value;
        return true;
    }

    /// <summary>'|' ayraçlı listeye çevirir; ayraç girdide varsa temizlenir.</summary>
    private static string? JoinList(string[]? values, int maxLength)
    {
        var cleaned = (values ?? [])
            .Select(v => v?.Trim().Replace('|', '/'))
            .Where(v => !string.IsNullOrEmpty(v))
            .ToArray();
        return cleaned.Length == 0 ? null : Truncate(string.Join('|', cleaned), maxLength);
    }

    /// <summary>Kolon sınırını aşan girdiyi 500 hatası yerine sessizce kırpar.</summary>
    private static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>Zorunlu alanlar için aynı kırpma — dönüş tipi nullable değil.</summary>
    private static string Cut(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
