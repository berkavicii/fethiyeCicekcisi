using KedWear.Core.Entities;
using KedWear.Core.Interfaces.Repositories;
using KedWear.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KedWear.Infrastructure.Repositories;

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context) { }

    public async Task<Product?> GetBySlugAsync(string slug) =>
        await _dbSet.Include(p => p.Category)
                    .Include(p => p.Images)
                    .Include(p => p.Variants)
                    .FirstOrDefaultAsync(p => p.Slug == slug);

    public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId) =>
        await _dbSet.Include(p => p.Category)
                    .Include(p => p.Images)
                    .Where(p => p.CategoryId == categoryId)
                    .OrderBy(p => p.DisplayOrder)
                    .ToListAsync();

    public async Task<IEnumerable<Product>> GetFeaturedAsync(int count = 8) =>
        await _dbSet.Include(p => p.Category)
                    .Include(p => p.Images)
                    .Where(p => p.IsFeatured && p.Status == Core.Enums.ProductStatus.Active)
                    .OrderBy(p => p.DisplayOrder)
                    .Take(count)
                    .ToListAsync();

    public async Task<(IEnumerable<Product> Products, int TotalCount)> GetPagedAsync(
        int page, int pageSize,
        int? categoryId = null,
        string? searchTerm = null,
        string? sortBy = null)
    {
        var query = _dbSet.Include(p => p.Category).Include(p => p.Images)
            .Include(p => p.Variants.Where(v => v.IsActive && !v.IsDeleted))
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(searchTerm))
            query = query.Where(p => p.Name.Contains(searchTerm) || (p.Description != null && p.Description.Contains(searchTerm)));

        query = sortBy switch
        {
            "price_asc" => query.OrderBy(p => p.Price),
            "price_desc" => query.OrderByDescending(p => p.Price),
            "newest" => query.OrderByDescending(p => p.CreatedAt),
            _ => query.OrderBy(p => p.DisplayOrder).ThenByDescending(p => p.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var products = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (products, totalCount);
    }

    public async Task<Product?> GetWithImagesAndVariantsAsync(int id) =>
        await _dbSet.Include(p => p.Category)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Include(p => p.Variants.Where(v => v.IsActive && !v.IsDeleted))
                    .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Product?> GetWithImagesAndVariantsBySlugAsync(string slug) =>
        await _dbSet.Include(p => p.Category)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Include(p => p.Variants.Where(v => v.IsActive && !v.IsDeleted))
                    .FirstOrDefaultAsync(p => p.Slug == slug);

    /// <summary>Admin editing needs to see inactive-but-not-deleted variants too (so they can
    /// be reactivated), unlike the customer-facing methods above which only show active ones.</summary>
    public async Task<Product?> GetForAdminEditAsync(int id) =>
        await _dbSet.Include(p => p.Category)
                    .Include(p => p.Images.OrderBy(i => i.DisplayOrder))
                    .Include(p => p.Variants.Where(v => !v.IsDeleted))
                    .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<ProductImage?> GetImageByIdAsync(int imageId) =>
        await _context.Set<ProductImage>().FindAsync(imageId);

    public void RemoveImage(ProductImage image) =>
        _context.Set<ProductImage>().Remove(image);
}

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context) { }

    public async Task<Category?> GetBySlugAsync(string slug) =>
        await _dbSet.Include(c => c.Products).FirstOrDefaultAsync(c => c.Slug == slug);

    public async Task<IEnumerable<Category>> GetActiveAsync() =>
        await _dbSet.Where(c => c.IsActive)
                    .OrderBy(c => c.DisplayOrder)
                    .ToListAsync();

    public async Task<Category?> GetWithProductsAsync(int id) =>
        await _dbSet.Include(c => c.Products).ThenInclude(p => p.Images)
                    .FirstOrDefaultAsync(c => c.Id == id);
}

