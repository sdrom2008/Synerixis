using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Senparc.CO2NET.Extensions;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Services;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

namespace Synerixis.Api.Controllers
{
    [ApiController]
    [Route("api/seller")]
    [Authorize]
    public class SellerController : BaseApiController
    {
        private readonly AppDbContext _db;
        private readonly IAuthService _authService;
        private readonly ProductService _productService;

        public SellerController(AppDbContext db, IAuthService authService, ProductService productService)
        {
            _db = db;
            _authService = authService;
            _productService = productService;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(sellerIdStr, out var sellerId))
                    return Unauthorized("无效身份");

                var seller = await _db.Sellers
                    .Include(s => s.Config)
                    .FirstOrDefaultAsync(s => s.Id == sellerId);

                if (seller == null)
                    return NotFound("商户不存在");

                if (seller.Config == null)
                {
                    seller.Config = new SellerConfig { /* 默认值 */ };
                    _db.SellerConfigs.Add(seller.Config);
                    await _db.SaveChangesAsync();
                }

                // 获取客服团队统计
                var agentCount = await _db.Agents
                    .CountAsync(a => a.ShopId == sellerId);
                var onlineAgentCount = await _db.Agents
                    .CountAsync(a => a.ShopId == sellerId && a.IsOnline);
                var activeAgentCount = await _db.Agents
                    .CountAsync(a => a.ShopId == sellerId && a.IsActive);

