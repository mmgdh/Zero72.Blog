using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Zero72.Blog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BlogDb")
            ?? throw new InvalidOperationException("Connection string 'BlogDb' is missing.");

        services.AddDbContext<BlogDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