public class CartRepository : Repository<CartItem>, ICartRepository
{
    public CartRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<CartItem>> GetCartItemsAsync(string? userId, string? sessionId) =>
        await _dbSet
            .Include(ci => ci.Product).ThenInclude(p => p.Images)
            .Include(ci => ci.ProductVariant)
            .Where(ci => (userId != null && ci.UserId == userId) ||
                         (userId == null && ci.SessionId == sessionId))
            .ToListAsync();

    public async Task<CartItem?> GetCartItemAsync(string? userId, string? sessionId, int productId, int? variantId) =>
        await _dbSet.FirstOrDefaultAsync(ci =>
            ((userId != null && ci.UserId == userId) || (userId == null && ci.SessionId == sessionId)) &&
            ci.ProductId == productId &&
            ci.ProductVariantId == variantId);

    public async Task ClearCartAsync(string? userId, string? sessionId)
    {
        var items = await GetCartItemsAsync(userId, sessionId);
        _dbSet.RemoveRange(items);
        await _context.SaveChangesAsync();
    }

    public async Task MigrateGuestCartAsync(string sessionId, string userId)
    {
        var guestItems = await _dbSet.Where(ci => ci.SessionId == sessionId && ci.UserId == null).ToListAsync();
        foreach (var item in guestItems)
        {
            var existing = await _dbSet.FirstOrDefaultAsync(ci =>
                ci.UserId == userId && ci.ProductId == item.ProductId && ci.ProductVariantId == item.ProductVariantId);

            if (existing != null)
                existing.Quantity += item.Quantity;
            else
            {
                item.UserId = userId;
                item.SessionId = null;
            }
        }

        var duplicates = guestItems.Where(i => i.UserId == sessionId).ToList();
        _dbSet.RemoveRange(duplicates);
        await _context.SaveChangesAsync();
    }
}

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(AppDbContext context) : base(context) { }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber) =>
        await _dbSet.Include(o => o.Items).Include(o => o.Payment)
                    .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);

    public async Task<IEnumerable<Order>> GetByUserIdAsync(string userId) =>
        await _dbSet.Include(o => o.Items)
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

    public async Task<Order?> GetWithItemsAsync(int id) =>
        await _dbSet.Include(o => o.Items).ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<Order?> GetWithItemsAndPaymentAsync(int id) =>
        await _dbSet.Include(o => o.Items).ThenInclude(oi => oi.Product)
                    .Include(o => o.Payment)
                    .FirstOrDefaultAsync(o => o.Id == id);

    public async Task<(IEnumerable<Order> Orders, int TotalCount)> GetPagedAsync(int page, int pageSize, string? status = null)
    {
        var query = _dbSet.Include(o => o.User).AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<Core.Enums.OrderStatus>(status, out var orderStatus))
            query = query.Where(o => o.Status == orderStatus);

        var total = await query.CountAsync();
        var orders = await query.OrderByDescending(o => o.CreatedAt)
                                .Skip((page - 1) * pageSize).Take(pageSize)
                                .ToListAsync();
        return (orders, total);
    }
}

public class AddressRepository : Repository<Address>, IAddressRepository
{
    public AddressRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Address>> GetByUserIdAsync(string userId) =>
        await _dbSet.Where(a => a.UserId == userId).OrderByDescending(a => a.IsDefault).ToListAsync();

    public async Task<Address?> GetDefaultAsync(string userId) =>
        await _dbSet.FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault);
}

public class PaymentRepository : Repository<Payment>, IPaymentRepository
{
    public PaymentRepository(AppDbContext context) : base(context) { }

    public async Task<Payment?> GetByOrderIdAsync(int orderId) =>
        await _dbSet.FirstOrDefaultAsync(p => p.OrderId == orderId);

    public async Task<Payment?> GetByMerchantOidAsync(string merchantOid) =>
        await _dbSet.Include(p => p.Order).ThenInclude(o => o.Items)
                    .FirstOrDefaultAsync(p => p.PayTRMerchantOid == merchantOid);
}
