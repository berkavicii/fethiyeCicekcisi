using KedWear.Core.Entities;

namespace KedWear.Core.Interfaces.Repositories;

public interface IProductRepository : IRepository<Product>
{
    Task<Product?> GetBySlugAsync(string slug);
    Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId);
    Task<IEnumerable<Product>> GetFeaturedAsync(int count = 8);
    Task<(IEnumerable<Product> Products, int TotalCount)> GetPagedAsync(
        int page, int pageSize,
        int? categoryId = null,
        string? searchTerm = null,
        string? sortBy = null);
    Task<Product?> GetWithImagesAndVariantsAsync(int id);
    Task<Product?> GetWithImagesAndVariantsBySlugAsync(string slug);
    Task<Product?> GetForAdminEditAsync(int id);
    Task<ProductImage?> GetImageByIdAsync(int imageId);
    void RemoveImage(ProductImage image);
}

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetBySlugAsync(string slug);
    Task<IEnumerable<Category>> GetActiveAsync();
    Task<Category?> GetWithProductsAsync(int id);
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
