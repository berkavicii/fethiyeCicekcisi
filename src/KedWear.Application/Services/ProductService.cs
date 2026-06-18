using KedWear.Core.Entities;
using KedWear.Core.Enums;
using KedWear.Core.Interfaces.Repositories;

namespace KedWear.Application.Services;

public class ProductService
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;

    public ProductService(IProductRepository productRepo, ICategoryRepository categoryRepo)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
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
        var product = await _productRepo.GetByIdAsync(id);
        if (product is null) return;
        product.IsDeleted = true;
        product.UpdatedAt = DateTime.UtcNow;
        _productRepo.Update(product);
        await _productRepo.SaveChangesAsync();
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
