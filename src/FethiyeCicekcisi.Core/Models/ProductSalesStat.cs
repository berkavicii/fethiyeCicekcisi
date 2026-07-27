namespace FethiyeCicekcisi.Core.Models;

/// <summary>Admin istatistik sayfası için ürün bazında satış/iade özeti. Ürün adı ve görseli
/// OrderItem'daki anlık kopyadan gelir; ürün sonradan soft-delete edilmiş olsa bile geçmiş
/// satışları raporda görünmeye devam eder (IsDeleted bayrağıyla işaretlenir).</summary>
public class ProductSalesStat
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? CategoryName { get; set; }
    public string? Slug { get; set; }
    public bool ProductDeleted { get; set; }

    /// <summary>Ödemesi alınmış, iade/iptale dönmemiş siparişlerdeki toplam adet.</summary>
    public int UnitsSold { get; set; }
    public decimal Revenue { get; set; }

    /// <summary>İade edilen siparişlerdeki toplam adet.</summary>
    public int UnitsRefunded { get; set; }

    /// <summary>İade oranı: iade adedi / (satış + iade adedi).</summary>
    public double RefundRate => UnitsSold + UnitsRefunded == 0
        ? 0
        : (double)UnitsRefunded / (UnitsSold + UnitsRefunded);
}
