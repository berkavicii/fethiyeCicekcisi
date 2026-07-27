using FethiyeCicekcisi.Core.Interfaces.Repositories;
using FethiyeCicekcisi.Infrastructure.Data;
using FethiyeCicekcisi.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FethiyeCicekcisi.Infrastructure;

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
                options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("FethiyeCicekcisi.Infrastructure"));
            else
                options.UseInMemoryDatabase("FethiyeCicekcisiDb");
        });

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IOccasionRepository, OccasionRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IAddressRepository, AddressRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IDeliveryZoneRepository, DeliveryZoneRepository>();
        services.AddScoped<IPromoCodeRepository, PromoCodeRepository>();

        return services;
    }
}
