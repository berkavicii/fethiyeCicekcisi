using FethiyeCicekcisi.Core.Enums;

namespace FethiyeCicekcisi.Core.Entities;

public class Payment : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public string? PayTRMerchantOid { get; set; }
    public string? PayTRToken { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; }
    public string? TransactionId { get; set; }
    public string? FailReason { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? RawResponse { get; set; }
}
