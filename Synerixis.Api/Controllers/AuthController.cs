using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Synerixis.Api.Controllers;
using Synerixis.Application.DTOs;
using Synerixis.Application.Interfaces;
using Synerixis.Domain.Common;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.Data;
using Synerixis.Infrastructure.Services;
using Synerixis.Infrastructure.Clients;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;

namespace Synerixis.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : BaseApiController
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly IAuthService _authService;
        private readonly AliyunSmsService _smsService;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _env;
        private readonly IAuditLogger _audit;
        private readonly ISystemSettingsService _ops;

        public AuthController(
            AppDbContext db,
            IAuthService authService,
            IConfiguration config,
            IHttpClientFactory factory,
            AliyunSmsService smsService,
            IMemoryCache cache,
            IWebHostEnvironment env,
            IPlatformClientRouter platformClientRouter,
            IAuditLogger audit,
            ISystemSettingsService ops)
        {
            _db = db;
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _config = config;
            _http = factory.CreateClient();
            _smsService = smsService;
            _cache = cache;
            _env = env;
            _router = platformClientRouter ?? throw new ArgumentNullException(nameof(platformClientRouter));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _ops = ops ?? throw new ArgumentNullException(nameof(ops));
        }

        private readonly IPlatformClientRouter _router;

        [HttpPost("wechat")]
        public async Task<IActionResult> WeChatLogin([FromBody] WeChatCode request)
        {
            if (string.IsNullOrEmpty(request.Code)) return BadRequest("Code required");

            var appId = _config["WeChat:AppId"];
            var secret = _config["WeChat:AppSecret"];
            var url = $"https://api.weixin.qq.com/sns/jscode2session?appid={appId}&secret={secret}&js_code={request.Code}&grant_type=authorization_code";

            var resp = await _http.GetFromJsonAsync<WeChatResp>(url);
            if (resp?.OpenId == null) return BadRequest(resp?.Errmsg ?? "微信登录失败");

            var openId = resp.OpenId;

            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.OpenId == openId);

            if (seller == null)
            {
                var blocked = await RejectNewRegistrationIfBlockedAsync();
                if (blocked != null) return blocked;

                seller = Seller.Create(openId);
                _db.Sellers.Add(seller);
                await _db.SaveChangesAsync();

                return Ok(new { needBind = true, openid = openId });
            }

            if (!string.IsNullOrEmpty(seller.Phone))
            {
                seller.RecordLogin("wechat");
                await _db.SaveChangesAsync();

                var token = _authService.GenerateJwt(seller.Id, "Seller", null);
                return Ok(new
                {
                    token,
                    sellerId = seller.Id,
                    nickname = seller.Nickname,
                    avatarUrl = seller.AvatarUrl,
                    subscriptionLevel = seller.SubscriptionLevel,
                    freeQuota = seller.FreeQuota
                });
            }
            else
            {
                return Ok(new { needBind = true, openid = openId });
            }
        }

        [HttpPost("phone-login")]
        [AllowAnonymous]  // 允许匿名登录
        public async Task<IActionResult> PhoneLogin([FromBody] PhoneLoginDto dto)
        {
            if (string.IsNullOrEmpty(dto.Phone) || string.IsNullOrEmpty(dto.Code))
                return BadRequest("手机号和验证码不能为空");

            // 拼接完整国际号码：+{CountryCode}{Phone}
            var fullPhone = $"+{dto.CountryCode}{dto.Phone}".Replace(" ", "");

            // 开发环境或未配置短信网关时，允许测试验证码通过
            if (_env.IsDevelopment() || string.IsNullOrEmpty(_config["AliyunSms:AccessKeyId"]))
            {
                if (dto.Code != "123456" && (!_cache.TryGetValue($"sms:{fullPhone}", out string? cachedCode) || cachedCode != dto.Code))
                    return BadRequest("验证码错误或已过期");
            }
            else
            {
                if (!_cache.TryGetValue($"sms:{fullPhone}", out string? cachedCode) || cachedCode != dto.Code)
                    return BadRequest("验证码错误或已过期");
            }

            // 先尝试作为 Seller 登录
            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Phone == fullPhone);
            if (seller != null)
            {
                bool isNew = false;
                if (string.IsNullOrEmpty(seller.Phone))
                {
                    seller.BindPhone(fullPhone);
                    isNew = true;
                }
                seller.RecordLogin("phone");
                await _db.SaveChangesAsync();

                var token = _authService.GenerateJwt(seller.Id, "Seller", null);
                return Ok(new
                {
                    token,
                    userId = seller.Id,
                    userType = "Seller",
                    nickname = seller.Nickname,
                    freeQuota = seller.FreeQuota,
                    subscriptionLevel = seller.SubscriptionLevel,
                    isNewRegistration = isNew
                });
            }

            // 再尝试作为 Agent 登录
            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Phone == fullPhone);
            if (agent != null)
            {
                if (!agent.IsActive)
                    return BadRequest("账号已被禁用");

                agent.RecordLogin();
                await _db.SaveChangesAsync();

                var userType = agent.Role.ToString(); // Agent | Supervisor | Admin
                var token = _authService.GenerateJwt(agent.Id, userType, agent.ShopId);
                return Ok(new
                {
                    token,
                    userId = agent.Id,
                    userType,
                    name = agent.Name,
                    nickname = agent.Name,
                    role = agent.Role.ToString(),
                    shopId = agent.ShopId
                });
            }

            // 都找不到，自动注册为新商户 (Seller)
            {
                var blocked = await RejectNewRegistrationIfBlockedAsync();
                if (blocked != null) return blocked;
            }
            var newSeller = Seller.CreateWithPhone(fullPhone);
            // 新商家 SellerConfig 使用系统 DefaultOutboundMode
            var ops = await _ops.GetOpsAsync();
            _db.Sellers.Add(newSeller);
            _db.SellerConfigs.Add(new SellerConfig
            {
                SellerId = newSeller.Id,
                OutboundMode = ops.DefaultOutboundMode
            });
            await _db.SaveChangesAsync();

            var newToken = _authService.GenerateJwt(newSeller.Id, "Seller", null);
            return Ok(new
            {
                code = 200,
                token = newToken,
                userId = newSeller.Id,
                userType = "Seller",
                nickname = newSeller.Nickname,
                freeQuota = newSeller.FreeQuota,
                subscriptionLevel = newSeller.SubscriptionLevel,
                isNewRegistration = true,
                message = "注册并登录成功"
            });
        }

        [HttpPost("bind-wechat")]
        public async Task<IActionResult> BindWechat([FromBody] BindWechatDto dto)
        {
            if (string.IsNullOrEmpty(dto.OpenId) || string.IsNullOrEmpty(dto.Phone))
                return BadRequest("参数缺失");

            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Phone == dto.Phone);

            if (seller == null)
                return BadRequest("手机号未注册，请先注册");

            var existing = await _db.Sellers.FirstOrDefaultAsync(s => s.OpenId == dto.OpenId);
            if (existing != null && !(existing.Id == seller.Id))
                return BadRequest("该微信已绑定其他账号");

            seller.BindWechat(dto.OpenId);
            seller.RecordLogin("wechat");
            await _db.SaveChangesAsync();

            var token = _authService.GenerateJwt(seller.Id, "Seller", null);
            return Ok(new
            {
                token,
                sellerId = seller.Id,
                nickname = seller.Nickname,
                freeQuota = seller.FreeQuota
            });
        }

        [HttpPost("send-code")]
        [AllowAnonymous]
        public async Task<IActionResult> SendCode([FromBody] SendCodeDto dto)
        {
            if (string.IsNullOrEmpty(dto.Phone))
                return BadRequest("手机号不能为空");

            // 拼接完整国际号码：+{CountryCode}{Phone}
            var fullPhone = $"+{dto.CountryCode}{dto.Phone}".Replace(" ", "");

            // 开发环境：固定验证码 "123456"
            var code = "123456";

            // 如果是开发环境，或未配置阿里云短信（即AccessKeyId为空），均使用固定验证码 123456 进行测试
            if (_env.IsDevelopment() || string.IsNullOrEmpty(_config["AliyunSms:AccessKeyId"]))
            {
                _cache.Set($"sms:{fullPhone}", code, TimeSpan.FromMinutes(5));
                return Ok(new { message = "测试模式已开启（验证码: 123456）" });
            }

            var realCode = new Random().Next(100000, 999999).ToString();
            var success = await _smsService.SendVerificationCodeAsync(fullPhone, realCode);

            if (!success)
                return StatusCode(500, "发送验证码失败，请稍后重试");

            _cache.Set($"sms:{fullPhone}", realCode, TimeSpan.FromMinutes(5));

            return Ok(new { message = "验证码已发送，5分钟内有效" });
        }

        [HttpPost("bind-phone")]
        public async Task<IActionResult> BindPhone([FromBody] BindPhoneDto dto)
        {
            if (string.IsNullOrEmpty(dto.Code))
                return BadRequest("验证码不能为空");

            // 校验验证码
            var fullPhoneCode = $"+{dto.CountryCode}{dto.Phone}".Replace(" ", "");
            if (_env.IsDevelopment() || string.IsNullOrEmpty(_config["AliyunSms:AccessKeyId"]))
            {
                if (dto.Code != "123456" && (!_cache.TryGetValue($"sms:{fullPhoneCode}", out string? cachedCode) || cachedCode != dto.Code))
                    return BadRequest("验证码错误或已过期");
            }
            else
            {
                if (!_cache.TryGetValue($"sms:{fullPhoneCode}", out string? cachedCode) || cachedCode != dto.Code)
                    return BadRequest("验证码错误或已过期");
            }

            // 拼接完整国际号码
            var fullPhone = $"+{dto.CountryCode}{dto.Phone}".Replace(" ", "");

            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.Phone == fullPhone);

            if (seller == null)
            {
                var blocked = await RejectNewRegistrationIfBlockedAsync();
                if (blocked != null) return blocked;

                seller = Seller.CreateWithPhone(fullPhone);
                seller.BindWechat(dto.OpenId);
                _db.Sellers.Add(seller);
                var ops = await _ops.GetOpsAsync();
                _db.SellerConfigs.Add(new SellerConfig
                {
                    SellerId = seller.Id,
                    OutboundMode = ops.DefaultOutboundMode
                });
            }
            else
            {
                seller.BindWechat(dto.OpenId);
            }

            seller.RecordLogin("phone");
            await _db.SaveChangesAsync();

            var token = _authService.GenerateJwt(seller.Id, "Seller", null);
            return Ok(new
            {
                code = 200,
                token,
                sellerId = seller.Id,
                nickname = seller.Nickname ?? "",
                freeQuota = seller.FreeQuota,
                subscriptionLevel = seller.SubscriptionLevel,
                msg = "绑定成功"
            });
        }

        [HttpPost("decrypt-phone")]
        public async Task<IActionResult> DecryptPhone([FromBody] DecryptPhoneDto dto)
        {
            if (string.IsNullOrEmpty(dto.Code) || string.IsNullOrEmpty(dto.EncryptedData) ||
                string.IsNullOrEmpty(dto.Iv) || string.IsNullOrEmpty(dto.OpenId))
                return BadRequest(new { code = 400, msg = "参数缺失" });

            var appId = _config["WeChat:AppId"];
            var secret = _config["WeChat:AppSecret"];
            var url = $"https://api.weixin.qq.com/sns/jscode2session?appid={appId}&secret={secret}&js_code={dto.Code}&grant_type=authorization_code";

            var response = await _http.GetStringAsync(url);
            var wxResult = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);

            if (wxResult == null || wxResult.ContainsKey("errcode"))
                return BadRequest(new { code = 400, msg = $"微信错误: {wxResult?["errmsg"]}" });

            var sessionKey = wxResult["session_key"]?.ToString();
            var openIdFromWx = wxResult["openid"]?.ToString();

            if (openIdFromWx != dto.OpenId)
                return BadRequest(new { code = 400, msg = "openid 不匹配" });

            try
            {
                var phoneInfo = WxDecryptHelper.DecryptPhone(dto.EncryptedData, dto.Iv, sessionKey ?? string.Empty, appId ?? string.Empty);
                var phone = $"+86{phoneInfo.PurePhoneNumber}"; // 微信手机号为中国，添加 +86

                var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.OpenId == dto.OpenId);

                if (seller == null)
                {
                    seller = Seller.Create(dto.OpenId);
                    _db.Sellers.Add(seller);
                }
                else
                {
                    if (string.IsNullOrEmpty(seller.Phone))
                    {
                        seller.BindPhone(phone);
                    }
                    else if (seller.Phone != phone)
                    {
                        return BadRequest(new { code = 400, msg = "手机号已绑定其他微信，无法更换" });
                    }
                }

                seller.RecordLogin("wechat-bind");
                await _db.SaveChangesAsync();

                var token = _authService.GenerateJwt(seller.Id, "Seller", null);

                return Ok(new
                {
                    code = 200,
                    token,
                    sellerId = seller.Id,
                    msg = "绑定成功"
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new { code = 500, msg = "系统错误，请重试" });
            }
        }

        [HttpGet("test")]
        public async Task<IActionResult> test()
        {
            return Ok(new { message = "Auth API is working" });
        }

        // ============================================
        // 新增：客服/主管登录
        // ============================================
        [HttpPost("agent-login")]
        [AllowAnonymous]
        public async Task<IActionResult> AgentLogin([FromBody] AgentLoginDto dto)
        {
            if (string.IsNullOrEmpty(dto.Email) || string.IsNullOrEmpty(dto.Password))
                return BadRequest("邮箱和密码不能为空");

            var agent = await _db.Agents.FirstOrDefaultAsync(a => a.Email == dto.Email);
            if (agent == null)
                return Unauthorized("账号或密码错误");

            if (!AgentPasswordHasher.Verify(dto.Password, agent.PasswordHash))
                return Unauthorized("账号或密码错误");

            if (!agent.IsActive)
                return Unauthorized("账号已被禁用");

            // 明文存量自动升级为哈希（开发可用）
            if (!agent.PasswordHash.StartsWith("sha256:", StringComparison.Ordinal))
            {
                agent.UpdatePassword(AgentPasswordHasher.Hash(dto.Password));
            }

            agent.RecordLogin();
            await _db.SaveChangesAsync();

            var userType = agent.Role.ToString();
            var token = _authService.GenerateJwt(agent.Id, userType, agent.ShopId);

            // Admin 登录写审计（运营控制台入口）
            if (string.Equals(userType, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                await _audit.LogAsync(
                    agent.Id,
                    "Admin",
                    AuditActions.AdminLogin,
                    "Agent",
                    agent.Id.ToString(),
                    new { email = agent.Email, role = userType },
                    agent.ShopId);
            }

            return Ok(new
            {
                token,
                userId = agent.Id,
                agentId = agent.Id,
                userType,
                name = agent.Name,
                nickname = agent.Name,
                role = userType,
                shopId = agent.ShopId
            });
        }


        private async Task<IActionResult?> RejectNewRegistrationIfBlockedAsync()
        {
            var ops = await _ops.GetOpsAsync();
            if (ops.MaintenanceMode)
            {
                return StatusCode(503, new { code = "MAINTENANCE", message = "系统维护中，暂停新商家注册" });
            }
            if (!ops.AllowNewRegistration)
            {
                return StatusCode(503, new { code = "REGISTRATION_CLOSED", message = "暂不开放新商家注册" });
            }
            return null;
        }

        // 开发用：确保存在 Admin 并签发 Admin JWT（仅 Development）
        // 注意：seed-demo 会先建店铺坐席；不可 FirstOrDefault 任意 Agent（会拿到 Role=Agent，Admin API 403）
        [HttpPost("init-agent")]
        [AllowAnonymous]
        public async Task<IActionResult> InitAgent()
        {
            var env = HttpContext.RequestServices.GetService<IWebHostEnvironment>();
            if (env == null || env.EnvironmentName != "Development")
                return StatusCode(403, "Not allowed in production");

            await _db.Database.EnsureCreatedAsync();

            const string demoAdminEmail = "admin@test.com";
            const string demoPassword = "Agent123!";

            // 优先已有 Admin（按邮箱或 Role）
            var admin = await _db.Agents.FirstOrDefaultAsync(a => a.Email == demoAdminEmail)
                ?? await _db.Agents.FirstOrDefaultAsync(a => a.Role == AgentRole.Admin);

            if (admin != null)
            {
                if (admin.Role != AgentRole.Admin)
                {
                    admin.UpdateRole(AgentRole.Admin);
                }
                if (!admin.IsActive)
                {
                    admin.SetActive(true);
                }
                if (!AgentPasswordHasher.Verify(demoPassword, admin.PasswordHash))
                {
                    admin.UpdatePassword(AgentPasswordHasher.Hash(demoPassword));
                }
                await _db.SaveChangesAsync();

                var existingToken = _authService.GenerateJwt(admin.Id, AgentRole.Admin.ToString(), admin.ShopId);
                return Ok(new
                {
                    token = existingToken,
                    agentId = admin.Id,
                    name = admin.Name,
                    email = admin.Email,
                    role = "Admin",
                    password = demoPassword,
                    message = "Admin already exists. Use POST /api/auth/agent-login"
                });
            }

            // 无 Admin：挂到已有 Seller 或新建
            var seller = await _db.Sellers.FirstOrDefaultAsync();
            if (seller == null)
            {
                seller = Seller.Create("test-openid-" + Guid.NewGuid().ToString("N"));
                _db.Sellers.Add(seller);
                await _db.SaveChangesAsync();
            }

            var agent = Agent.Create(
                shopId: seller.Id,
                email: demoAdminEmail,
                name: "Admin Agent",
                passwordHash: AgentPasswordHasher.Hash(demoPassword),
                role: AgentRole.Admin
            );
            _db.Agents.Add(agent);
            await _db.SaveChangesAsync();

            var token = _authService.GenerateJwt(agent.Id, agent.Role.ToString(), agent.ShopId);
            return Ok(new
            {
                token,
                agentId = agent.Id,
                name = agent.Name,
                email = agent.Email,
                role = "Admin",
                password = demoPassword,
                message = "Dev Admin created. Login via POST /api/auth/agent-login"
            });
        }
    }

    public class AgentLoginDto
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
