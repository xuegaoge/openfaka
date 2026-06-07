using System.Text;
using System.Threading.RateLimiting;
using DotNetCore.Security;
using FreeRedis;
using FreeSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenFaka.Application.Interfaces;
using OpenFaka.Application.Services;
using OpenFaka.Core.Interfaces;
using OpenFaka.Infrastructure.Services;
using OpenFaka.Infrastructure.Services.Payment;
using OpenFaka.Web.BackgroundServices;
using OpenFaka.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ============ FreeSql ============
var dbBuilder = new FreeSqlBuilder()
    .UseConnectionString(DataType.Sqlite, config.GetConnectionString("DefaultConnection"))
    .UseAutoSyncStructure(true);

if (builder.Environment.IsDevelopment())
{
    dbBuilder.UseMonitorCommand(cmd => Console.WriteLine($"SQL: {cmd.CommandText}"));
}

var db = dbBuilder.Build();

// 启动时同步所有实体表结构
db.CodeFirst.SyncStructure(
    typeof(OpenFaka.Core.Entities.FakaProduct),
    typeof(OpenFaka.Core.Entities.FakaProductSpec),
    typeof(OpenFaka.Core.Entities.FakaCategory),
    typeof(OpenFaka.Core.Entities.FakaWholesaleRule),
    typeof(OpenFaka.Core.Entities.FakaCardKey),
    typeof(OpenFaka.Core.Entities.FakaCardImportBatch),
    typeof(OpenFaka.Core.Entities.FakaOrder),
    typeof(OpenFaka.Core.Entities.FakaOrderItem),
    typeof(OpenFaka.Core.Entities.FakaCartItem),
    typeof(OpenFaka.Core.Entities.FakaPaymentChannel),
    typeof(OpenFaka.Core.Entities.FakaWebhookEvent),
    typeof(OpenFaka.Core.Entities.FakaUnmatchedTransaction),
    typeof(OpenFaka.Core.Entities.FakaVisitStats)
);

// LinCms 基础表（使用 OpenFaka 轻量影子实体，不继承 FullAuditEntity）
db.CodeFirst.SyncStructure(
    typeof(OpenFaka.Core.Entities.Cms.LinUser),
    typeof(OpenFaka.Core.Entities.Cms.LinGroup),
    typeof(OpenFaka.Core.Entities.Cms.LinUserIdentity),
    typeof(OpenFaka.Core.Entities.Cms.LinUserGroup),
    typeof(OpenFaka.Core.Entities.Cms.LinSetting)
);

builder.Services.AddSingleton(db);

// ============ Redis Cache ============
var redisConfig = config.GetSection("Redis");
var redisEnabled = redisConfig.GetValue<bool?>("Enabled") ?? true;
var redisConnectionString = redisConfig["ConnectionString"] ?? "localhost:6379";

ICacheService cacheService;
if (redisEnabled)
{
    try
    {
        var redisClient = new RedisClient(redisConnectionString);
        // 健康检查：ping 确认 Redis 可用
        var pingResult = redisClient.Ping();
        if (!string.IsNullOrEmpty(pingResult))
        {
            builder.Services.AddSingleton(redisClient);
            builder.Services.AddSingleton<ICacheService, RedisCacheService>();
            Console.WriteLine($"[Redis] Connected to {redisConnectionString}, ping={pingResult}");
            cacheService = null; // will be resolved via DI
            goto skipFallback;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Redis] Connection failed ({ex.Message}), falling back to InMemoryCache");
    }
}

// 降级：使用内存缓存
builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
Console.WriteLine("[Redis] Using InMemoryCache (Redis disabled or unreachable)");

skipFallback:
// ============ JWT Authentication ============
var jwtSection = config.GetSection("Jwt");
var secretKey = jwtSection["SecretKey"];

