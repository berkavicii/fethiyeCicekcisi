using KedWear.Application.Services;
using KedWear.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KedWear.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ProductService>();
        services.AddScoped<CategoryService>();
        services.AddScoped<CartService>();
        services.AddScoped<OrderService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<ISlugService, SlugService>();
        services.AddHttpClient<PayTRService>();

        // No SMTP host configured yet (no provider set up) → log instead of send, so
        // registration/checkout keep working while a real provider gets wired in.
        if (!string.IsNullOrWhiteSpace(configuration["Smtp:Host"]))
            services.AddScoped<IEmailService, SmtpEmailService>();
        else
            services.AddScoped<IEmailService, NullEmailService>();

        return services;
    }
}
