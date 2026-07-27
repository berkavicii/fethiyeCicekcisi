using FethiyeCicekcisi.Application.Services;
using FethiyeCicekcisi.Core.Entities;
using FethiyeCicekcisi.Core.Enums;
using FethiyeCicekcisi.Core.Interfaces.Repositories;
using FethiyeCicekcisi.Web.ViewModels.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FethiyeCicekcisi.Web.Controllers;

[Route("siparis")]
public class OrderController : Controller
{
    private readonly OrderService _orderService;
    private readonly CartService _cartService;
    private readonly PayTRService _payTRService;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IDeliveryZoneRepository _zoneRepo;
    private readonly IPromoCodeRepository _promoRepo;
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;
    private const string SessionIdKey = "cart_session_id";

    public OrderController(
        OrderService orderService,
        CartService cartService,
        PayTRService payTRService,
        IPaymentRepository paymentRepo,
        IDeliveryZoneRepository zoneRepo,
        IPromoCodeRepository promoRepo,
        UserManager<AppUser> userManager,
        IConfiguration config)
    {
        _orderService = orderService;
        _cartService = cartService;
        _payTRService = payTRService;
        _paymentRepo = paymentRepo;
        _zoneRepo = zoneRepo;
        _promoRepo = promoRepo;
        _userManager = userManager;
        _config = config;
    }

    private string? GetUserId() => User.Identity?.IsAuthenticated == true
        ? _userManager.GetUserId(User)
        : null;

    private string GetSessionId() => HttpContext.Session.GetString(SessionIdKey) ?? string.Empty;