// 自动生成安全密钥（如果使用占位符）
if (string.IsNullOrEmpty(secretKey) || secretKey.Contains("CHANGE_ME"))
{
    var keyFilePath = Path.Combine(builder.Environment.ContentRootPath, ".jwt-secret");
    if (File.Exists(keyFilePath))
    {
        secretKey = File.ReadAllText(keyFilePath).Trim();
    }
    else
    {
        var randomKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        File.WriteAllText(keyFilePath, randomKey);
        File.SetAttributes(keyFilePath, File.GetAttributes(keyFilePath) | FileAttributes.Hidden);
        secretKey = randomKey;
        Console.WriteLine("[JWT] Generated and saved new secret key to .jwt-secret");
    }
}

if (!string.IsNullOrEmpty(secretKey) && secretKey.Length >= 32)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSection["Issuer"],
                ValidAudience = jwtSection["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });
    builder.Services.AddAuthorization();
}

// ============ CORS ============
var corsOrigins = config.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:3000" };
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// ============ Controllers ============
builder.Services.AddControllers()
.AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});

// ============ Cryptography (for password hashing) ============
builder.Services.AddICryptographyService("lin-cms-dotnetcore-cryptography");

// ============ Application Services ============
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICardKeyService, CardKeyService>();
builder.Services.AddScoped<UsdtTxidService>();

// ============ Payment Providers ============
builder.Services.AddScoped<OpenFaka.Core.Interfaces.IPaymentProviderFactory, PaymentProviderFactory>();
builder.Services.AddScoped<PaymentProviderFactory>();
// 支付 Provider（从 PaymentChannel.configData 解析配置）
builder.Services.AddHttpClient<EpayProvider>();
builder.Services.AddHttpClient<BepusdtProvider>();

// ============ Background Services ============
builder.Services.AddHostedService<OrderExpirationBackgroundService>();

// ============ Swagger ============
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============ Rate Limiting ============
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // 全局固定窗口限流：每 IP 每分钟 120 次请求
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10
        });
    });
});

var app = builder.Build();

