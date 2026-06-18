using KedWear.Application.Services;
using KedWear.Core.Entities;
using KedWear.Core.Interfaces.Repositories;
using KedWear.Web.ViewModels.Order;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace KedWear.Web.Controllers;

[Route("siparis")]
public class OrderController : Controller
{
    private readonly OrderService _orderService;
    private readonly CartService _cartService;
    private readonly PayTRService _payTRService;
    private readonly IPaymentRepository _paymentRepo;
    private readonly IAddressRepository _addressRepo;
    private readonly UserManager<AppUser> _userManager;
    private readonly IConfiguration _config;
    private const string SessionIdKey = "cart_session_id";

    public OrderController(
        OrderService orderService,
        CartService cartService,
        PayTRService payTRService,
        IPaymentRepository paymentRepo,
        IAddressRepository addressRepo,
        UserManager<AppUser> userManager,
        IConfiguration config)
    {
        _orderService = orderService;
        _cartService = cartService;
        _payTRService = payTRService;
        _paymentRepo = paymentRepo;
        _addressRepo = addressRepo;
        _userManager = userManager;
        _config = config;
    }

    private string? GetUserId() => User.Identity?.IsAuthenticated == true
        ? _userManager.GetUserId(User)
        : null;

    private string GetSessionId() => HttpContext.Session.GetString(SessionIdKey) ?? string.Empty;

    [HttpGet("odeme")]
    [Authorize]
    public async Task<IActionResult> Checkout()
    {
        var userId = GetUserId();
        var sessionId = GetSessionId();
        var items = (await _cartService.GetCartItemsAsync(userId, sessionId)).ToList();

        if (!items.Any())
            return RedirectToAction("Index", "Cart");

        var subTotal = items.Sum(i => i.TotalPrice);
        var shipping = subTotal >= 500 ? 0 : 49.90m;

        var vm = new CheckoutViewModel
        {
            CartItems = items,
            SubTotal = subTotal,
            ShippingCost = shipping,
            TotalAmount = subTotal + shipping
        };

        if (userId != null)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                vm.FirstName = user.FirstName;
                vm.LastName = user.LastName;
                vm.Email = user.Email ?? string.Empty;
                vm.Phone = user.PhoneNumber ?? string.Empty;
            }
            vm.SavedAddresses = await _addressRepo.GetByUserIdAsync(userId);
        }

        return View(vm);
    }

    [HttpPost("odeme")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(CheckoutViewModel model)
    {
        var userId = GetUserId();
        var sessionId = GetSessionId();
        var items = (await _cartService.GetCartItemsAsync(userId, sessionId)).ToList();

        if (!items.Any())
            return RedirectToAction("Index", "Cart");

        if (!ModelState.IsValid)
        {
            var subTotal = items.Sum(i => i.TotalPrice);
            model.CartItems = items;
            model.SubTotal = subTotal;
            model.ShippingCost = subTotal >= 500 ? 0 : 49.90m;
            model.TotalAmount = model.SubTotal + model.ShippingCost;
            if (userId != null) model.SavedAddresses = await _addressRepo.GetByUserIdAsync(userId);
            return View(model);
        }

        var shippingAddress = new Address
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Phone = model.Phone,
            AddressLine1 = model.AddressLine1,
            AddressLine2 = model.AddressLine2,
            City = model.City,
            District = model.District,
            ZipCode = model.ZipCode,
            User = userId != null ? await _userManager.FindByIdAsync(userId) : null
        };

        if (shippingAddress.User != null)
            shippingAddress.User.Email = model.Email;

        if (userId != null && model.SaveAddress)
        {
            shippingAddress.UserId = userId;
            shippingAddress.Title = $"{model.City} Adresim";
            await _addressRepo.AddAsync(shippingAddress);
            await _addressRepo.SaveChangesAsync();
        }

        var order = await _orderService.CreateOrderFromCartAsync(userId, sessionId, shippingAddress, model.Notes);
        order.ShippingEmail = model.Email;

        // Get user IP
        var userIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        if (userIp == "::1") userIp = "127.0.0.1";

        var basketItems = items.Select(i => new object[]
        {
            i.Product?.Name ?? "Ürün",
            i.UnitPrice.ToString("F2"),
            i.Quantity.ToString()
        }).ToList();

        var (success, iframeToken, errorMsg) = await _payTRService.GetIframeTokenAsync(
            order.OrderNumber,
            model.Email,
            order.TotalAmount,
            userIp,
            $"{model.FirstName} {model.LastName}",
            $"{model.AddressLine1}, {model.District}, {model.City}",
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

        return View(new OrderDetailViewModel { Order = order });
    }
}
