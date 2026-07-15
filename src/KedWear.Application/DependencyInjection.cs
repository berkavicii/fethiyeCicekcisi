using Amazon.Runtime;
using Amazon.S3;
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
        AddImageStorage(services, configuration);
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

    // R2 bilgileri eksiksiz geldiyse (env var / user-secrets) görseller Cloudflare R2'ye,
    // yoksa eskisi gibi diske (wwwroot/uploads) yazılır — dev ortamı credential'sız çalışmaya
    // devam eder.
    private static void AddImageStorage(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(R2StorageOptions.SectionName);
        var r2 = new R2StorageOptions
        {
            AccountId = section["AccountId"] ?? string.Empty,
            AccessKeyId = section["AccessKeyId"] ?? string.Empty,
            SecretAccessKey = section["SecretAccessKey"] ?? string.Empty,
            BucketName = section["BucketName"] ?? string.Empty,
            PublicBaseUrl = section["PublicBaseUrl"] ?? string.Empty
        };

        if (!r2.IsConfigured)
        {
            services.AddScoped<IFileService, FileService>();
            return;
        }

        services.AddSingleton(r2);
        services.AddSingleton<IAmazonS3>(_ =>
        {
            var s3Config = new AmazonS3Config
            {
                ServiceURL = r2.ServiceUrl,
                // R2'nin hesap-bazlı endpoint'inde bucket adı path'te taşınmalı
                // (virtual-hosted style DNS'i çözülmez).
                ForcePathStyle = true,
                // SDK v4 varsayılan olarak CRC tabanlı checksum'ları zorunlu kılıyor;
                // R2 bunları desteklemediğinden Cloudflare'in önerdiği WHEN_REQUIRED moduna çekiyoruz.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
            };
            return new AmazonS3Client(r2.AccessKeyId, r2.SecretAccessKey, s3Config);
        });
        services.AddScoped<IFileService, R2FileService>();
    }
}
