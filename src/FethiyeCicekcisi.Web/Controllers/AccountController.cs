using FethiyeCicekcisi.Application.Services;
using FethiyeCicekcisi.Core.Entities;
using FethiyeCicekcisi.Core.Interfaces.Services;
using FethiyeCicekcisi.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FethiyeCicekcisi.Web.Controllers;

[Route("hesap")]
public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly CartService _cartService;
    private readonly IEmailService _emailService;
    private const string SessionIdKey = "cart_session_id";

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        CartService cartService,
        IEmailService emailService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _cartService = cartService;
        _emailService = emailService;
    }

    [HttpGet("giris")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("giris")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var sessionId = HttpContext.Session.GetString(SessionIdKey);
                if (!string.IsNullOrEmpty(sessionId))
                    await _cartService.MigrateGuestCartAsync(sessionId, user.Id);
            }

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            if (user != null && await _userManager.IsInRoleAsync(user, "Admin"))
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin.");
            return View(model);
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "Giriş yapmadan önce e-posta adresinizi doğrulamanız gerekiyor. Gelen kutunuzu (ve spam klasörünü) kontrol edin.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
        return View(model);
    }

    [HttpGet("kayit")]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
            return RedirectToAction("Index", "Home");
        }
        return View();
    }

    [HttpPost("kayit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = new AppUser
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            UserName = model.Email,
            Email = model.Email,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Customer");

            var sessionId = HttpContext.Session.GetString(SessionIdKey);
            if (!string.IsNullOrEmpty(sessionId))
                await _cartService.MigrateGuestCartAsync(sessionId, user.Id);

            var confirmToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, token = confirmToken }, Request.Scheme)!;
            await _emailService.SendAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                "E-postanızı Doğrulayın — FethiyeCicekcisi", EmailTemplates.ConfirmEmail(user.FirstName, confirmUrl));

            // Confirmation is only actually enforced once SMTP is configured (see Program.cs) —
            // until then, sign the user in immediately since they have no way to receive the link.
            if (_userManager.Options.SignIn.RequireConfirmedEmail)
            {
                TempData["Success"] = "Kayıt başarılı! Hesabınızı aktifleştirmek için e-postanıza gönderilen bağlantıya tıklayın.";
                return RedirectToAction("Login");
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Home");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    [HttpGet("email-dogrula")]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            return RedirectToAction("Login");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            TempData["Error"] = "Doğrulama bağlantısı geçersiz.";
            return RedirectToAction("Login");
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? "E-posta adresiniz doğrulandı! Şimdi giriş yapabilirsiniz."
            : "Doğrulama bağlantısı geçersiz veya süresi dolmuş.";
        return RedirectToAction("Login");
    }

    [HttpPost("cikis")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet("profilim")]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        return View(new ProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber
        });
    }

    [HttpPost("profilim")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.PhoneNumber = model.PhoneNumber;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);
        TempData["Success"] = "Profil bilgileriniz güncellendi.";
        return RedirectToAction("Profile");
    }

    [HttpGet("sifremi-unuttum")]
    public IActionResult ForgotPassword() => View();

    [HttpPost("sifremi-unuttum")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        // Always show success to prevent email enumeration
        if (user != null)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = Url.Action("ResetPassword", "Account", new { email = model.Email, token }, Request.Scheme)!;
            await _emailService.SendAsync(user.Email!, $"{user.FirstName} {user.LastName}",
                "Şifre Sıfırlama — FethiyeCicekcisi", EmailTemplates.PasswordReset(user.FirstName, resetUrl));
        }

        TempData["Success"] = "Şifre sıfırlama bağlantısı e-posta adresinize gönderildi.";
        return RedirectToAction("Login");
    }

    [HttpGet("sifre-sifirla")]
    public IActionResult ResetPassword(string? token = null, string? email = null)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(email))
            return RedirectToAction("Login");
        return View(new ResetPasswordViewModel { Token = token, Email = email });
    }

    [HttpPost("sifre-sifirla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            // Don't reveal whether the account exists
            TempData["Success"] = "Şifreniz başarıyla sıfırlandı.";
            return RedirectToAction("Login");
        }

        var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (result.Succeeded)
        {
            TempData["Success"] = "Şifreniz başarıyla sıfırlandı, şimdi giriş yapabilirsiniz.";
            return RedirectToAction("Login");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpGet("erisim-engellendi")]
    public IActionResult AccessDenied() => View();
}
