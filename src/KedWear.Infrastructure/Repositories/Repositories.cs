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
        string? sortBy = null,
        string? size = null)
    {
        var query = _dbSet.Include(p => p.Category).Include(p => p.Images)
            .Include(p => p.Variants.Where(v => v.IsActive && !v.IsDeleted))
            .AsQueryable();

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(size))
        {
            // Bedenler admin panelinde serbest metin girildiği için ("L" / "l") harf
            // duyarsız karşılaştırılır. Stoğu bitmiş beden "var" sayılmaz.
            var sizeLower = size.Trim().ToLower();
            query = query.Where(p => p.Variants.Any(v =>
                v.IsActive && !v.IsDeleted && v.StockQuantity > 0 &&
                v.Size != null && v.Size.Trim().ToLower() == sizeLower));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            // Postgres'te LIKE (EF'in Contains çevirisi) harf duyarlıdır — "coco" yazan
            // müşteri "Coconut"u bulamazdı. ILIKE ile harf duyarsız arama yapılır.
            var pattern = $"%{searchTerm}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern) ||
                                     (p.Description != null && EF.Functions.ILike(p.Description, pattern)));
        }

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

    public async Task<IEnumerable<string>> GetAvailableSizesAsync() =>
        await _context.Set<ProductVariant>()
            .Where(v => v.IsActive && !v.IsDeleted && v.StockQuantity > 0 &&
                        v.Size != null && v.Size != "" &&
                        !v.Product.IsDeleted && v.Product.Status == Core.Enums.ProductStatus.Active)
            .Select(v => v.Size!)
            .Distinct()
            .ToListAsync();

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

    public async Task<bool> HasOrderItemsAsync(int productId) =>
        await _context.Set<OrderItem>().AnyAsync(oi => oi.ProductId == productId);

    public async Task<bool> SlugExistsAsync(string slug, int? excludeId = null) =>
        await _dbSet.IgnoreQueryFilters()
                    .AnyAsync(p => p.Slug == slug && (!excludeId.HasValue || p.Id != excludeId.Value));
}

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context) { }

    public async Task<Category?> GetBySlugAsync(string slug) =>
        await _dbSet.Include(c => c.Products).FirstOrDefaultAsync(c => c.Slug == slug);

    public async Task<IEnumerable<Category>> GetActiveAsync() =>
        await _dbSet.Where(c => c.IsActive && !c.IsDeleted)
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

    public async Task<CartItem?> GetCartItemAsync(string? userId, string? sessionId, int productId, int? variantId, string? pantSize = null) =>
        await _dbSet.FirstOrDefaultAsync(ci =>
            ((userId != null && ci.UserId == userId) || (userId == null && ci.SessionId == sessionId)) &&
            ci.ProductId == productId &&
            ci.ProductVariantId == variantId &&
            ci.PantSize == pantSize);

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
                ci.UserId == userId && ci.ProductId == item.ProductId && ci.ProductVariantId == item.ProductVariantId && ci.PantSize == item.PantSize);

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

    public async Task<IReadOnlyList<Core.Models.ProductSalesStat>> GetProductSalesStatsAsync(DateTime? from = null)
    {
        // Satış: ödemesi alınmış ve iptale/iadeye dönmemiş siparişler. İade ayrı sayılır ki
        // "kaç sattım" ile "kaç geri geldi" yan yana izlenebilsin. Ödemesi hiç tamamlanmayan
        // (Pending/PaymentPending/PaymentFailed) ve iptal edilen siparişler istatistiğe girmez.
        var saleStatuses = new[]
        {
            Core.Enums.OrderStatus.PaymentSuccess,
            Core.Enums.OrderStatus.Processing,
            Core.Enums.OrderStatus.Shipped,
            Core.Enums.OrderStatus.Delivered
        };

        // IgnoreQueryFilters: soft-delete edilmiş ürünün satırları da rapora girmeli;
        // Product'taki query filter inner join'de o satırları sessizce düşürürdü.
        var rows = await _context.Set<OrderItem>()
            .IgnoreQueryFilters()
            .Where(oi => saleStatuses.Contains(oi.Order.Status) || oi.Order.Status == Core.Enums.OrderStatus.Refunded)
            .Where(oi => !from.HasValue || oi.Order.CreatedAt >= from.Value)
            .Select(oi => new
            {
                oi.ProductId,
                oi.ProductName,
                oi.ProductImageUrl,
                oi.Quantity,
                oi.TotalPrice,
                oi.OrderId,
                oi.Order.Status,
                CategoryName = (string?)oi.Product.Category.Name,
                oi.Product.Slug,
                ProductDeleted = oi.Product.IsDeleted
            })
            .ToListAsync();

        // Butik ölçeğinde satır sayısı küçük; gruplamayı bellekte yapmak hem yeterli hem de
        // koşullu COUNT(DISTINCT) çevirisi gibi sağlayıcı kısıtlarından bağımsız.
        return rows
            .GroupBy(r => r.ProductId)
            .Select(g =>
            {
                var sales = g.Where(r => r.Status != Core.Enums.OrderStatus.Refunded).ToList();
                var refunds = g.Where(r => r.Status == Core.Enums.OrderStatus.Refunded).ToList();
                var last = g.OrderByDescending(r => r.OrderId).First();
                return new Core.Models.ProductSalesStat
                {
                    ProductId = g.Key,
                    ProductName = last.ProductName,
                    ImageUrl = last.ProductImageUrl,
                    CategoryName = last.CategoryName,
                    Slug = last.Slug,
                    ProductDeleted = last.ProductDeleted,
                    UnitsSold = sales.Sum(r => r.Quantity),
                    Revenue = sales.Sum(r => r.TotalPrice),
                    UnitsRefunded = refunds.Sum(r => r.Quantity)
                };
            })
            .OrderByDescending(s => s.UnitsSold)
            .ThenByDescending(s => s.Revenue)
            .ToList();
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
                    .ThenInclude(i => i.ProductVariant)
                    .FirstOrDefaultAsync(p => p.PayTRMerchantOid == merchantOid);
}
