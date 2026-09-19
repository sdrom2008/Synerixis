using Humanizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Senparc.CO2NET.Extensions;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Services;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.AI;
using Synerixis.Infrastructure.Data;
using Synerixis.Infrastructure.Services;
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
        private readonly IAuditLogger _audit;
        private readonly ISystemSettingsService _ops;
        private readonly LlmRuntime _llm;

        public SellerController(AppDbContext db, IAuthService authService, ProductService productService, IAuditLogger audit, ISystemSettingsService ops, LlmRuntime llm)
        {
            _db = db;
            _authService = authService;
            _productService = productService;
            _audit = audit;
            _ops = ops;
            _llm = llm;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                // Seller 本人；Supervisor/Admin 读本店 Seller（JWT shopId）
                if (!CanManageShopOwnerResources())
                    return Forbid();
                Guid sellerId;
                try { sellerId = GetShopOwnerSellerId(); }
                catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }

                var seller = await _db.Sellers
                    .Include(s => s.Config)
                    .FirstOrDefaultAsync(s => s.Id == sellerId);

                if (seller == null)
                    return NotFound("商户不存在");

                if (seller.Config == null)
                {
                    var ops = await _ops.GetOpsAsync();
                    seller.Config = new SellerConfig
                    {
                        SellerId = seller.Id,
                        OutboundMode = ops.DefaultOutboundMode
                    };
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

                var sellerKey = seller.Config?.LlmApiKey;
                var sellerKeySet = !string.IsNullOrWhiteSpace(sellerKey);
                using (_llm.UseSellerKey(sellerKey))
                {
                    var llmConfigured = _llm.IsConfigured;
                    var cfg = seller.Config!;
                    // 勿把明文 Key 回传前端
                    var configPayload = new
                    {
                        cfg.Id,
                        cfg.SellerId,
                        cfg.ShopName,
                        cfg.ShopLogo,
                        cfg.MainCategory,
                        cfg.TargetCustomerDesc,
                        cfg.DefaultReplyTone,
                        cfg.PreferredLanguage,
                        WorkingLanguage = string.IsNullOrWhiteSpace(cfg.PreferredLanguage) ? "zh" : cfg.PreferredLanguage,
                        cfg.SupportedLanguages,
                        SupportedLanguageList = Synerixis.Application.Helpers.CbecLanguageHelper.ParseSupportedList(cfg.SupportedLanguages),
                        cfg.EnableAutoMarketingReminder,
                        cfg.MemoryRetentionDays,
                        cfg.EnableAutoReply,
                        cfg.OutboundMode,
                        cfg.AssignmentMode,
                        cfg.BusinessHoursStart,
                        cfg.BusinessHoursEnd,
                        cfg.ResponseSlaHours,
                        cfg.AlertThresholdHours,
                        cfg.AutoHandoffOnLowConfidence,
                        cfg.HandoffConfidenceThreshold,
                        cfg.SensitiveKeywords,
                        cfg.HandoffOutsideBusinessHours,
                        cfg.TimeZoneId,
                        cfg.CreatedAt,
                        cfg.UpdatedAt,
                        LlmApiKeyConfigured = sellerKeySet,
                        LlmKeyHint = LlmKeyResolver.MaskHint(sellerKey)
                    };

                    return Ok(new
                    {
                        seller.Id,
                        seller.Nickname,
                        seller.AvatarUrl,
                        seller.Phone,
                        seller.FreeQuota,
                        seller.SubscriptionLevel,
                        seller.SubscriptionEnd,
                        Config = configPayload,
                        Llm = new
                        {
                            configured = llmConfigured,
                            sellerKeyConfigured = sellerKeySet,
                            platformKeyConfigured = _llm.PlatformConfigured,
                            keyHint = _llm.KeyHint,
                            source = _llm.Source,
                            degradeHint = llmConfigured
                                ? null
                                : "未配置 AI（规则草稿）：入站/注入生成规则草稿；请到「AI 设置」填 Key 启用真实起草。seed / 人审发送 / SIM mock 不受影响"
                        },
                        TeamStats = new
                        {
                            TotalAgents = agentCount,
                            OnlineAgents = onlineAgentCount,
                            ActiveAgents = activeAgentCount
                        }
                    });
                }
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
            // AI 设置：Seller + Supervisor + Admin 可写本店 SellerConfig
            if (!CanManageShopOwnerResources())
                return Forbid();
            Guid sellerId;
            try { sellerId = GetShopOwnerSellerId(); }
            catch (UnauthorizedAccessException) { return Unauthorized(); }

            var config = await _db.SellerConfigs.FirstOrDefaultAsync(c => c.SellerId == sellerId);

            if (config == null)
                return NotFound("配置不存在");

            config.ShopName = dto.ShopName ?? config.ShopName;
            config.ShopLogo = dto.ShopLogo ?? config.ShopLogo;
            config.MainCategory = dto.MainCategory ?? config.MainCategory;
            config.TargetCustomerDesc = dto.TargetCustomerDesc ?? config.TargetCustomerDesc;
            config.DefaultReplyTone = dto.DefaultReplyTone ?? config.DefaultReplyTone;
            if (!string.IsNullOrWhiteSpace(dto.WorkingLanguage))
                config.PreferredLanguage = Synerixis.Application.Helpers.CbecLanguageHelper.Normalize(dto.WorkingLanguage).ToLowerInvariant();
            else if (!string.IsNullOrWhiteSpace(dto.PreferredLanguage))
                config.PreferredLanguage = Synerixis.Application.Helpers.CbecLanguageHelper.Normalize(dto.PreferredLanguage).ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(dto.SupportedLanguages))
                config.SupportedLanguages = Synerixis.Application.Helpers.CbecLanguageHelper.ToCsv(
                    Synerixis.Application.Helpers.CbecLanguageHelper.ParseSupportedList(dto.SupportedLanguages));
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
            if (!string.IsNullOrWhiteSpace(dto.OutboundMode))
                config.OutboundMode = OutboundModes.Normalize(dto.OutboundMode);
            if (!string.IsNullOrWhiteSpace(dto.AssignmentMode))
                config.AssignmentMode = AssignmentModes.Normalize(dto.AssignmentMode);
            if (dto.ResponseSlaHours.HasValue && dto.ResponseSlaHours.Value > 0 && dto.ResponseSlaHours.Value <= 168)
                config.ResponseSlaHours = dto.ResponseSlaHours.Value;
            if (!string.IsNullOrWhiteSpace(dto.AlertThresholdHours))
                config.AlertThresholdHours = dto.AlertThresholdHours.Trim();
            if (dto.AutoHandoffOnLowConfidence.HasValue)
                config.AutoHandoffOnLowConfidence = dto.AutoHandoffOnLowConfidence.Value;
            if (dto.HandoffConfidenceThreshold.HasValue)
            {
                var th = dto.HandoffConfidenceThreshold.Value;
                if (th < 0) th = 0;
                if (th > 1) th = 1;
                config.HandoffConfidenceThreshold = th;
            }
            if (dto.SensitiveKeywords != null)
                config.SensitiveKeywords = dto.SensitiveKeywords.Trim();
            if (dto.HandoffOutsideBusinessHours.HasValue)
                config.HandoffOutsideBusinessHours = dto.HandoffOutsideBusinessHours.Value;
            if (!string.IsNullOrWhiteSpace(dto.TimeZoneId))
                config.TimeZoneId = dto.TimeZoneId.Trim();
            // LlmApiKey：null=不改；空串=清除商家 Key；非空且非掩码=更新
            if (dto.LlmApiKey != null)
            {
                var raw = dto.LlmApiKey.Trim();
                if (raw.Length == 0)
                    config.LlmApiKey = null;
                else if (!raw.StartsWith("****", StringComparison.Ordinal))
                    config.LlmApiKey = raw;
            }
            config.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            try
            {
                var actor = GetCurrentUser();
                await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.AiSettingsUpdate,
                    "SellerConfig", sellerId.ToString(),
                    new { outboundMode = config.OutboundMode, assignmentMode = config.AssignmentMode, enableAutoReply = config.EnableAutoReply, llmKeyConfigured = !string.IsNullOrWhiteSpace(config.LlmApiKey) },
                    sellerId);
            }
            catch { }

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
                var ops = await _ops.GetOpsAsync();
                config = new SellerConfig { SellerId = sellerId, OutboundMode = ops.DefaultOutboundMode };
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
        // 客服团队管理（Seller 本人 + 同店 Supervisor/平台 Admin JWT 挂靠本店）
        // 可选角色仅 Agent|Supervisor；AgentRole.Admin 为平台运营，禁止商家侧创建/升格
        // ============================================
        [HttpGet("team")]
        public async Task<IActionResult> GetTeam()
        {
            try
            {
                var shopId = GetTeamManagedShopId();
                var agents = await _db.Agents
                    .Where(a => a.ShopId == shopId)
                    .OrderBy(a => a.Role)
                    .ThenBy(a => a.CreatedAt)
                    .Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        email = a.Email,
                        role = a.Role.ToString(),
                        isActive = a.IsActive,
                        isOnline = a.IsOnline,
                        maxConcurrentSessions = a.MaxConcurrentSessions,
                        currentSessionCount = a.CurrentSessionCount,
                        lastLoginAt = a.LastLoginAt,
                        createdAt = a.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { items = agents, total = agents.Count, shopId });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("team")]
        public async Task<IActionResult> AddTeamMember([FromBody] AddAgentDto dto)
        {
            try
            {
                var shopId = GetTeamManagedShopId();

                if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Name))
                    return BadRequest(new { message = "邮箱、姓名、初始密码不能为空" });

                var role = ParseAgentRole(dto.Role, dto.RoleName) ?? AgentRole.Agent;
                // AgentRole.Admin is PLATFORM-only (JWT → /api/admin/*). Merchant team APIs must never create it.
                if (role == AgentRole.Admin)
                    return BadRequest(new { message = "不能通过商家团队接口创建平台 Admin；仅 Agent / Supervisor 可选" });

                var existing = await _db.Agents.FirstOrDefaultAsync(a => a.Email == dto.Email);
                if (existing != null)
                    return BadRequest(new { message = "该邮箱已被使用" });

                var agent = Agent.Create(
                    shopId: shopId,
                    email: dto.Email.Trim(),
                    name: dto.Name.Trim(),
                    passwordHash: AgentPasswordHasher.Hash(dto.Password),
                    role: role
                );

                _db.Agents.Add(agent);
                await _db.SaveChangesAsync();

                try
                {
                    var actor = GetCurrentUser();
                    await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.TeamCreate,
                        "Agent", agent.Id.ToString(),
                        new { email = agent.Email, role = agent.Role.ToString() },
                        shopId);
                }
                catch { }

                return Ok(new
                {
                    message = "客服添加成功",
                    agentId = agent.Id,
                    role = agent.Role.ToString()
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPut("team/{agentId}")]
        [HttpPatch("team/{agentId}")]
        public async Task<IActionResult> UpdateTeamMember(
            Guid agentId,
            [FromBody] UpdateAgentDto dto)
        {
            try
            {
                var shopId = GetTeamManagedShopId();
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId && a.ShopId == shopId);
                if (agent == null)
                    return NotFound(new { message = "客服不存在或无权限" });

                if (!string.IsNullOrEmpty(dto.Name))
                    agent.UpdateProfile(dto.Name, null);
                if (dto.MaxConcurrentSessions.HasValue)
                    agent.SetMaxConcurrentSessions(dto.MaxConcurrentSessions.Value);
                if (dto.IsActive.HasValue)
                    agent.SetActive(dto.IsActive.Value);

                var newRole = ParseAgentRole(dto.Role, dto.RoleName);
                if (newRole.HasValue)
                {
                    // AgentRole.Admin is PLATFORM-only; merchant team cannot promote to Admin.
                    if (newRole.Value == AgentRole.Admin)
                        return BadRequest(new { message = "不能通过商家团队接口升为平台 Admin；仅 Agent / Supervisor 可选" });
                    agent.UpdateRole(newRole.Value);
                }

                await _db.SaveChangesAsync();

                try
                {
                    var actor = GetCurrentUser();
                    await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.TeamUpdate,
                        "Agent", agent.Id.ToString(),
                        new { role = agent.Role.ToString(), isActive = agent.IsActive },
                        shopId);
                }
                catch { }

                return Ok(new
                {
                    message = "客服信息更新成功",
                    agentId = agent.Id,
                    role = agent.Role.ToString(),
                    isActive = agent.IsActive
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpDelete("team/{agentId}")]
        public async Task<IActionResult> RemoveTeamMember(Guid agentId)
        {
            try
            {
                var shopId = GetTeamManagedShopId();
                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId && a.ShopId == shopId);
                if (agent == null)
                    return NotFound(new { message = "客服不存在或无权限" });

                agent.SetActive(false);
                await _db.SaveChangesAsync();

                try
                {
                    var actor = GetCurrentUser();
                    await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.TeamDisable,
                        "Agent", agent.Id.ToString(), null, shopId);
                }
                catch { }

                return Ok(new { message = "客服已禁用" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("team/{agentId}/reset-password")]
        public async Task<IActionResult> ResetTeamMemberPassword(
            Guid agentId,
            [FromBody] ResetPasswordDto dto)
        {
            try
            {
                var shopId = GetTeamManagedShopId();
                if (string.IsNullOrWhiteSpace(dto.NewPassword))
                    return BadRequest(new { message = "新密码不能为空" });

                var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Id == agentId && a.ShopId == shopId);
                if (agent == null)
                    return NotFound(new { message = "客服不存在或无权限" });

                agent.UpdatePassword(AgentPasswordHasher.Hash(dto.NewPassword));
                await _db.SaveChangesAsync();

                try
                {
                    var actor = GetCurrentUser();
                    await _audit.LogAsync(actor.UserId, actor.UserType, AuditActions.TeamResetPassword,
                        "Agent", agent.Id.ToString(),
                        new { note = "password_reset" },
                        shopId);
                }
                catch { }

                return Ok(new { message = "密码重置成功" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        private static AgentRole? ParseAgentRole(AgentRole? role, string? roleName)
        {
            if (role.HasValue) return role;
            if (string.IsNullOrWhiteSpace(roleName)) return null;
            return Enum.TryParse<AgentRole>(roleName.Trim(), true, out var parsed) ? parsed : null;
        }
    }

    public class AddAgentDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Name { get; set; } = null!;
        public AgentRole? Role { get; set; }
        /// <summary>仅 "Agent" | "Supervisor"；Admin 为平台角色，商家侧拒绝</summary>
        public string? RoleName { get; set; }
    }

    public class UpdateAgentDto
    {
        public string? Name { get; set; }
        /// <summary>仅 Agent|Supervisor；拒绝 Admin</summary>
        public AgentRole? Role { get; set; }
        /// <summary>仅 "Agent" | "Supervisor"；Admin 为平台角色，商家侧拒绝</summary>
        public string? RoleName { get; set; }
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
        public string? WorkingLanguage { get; set; }
        public string? SupportedLanguages { get; set; }
        public bool? EnableAutoMarketingReminder { get; set; }
        public int? MemoryRetentionDays { get; set; }
        public bool? EnableAutoReply { get; set; }
        public string? BusinessHoursStart { get; set; }
        public string? BusinessHoursEnd { get; set; }
        public string? OutboundMode { get; set; }
        public string? AssignmentMode { get; set; }
        public int? ResponseSlaHours { get; set; }
        public string? AlertThresholdHours { get; set; }
        public bool? AutoHandoffOnLowConfidence { get; set; }
        public double? HandoffConfidenceThreshold { get; set; }
        public string? SensitiveKeywords { get; set; }
        public bool? HandoffOutsideBusinessHours { get; set; }
        public string? TimeZoneId { get; set; }
        /// <summary>null=不改；""=清除；明文=更新（勿传 **** 掩码）</summary>
        public string? LlmApiKey { get; set; }
    }
}
