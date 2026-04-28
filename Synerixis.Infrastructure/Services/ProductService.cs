using Microsoft.EntityFrameworkCore;
using Synerixis.Application.DTOs;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Synerixis.Application.Services
{
    public class ProductService
    {
        private readonly AppDbContext _db;

        public ProductService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Guid> ImportProductAsync(Guid sellerId, ProductImportDto dto)
        {
            // 字符串转 decimal
            decimal? price = null;
            if (!string.IsNullOrWhiteSpace(dto.Price) && decimal.TryParse(dto.Price, out var parsedPrice))
            {
                price = parsedPrice;
            }

            // 先检查是否已存在相同 ExternalId 的商品（避免重复导入）
            var existingProduct = await _db.Products.FirstOrDefaultAsync(p =>
                p.ExternalId == dto.ExternalId && p.ExternalSource == (dto.Source ?? "manual"));

            Product product;

            if (existingProduct != null)
            {
                // 已存在 → 更新
                product = existingProduct;
                product.Title = dto.Title ?? product.Title;
                product.Description = dto.Description ?? product.Description;
                product.Price = price ?? product.Price;
                product.ImagesJson = dto.ImagesJson ?? product.ImagesJson;
                product.TagsJson = dto.TagsJson ?? product.TagsJson;
                product.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // 新增商品
                product = new Product
                {
                    Id = Guid.NewGuid(),
                    ExternalId = dto.ExternalId ?? string.Empty,
                    ExternalSource = dto.Source ?? "manual",
                    Title = dto.Title ?? string.Empty,
                    Description = dto.Description ?? string.Empty,
                    Price = price,
                    ImagesJson = dto.ImagesJson ?? "[]",
                    TagsJson = dto.TagsJson ?? "[]",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.Products.Add(product);
            }

            // 关联到当前商户（SellerProduct）
            var sellerProduct = new SellerProduct
            {
                Id = Guid.NewGuid(),
                SellerId = sellerId,
                ProductId = product.Id,
                CustomPrice = price,  // 可自定义
                CustomStock = null,   // 可自定义
                Source = dto.Source ?? "manual",  // 记录来源
                ImportedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.SellerProducts.Add(sellerProduct);
            await _db.SaveChangesAsync();

            return product.Id;
        }

        public async Task<List<SellerProduct>> GetProductsAsync(
            Guid sellerId,
            int page = 1,
            int pageSize = 10,
            string? keyword = null)
        {
            var query = _db.SellerProducts
                .Include(p => p.Product)          // 加载主商品
                .ThenInclude(p => p.Category)     // 加载类目实体（必须加这行！）
                .Where(p => p.SellerId == sellerId);

            if (!string.IsNullOrEmpty(keyword))
            {
                keyword = keyword.Trim().ToLower();

                query = query.Where(p =>
                    p.Product.Title.ToLower().Contains(keyword) ||
                    (p.Product.Category != null && p.Product.Category.Name.ToLower().Contains(keyword))  // ← 改成 Category.Name
                );
            }

            return await query
                .OrderByDescending(p => p.ImportedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
    }

}