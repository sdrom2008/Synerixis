using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Senparc.Weixin.RegisterServices;
using Synerixis.Application.Agents;
using Synerixis.Application.Interfaces;
using Synerixis.Application.Interfaces.Ai;
using Synerixis.Application.Interfaces.Infrastructure;
using Synerixis.Application.Services;
using Synerixis.Domain.Entities;
using Synerixis.Infrastructure.AI;
using Synerixis.Infrastructure.AIServices;
using Synerixis.Infrastructure.Data;
using Synerixis.Infrastructure.Payment;
using Synerixis.Infrastructure.Repositories;
using Synerixis.Infrastructure.Services;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();          // 输出到控制台
builder.Logging.AddDebug();            // 输出到调试窗口（如 VS Output）
builder.Logging.SetMinimumLevel(LogLevel.Debug);  // 必须设为 Debug 才能看到 LogDebug

builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// 1 添加控制器支持
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;  // 忽略大小写绑定
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;  // 可选
    });

//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen(c =>
//{
//    c.SwaggerDoc("v1", new OpenApiInfo
//    {
//        Title = "Synerixis API",
//        Version = "v1",
//        Description = "Synerixis AI SaaS 平台 API",
//        Contact = new OpenApiContact
//        {
//            Name = "Your Name",
//            Email = "sdrom2008@qq.com"
//        }
//    });

//    // 可选：让 Swagger 显示 XML 注释（如果你的 Controller 有 /// 注释）
//    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
//    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
//    if (File.Exists(xmlPath))
//        c.IncludeXmlComments(xmlPath);
//});

// 2 Semantic Kernel 配置
builder.Services.AddSingleton<SemanticKernelConfig>();

builder.Services.AddSingleton<Kernel>(sp => sp.GetRequiredService<SemanticKernelConfig>().Kernel);

builder.Services.AddScoped<IChatCompletionService>(sp =>
    sp.GetRequiredService<Kernel>().GetRequiredService<IChatCompletionService>());

// 注册 SemanticKernelService（AliyunLlmClient 依赖它）
builder.Services.AddSingleton<SemanticKernelService>();


// 4. 业务服务（顺序：先基础，后依赖）
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IGeneralChatAgent, GeneralChatAgent>();
builder.Services.AddScoped<IIntentClassifier, IntentClassifier>();

// --- NEWLY ADDED SERVICES FOR MVP ---
builder.Services.AddScoped<IMarketingCopyService, MarketingCopyService>();
builder.Services.AddScoped<ILlmClient, AliyunLlmClient>(); // Maps the interface to our Aliyun implementation
// --- END OF NEWLY ADDED SERVICES ---

// --- SERVICES FOR AI CUSTOMER SUPPORT ---
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<IECommercePlatformClient, ECommercePlatformClient>();
builder.Services.AddScoped<IAgentStatsService, AgentStatsService>();

// Register all agents. The DI container will provide them to the AgentRouter.
// Temporarily commented out due to interface mismatch (these agents need to implement Synerixis.Application.Interfaces.IAgent)
// builder.Services.AddScoped<IAgent, OrderAgent>();
// builder.Services.AddScoped<IAgent, LogisticsAgent>();
// --- END OF AI CUSTOMER SUPPORT SERVICES ---


// 5. Agent 注册（所有具体 Agent）
builder.Services.AddScoped<IAgent, ProductOptimizationAgent>();
builder.Services.AddScoped<IAgent, CompetitorAnalysisAgent>();
// 如果有其他 Agent，在这里继续加
builder.Services.AddSingleton<AliyunSmsService>();

// 微信支付（生产环境才启用，开发环境暂时注释）
// builder.Services.AddScoped<WeChatPayV3Client>(serviceProvider =>
// {
//     var config = serviceProvider.GetRequiredService<IConfiguration>();
//     var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
//     return new WeChatPayV3Client(
//         config["WeChatPay:MchId"],
//         config["WeChatPay:AppId"],
//         config["WeChatPay:ApiV3Key"],
//         config["WeChatPay:CertPath"],
//         config["WeChatPay:CertPassword"],
//         dbContext);
// });

// builder.Services.AddScoped<WechatPaymentProvider>();
// builder.Services.AddScoped<AlipayPaymentProvider>();
// builder.Services.AddScoped<IPaymentProviderFactory, PaymentProviderFactory>();

builder.Services.AddScoped<ProductService>();

// 内存缓存（用于意图分类等）
builder.Services.AddMemoryCache();

// 专用仓储（如果有）
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();


// 意图分类器（用 LLM 版本）
builder.Services.AddScoped<IIntentClassifier, IntentClassifier>();


// 6. AgentRouter（注册为接口！必须在所有 Agent 后）
builder.Services.AddScoped<IAgentRouter, AgentRouter>();

#region 添加微信配置
builder.Services.AddSenparcWeixinServices(builder.Configuration);

#endregion

// 添加 OpenAPI/Swagger（可选，开发时方便）
//builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

// CORS（允许所有，生产环境建议收紧）
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

// JWT 认证
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

// 数据库配置：MySQL
var conn = builder.Configuration.GetConnectionString("MySqlConnection");
if (string.IsNullOrEmpty(conn))
{
    conn = builder.Configuration["Database:ConnectionString"];
}
if (string.IsNullOrEmpty(conn))
    throw new InvalidOperationException("MySQL connection string not configured");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseMySql(conn, ServerVersion.AutoDetect(conn), mysql =>
    {
        mysql.EnableRetryOnFailure();
    }));

// 泛型仓储（推荐只注册一次）
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// HttpClient 工厂（如果 Agent 里需要调用外部 API）
builder.Services.AddHttpClient();

// Health Checks（可选）
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("Database");

// 7. AiChatService（最后注册，依赖 Router）
builder.Services.AddScoped<IAiChatService, AiChatService>();

var app = builder.Build();

// 开发环境自动建表（确保所有表存在）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();  // 根据当前模型创建所有表（开发环境用）
}

//配置静态文件服务
app.UseStaticFiles();

// 中间件管道
//app.UseCors("AllowAll");

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI(c =>
//    {
//        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Synerixis API v1");
//        c.RoutePrefix = string.Empty;  // 访问根路径 / 就直接打开 Swagger 页面
//        // 可选：c.DefaultModelsExpandDepth(-1);  // 默认折叠模型
//    });
//}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();