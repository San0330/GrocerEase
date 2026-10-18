using Microsoft.Extensions.DependencyInjection;

namespace GrocerEase.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // DbContext and repository registrations will be added here
        // e.g. services.AddDbContext<AppDbContext>(options =>
        //          options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        return services;
    }
}
