using KedWear.Core.Entities;
using KedWear.Core.Enums;
using KedWear.Core.Interfaces.Repositories;

namespace KedWear.Application.Services;

public class OrderService
{
    private readonly IOrderRepository _orderRepo;
    private readonly ICartRepository _cartRepo;
    private readonly IProductRepository _productRepo;
    private readonly IPaymentRepository _paymentRepo;

    public OrderService(
        IOrderRepository orderRepo,
        ICartRepository cartRepo,
        IProductRepository productRepo,
        IPaymentRepository paymentRepo)
    {
        _orderRepo = orderRepo;
        _cartRepo = cartRepo;
        _productRepo = productRepo;
        _paymentRepo = paymentRepo;
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

    public async Task<Order> CreateOrderFromCartAsync(
        string? userId, string? sessionId, Address shippingAddress, string? notes = null)
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
            if (item.ProductVariant != null)
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(item.ProductVariant.Size)) parts.Add($"Beden: {item.ProductVariant.Size}");
                if (!string.IsNullOrEmpty(item.ProductVariant.Color)) parts.Add($"Renk: {item.ProductVariant.Color}");
                variantInfo = string.Join(", ", parts);
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
            ShippingFirstName = shippingAddress.FirstName,
            ShippingLastName = shippingAddress.LastName,
            ShippingPhone = shippingAddress.Phone,
            ShippingEmail = shippingAddress.User?.Email ?? string.Empty,
            ShippingAddressLine1 = shippingAddress.AddressLine1,
            ShippingAddressLine2 = shippingAddress.AddressLine2,
            ShippingCity = shippingAddress.City,
            ShippingDistrict = shippingAddress.District,
            ShippingZipCode = shippingAddress.ZipCode,
            SubTotal = subTotal,
            ShippingCost = subTotal >= 500 ? 0 : 49.90m,
            Discount = 0,
            Notes = notes,
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

        _paymentRepo.Update(payment);
        await _paymentRepo.SaveChangesAsync();

        if (isSuccess && payment.Order.UserId != null)
            await _cartRepo.ClearCartAsync(payment.Order.UserId, null);
    }

    private static string GenerateOrderNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyMMddHHmmss");
        var random = Random.Shared.Next(100, 999);
        return $"KW{timestamp}{random}";
    }
}
