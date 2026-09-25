using System.Globalization;
using FethiyeCicekcisi.Application;
using FethiyeCicekcisi.Core.Entities;
using FethiyeCicekcisi.Infrastructure;
using FethiyeCicekcisi.Infrastructure.Data;
using FethiyeCicekcisi.Infrastructure.Data.Seed;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;

// Görüntüleme kültürü sunucunun işletim sistemi diline değil, sabit tr-TR'ye bağlanır — aksi
// halde İngilizce yerelli bir sunucuya deploy edilince ".ToString("N2")" çağrıları "1,750.00"
// gibi yanlış biçimde basar. Form/JSON PARSE etme kültürü ayrıca aşağıdaki InvariantDecimal
// model binder'la sabitlenir (bkz. yorum orada) — bu ikisi kasıtlı olarak farklı kültürler.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");

var builder = WebApplication.CreateBuilder(args);

// Prod'da Nginx reverse proxy arkasında çalışır — bu olmadan uygulama her isteği
// 127.0.0.1'den geliyormuş ve http (https değil) sanır; PayTR'ye giden müşteri IP'si
// yanlış olur, HTTPS yönlendirmesi bozulur. KnownProxies temizleniyor çünkü Kestrel
// prod'da yalnızca localhost'u dinler, başlığı sahteleyebilecek dış erişim yoktur.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddControllersWithViews(options =>
    options.ModelBinderProviders.Insert(0, new FethiyeCicekcisi.Web.Validation.InvariantDecimalModelBinderProvider()));
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Dil değiştirici (TR/EN/RU/DE) — seçim "site_lang" çerezinde tutulur. Yasal sayfalar ve ürün
// kataloğunun orijinal içeriği kültürden bağımsız her zaman Türkçe kalır (bkz. LegalController,
// Product entity) — bu yalnızca arayüz metinlerini (SharedResource) ve genel sayı/tarih
// biçimlendirmesini etkiler. Form/JSON decimal PARSE etme ise ayrıca InvariantDecimalModelBinder
// ile kültürden bağımsız sabitlenmiştir, dil değişse de fiyat girişleri bozulmaz.
var supportedCultures = new[]
{
    CultureInfo.GetCultureInfo("tr-TR"),
    CultureInfo.GetCultureInfo("en-US"),
    CultureInfo.GetCultureInfo("ru-RU"),
    CultureInfo.GetCultureInfo("de-DE")
};
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("tr-TR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = [new CookieRequestCultureProvider { CookieName = "site_lang" }];
});
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication(builder.Configuration);

// Email confirmation is only enforced once a real SMTP provider is configured — otherwise
// the confirmation link would never actually be delivered and every new signup would be
// locked out. Add Smtp:Host in appsettings (or an env var override) to turn this on.
var smtpConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Smtp:Host"]);

builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = smtpConfigured;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddErrorDescriber<FethiyeCicekcisi.Web.TurkishIdentityErrorDescriber>()
.AddDefaultTokenProviders();

// "Google ile devam et" yalnızca gerçek bir Client Id/Secret girildiğinde devreye girer —
// aksi halde buton hiç gösterilmez (bkz. AccountController.IsGoogleEnabledAsync), yarım
// yapılandırmayla kırık bir giriş denemesi sunulmaz. Google Cloud Console'da OAuth istemcisi
// oluşturup Authentication__Google__ClientId / ClientSecret ortam değişkeni ya da
// user-secrets ile sağlanır (appsettings.json'a düz metin yazılmaz).
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.CallbackPath = "/hesap/google-callback";
            // Varsayılan "state" doğrulama çerezi bazı ortamlarda Secure gerektirir ve
            // http://localhost üzerinde sessizce düşürülüp "oauth state was missing or
            // invalid" hatasına yol açar.
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/hesap/giris";
    options.LogoutPath = "/hesap/cikis";
    options.AccessDeniedPath = "/hesap/erisim-engellendi";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseForwardedHeaders();

// Standart güvenlik başlıkları. CSP bilinçli olarak yok: sayfalar inline script/style,
// CDN (Bootstrap, Google Fonts), R2 görselleri kullanıyor ve ödeme formu doğrudan bankanın
// domain'ine post ediliyor — katı bir CSP bunları tek tek istisnalamadan siteyi kırar;
// ihtiyaç olursa ayrıca ele alınmalı.
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "SAMEORIGIN";
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/hata");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization(app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value);

app.UseRouting();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapAreaControllerRoute(
    name: "admin",
    areaName: "Admin",
    pattern: "admin/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await DbSeeder.SeedAsync(app.Services);

app.Run();
