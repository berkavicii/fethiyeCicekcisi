namespace KedWear.Core.Enums;

public enum OrderStatus
{
    Pending = 0,
    PaymentPending = 1,
    PaymentSuccess = 2,
    PaymentFailed = 3,
    Processing = 4,
    Shipped = 5,
    Delivered = 6,
    Cancelled = 7,
    Refunded = 8
}

public enum PaymentStatus
{
    Pending = 0,
    Success = 1,
    Failed = 2,
    Refunded = 3
}

public enum ProductStatus
{
    Active = 0,
    Inactive = 1,
    OutOfStock = 2
}
