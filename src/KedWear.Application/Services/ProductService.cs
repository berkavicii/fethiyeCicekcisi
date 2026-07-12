using KedWear.Core.Entities;
using KedWear.Core.Enums;
using KedWear.Core.Interfaces.Repositories;
using KedWear.Core.Interfaces.Services;

namespace KedWear.Application.Services;

public class ProductService
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IFileService _fileService;

    public ProductService(IProductRepository productRepo, ICategoryRepository categoryRepo, IFileService fileService)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
        _fileService = fileService;
    }

    public async Task<(IEnumerable<Product> Products, int TotalCount, int TotalPages)> GetPagedProductsAsync(
        int page = 1, int pageSize = 12, int? categoryId = null, string? searchTerm = null, string? sortBy = null)
    {
        var (products, totalCount) = await _productRepo.GetPagedAsync(page, pageSize, categoryId, searchTerm, sortBy);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        return (products, totalCount, totalPages);
    }

    public Task<Product?> GetProductBySlugAsync(string slug) =>
        _productRepo.GetWithImagesAndVariantsBySlugAsync(slug);

    public Task<Product?> GetProductByIdAsync(int id) =>
        _productRepo.GetWithImagesAndVariantsAsync(id);

    /// <summary>Like GetProductByIdAsync but includes inactive (not just active) variants,
    /// so the admin edit screen can show/reactivate a size that was toggled off.</summary>
    public Task<Product?> GetProductForAdminEditAsync(int id) =>
        _productRepo.GetForAdminEditAsync(id);

    public Task<IEnumerable<Product>> GetFeaturedProductsAsync(int count = 8) =>
        _productRepo.GetFeaturedAsync(count);

    public Task<IEnumerable<Category>> GetActiveCategoriesAsync() =>
        _categoryRepo.GetActiveAsync();

    public async Task<Product> CreateProductAsync(Product product)
    {
        product.CreatedAt = DateTime.UtcNow;
        await _productRepo.AddAsync(product);
        await _productRepo.SaveChangesAsync();
        return product;
    }

    public async Task UpdateProductAsync(Product product)
    {
        product.UpdatedAt = DateTime.UtcNow;
        _productRepo.Update(product);
        await _productRepo.SaveChangesAsync();
    }

    public async Task DeleteProductAsync(int id)
    {
        var product = await _productRepo.GetWithImagesAndVariantsAsync(id);
        if (product is null) return;

        foreach (var image in product.Images)
            await _fileService.DeleteImageAsync(image.ImageUrl);

        product.IsDeleted = true;
        product.UpdatedAt = DateTime.UtcNow;
        _productRepo.Update(product);
        await _productRepo.SaveChangesAsync();
    }

    /// <summary>Removes a single product image: deletes the DB row, its physical file, and
    /// promotes another image to "main" if the removed one was the cover photo.</summary>
    public async Task<bool> DeleteProductImageAsync(int imageId)
    {
        var image = await _productRepo.GetImageByIdAsync(imageId);
        if (image is null) return false;

        var product = await _productRepo.GetWithImagesAndVariantsAsync(image.ProductId);
        if (product is null) return false;

        var wasMain = image.IsMain;
        product.Images.Remove(image);
        _productRepo.RemoveImage(image);

        if (wasMain)
        {
            var next = product.Images.OrderBy(i => i.DisplayOrder).FirstOrDefault();
            if (next != null) next.IsMain = true;
            product.MainImageUrl = next?.ImageUrl;
        }

        product.UpdatedAt = DateTime.UtcNow;
        _productRepo.Update(product);
        await _productRepo.SaveChangesAsync();

        await _fileService.DeleteImageAsync(image.ImageUrl);
        return true;
    }

    public async Task<bool> SetMainProductImageAsync(int productId, int imageId)
    {
        var product = await _productRepo.GetWithImagesAndVariantsAsync(productId);
        var target = product?.Images.FirstOrDefault(i => i.Id == imageId);
        if (product is null || target is null) return false;

        foreach (var img in product.Images)
            img.IsMain = img.Id == imageId;
        product.MainImageUrl = target.ImageUrl;
        product.UpdatedAt = DateTime.UtcNow;

        _productRepo.Update(product);
        await _productRepo.SaveChangesAsync();
        return true;
    }

    public async Task ToggleProductStatusAsync(int id)
    {
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null) return;
        product.Status = product.Status == ProductStatus.Active ? ProductStatus.Inactive : ProductStatus.Active;
        product.UpdatedAt = DateTime.UtcNow;
        _productRepo.Update(product);
        await _productRepo.SaveChangesAsync();
    }
}

public class CategoryService
{
    private readonly ICategoryRepository _categoryRepo;

    public CategoryService(ICategoryRepository categoryRepo) => _categoryRepo = categoryRepo;

    public Task<IEnumerable<Category>> GetActiveCategoriesAsync() => _categoryRepo.GetActiveAsync();

    public Task<Category?> GetCategoryByIdAsync(int id) => _categoryRepo.GetByIdAsync(id);

    public Task<Category?> GetCategoryBySlugAsync(string slug) => _categoryRepo.GetBySlugAsync(slug);

    public async Task<IEnumerable<Category>> GetAllCategoriesAsync() => await _categoryRepo.GetAllAsync();

    public async Task CreateCategoryAsync(Category category)
    {
        category.CreatedAt = DateTime.UtcNow;
        await _categoryRepo.AddAsync(category);
        await _categoryRepo.SaveChangesAsync();
    }

    public async Task UpdateCategoryAsync(Category category)
    {
        category.UpdatedAt = DateTime.UtcNow;
        _categoryRepo.Update(category);
        await _categoryRepo.SaveChangesAsync();
    }

    public async Task DeleteCategoryAsync(int id)
    {
        var category = await _categoryRepo.GetByIdAsync(id);
        if (category is null) return;
        category.IsDeleted = true;
        _categoryRepo.Update(category);
        await _categoryRepo.SaveChangesAsync();
    }
}
