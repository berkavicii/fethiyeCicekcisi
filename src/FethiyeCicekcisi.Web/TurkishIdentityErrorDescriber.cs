using Microsoft.AspNetCore.Identity;

namespace FethiyeCicekcisi.Web;

/// <summary>ASP.NET Identity'nin varsayılan (İngilizce) hata mesajlarının Türkçe karşılıkları.
/// Kayıt/giriş/şifre formlarında kullanıcıya gösterilen tüm Identity hataları buradan geçer.
/// Bu sitede kullanıcı adı = e-posta olduğundan "user name" hataları da e-posta diliyle yazıldı.</summary>
public class TurkishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Bilinmeyen bir hata oluştu. Lütfen tekrar deneyin." };

    public override IdentityError ConcurrencyFailure() =>
        new() { Code = nameof(ConcurrencyFailure), Description = "Kayıt siz işlem yaparken başka bir yerden değiştirildi. Lütfen sayfayı yenileyip tekrar deneyin." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Mevcut şifreniz hatalı." };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "Bağlantının süresi dolmuş ya da geçersiz. Lütfen işlemi yeniden başlatın." };

    public override IdentityError LoginAlreadyAssociated() =>
        new() { Code = nameof(LoginAlreadyAssociated), Description = "Bu hesap zaten başka bir kullanıcıya bağlı." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = "Geçerli bir e-posta adresi giriniz." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "Geçerli bir e-posta adresi giriniz." };

    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = "Bu e-posta adresiyle zaten bir hesap var. Giriş yapmayı ya da şifre sıfırlamayı deneyebilirsiniz." };

    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "Bu e-posta adresiyle zaten bir hesap var. Giriş yapmayı ya da şifre sıfırlamayı deneyebilirsiniz." };

    public override IdentityError InvalidRoleName(string? role) =>
        new() { Code = nameof(InvalidRoleName), Description = $"'{role}' geçerli bir rol adı değil." };

    public override IdentityError DuplicateRoleName(string role) =>
        new() { Code = nameof(DuplicateRoleName), Description = $"'{role}' rolü zaten mevcut." };

    public override IdentityError UserAlreadyHasPassword() =>
        new() { Code = nameof(UserAlreadyHasPassword), Description = "Bu hesabın zaten bir şifresi var." };

    public override IdentityError UserLockoutNotEnabled() =>
        new() { Code = nameof(UserLockoutNotEnabled), Description = "Bu hesap için kilitleme etkin değil." };

    public override IdentityError UserAlreadyInRole(string role) =>
        new() { Code = nameof(UserAlreadyInRole), Description = "Kullanıcı bu role zaten sahip." };

    public override IdentityError UserNotInRole(string role) =>
        new() { Code = nameof(UserNotInRole), Description = "Kullanıcı bu role sahip değil." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Şifreniz en az {length} karakter olmalı." };

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Şifreniz en az bir özel karakter (!, @, # gibi) içermeli." };

    public override IdentityError PasswordRequiresDigit() =>
        new() { Code = nameof(PasswordRequiresDigit), Description = "Şifreniz en az bir rakam (0-9) içermeli." };

    public override IdentityError PasswordRequiresLower() =>
        new() { Code = nameof(PasswordRequiresLower), Description = "Şifreniz en az bir küçük harf (a-z) içermeli." };

    public override IdentityError PasswordRequiresUpper() =>
        new() { Code = nameof(PasswordRequiresUpper), Description = "Şifreniz en az bir büyük harf (A-Z) içermeli." };

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Şifreniz en az {uniqueChars} farklı karakter içermeli." };

    public override IdentityError RecoveryCodeRedemptionFailed() =>
        new() { Code = nameof(RecoveryCodeRedemptionFailed), Description = "Kurtarma kodu doğrulanamadı." };
}
