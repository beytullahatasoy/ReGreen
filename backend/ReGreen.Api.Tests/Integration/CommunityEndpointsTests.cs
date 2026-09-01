using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using ReGreen.Api.Dtos;
using ReGreen.Data.Entities;
using Xunit;

namespace ReGreen.Api.Tests.Integration;

/// <summary>
/// Topluluk katmanının uçtan uca testi: gönüllü kaydı → etkinlik → katılım →
/// gözlem → kurum incelemesi. FireEndpointsTests ile aynı desen (benzersiz
/// LocalDB, her testten önce ResetAsync).
///
/// Kapsanan asıl davranışlar, arayüzün doğru çalışması için kritik olanlar:
/// kontenjan doldu mu, aynı kişi iki kez sayılıyor mu, cevapsız gözlem
/// kuyruğa giriyor mu, karar zamanı yazılıyor mu.
/// </summary>
[Collection("Database")]
#pragma warning disable CS9113 // xUnit collection fixture DI'si için gerekli, gövdede kullanılmıyor.
public class CommunityEndpointsTests(DatabaseFixture fixture) : IAsyncLifetime
#pragma warning restore CS9113
{
    private readonly ApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        if (!LocalDbFactAttribute.Enabled) return;
        await DatabaseFixture.ResetAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [LocalDbFact]
    public async Task Migration_seeds_a_single_organisation()
    {
        var client = _factory.CreateClient();
        var organisations = await client.GetFromJsonAsync<OrganisationDto[]>("/api/organisations");

        // Organisation ekranı bir kurum olarak çalışıyor; o kurum var olmalı.
        Assert.NotNull(organisations);
        Assert.Single(organisations);
        Assert.True(organisations[0].Verified);
    }

    [LocalDbFact]
    public async Task Volunteer_is_created_with_a_generated_alias_when_none_given()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/volunteers", new CreateVolunteerRequest(null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var volunteer = await response.Content.ReadFromJsonAsync<VolunteerDto>();
        Assert.NotNull(volunteer);
        Assert.NotEqual(Guid.Empty, volunteer.Id);
        Assert.False(string.IsNullOrWhiteSpace(volunteer.Alias));
    }

    [LocalDbFact]
    public async Task Activity_is_created_for_a_real_fire_and_rejected_for_an_unknown_one()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/activities", NewActivity());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var activity = await created.Content.ReadFromJsonAsync<FieldActivityDto>();
        Assert.NotNull(activity);
        Assert.Equal(ActivityStatuses.Open, activity.Status);
        Assert.Equal(0, activity.Joined);
        // Requirements '|' ile saklanıp diziye açılıyor.
        Assert.Equal(["Kapalı ayakkabı", "18 yaş sınırı"], activity.Requirements);

        var unknown = await client.PostAsJsonAsync(
            "/api/activities", NewActivity() with { FireId = "YOK_2026_99" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [LocalDbFact]
    public async Task Joining_twice_counts_the_volunteer_once()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var activityId = await CreateActivityAsync(client);
        var volunteerId = await CreateVolunteerAsync(client);

        var first = await Join(client, activityId, volunteerId);
        var second = await Join(client, activityId, volunteerId);

        // "Katıl" düğmesine ikinci kez basmak hata vermemeli, sayıyı da artırmamalı.
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, (await first.Content.ReadFromJsonAsync<FieldActivityDto>())!.Joined);
        Assert.Equal(1, (await second.Content.ReadFromJsonAsync<FieldActivityDto>())!.Joined);
    }

    [LocalDbFact]
    public async Task Activity_list_remembers_that_this_volunteer_joined()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var activityId = await CreateActivityAsync(client);
        var volunteerId = await CreateVolunteerAsync(client);
        await Join(client, activityId, volunteerId);

        // Sayfa yenilendiginde ekran gonullunun kaydini unutmamali.
        var mine = await client.GetFromJsonAsync<FieldActivityDto[]>(
            $"/api/activities?volunteer_id={volunteerId}");
        // volunteer_id verilmezse bayrak her zaman false.
        var anonymous = await client.GetFromJsonAsync<FieldActivityDto[]>("/api/activities");
        var someoneElse = await client.GetFromJsonAsync<FieldActivityDto[]>(
            $"/api/activities?volunteer_id={await CreateVolunteerAsync(client)}");

        Assert.True(mine!.Single(a => a.Id == activityId).JoinedByMe);
        Assert.False(anonymous!.Single(a => a.Id == activityId).JoinedByMe);
        Assert.False(someoneElse!.Single(a => a.Id == activityId).JoinedByMe);
    }

    [LocalDbFact]
    public async Task Joining_a_full_activity_is_rejected()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var activityId = await CreateActivityAsync(client, capacity: 1);

        await Join(client, activityId, await CreateVolunteerAsync(client));
        var overflow = await Join(client, activityId, await CreateVolunteerAsync(client));

        Assert.Equal(HttpStatusCode.Conflict, overflow.StatusCode);
    }

    [LocalDbFact]
    public async Task Joining_a_closed_activity_is_rejected()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var activityId = await CreateActivityAsync(client);

        var closed = await client.PatchAsJsonAsync(
            $"/api/activities/{activityId}", new UpdateActivityRequest(ActivityStatuses.Closed, null));
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        var join = await Join(client, activityId, await CreateVolunteerAsync(client));
        Assert.Equal(HttpStatusCode.Conflict, join.StatusCode);
    }

    [LocalDbFact]
    public async Task Capacity_cannot_drop_below_the_volunteers_already_joined()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var activityId = await CreateActivityAsync(client, capacity: 5);
        await Join(client, activityId, await CreateVolunteerAsync(client));
        await Join(client, activityId, await CreateVolunteerAsync(client));

        // Aksi halde kayıtlı gönüllülerin bir kısmı sessizce "fazlalık" olurdu.
        var shrink = await client.PatchAsJsonAsync(
            $"/api/activities/{activityId}", new UpdateActivityRequest(null, 1));
        Assert.Equal(HttpStatusCode.BadRequest, shrink.StatusCode);
    }

    [LocalDbFact]
    public async Task Leaving_removes_the_volunteer_and_is_safe_to_repeat()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var activityId = await CreateActivityAsync(client);
        var volunteerId = await CreateVolunteerAsync(client);
        await Join(client, activityId, volunteerId);

        var left = await client.DeleteAsync($"/api/activities/{activityId}/participants/{volunteerId}");
        var again = await client.DeleteAsync($"/api/activities/{activityId}/participants/{volunteerId}");

        Assert.Equal(0, (await left.Content.ReadFromJsonAsync<FieldActivityDto>())!.Joined);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [LocalDbFact]
    public async Task Observation_without_an_answer_is_rejected()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var volunteerId = await CreateVolunteerAsync(client);

        // Cevapsız gözlem kanıt değil, kuyrukta gürültüdür.
        var empty = await client.PostAsJsonAsync(
            $"/api/fires/{SeedHelper.FireId}/observations",
            new CreateObservationRequest(volunteerId, null, "Nokta A", null, [], null));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var blank = await client.PostAsJsonAsync(
            $"/api/fires/{SeedHelper.FireId}/observations",
            new CreateObservationRequest(volunteerId, null, "Nokta A", null, ["   "], null));
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
    }

    [LocalDbFact]
    public async Task Observation_from_an_unknown_volunteer_is_rejected()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/fires/{SeedHelper.FireId}/observations",
            new CreateObservationRequest(Guid.NewGuid(), null, "Nokta A", null, ["Bitki örtüsü: seyrek"], null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [LocalDbFact]
    public async Task Observation_cannot_reference_an_activity_from_another_fire()
    {
        await SeedFireAsync();
        await SeedFireAsync("OTHER_2026_01");
        var client = _factory.CreateClient();
        var volunteerId = await CreateVolunteerAsync(client);
        var otherActivityId = await CreateActivityAsync(client, fireId: "OTHER_2026_01");

        var response = await client.PostAsJsonAsync(
            $"/api/fires/{SeedHelper.FireId}/observations",
            new CreateObservationRequest(volunteerId, otherActivityId, "Nokta A", null, ["Bitki örtüsü: seyrek"], null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [LocalDbFact]
    public async Task Review_records_the_decision_its_author_and_its_time()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var observationId = await CreateObservationAsync(client);

        var reviewed = await client.PatchAsJsonAsync(
            $"/api/observations/{observationId}",
            new ReviewObservationRequest(ObservationStatuses.Accepted, "Ekip doğruladı.", 1));

        var dto = await reviewed.Content.ReadFromJsonAsync<FieldObservationDto>();
        Assert.NotNull(dto);
        Assert.Equal(ObservationStatuses.Accepted, dto.Status);
        Assert.Equal("Ekip doğruladı.", dto.ReviewNote);
        Assert.Equal("ReGreen Saha Ekibi", dto.ReviewedBy);
        // CHECK kısıtı: 'pending' dışındaki her durumun karar zamanı olmalı.
        Assert.NotNull(dto.ReviewedAt);
    }

    [LocalDbFact]
    public async Task Review_queue_puts_pending_observations_first()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var decided = await CreateObservationAsync(client);
        await client.PatchAsJsonAsync(
            $"/api/observations/{decided}",
            new ReviewObservationRequest(ObservationStatuses.Accepted, null, null));
        var waiting = await CreateObservationAsync(client);

        var queue = await client.GetFromJsonAsync<FieldObservationDto[]>("/api/observations");

        // Kurumun bakması gereken kayıt en üstte olmalı, karar verilmiş olan değil.
        Assert.NotNull(queue);
        Assert.Equal(waiting, queue[0].Id);
    }

    [LocalDbFact]
    public async Task Observations_can_be_filtered_by_volunteer()
    {
        await SeedFireAsync();
        var client = _factory.CreateClient();
        var mine = await CreateVolunteerAsync(client);
        var theirs = await CreateVolunteerAsync(client);
        await CreateObservationAsync(client, mine);
        await CreateObservationAsync(client, theirs);

        // Community ekranı "benim gözlemlerim"i bu filtreyle çekiyor.
        var filtered = await client.GetFromJsonAsync<FieldObservationDto[]>(
            $"/api/observations?volunteer_id={mine}");

        Assert.NotNull(filtered);
        Assert.Single(filtered);
        Assert.Equal(mine, filtered[0].VolunteerId);
    }

    [LocalDbFact]
    public async Task Invalid_status_filter_is_rejected()
    {
        var client = _factory.CreateClient();

        var activities = await client.GetAsync("/api/activities?status=yok");
        var observations = await client.GetAsync("/api/observations?status=yok");

        Assert.Equal(HttpStatusCode.BadRequest, activities.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, observations.StatusCode);
    }

    // ------------------------------------------------------------ yardımcılar

    private static async Task SeedFireAsync(string fireId = SeedHelper.FireId)
    {
        await using var db = DatabaseFixture.CreateContext();
        db.Fires.Add(SeedHelper.BuildFire(fireId));
        await db.SaveChangesAsync();
    }

    private static CreateActivityRequest NewActivity() => new(
        SeedHelper.FireId, null, ActivityKinds.Planting, "Test dikim günü",
        "Uzman değerlendirmesinden geçmiş alanda kurum gözetiminde dikim.",
        new DateOnly(2026, 10, 17), "Test buluşma noktası", 20,
        ["Kapalı ayakkabı", "18 yaş sınırı"]);

    private static async Task<int> CreateActivityAsync(
        HttpClient client, string fireId = SeedHelper.FireId, int capacity = 20)
    {
        var response = await client.PostAsJsonAsync(
            "/api/activities", NewActivity() with { FireId = fireId, Capacity = capacity });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FieldActivityDto>())!.Id;
    }

    private static async Task<Guid> CreateVolunteerAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/volunteers", new CreateVolunteerRequest(null));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<VolunteerDto>())!.Id;
    }

    private static async Task<int> CreateObservationAsync(HttpClient client, Guid? volunteerId = null)
    {
        var volunteer = volunteerId ?? await CreateVolunteerAsync(client);
        var response = await client.PostAsJsonAsync(
            $"/api/fires/{SeedHelper.FireId}/observations",
            new CreateObservationRequest(volunteer, null, "Gözlem noktası B", null,
                ["Bitki örtüsü: seyrek", "Erozyon izi: görünür"], "Yamaçta gevşek toprak."));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FieldObservationDto>())!.Id;
    }

    private static Task<HttpResponseMessage> Join(HttpClient client, int activityId, Guid volunteerId) =>
        client.PostAsJsonAsync($"/api/activities/{activityId}/participants",
            new JoinActivityRequest(volunteerId));
}
