using D2ST.Core.Profiles;
using D2ST.Persistence;
using Microsoft.EntityFrameworkCore;

namespace D2ST.Api.Profiles;

/// <summary>
/// SQLite-backed showcase storage. Showcase payloads are already protobuf
/// bytes, so the database preserves the exact item/background data submitted
/// by the client and can return it to any account that requests the profile.
/// </summary>
public sealed class ShowcaseStore : IShowcaseStore
{
    private readonly IServiceScopeFactory _scopes;
    private readonly Lock _gate = new();

    public ShowcaseStore(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    public ShowcaseRecord? Get(uint accountId, uint showcaseType)
    {
        if (accountId == 0 || showcaseType == 0)
        {
            return null;
        }

        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
        var entity = db.Showcases.AsNoTracking()
            .SingleOrDefault(showcase =>
                showcase.AccountId == accountId && showcase.ShowcaseType == showcaseType);

        return entity is null
            ? null
            : new ShowcaseRecord(
                entity.ShowcaseType,
                entity.FormatVersion,
                entity.Payload.ToArray());
    }

    public void Set(uint accountId, uint showcaseType, uint formatVersion, byte[] payload)
    {
        if (accountId == 0 || showcaseType == 0)
        {
            return;
        }

        var snapshot = payload.ToArray();
        lock (_gate)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<D2stDbContext>();
            var entity = db.Showcases.SingleOrDefault(showcase =>
                showcase.AccountId == accountId && showcase.ShowcaseType == showcaseType);

            if (entity is null)
            {
                entity = new ShowcaseEntity
                {
                    AccountId = accountId,
                    ShowcaseType = showcaseType
                };
                db.Showcases.Add(entity);
            }

            entity.FormatVersion = formatVersion;
            entity.Payload = snapshot;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            db.SaveChanges();
        }
    }
}
