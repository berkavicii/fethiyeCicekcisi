using KedWear.Core.Entities;

namespace KedWear.Application.Services;

/// <summary>Plain inline-styled HTML — email clients don't reliably load external CSS, so
/// everything here is intentionally self-contained rather than referencing the site's stylesheet.</summary>
public static class EmailTemplates
{
    private const string Black = "#111111";
    private const string Beige = "#f5f0e8";
    private const string Accent = "#c1573f";

    private static string Layout(string title, string bodyHtml) => $$"""
        <!DOCTYPE html>
        <html lang="tr">
        <body style="margin:0;padding:0;background:{{Beige}};font-family:-apple-system,Segoe UI,Roboto,sans-serif;color:{{Black}}">
            <table width="100%" cellpadding="0" cellspacing="0" style="background:{{Beige}};padding:32px 0">
                <tr><td align="center">
                    <table width="480" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:16px;overflow:hidden">
                        <tr><td style="background:{{Black}};padding:24px 32px">
                            <span style="color:#ffffff;font-size:20px;font-weight:700;letter-spacing:.05em">KEDWEAR</span>
                        </td></tr>
                        <tr><td style="padding:32px">
                            {{bodyHtml}}
                        </td></tr>
                        <tr><td style="padding:20px 32px;border-top:1px solid #eee">
                            <span style="font-size:12px;color:#888">Bu e-posta KedWear tarafından gönderilmiştir. Sorularınız için info@kedwear.com</span>
                        </td></tr>
                    </table>
                </td></tr>
            </table>
        </body>
        </html>
        """;

    private static string Button(string text, string url) => $"""
        <a href="{url}" style="display:inline-block;background:{Black};color:#ffffff;text-decoration:none;padding:14px 28px;border-radius:999px;font-size:14px;font-weight:600;letter-spacing:.03em">{text}</a>
        """;

    public static string ConfirmEmail(string firstName, string confirmUrl) => Layout("E-postanızı doğrulayın", $"""
        <h2 style="margin:0 0 16px;font-size:22px">Merhaba {firstName},</h2>
        <p style="font-size:15px;line-height:1.6;color:#333">KedWear'e hoş geldiniz! Hesabınızı aktifleştirmek için aşağıdaki butona tıklayarak e-posta adresinizi doğrulayın.</p>
        <div style="margin:28px 0">{Button("E-postamı Doğrula", confirmUrl)}</div>
        <p style="font-size:13px;color:#888">Bu bağlantı 24 saat geçerlidir. Eğer bu kaydı siz yapmadıysanız bu e-postayı görmezden gelebilirsiniz.</p>
        """);

    public static string PasswordReset(string firstName, string resetUrl) => Layout("Şifre sıfırlama", $"""
        <h2 style="margin:0 0 16px;font-size:22px">Merhaba {firstName},</h2>
        <p style="font-size:15px;line-height:1.6;color:#333">Şifrenizi sıfırlamak için bir talep aldık. Yeni şifre belirlemek için aşağıdaki butona tıklayın.</p>
        <div style="margin:28px 0">{Button("Şifremi Sıfırla", resetUrl)}</div>
        <p style="font-size:13px;color:#888">Bu bağlantı 1 saat geçerlidir. Bu talebi siz yapmadıysanız bu e-postayı görmezden gelebilirsiniz, şifreniz değişmeyecektir.</p>
        """);

    public static string OrderConfirmationCustomer(Order order) => Layout("Siparişiniz alındı", $"""
        <h2 style="margin:0 0 16px;font-size:22px">Teşekkürler, {order.ShippingFirstName}!</h2>
        <p style="font-size:15px;line-height:1.6;color:#333">Siparişiniz alındı ve ödemeniz onaylandı. Sipariş numaranız: <strong>{order.OrderNumber}</strong></p>
        <table width="100%" cellpadding="8" cellspacing="0" style="margin:20px 0;border-collapse:collapse;font-size:14px">
            {string.Join("", order.Items.Select(i => $"""
            <tr style="border-bottom:1px solid #eee">
                <td>{i.ProductName}{(string.IsNullOrEmpty(i.VariantInfo) ? "" : $" ({i.VariantInfo})")} × {i.Quantity}</td>
                <td align="right">{i.TotalPrice:N2} ₺</td>
            </tr>
            """))}
            <tr><td style="padding-top:12px;font-weight:700">Toplam</td><td align="right" style="padding-top:12px;font-weight:700">{order.TotalAmount:N2} ₺</td></tr>
        </table>
        <p style="font-size:14px;color:#333">Teslimat adresi: {order.ShippingAddressLine1}, {order.ShippingDistrict}/{order.ShippingCity}</p>
        <p style="font-size:13px;color:#888;margin-top:20px">Siparişinizin durumunu hesabınızdan takip edebilirsiniz.</p>
        """);

    public static string OrderNotificationAdmin(Order order) => Layout("Yeni sipariş", $"""
        <h2 style="margin:0 0 16px;font-size:22px;color:{Accent}">Yeni sipariş alındı 🎉</h2>
        <p style="font-size:15px;line-height:1.6;color:#333">Sipariş no: <strong>{order.OrderNumber}</strong></p>
        <p style="font-size:15px;color:#333">Müşteri: {order.ShippingFirstName} {order.ShippingLastName} ({order.ShippingEmail})</p>
        <table width="100%" cellpadding="8" cellspacing="0" style="margin:20px 0;border-collapse:collapse;font-size:14px">
            {string.Join("", order.Items.Select(i => $"""
            <tr style="border-bottom:1px solid #eee">
                <td>{i.ProductName}{(string.IsNullOrEmpty(i.VariantInfo) ? "" : $" ({i.VariantInfo})")} × {i.Quantity}</td>
                <td align="right">{i.TotalPrice:N2} ₺</td>
            </tr>
            """))}
            <tr><td style="padding-top:12px;font-weight:700">Toplam</td><td align="right" style="padding-top:12px;font-weight:700">{order.TotalAmount:N2} ₺</td></tr>
        </table>
        <p style="font-size:14px;color:#333">Teslimat: {order.ShippingAddressLine1}, {order.ShippingDistrict}/{order.ShippingCity} — {order.ShippingPhone}</p>
        """);
}