                return Ok(new
                {
                    seller.Id,
                    seller.Nickname,
                    seller.AvatarUrl,
                    seller.Phone,
                    seller.FreeQuota,
                    seller.SubscriptionLevel,
                    seller.SubscriptionEnd,
                    Config = seller.Config,
                    TeamStats = new
                    {
                        TotalAgents = agentCount,
                        OnlineAgents = onlineAgentCount,
                        ActiveAgents = activeAgentCount
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("GetProfile 异常: " + ex.Message);
                return StatusCode(500, "服务器内部错误，请检查日志");
            }
        }

        [HttpPut("config")]
        public async Task<IActionResult> UpdateConfig([FromBody] SellerConfigUpdateDto dto)
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var config = await _db.SellerConfigs.FirstOrDefaultAsync(c => c.SellerId == sellerId);

            if (config == null)
                return NotFound("配置不存在");

            config.ShopName = dto.ShopName ?? config.ShopName;
            config.ShopLogo = dto.ShopLogo ?? config.ShopLogo;
            config.MainCategory = dto.MainCategory ?? config.MainCategory;
            config.TargetCustomerDesc = dto.TargetCustomerDesc ?? config.TargetCustomerDesc;
            config.DefaultReplyTone = dto.DefaultReplyTone ?? config.DefaultReplyTone;
            config.PreferredLanguage = dto.PreferredLanguage ?? config.PreferredLanguage;
            config.EnableAutoMarketingReminder = dto.EnableAutoMarketingReminder ?? config.EnableAutoMarketingReminder;
            config.MemoryRetentionDays = dto.MemoryRetentionDays.HasValue && dto.MemoryRetentionDays.Value > 0
                ? dto.MemoryRetentionDays.Value
                : config.MemoryRetentionDays;
            if (dto.EnableAutoReply.HasValue)
                config.EnableAutoReply = dto.EnableAutoReply.Value;
            if (!string.IsNullOrWhiteSpace(dto.BusinessHoursStart))
                config.BusinessHoursStart = dto.BusinessHoursStart.Trim();
            if (!string.IsNullOrWhiteSpace(dto.BusinessHoursEnd))
                config.BusinessHoursEnd = dto.BusinessHoursEnd.Trim();
            config.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new { message = "配置更新成功" });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] ProfileUpdateDto dto)
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Id == sellerId);
            if (seller == null)
                return NotFound("商户不存在");

            seller.UpdateProfile(dto.Nickname, dto.AvatarUrl);
            await _db.SaveChangesAsync();

            return Ok(new { message = "个人信息更新成功" });
        }

        [HttpPost("upload/logo")]
        [Authorize]
        public async Task<IActionResult> UploadLogo(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("请选择文件");

            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            // 1. 验证文件扩展名
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!new[] { ".jpg", ".jpeg", ".png", ".gif" }.Contains(extension))
                return BadRequest("仅支持 JPG/PNG/GIF 格式");

            // 2. 验证 MIME 类型
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
            if (!allowedTypes.Contains(file.ContentType.ToLowerInvariant()))
                return BadRequest("文件类型不正确");

            // 3. 验证文件大小（5MB 限制）
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest("文件大小不能超过 5MB");

            // 4. 验证文件头（Magic Number）防止伪装
            using var binaryReader = new BinaryReader(file.OpenReadStream());
            var header = binaryReader.ReadBytes(4);
            file.OpenReadStream().Seek(0, SeekOrigin.Begin); // 重置流位置

            bool isValidHeader = file.ContentType.ToLowerInvariant() switch
            {
                "image/jpeg" => header.Take(3).SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }),
                "image/png" => header.Take(4).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
                "image/gif" => header.Take(3).SequenceEqual(new byte[] { 0x47, 0x49, 0x46 }),
                _ => false
            };

            if (!isValidHeader)
                return BadRequest("文件内容与格式不匹配，可能已损坏或被篡改");

            var fileName = $"{Guid.NewGuid()}{extension}";
            var dir = Path.Combine("wwwroot", "uploads", "logo");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);

            using (var stream = new FileStream(path, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/logo/{fileName}";

            // 自动保存到 SellerConfig
            var config = await _db.SellerConfigs.FirstOrDefaultAsync(c => c.SellerId == sellerId);
            if (config == null)
            {
                config = new SellerConfig { SellerId = sellerId };
                _db.SellerConfigs.Add(config);
            }
            config.ShopLogo = url;
            config.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { url });
        }

        [HttpGet("products")]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
             [FromQuery] string? keyword = null)
        {
            try
            {
                var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(sellerIdStr, out var sellerId))
                    return Unauthorized("无效的身份令牌");

                var products = await _productService.GetProductsAsync(sellerId, page, pageSize, keyword);

                var items = products.Select(p => new
                {
                    id = p.Id,
                    title = p.Product?.Title ?? "未命名商品",
                    price = p.CustomPrice ?? p.Product?.Price ?? 0,
                    category = p.Product?.Category?.Name ?? "未分类",
                    imagesJson = p.Product?.ImagesJson ?? "[]",
                    tagsJson = p.Product?.TagsJson ?? "[]",
                    source = p.Source ?? "manual",
                    importedAt = p.ImportedAt,
                    optimizedTitle = p.OptimizedTitle,
                    optimizedDescription = p.OptimizedDescription,
                    optimizedTagsJson = p.OptimizedTagsJson
                }).ToList();

                var total = await _db.SellerProducts.CountAsync(p => p.SellerId == sellerId);

                return Ok(new { total, page, pageSize, items });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] GetProducts 异常: {ex.Message}");
                return StatusCode(500, new { message = "获取商品列表失败", error = ex.Message });
            }
        }

        [HttpDelete("products/{id}")]
        public async Task<IActionResult> DeleteProduct(Guid id)
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var product = await _db.SellerProducts.FirstOrDefaultAsync(p => p.Id == id && p.SellerId == sellerId);
            if (product == null)
                return NotFound("商品不存在或无权限");

            _db.SellerProducts.Remove(product);
            await _db.SaveChangesAsync();

            return Ok(new { message = "删除成功" });
        }

        [HttpPut("products/{id}/optimize")]
        public async Task<IActionResult> OptimizeProduct(Guid id, [FromBody] OptimizedProductDto dto)
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var sellerProduct = await _db.SellerProducts.FirstOrDefaultAsync(p => p.Id == id && p.SellerId == sellerId);
            if (sellerProduct == null)
                return NotFound("商品不存在或无权限");

            sellerProduct.OptimizedTitle = dto.OptimizedTitle;
            sellerProduct.OptimizedDescription = dto.OptimizedDescription;
            sellerProduct.OptimizedTagsJson = dto.OptimizedTagsJson;
            sellerProduct.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new { message = "优化结果保存成功" });
        }

        [HttpPost("products/fetch-url")]
        public async Task<IActionResult> FetchFromUrl([FromBody] FetchUrlDto dto)
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var product = new
            {
                title = "抓取商品 - 示例",
                description = "抓取描述",
                price = 99.99,
                imagesJson = "[]",
                category = "测试类目",
                tagsJson = "[]"
            };

            return Ok(product);
        }

        // ============================================
        // DTOs
        // ============================================
        public class FetchUrlDto
        {
            public string Url { get; set; } = null!;
        }

        [HttpPost("products/import")]
        public async Task<IActionResult> ImportProduct([FromBody] ProductImportDto dto)
        {
            try
            {
                var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!Guid.TryParse(sellerIdStr, out var sellerId))
                    return Unauthorized("无效的身份令牌");

                if (dto == null)
                    return BadRequest("请求体为空");

                if (string.IsNullOrWhiteSpace(dto.Title))
                    return BadRequest("商品标题不能为空");

                dto.Description ??= "";
                dto.ImagesJson ??= "[]";
                dto.TagsJson ??= "[]";
                dto.Category ??= "";
                dto.Source ??= "manual";

                var productId = await _productService.ImportProductAsync(sellerId, dto);

                return Ok(new { message = "商品导入成功", productId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] ImportProduct 异常: {ex.Message}");
                return StatusCode(500, new { message = "商品导入失败", error = ex.Message });
            }
        }

        // ============================================
        // 客服团队管理
        // ============================================
        [HttpGet("team")]
        public async Task<IActionResult> GetTeam()
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var agents = await _db.Agents
                .Where(a => a.ShopId == sellerId)
                .OrderBy(a => a.Role)
                .ThenBy(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.Email,
                    a.Role,
                    a.IsActive,
                    a.IsOnline,
                    a.MaxConcurrentSessions,
                    a.CurrentSessionCount,
                    a.LastLoginAt,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(agents);
        }

        [HttpPost("team")]
        public async Task<IActionResult> AddTeamMember([FromBody] AddAgentDto dto)
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var existing = await _db.Agents.FirstOrDefaultAsync(a => a.Email == dto.Email);
            if (existing != null)
                return BadRequest("该邮箱已被使用");

            var agent = Agent.Create(
                shopId: sellerId,
                email: dto.Email,
                name: dto.Name,
                passwordHash: dto.Password,
                role: dto.Role ?? AgentRole.Agent
            );

            _db.Agents.Add(agent);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "客服添加成功",
                agentId = agent.Id
            });
        }

        [HttpPut("team/{agentId}")]
        public async Task<IActionResult> UpdateTeamMember(
            Guid agentId,
            [FromBody] UpdateAgentDto dto)
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId && a.ShopId == sellerId);
            if (agent == null)
                return NotFound("客服不存在或无权限");

            if (!string.IsNullOrEmpty(dto.Name))
                agent.UpdateProfile(dto.Name, null);
            if (dto.MaxConcurrentSessions.HasValue)
                agent.SetMaxConcurrentSessions(dto.MaxConcurrentSessions.Value);
            if (dto.IsActive.HasValue)
                agent.SetActive(dto.IsActive.Value);

            await _db.SaveChangesAsync();

            return Ok(new { message = "客服信息更新成功" });
        }

        [HttpDelete("team/{agentId}")]
        public async Task<IActionResult> RemoveTeamMember(Guid agentId)
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId && a.ShopId == sellerId);
            if (agent == null)
                return NotFound("客服不存在或无权限");

            agent.SetActive(false);
            await _db.SaveChangesAsync();

            return Ok(new { message = "客服已移除" });
        }

        [HttpPost("team/{agentId}/reset-password")]
        public async Task<IActionResult> ResetTeamMemberPassword(
            Guid agentId,
            [FromBody] ResetPasswordDto dto)
        {
            var sellerIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(sellerIdStr, out var sellerId))
                return Unauthorized();

            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId && a.ShopId == sellerId);
            if (agent == null)
                return NotFound("客服不存在或无权限");

            agent.UpdatePassword(dto.NewPassword);
            await _db.SaveChangesAsync();

            return Ok(new { message = "密码重置成功" });
        }
    }

    public class AddAgentDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Name { get; set; } = null!;
        public AgentRole? Role { get; set; } = AgentRole.Agent;
    }

    public class UpdateAgentDto
    {
        public string? Name { get; set; }
        public AgentRole? Role { get; set; }
        public int? MaxConcurrentSessions { get; set; }
        public bool? IsActive { get; set; }
    }

    public class ResetPasswordDto
    {
        public string NewPassword { get; set; } = null!;
    }

    // ============================================
    // 其他业务 DTOs (本地定义)
    // ============================================
    public class ProfileUpdateDto
    {
        public string? Nickname { get; set; }
        public string? AvatarUrl { get; set; }
    }

    public class SellerConfigUpdateDto
    {
        public string? ShopName { get; set; }
        public string? ShopLogo { get; set; }
        public string? MainCategory { get; set; }
        public string? TargetCustomerDesc { get; set; }
        public string? DefaultReplyTone { get; set; }
        public string? PreferredLanguage { get; set; }
        public bool? EnableAutoMarketingReminder { get; set; }
        public int? MemoryRetentionDays { get; set; }
        public bool? EnableAutoReply { get; set; }
        public string? BusinessHoursStart { get; set; }
        public string? BusinessHoursEnd { get; set; }
    }
}
