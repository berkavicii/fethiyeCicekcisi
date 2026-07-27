using FethiyeCicekcisi.Core.Entities;

namespace FethiyeCicekcisi.Core.Interfaces.Repositories;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySlugAsync(string slug);
    Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId);
    Task<IEnumerable<Product>> GetFeaturedAsync(int count = 8);
    Task<(IEnumerable<Product> Products, int TotalCount)> GetPagedAsync(
        int page, int pageSize,
        int? categoryId = null,
        string? searchTerm = null,
        string? sortBy = null,
        string? size = null,
        int? occasionId = null);

    /// <summary>Distinct sizes across all active, in-stock variants — feeds the option filter
    /// ("11 Adet", "21 Adet", "Büyük Boy"...) in the product list sidebar, so only options a
    /// customer can actually buy are offered.</summary>
    Task<IEnumerable<string>> GetAvailableSizesAsync();
    Task<Product?> GetWithImagesAndVariantsAsync(int id);
    Task<Product?> GetWithImagesAndVariantsBySlugAsync(string slug);
    Task<Product?> GetForAdminEditAsync(int id);
    Task<ProductImage?> GetImageByIdAsync(int imageId);
    void RemoveImage(ProductImage image);

    /// <summary>True if this product appears in at least one past order — such a product
    /// can't be hard-deleted (FK_OrderItems_Products cascades and would wipe order history).</summary>
    Task<bool> HasOrderItemsAsync(int productId);

    /// <summary>Checks slug uniqueness against ALL rows, including soft-deleted ones — the
    /// Product query filter hides IsDeleted rows from normal queries, but the DB's unique
    /// index on Slug still covers them, so uniqueness checks must bypass the filter or they'll
    /// green-light a slug that Postgres then rejects with a 23505 duplicate key error.</summary>
    Task<bool> SlugExistsAsync(string slug, int? excludeId = null);
}

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetBySlugAsync(string slug);
    Task<IEnumerable<Category>> GetActiveAsync();
    Task<Category?> GetWithProductsAsync(int id);
}

public interface IOccasionRepository : IRepository<Occasion>
{
    Task<Occasion?> GetBySlugAsync(string slug);
    Task<IEnumerable<Occasion>> GetActiveAsync();
}

public interface IDeliveryZoneRepository : IRepository<DeliveryZone>
{
    Task<IEnumerable<DeliveryZone>> GetActiveAsync();
}

public interface IPromoCodeRepository : IRepository<PromoCode>
{
    Task<PromoCode?> GetByCodeAsync(string code);
}

public interface ICartRepository : IRepository<CartItem>
{
    Task<IEnumerable<CartItem>> GetCartItemsAsync(string? userId, string? sessionId);
    Task<CartItem?> GetCartItemAsync(string? userId, string? sessionId, int productId, int? variantId);
    Task ClearCartAsync(string? userId, string? sessionId);
    Task MigrateGuestCartAsync(string sessionId, string userId);
}

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByOrderNumberAsync(string orderNumber);
    Task<IEnumerable<Order>> GetByUserIdAsync(string userId);
    Task<Order?> GetWithItemsAsync(int id);
    Task<Order?> GetWithItemsAndPaymentAsync(int id);
    Task<(IEnumerable<Order> Orders, int TotalCount)> GetPagedAsync(int page, int pageSize, string? status = null);

    /// <summary>Ürün bazında satış/iade istatistikleri; <paramref name="from"/> verilirse
    /// yalnızca o tarihten sonra oluşturulan siparişler sayılır.</summary>
    Task<IReadOnlyList<Core.Models.ProductSalesStat>> GetProductSalesStatsAsync(DateTime? from = null);
}

public interface IAddressRepository : IRepository<Address>
{
    Task<IEnumerable<Address>> GetByUserIdAsync(string userId);
    Task<Address?> GetDefaultAsync(string userId);
}

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByOrderIdAsync(int orderId);
    Task<Payment?> GetByMerchantOidAsync(string merchantOid);
}
