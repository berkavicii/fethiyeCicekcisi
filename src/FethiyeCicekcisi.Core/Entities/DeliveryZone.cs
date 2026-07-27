namespace FethiyeCicekcisi.Core.Entities;

/// <summary>Teslimat bölgesi — bölgeye göre ek yol/teslimat ücreti. Admin panelinden
/// yönetilir; ödeme adımında müşteri bölge seçer ve ücreti sipariş toplamına eklenir.</summary>
public class DeliveryZone : BaseEntity
{
    public string City { get; set; } = "Fethiye";
    public string Name { get; set; } = string.Empty;
    public decimal Fee { get; set; } = 0;
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
