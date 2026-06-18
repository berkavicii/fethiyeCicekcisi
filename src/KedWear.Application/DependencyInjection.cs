using KedWear.Application.Services;
using KedWear.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace KedWear.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ProductService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<CartService>();
        services.AddScoped<OrderService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<ISlugService, SlugService>();
        services.AddHttpClient<PayTRService>();

        return services;
    }
}
