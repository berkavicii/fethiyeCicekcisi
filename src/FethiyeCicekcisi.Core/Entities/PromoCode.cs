using FethiyeCicekcisi.Core.Enums;

namespace FethiyeCicekcisi.Core.Entities;

/// <summary>Promosyon kodu — yüzde ya da sabit TL indirimi. Ödeme adımında girilir,
/// indirim ara toplam üzerinden hesaplanıp siparişin Discount alanına yazılır.</summary>
public class PromoCode : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public PromoDiscountType DiscountType { get; set; } = PromoDiscountType.Percent;
    public decimal Amount { get; set; }
    public bool IsActive { get; set; } = true;
}
