using KedWear.Core.Entities;
using KedWear.Core.Interfaces.Repositories;

namespace KedWear.Application.Services;

public class CartService
{
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;

    public CartService(ICartRepository cartRepo, IProductRepository productRepo)
    {
        _cartRepo = cartRepo;
        _productRepo = productRepo;
    }

    public Task<IEnumerable<CartItem>> GetCartItemsAsync(string? userId, string? sessionId) =>
        _cartRepo.GetCartItemsAsync(userId, sessionId);

    public async Task<decimal> GetCartTotalAsync(string? userId, string? sessionId)
    {
        var items = await _cartRepo.GetCartItemsAsync(userId, sessionId);
        return items.Sum(i => i.TotalPrice);
    }

    public async Task<int> GetCartCountAsync(string? userId, string? sessionId)
    {
        var items = await _cartRepo.GetCartItemsAsync(userId, sessionId);
        return items.Sum(i => i.Quantity);
    }

    public async Task<(bool Success, string Message)> AddToCartAsync(
        string? userId, string? sessionId, int productId, int? variantId, int quantity = 1, string? pantSize = null)
    {
        var product = await _productRepo.GetWithImagesAndVariantsAsync(productId);
        if (product is null) return (false, "Ürün bulunamadı.");

        if (product.Status != Core.Enums.ProductStatus.Active)
            return (false, "Bu ürün şu anda satışta değil.");

        int availableStock;
        decimal unitPrice = product.CurrentPrice;

        if (variantId.HasValue)
        {
            var variant = product.Variants.FirstOrDefault(v => v.Id == variantId.Value);
            if (variant is null) return (false, "Seçilen varyant bulunamadı.");
            if (!variant.IsActive) return (false, "Bu varyant aktif değil.");
            availableStock = variant.StockQuantity;
            unitPrice += variant.PriceDifference ?? 0;
        }
        else
        {
            // Varyantsız ürün = stok tanımlanmamış ürün; satışa kapalıdır (aksi hâlde
            // stok takibi hiç yapılmadan sınırsız satılırdı). Stok, admin panelinden
            // ya da seed manifest'indeki bedenler/stok satırıyla varyant olarak girilir.
            availableStock = product.Variants.Where(v => v.IsActive).Sum(v => v.StockQuantity);
        }

        var existing = await _cartRepo.GetCartItemAsync(userId, sessionId, productId, variantId, pantSize);
        var newQty = (existing?.Quantity ?? 0) + quantity;

        if (availableStock < newQty)
            return (false, $"Stokta yalnızca {availableStock} adet kaldı.");

        if (existing != null)
        {
            existing.Quantity = newQty;
            _cartRepo.Update(existing);
        }
        else
        {
            await _cartRepo.AddAsync(new CartItem
            {
                UserId = userId,
                SessionId = userId == null ? sessionId : null,
                ProductId = productId,
                ProductVariantId = variantId,
                PantSize = pantSize,
                Quantity = quantity,
                UnitPrice = unitPrice,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _cartRepo.SaveChangesAsync();
        return (true, "Ürün sepete eklendi.");
    }

    public async Task<(bool Success, string Message)> UpdateQuantityAsync(
        string? userId, string? sessionId, int cartItemId, int quantity)
    {
        var item = await _cartRepo.GetByIdAsync(cartItemId);
        if (item is null) return (false, "Sepet öğesi bulunamadı.");

        var isOwner = (userId != null && item.UserId == userId) ||
                      (userId == null && item.SessionId == sessionId);
        if (!isOwner) return (false, "Yetkisiz işlem.");

        if (quantity <= 0)
        {
            _cartRepo.Remove(item);
        }
        else
        {
            // Sepete eklemedeki stok kontrolünün aynısı — bu olmadan müşteri 1 adet
            // ekleyip sepet sayfasından adedi stokun üzerine çıkarabilirdi.
            var product = await _productRepo.GetWithImagesAndVariantsAsync(item.ProductId);
            var availableStock = 0;
            if (product is not null)
            {
                availableStock = item.ProductVariantId.HasValue
                    ? product.Variants.FirstOrDefault(v => v.Id == item.ProductVariantId.Value && v.IsActive)?.StockQuantity ?? 0
                    : product.Variants.Where(v => v.IsActive).Sum(v => v.StockQuantity);
            }

            if (quantity > availableStock)
                return (false, $"Stokta yalnızca {availableStock} adet var.");

            item.Quantity = quantity;
            _cartRepo.Update(item);
        }

        await _cartRepo.SaveChangesAsync();
        return (true, "Güncellendi.");
    }

    public async Task<bool> RemoveFromCartAsync(string? userId, string? sessionId, int cartItemId)
    {
        var item = await _cartRepo.GetByIdAsync(cartItemId);
        if (item is null) return false;

        var isOwner = (userId != null && item.UserId == userId) ||
                      (userId == null && item.SessionId == sessionId);
        if (!isOwner) return false;

        _cartRepo.Remove(item);
        await _cartRepo.SaveChangesAsync();
        return true;
    }

    public Task ClearCartAsync(string? userId, string? sessionId) =>
        _cartRepo.ClearCartAsync(userId, sessionId);

    public Task MigrateGuestCartAsync(string sessionId, string userId) =>
        _cartRepo.MigrateGuestCartAsync(sessionId, userId);
}
