using FethiyeCicekcisi.Core.Entities;
using FethiyeCicekcisi.Core.Enums;
using FethiyeCicekcisi.Core.Interfaces.Repositories;
using FethiyeCicekcisi.Core.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FethiyeCicekcisi.Application.Services;

/// <summary>Sipariş formundan gelen, adres dışındaki çiçekçilik alanları — gönderen
/// bilgileri ve teslimat planı. Alıcı adresi ayrıca <see cref="Address"/> ile taşınır.</summary>
public record CheckoutDetails(
    string SenderName,
    string SenderPhone,
    string SenderEmail,
    bool IsAnonymousSender,
    DateOnly DeliveryDate,
    DeliveryTimeSlot DeliveryTimeSlot,
    string? CardMessage,
    string? Notes,
    decimal ZoneFee,
    decimal Discount = 0,
    string? PromoCode = null,
    string? GuestSessionId = null);

public class OrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepo,
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IPaymentRepository paymentRepo,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<OrderService> logger)
    {
        _orderRepo = orderRepo;
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _paymentRepo = paymentRepo;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public Task<Order?> GetOrderByIdAsync(int id) => _orderRepo.GetWithItemsAndPaymentAsync(id);

    public Task<Order?> GetOrderByNumberAsync(string orderNumber) => _orderRepo.GetByOrderNumberAsync(orderNumber);

    public Task<IEnumerable<Order>> GetUserOrdersAsync(string userId) => _orderRepo.GetByUserIdAsync(userId);

    public async Task<(IEnumerable<Order> Orders, int TotalCount, int TotalPages)> GetPagedOrdersAsync(
        int page = 1, int pageSize = 20, string? status = null)
    {
        var (orders, total) = await _orderRepo.GetPagedAsync(page, pageSize, status);
        return (orders, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public Task<IReadOnlyList<Core.Models.ProductSalesStat>> GetProductSalesStatsAsync(DateTime? from = null) =>
        _orderRepo.GetProductSalesStatsAsync(from);

    public async Task<Order> CreateOrderFromCartAsync(
        string? userId, string? sessionId, Address recipientAddress, CheckoutDetails details)
    {
        var cartItems = (await _cartRepo.GetCartItemsAsync(userId, sessionId)).ToList();
        if (!cartItems.Any())
            throw new InvalidOperationException("Sepet boş.");

        var orderItems = new List<OrderItem>();
        decimal subTotal = 0;

        foreach (var item in cartItems)
        {
            var product = await _productRepo.GetWithImagesAndVariantsAsync(item.ProductId);
            if (product is null) continue;

            string? variantInfo = null;
            if (!string.IsNullOrEmpty(item.ProductVariant?.Size))
                variantInfo = $"Seçenek: {item.ProductVariant.Size}";

            // Sepete eklendikten sonra stok değişmiş olabilir (başka bir müşteri son
            // adedi almış olabilir) — ödemeye geçilmeden burada son bir kez doğrulanır.
            if (item.ProductVariantId.HasValue)
            {
                var variant = product.Variants.FirstOrDefault(v => v.Id == item.ProductVariantId.Value);
                var stock = variant is { IsActive: true } ? variant.StockQuantity : 0;
                if (stock < item.Quantity)
                {
                    var label = string.IsNullOrEmpty(variantInfo) ? product.Name : $"{product.Name} ({variantInfo})";
                    throw new InvalidOperationException(stock == 0
                        ? $"'{label}' tükendi. Lütfen sepetinizden çıkarın."
                        : $"'{label}' için stokta yalnızca {stock} adet kaldı. Lütfen sepetinizdeki adedi güncelleyin.");
                }
            }

            var lineTotal = item.UnitPrice * item.Quantity;
            subTotal += lineTotal;

            orderItems.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductVariantId = item.ProductVariantId,
                ProductName = product.Name,
                VariantInfo = variantInfo,
                ProductImageUrl = product.MainImageUrl ?? product.Images.FirstOrDefault()?.ImageUrl,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = lineTotal,
                CreatedAt = DateTime.UtcNow
            });
        }

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            Status = OrderStatus.PaymentPending,
            SenderName = details.SenderName,
            SenderPhone = details.SenderPhone,
            SenderEmail = details.SenderEmail,
            IsAnonymousSender = details.IsAnonymousSender,
            RecipientFirstName = recipientAddress.FirstName,
            RecipientLastName = recipientAddress.LastName,
            RecipientPhone = recipientAddress.Phone,
            RecipientAddressLine1 = recipientAddress.AddressLine1,
            RecipientAddressLine2 = recipientAddress.AddressLine2,
            RecipientCity = recipientAddress.City,
            RecipientDistrict = recipientAddress.District,
            RecipientZipCode = recipientAddress.ZipCode,
            DeliveryDate = details.DeliveryDate,
            DeliveryTimeSlot = details.DeliveryTimeSlot,
            CardMessage = string.IsNullOrWhiteSpace(details.CardMessage) ? null : details.CardMessage.Trim(),
            SubTotal = subTotal,
            // Teslimat ücreti bölgeye göre; indirim promosyon kodundan (ara toplamı aşamaz).
            ShippingCost = details.ZoneFee,
            Discount = Math.Min(details.Discount, subTotal),
            PromoCode = details.PromoCode,
            GuestSessionId = userId == null ? details.GuestSessionId : null,
            GuestEmail = userId == null ? details.SenderEmail : string.Empty,
            Notes = details.Notes,
            Items = orderItems,
            CreatedAt = DateTime.UtcNow
        };

        order.TotalAmount = order.SubTotal + order.ShippingCost - order.Discount;

        await _orderRepo.AddAsync(order);
        await _orderRepo.SaveChangesAsync();

        return order;
    }

    public async Task UpdateOrderStatusAsync(int orderId, OrderStatus status, string? trackingNumber = null)
    {
        var order = await _orderRepo.GetByIdAsync(orderId);
        if (order is null) return;

        order.Status = status;
        if (!string.IsNullOrEmpty(trackingNumber))
            order.TrackingNumber = trackingNumber;
        order.UpdatedAt = DateTime.UtcNow;

        _orderRepo.Update(order);
        await _orderRepo.SaveChangesAsync();
    }

    public async Task HandlePaymentCallbackAsync(string merchantOid, bool isSuccess, string? transactionId = null, string? failReason = null, string? rawResponse = null)
    {
        var payment = await _paymentRepo.GetByMerchantOidAsync(merchantOid);
        if (payment is null) return;

        payment.Status = isSuccess ? PaymentStatus.Success : PaymentStatus.Failed;
        payment.TransactionId = transactionId;
        payment.FailReason = failReason;
        payment.RawResponse = rawResponse;
        payment.PaidAt = isSuccess ? DateTime.UtcNow : null;
        payment.UpdatedAt = DateTime.UtcNow;

        payment.Order.Status = isSuccess ? OrderStatus.Processing : OrderStatus.PaymentFailed;
        payment.Order.UpdatedAt = DateTime.UtcNow;

        // Stok, sipariş oluşturulduğunda değil ödeme kesinleştiğinde düşülür — ödemesi
        // tamamlanmayan (iframe'i kapatan) siparişler stok kilitlememeli. Math.Max ile
        // sıfırın altına inmesi engellenir (aynı son ürüne eşzamanlı iki ödeme gelirse).
        if (isSuccess)
        {
            foreach (var item in payment.Order.Items)
            {
                if (item.ProductVariant is not null)
                    item.ProductVariant.StockQuantity = Math.Max(0, item.ProductVariant.StockQuantity - item.Quantity);
            }
        }

        _paymentRepo.Update(payment);
        await _paymentRepo.SaveChangesAsync();

        if (isSuccess)
        {
            // Üyeli siparişte kullanıcı sepeti, misafir siparişte oturum sepeti temizlenir.
            if (payment.Order.UserId != null)
                await _cartRepo.ClearCartAsync(payment.Order.UserId, null);
            else if (!string.IsNullOrEmpty(payment.Order.GuestSessionId))
                await _cartRepo.ClearCartAsync(null, payment.Order.GuestSessionId);
        }

        if (isSuccess)
            await SendOrderConfirmationEmailsAsync(payment.Order);
    }

    /// <summary>Fired once payment is actually confirmed (not at checkout submission, since
    /// the payment can still fail after that) — emails the sender (buyer) and, separately,
    /// notifies the store's contact address so a new order doesn't go unnoticed.</summary>
    private async Task SendOrderConfirmationEmailsAsync(Order order)
    {
        if (!string.IsNullOrWhiteSpace(order.SenderEmail))
        {
            try
            {
                await _emailService.SendAsync(order.SenderEmail, order.SenderName,
                    $"Siparişiniz Alındı — {order.OrderNumber}", EmailTemplates.OrderConfirmationCustomer(order));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sipariş onay e-postası gönderilemedi: {OrderNumber}", order.OrderNumber);
            }
        }

        var adminEmail = _configuration["SiteSettings:ContactEmail"];
        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            try
            {
                await _emailService.SendAsync(adminEmail, "Yonca Çiçekçilik",
                    $"Yeni Sipariş — {order.OrderNumber}", EmailTemplates.OrderNotificationAdmin(order));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin sipariş bildirimi gönderilemedi: {OrderNumber}", order.OrderNumber);
            }
        }
    }

    private static string GenerateOrderNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyMMddHHmmss");
        var random = Random.Shared.Next(100, 999);
        return $"YC{timestamp}{random}";
    }
}
