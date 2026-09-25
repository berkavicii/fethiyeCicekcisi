using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace FethiyeCicekcisi.Web.Validation;

/// <summary>Bir checkbox'ın işaretli olmasını zorunlu kılar. [Range(typeof(bool),"true","true")]
/// kullanılmıyor çünkü jQuery Validate'in unobtrusive range adaptörü bool min/max'i "True"/"False"
/// string'i olarak render eder ve sayısal aralık ayrıştırıcısı bunu sessizce atlar.</summary>
public class MustBeTrueAttribute : ValidationAttribute, IClientModelValidator
{
    public MustBeTrueAttribute() : base("Bu alanı onaylamanız gerekmektedir.") { }

    public override bool IsValid(object? value) => value is true;

    public void AddValidation(ClientModelValidationContext context)
    {
        context.Attributes.TryAdd("data-val", "true");
        context.Attributes.TryAdd("data-val-mustbetrue", FormatErrorMessage(string.Empty));
    }
}
