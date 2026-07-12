using KedWear.Core.Interfaces.Repositories;
using KedWear.Infrastructure.Data;
using KedWear.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KedWear.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<AppDbContext>(options =>
        {
            // A configured connection string means a real Postgres is expected (production,
            // or a developer who set up local Postgres) — use it with migrations. Leaving
            // ConnectionStrings:DefaultConnection empty keeps the zero-setup in-memory mode
            // for anyone who just wants to run the app without installing Postgres.
            if (!string.IsNullOrWhiteSpace(connectionString))
                options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("KedWear.Infrastructure"));
            else
                options.UseInMemoryDatabase("KedWearDb");
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        return services;
    }
}
