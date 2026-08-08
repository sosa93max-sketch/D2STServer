using System.Text.Json;
using D2ST.Core.Profiles;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.Profiles;

/// <summary>
/// SQLite-backed profile-card layout. Each operation uses a short-lived EF
/// scope because GC handlers are registered as singletons.
/// </summary>
public sealed class ProfileCardStore : IProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceScopeFactory _scopes;
    private readonly Lock _gate = new();

    public ProfileCardStore(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public ProfileCardData GetCard(uint accountId)
    {
        if (accountId == 0)
        {
            return ProfileCardData.Empty;
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var entity = db.ProfileCards.AsNoTracking()
            .SingleOrDefault(card => card.AccountId == accountId);
        if (entity is null || string.IsNullOrWhiteSpace(entity.SlotsJson))
        {
            return ProfileCardData.Empty;
        }

        try
        {
            var slots = JsonSerializer.Deserialize<List<ProfileCardSlot>>(
                entity.SlotsJson,
                JsonOptions);
            return slots is null ? ProfileCardData.Empty : new ProfileCardData(slots);
        }
        catch (JsonException)
        {
            // A malformed legacy row must not prevent the profile from
            // loading. The next successful edit replaces it.
            return ProfileCardData.Empty;
        }
    }

    public void SetCard(uint accountId, IReadOnlyList<ProfileCardSlot> slots)
    {
        if (accountId == 0)
        {
            return;
        }

        var snapshot = slots.ToArray();
        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            var entity = db.ProfileCards.SingleOrDefault(card => card.AccountId == accountId);
            if (entity is null)
            {
                entity = new ProfileCardEntity { AccountId = accountId };
                db.ProfileCards.Add(entity);
            }

            entity.SlotsJson = JsonSerializer.Serialize(snapshot, JsonOptions);
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
        }
    }
}
