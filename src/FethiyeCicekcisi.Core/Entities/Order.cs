using FethiyeCicekcisi.Core.Enums;

namespace FethiyeCicekcisi.Core.Entities;

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public AppUser? User { get; set; }
    public string GuestEmail { get; set; } = string.Empty;

    /// <summary>Üyeliksiz siparişte sepetin oturum kimliği — ödeme onaylanınca misafir
    /// sepetinin temizlenebilmesi için tutulur.</summary>
    public string? GuestSessionId { get; set; }

    /// <summary>Uygulanan promosyon kodu (varsa); indirim tutarı Discount alanında.</summary>
    public string? PromoCode { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    // Gönderen (siparişi veren / ödeyen) — çiçek siparişinde teslimat adresi çoğu
    // zaman başka birine ait olduğu için alıcıdan ayrı tutulur.
    public string SenderName { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>"Gönderen bilgim gizli kalsın" — kart ve teslimatta gönderen adı
    /// alıcıya gösterilmez.</summary>
    public bool IsAnonymousSender { get; set; } = false;

    // Alıcı (çiçeğin teslim edileceği kişi)
    public string RecipientFirstName { get; set; } = string.Empty;
    public string RecipientLastName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string RecipientAddressLine1 { get; set; } = string.Empty;
    public string? RecipientAddressLine2 { get; set; }
    public string RecipientCity { get; set; } = string.Empty;
    public string RecipientDistrict { get; set; } = string.Empty;
    public string? RecipientZipCode { get; set; }

    // Teslimat planı
    public DateOnly DeliveryDate { get; set; }
    public DeliveryTimeSlot DeliveryTimeSlot { get; set; } = DeliveryTimeSlot.Belirtilmemis;

    /// <summary>Buketle gönderilecek not kartı metni (~200 karakter).</summary>
    public string? CardMessage { get; set; }

    public decimal SubTotal { get; set; }
    public decimal ShippingCost { get; set; } = 0;
    public decimal Discount { get; set; } = 0;
    public decimal TotalAmount { get; set; }

    public string? Notes { get; set; }
    public string? TrackingNumber { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public Payment? Payment { get; set; }
}
