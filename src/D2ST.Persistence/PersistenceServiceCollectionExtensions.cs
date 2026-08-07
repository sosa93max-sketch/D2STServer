using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace D2ST.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddD2stPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<D2stDbContext>(options => options.UseSqlite(connectionString));
        return services;
    }
}
