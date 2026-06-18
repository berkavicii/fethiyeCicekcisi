using KedWear.Application.Services;
using KedWear.Web.ViewModels.Cart;
using Microsoft.AspNetCore.Mvc;

namespace KedWear.Web.Controllers;

[Route("sepet")]
public class CartController : Controller
{
    private readonly CartService _cartService;
    private const string SessionIdKey = "cart_session_id";

    public CartController(CartService cartService) => _cartService = cartService;

    private string? GetUserId() => User.Identity?.IsAuthenticated == true
        ? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        : null;

    private string GetSessionId()
    {
        var sessionId = HttpContext.Session.GetString(SessionIdKey);
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
            HttpContext.Session.SetString(SessionIdKey, sessionId);
        }
        return sessionId;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var sessionId = GetSessionId();
        var items = await _cartService.GetCartItemsAsync(userId, sessionId);
        var subTotal = items.Sum(i => i.TotalPrice);

        var vm = new CartViewModel
        {
            Items = items,
            ShippingCost = subTotal >= 500 ? 0 : 49.90m,
            FreeShippingThreshold = 500
        };
        return View(vm);
    }

    [HttpPost("ekle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add([FromBody] AddToCartViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { success = false, message = "Geçersiz istek." });

        var userId = GetUserId();
        var sessionId = GetSessionId();

        var (success, message) = await _cartService.AddToCartAsync(userId, sessionId, model.ProductId, model.VariantId, model.Quantity);
        var count = await _cartService.GetCartCountAsync(userId, sessionId);

        return Json(new { success, message, cartCount = count });
    }

    [HttpPost("guncelle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateCartModel model)
    {
        var userId = GetUserId();
        var sessionId = GetSessionId();

        var (success, message) = await _cartService.UpdateQuantityAsync(userId, sessionId, id, model.Quantity);
        var items = await _cartService.GetCartItemsAsync(userId, sessionId);
        var subTotal = items.Sum(i => i.TotalPrice);
        var shipping = subTotal >= 500 ? 0 : 49.90m;

        return Json(new
        {
            success,
            message,
            subTotal = subTotal.ToString("N2"),
            total = (subTotal + shipping).ToString("N2"),
            shipping = shipping.ToString("N2"),
            cartCount = items.Sum(i => i.Quantity)
        });
    }

    [HttpPost("sil/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var userId = GetUserId();
        var sessionId = GetSessionId();

        var success = await _cartService.RemoveFromCartAsync(userId, sessionId, id);
        var count = await _cartService.GetCartCountAsync(userId, sessionId);

        return Json(new { success, cartCount = count });
    }

    [HttpGet("ozet")]
    public async Task<IActionResult> Summary()
    {
        var userId = GetUserId();
        var sessionId = GetSessionId();
        var count = await _cartService.GetCartCountAsync(userId, sessionId);
        var total = await _cartService.GetCartTotalAsync(userId, sessionId);
        return Json(new { count, total = total.ToString("N2") });
    }
}

public class UpdateCartModel
{
    public int Quantity { get; set; }
}
