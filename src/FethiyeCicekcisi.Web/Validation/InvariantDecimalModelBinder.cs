using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace FethiyeCicekcisi.Web.Validation;

/// <summary>decimal/decimal? alanları her zaman "." ondalık ayracıyla (InvariantCulture) ayrıştırır,
/// sunucunun ambient kültüründen bağımsız olarak. HTML5 &lt;input type="number"&gt; ve JS'ten giden
/// FormData sayısal değerleri her zaman "1750.00" biçiminde gönderir; sunucunun görüntüleme için
/// sabitlenen kültürü tr-TR olduğunda (Program.cs) bu değer "." binlik ayracı sanılıp 175000'e
/// dönüşürdü — fiyat/ücret alanlarında sessiz 100 kat hataya yol açan asıl kök neden buydu.</summary>
public class InvariantDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.ModelName;
        var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);
        if (valueProviderResult == ValueProviderResult.None)
            return Task.CompletedTask;

        bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;
        var isNullable = Nullable.GetUnderlyingType(bindingContext.ModelType) != null;

        if (string.IsNullOrWhiteSpace(value))
        {
            if (isNullable)
                bindingContext.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
        {
            bindingContext.Result = ModelBindingResult.Success(result);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(modelName,
                bindingContext.ModelMetadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(value));
        }

        return Task.CompletedTask;
    }
}

public class InvariantDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?))
            return new InvariantDecimalModelBinder();

        return null;
    }
}