    /// <summary>Sepetteki TÜM ürünler aynı gün teslimata uygunsa bugün, aksi hâlde en
    /// erken yarın teslim edilebilir.</summary>
    private static DateOnly ComputeMinDeliveryDate(IReadOnlyCollection<CartItem> items)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var allSameDay = items.All(i => i.Product?.IsSameDayDelivery == true);
        return allSameDay ? today : today.AddDays(1);
    }

    /// <summary>Promosyon kodunun ara toplam üzerinden indirimi (kod geçersizse 0).</summary>
    private static decimal ComputeDiscount(PromoCode? promo, decimal subTotal) => promo switch
    {
        null => 0,
        { DiscountType: PromoDiscountType.Percent } => Math.Round(subTotal * promo.Amount / 100m, 2),
        _ => Math.Min(promo.Amount, subTotal)
    };

    // Üyeliksiz sipariş: [Authorize] yok — misafir sepeti oturum kimliğiyle taşınır.
    [HttpGet("odeme")]
    public async Task<IActionResult> Checkout()
    {
        var userId = GetUserId();
        var sessionId = GetSessionId();
        var items = (await _cartService.GetCartItemsAsync(userId, sessionId)).ToList();

        if (!items.Any())
            return RedirectToAction("Index", "Cart");

        var subTotal = items.Sum(i => i.TotalPrice);
        var minDate = ComputeMinDeliveryDate(items);

        var vm = new CheckoutViewModel
        {
            CartItems = items,
            SubTotal = subTotal,
            ShippingCost = 0,
            TotalAmount = subTotal,
            MinDeliveryDate = minDate,
            DeliveryDate = minDate,
            AnyItemAllowsMessageCard = items.Any(i => i.Product?.AllowsMessageCard == true),
            Zones = await _zoneRepo.GetActiveAsync()
        };

        if (userId != null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                vm.SenderName = user.FullName.Trim();
                vm.SenderEmail = user.Email ?? string.Empty;
                vm.SenderPhone = user.PhoneNumber ?? string.Empty;
            }
        }

        return View(vm);
    }

    [HttpPost("odeme")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        var userId = GetUserId();
        var sessionId = GetSessionId();
        var items = (await _cartService.GetCartItemsAsync(userId, sessionId)).ToList();

        if (!items.Any())
            return RedirectToAction("Index", "Cart");

        var subTotal = items.Sum(i => i.TotalPrice);
        var minDate = ComputeMinDeliveryDate(items);
        var anyCardAllowed = items.Any(i => i.Product?.AllowsMessageCard == true);

        // Tarih alt sınırı sunucuda da doğrulanır — input'un min attribute'u aşılabilir.
        if (model.DeliveryDate.HasValue && model.DeliveryDate.Value < minDate)
            ModelState.AddModelError(nameof(model.DeliveryDate),
                $"Bu sepet için en erken teslimat tarihi {minDate:dd.MM.yyyy}.");

        if (!anyCardAllowed && !string.IsNullOrWhiteSpace(model.CardMessage))
            ModelState.AddModelError(nameof(model.CardMessage),
                "Sepetinizdeki ürünler not kartı kabul etmiyor.");

        DeliveryZone? zone = null;
        if (model.DeliveryZoneId.HasValue)
        {
            zone = await _zoneRepo.GetByIdAsync(model.DeliveryZoneId.Value);
            if (zone is null || !zone.IsActive || zone.IsDeleted)
                ModelState.AddModelError(nameof(model.DeliveryZoneId), "Geçerli bir teslimat bölgesi seçiniz.");
        }

        PromoCode? promo = null;
        if (!string.IsNullOrWhiteSpace(model.PromoCode))
        {
            promo = await _promoRepo.GetByCodeAsync(model.PromoCode);
            if (promo is null)
                ModelState.AddModelError(nameof(model.PromoCode), "Promosyon kodu geçersiz.");
        }

        if (!ModelState.IsValid)
        {
            model.CartItems = items;
            model.SubTotal = subTotal;
            model.ShippingCost = zone?.Fee ?? 0;
            model.TotalAmount = subTotal + model.ShippingCost - ComputeDiscount(promo, subTotal);
            model.MinDeliveryDate = minDate;
            model.AnyItemAllowsMessageCard = anyCardAllowed;
            model.Zones = await _zoneRepo.GetActiveAsync();
            return View(model);
        }

        var recipientAddress = new Address
        {
            FirstName = model.RecipientFirstName,
            LastName = model.RecipientLastName,
            Phone = model.RecipientPhone,
            AddressLine1 = model.RecipientAddressLine1,
            AddressLine2 = model.RecipientAddressLine2,
            City = zone!.City,
            District = zone.Name
        };

        var details = new CheckoutDetails(
            model.SenderName,
            model.SenderPhone,
            model.SenderEmail,
            model.IsAnonymousSender,
            model.DeliveryDate!.Value,
            model.DeliveryTimeSlot,
            anyCardAllowed ? model.CardMessage : null,
            model.Notes,
            ZoneFee: zone.Fee,
            Discount: ComputeDiscount(promo, subTotal),
            PromoCode: promo?.Code,
            GuestSessionId: sessionId);

        Order order;
        try
        {
            order = await _orderService.CreateOrderFromCartAsync(userId, sessionId, recipientAddress, details);
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Index", "Cart");
        }

        // Get user IP
        var userIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        if (userIp == "::1") userIp = "127.0.0.1";

        var basketItems = items.Select(i => new object[]
        {
            i.Product?.Name ?? "Ürün",
            i.UnitPrice.ToString("F2"),
            i.Quantity.ToString()
        }).ToList();

        if (_config.GetValue<bool>("PayTR:TestMode"))
        {
            var testPayment = new Payment
            {
                OrderId = order.Id,
                PayTRMerchantOid = order.OrderNumber,
                Amount = order.TotalAmount,
                CreatedAt = DateTime.UtcNow
            };
            await _paymentRepo.AddAsync(testPayment);
            await _paymentRepo.SaveChangesAsync();

            return View("SimulatedPayment", new OrderSummaryViewModel { Order = order });
        }

        var (success, iframeToken, errorMsg) = await _payTRService.GetIframeTokenAsync(
            order.OrderNumber,
            model.SenderEmail,
            order.TotalAmount,
            userIp,
            model.SenderName,
            $"{model.RecipientAddressLine1}, {zone.Name}, {zone.City}",
            basketItems);

        if (!success)
        {
            TempData["Error"] = $"Ödeme başlatılamadı: {errorMsg}";
            return RedirectToAction("Checkout");
        }

        // Save payment record
        var payment = new Payment
        {
            OrderId = order.Id,
            PayTRMerchantOid = order.OrderNumber,
            Amount = order.TotalAmount,
            CreatedAt = DateTime.UtcNow
        };
        await _paymentRepo.AddAsync(payment);
        await _paymentRepo.SaveChangesAsync();

        return View("PaymentIframe", new OrderSummaryViewModel
        {
            Order = order,
            PayTRIframeToken = iframeToken
        });
    }

    /// <summary>Ödeme sayfasında promosyon kodunu anında doğrulayıp indirimi döndürür —
    /// asıl hesap her zaman sunucuda POST sırasında yeniden yapılır.</summary>
    [HttpGet("promo-kontrol")]
    public async Task<IActionResult> CheckPromo(string? kod)
    {
        if (string.IsNullOrWhiteSpace(kod))
            return Json(new { valid = false, message = "Kod giriniz." });

        var userId = GetUserId();
        var items = await _cartService.GetCartItemsAsync(userId, GetSessionId());
        var subTotal = items.Sum(i => i.TotalPrice);

        var promo = await _promoRepo.GetByCodeAsync(kod);
        if (promo is null)
            return Json(new { valid = false, message = "Promosyon kodu geçersiz." });

        var discount = ComputeDiscount(promo, subTotal);
        return Json(new { valid = true, discount, message = $"Kod uygulandı: −{discount:N2} ₺" });
    }

    [HttpPost("test-odeme-sonucu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SimulatePaymentResult(string orderNumber, bool approve)
    {
        if (!_config.GetValue<bool>("PayTR:TestMode"))
            return NotFound();

        var order = await _orderService.GetOrderByNumberAsync(orderNumber);
        if (order is null) return NotFound();

        // Üyeli siparişte sahiplik kontrolü; misafir siparişinde oturumun sepeti bu
        // siparişi oluşturduysa aynı oturumdan onaylanabilir.
        var userId = GetUserId();
        var isOwner = order.UserId == userId ||
                      (order.UserId == null && order.GuestSessionId == GetSessionId());
        if (!isOwner && !User.IsInRole("Admin"))
            return Forbid();

        await _orderService.HandlePaymentCallbackAsync(
            orderNumber,
            approve,
            approve ? $"TEST-{Guid.NewGuid():N}".Substring(0, 20) : null,
            approve ? null : "Test modu: ödeme reddedildi (simülasyon)",
            "Simüle edilmiş yerel test ödemesi, PayTR'ye istek gönderilmedi.");

        return approve
            ? RedirectToAction("Success", new { siparis = orderNumber })
            : RedirectToAction("Fail", new { siparis = orderNumber });
    }

    [HttpPost("callback")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Callback(
        [FromForm] string merchant_oid,
        [FromForm] string status,
        [FromForm] string total_amount,
        [FromForm] string hash,
        [FromForm] string? failed_reason_code = null,
        [FromForm] string? failed_reason_msg = null)
    {
        if (!_payTRService.ValidateCallback(merchant_oid, status, total_amount, hash))
            return Content("PAYTR_INVALID_HASH");

        var isSuccess = status == "success";
        await _orderService.HandlePaymentCallbackAsync(
            merchant_oid, isSuccess,
            isSuccess ? merchant_oid : null,
            !isSuccess ? $"{failed_reason_code}: {failed_reason_msg}" : null,
            Request.Form.ToString());

        return Content("OK");
    }

    [HttpGet("basarili")]
    public async Task<IActionResult> Success([FromQuery] string? siparis = null)
    {
        if (siparis is null) return RedirectToAction("Index", "Home");
        var order = await _orderService.GetOrderByNumberAsync(siparis);
        if (order is null) return NotFound();
        return View(order);
    }

    [HttpGet("basarisiz")]
    public IActionResult Fail([FromQuery] string? siparis = null)
    {
        ViewBag.OrderNumber = siparis;
        return View();
    }

    [HttpGet("siparislerim")]
    [Authorize]
    public async Task<IActionResult> MyOrders()
    {
        var userId = GetUserId()!;
        var orders = await _orderService.GetUserOrdersAsync(userId);
        return View(orders);
    }

    [HttpGet("detay/{orderNumber}")]
    [Authorize]
    public async Task<IActionResult> Detail(string orderNumber)
    {
        var order = await _orderService.GetOrderByNumberAsync(orderNumber);
        if (order is null) return NotFound();

        var userId = GetUserId();
        if (order.UserId != userId && !User.IsInRole("Admin"))
            return Forbid();

        return View(order);
    }
}
