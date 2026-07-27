using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace FethiyeCicekcisi.Application.Services;

public class PayTRSettings
{
    public string MerchantId { get; set; } = string.Empty;
    public string MerchantKey { get; set; } = string.Empty;
    public string MerchantSalt { get; set; } = string.Empty;
    public string NotificationUrl { get; set; } = string.Empty;
    public string OkUrl { get; set; } = string.Empty;
    public string FailUrl { get; set; } = string.Empty;
    public bool TestMode { get; set; } = true;
}

public class PayTRService
{
    private readonly PayTRSettings _settings;
    private readonly HttpClient _httpClient;

    public PayTRService(IConfiguration configuration, HttpClient httpClient)
    {
        _settings = configuration.GetSection("PayTR").Get<PayTRSettings>()
            ?? throw new InvalidOperationException("PayTR ayarları yapılandırılmamış.");
        _httpClient = httpClient;
    }

    public async Task<(bool Success, string? IframeToken, string? ErrorMessage)> GetIframeTokenAsync(
        string merchantOid,
        string email,
        decimal paymentAmount,
        string userIp,
        string userName,
        string userAddress,
        List<object[]> basketItems)
    {
        var amountInKurus = (int)(paymentAmount * 100);
        var currency = "TL";
        var testMode = _settings.TestMode ? "1" : "0";
        var noInstallment = "0";
        var maxInstallment = "0";
        var lang = "tr";

        var basketJson = System.Text.Json.JsonSerializer.Serialize(basketItems);
        var basketBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(basketJson));

        var hashStr = $"{_settings.MerchantId}{userIp}{merchantOid}{email}{amountInKurus}{basketBase64}{noInstallment}{maxInstallment}{currency}{testMode}";
        var paytrToken = $"{hashStr}{_settings.MerchantSalt}";
        var token = ComputeHmacSha256(_settings.MerchantKey, paytrToken);

        var formData = new Dictionary<string, string>
        {
            ["merchant_id"] = _settings.MerchantId,
            ["user_ip"] = userIp,
            ["merchant_oid"] = merchantOid,
            ["email"] = email,
            ["payment_amount"] = amountInKurus.ToString(),
            ["paytr_token"] = token,
            ["user_basket"] = basketBase64,
            ["debug_on"] = _settings.TestMode ? "1" : "0",
            ["no_installment"] = noInstallment,
            ["max_installment"] = maxInstallment,
            ["user_name"] = userName,
            ["user_address"] = userAddress,
            ["user_phone"] = "",
            ["merchant_ok_url"] = _settings.OkUrl,
            ["merchant_fail_url"] = _settings.FailUrl,
            ["merchant_notify_url"] = _settings.NotificationUrl,
            ["currency"] = currency,
            ["test_mode"] = testMode,
            ["lang"] = lang
        };

        try
        {
            var response = await _httpClient.PostAsync(
                "https://www.paytr.com/odeme/api/get-token",
                new FormUrlEncodedContent(formData));

            var content = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(content);

            if (result != null && result.TryGetValue("status", out var status) && status.ToString() == "success")
            {
                var iframeToken = result["token"]?.ToString();
                return (true, iframeToken, null);
            }

            var reason = result?.GetValueOrDefault("reason")?.ToString() ?? "Bilinmeyen hata";
            return (false, null, reason);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public bool ValidateCallback(string merchantOid, string status, string totalAmount, string hash)
    {
        var hashStr = $"{merchantOid}{_settings.MerchantSalt}{status}{totalAmount}";
        var expectedHash = Convert.ToBase64String(
            new HMACSHA256(Encoding.UTF8.GetBytes(_settings.MerchantKey))
                .ComputeHash(Encoding.UTF8.GetBytes(hashStr)));
        return expectedHash == hash;
    }

    private static string ComputeHmacSha256(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToBase64String(hmac.ComputeHash(dataBytes));
    }
}