// ============ Middleware ============
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseMiddleware<VisitStatsMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ============ Seed Data ============
try
{
    using var scope = app.Services.CreateScope();
    var freeSql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
    var cryptoService = scope.ServiceProvider.GetRequiredService<ICryptographyService>();

    // 1. Seed admin group
    var adminGroup = await freeSql.Select<OpenFaka.Core.Entities.Cms.LinGroup>()
        .Where(g => g.Name == "Admin").FirstAsync();
    if (adminGroup == null)
    {
        await freeSql.Insert(new OpenFaka.Core.Entities.Cms.LinGroup
        {
            Name = "Admin", Info = "超级管理员", IsStatic = true, CreateTime = DateTime.Now
        }).ExecuteAffrowsAsync();
        Console.WriteLine("[Seed] Created admin group.");
    }

    // 2. Seed default admin user
    var adminUser = await freeSql.Select<OpenFaka.Core.Entities.Cms.LinUser>()
        .Where(u => u.Username == "admin").FirstAsync();
    if (adminUser == null)
    {
        var adminPwd = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12));
        var salt = Guid.NewGuid().ToString();
        var encryptedPwd = cryptoService.Encrypt(adminPwd, salt);

        var userId = await freeSql.Insert(new OpenFaka.Core.Entities.Cms.LinUser
        {
            Username = "admin", Nickname = "管理员", Email = "admin@openfaka.com",
            Salt = salt, Active = 1, CreateTime = DateTime.Now
        }).ExecuteIdentityAsync();

        await freeSql.Insert(new OpenFaka.Core.Entities.Cms.LinUserIdentity
        {
            Id = Guid.NewGuid().ToString(), IdentityType = "Password", Identifier = "admin",
            Credential = encryptedPwd, CreateTime = DateTime.Now, CreateUserId = (long)userId
        }).ExecuteAffrowsAsync();

        var adminGroupId = await freeSql.Select<OpenFaka.Core.Entities.Cms.LinGroup>()
            .Where(g => g.Name == "Admin").ToOneAsync(g => g.Id);

        await freeSql.Insert(new OpenFaka.Core.Entities.Cms.LinUserGroup
        {
            UserId = (long)userId, GroupId = adminGroupId, CreateTime = DateTime.Now
        }).ExecuteAffrowsAsync();

        Console.WriteLine($"[Seed] Created admin user (admin). Password: {adminPwd}");
    }

    // 3. Seed default categories
    var hasCategories = await freeSql.Ado.ExecuteScalarAsync(
        "SELECT COUNT(*) FROM faka_category WHERE IsDeleted = 0");
    if (hasCategories == null || Convert.ToInt32(hasCategories) == 0)
    {
        var categories = new[] { "游戏点卡", "软件授权", "会员账号", "优惠券码", "其他" };
        for (int i = 0; i < categories.Length; i++)
        {
            await freeSql.Ado.ExecuteNonQueryAsync(
                "INSERT INTO faka_category (Name, SortOrder, IsDeleted, CreateTime) VALUES (@name, @sort, 0, datetime('now'))",
                new { name = categories[i], sort = i });
        }
        Console.WriteLine("[Seed] Created default categories.");
    }

    // 4. Seed default payment channels
    var hasChannels = await freeSql.Ado.ExecuteScalarAsync(
        "SELECT COUNT(*) FROM faka_payment_channel WHERE IsDeleted = 0");
    if (hasChannels == null || Convert.ToInt32(hasChannels) == 0)
    {
        var channels = new[]
        {
            new { code = "epay_alipay", name = "支付宝（易支付）", type = 0, sort = 0 },
            new { code = "epay_wechat", name = "微信支付（易支付）", type = 1, sort = 1 },
            new { code = "usdt_trc20", name = "USDT-TRC20", type = 2, sort = 2 },
        };
        foreach (var ch in channels)
        {
            await freeSql.Ado.ExecuteNonQueryAsync(
                "INSERT INTO faka_payment_channel (ChannelCode, ChannelName, ProviderType, ConfigData, IsEnabled, SortOrder, IsDeleted, CreateTime) VALUES (@code, @name, @type, '{}', 0, @sort, 0, datetime('now'))",
                new { ch.code, ch.name, ch.type, ch.sort });
        }
        Console.WriteLine("[Seed] Created default payment channels (disabled, needs configuration).");
    }

    // 5. Seed default site configurations
    var hasSiteConfig = await freeSql.Select<OpenFaka.Core.Entities.Cms.LinSetting>()
        .Where(s => s.Name == "site_name").FirstAsync();
    if (hasSiteConfig == null)
    {
        var siteConfigs = new[]
        {
            new { key = "site_name", value = "OpenFaka" },
            new { key = "site_slogan", value = "数字商品自动发卡平台" },
            new { key = "site_description", value = "安全、稳定、高效的数字商品自动发卡平台，支持多种支付方式" },
            new { key = "site_logo", value = "" },
            new { key = "site_favicon", value = "" },
            new { key = "site_announcement_enabled", value = "false" },
            new { key = "site_announcement", value = "" },
            new { key = "site_popup_enabled", value = "false" },
            new { key = "site_popup_content", value = "" },
            new { key = "site_contact_email", value = "" },
            new { key = "site_contact_telegram", value = "" },
            new { key = "site_contact_telegram_group", value = "" },
            new { key = "site_points_enabled", value = "false" },
            new { key = "site_points_rate", value = "1" },
            new { key = "site_maintenance", value = "false" },
            new { key = "site_maintenance_message", value = "系统维护中，请稍后再试" },
            new { key = "site_footer_text", value = "© 2024 OpenFaka. All rights reserved." },
            new { key = "site_github_url", value = "" },
            new { key = "site_custom_css", value = "" },
            new { key = "order_expiry_minutes", value = "60" },
            new { key = "site_currency", value = "CNY" },
            new { key = "site_currency_symbol", value = "¥" },
        };
        var settings = siteConfigs.Select(c => new OpenFaka.Core.Entities.Cms.LinSetting
        {
            Name = c.key, Value = c.value, ProviderName = "Site", ProviderKey = "Default"
        }).ToList();
        await freeSql.Insert(settings).ExecuteAffrowsAsync();
        Console.WriteLine("[Seed] Created default site configurations.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Seed] Warning: {ex.Message}");
}

app.Run();
