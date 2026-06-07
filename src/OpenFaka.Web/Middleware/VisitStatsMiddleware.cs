using System.Collections.Concurrent;
using FreeSql;
using OpenFaka.Core.Entities;

namespace OpenFaka.Web.Middleware;

/// <summary>
/// 访问统计中间件：记录每日 PV/UV 到 faka_visit_stats 表
/// UV 基于客户端 IP 去重，每日自动重置
/// </summary>
public class VisitStatsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<VisitStatsMiddleware> _logger;

    // 每日 UV 去重：key=日期字符串, value=当天已访问的 IP 集合
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _dailyVisitors = new();

    // 排除的路径前缀（静态资源、健康检查等）
    private static readonly string[] _excludedPrefixes = new[]
    {
        "/swagger", "/_framework", "/_content", "/health", "/favicon.ico",
        "/css", "/js", "/images", "/fonts", "/assets"
    };

    public VisitStatsMiddleware(RequestDelegate next, ILogger<VisitStatsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IFreeSql db)
    {
        await _next(context);

        // 只统计成功的页面请求（非 API 错误、非静态资源）
        if (ShouldTrack(context))
        {
            try
            {
                await RecordVisitAsync(db, context);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record visit stats");
            }
        }
    }

    private static bool ShouldTrack(HttpContext context)
    {
        // 只统计 GET 请求
        if (!HttpMethods.IsGet(context.Request.Method) &&
            !HttpMethods.IsPost(context.Request.Method))
            return false;

        // 排除静态资源和健康检查
        var path = context.Request.Path.Value ?? "";
        if (_excludedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return false;

        // 排除 API 调用（只统计页面访问）
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/admin/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/store/", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private async Task RecordVisitAsync(IFreeSql db, HttpContext context)
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // 获取或创建当天的访问者集合
        var todayVisitors = _dailyVisitors.GetOrAdd(today, _ => new ConcurrentDictionary<string, byte>());

        // 尝试添加 IP，判断是否为新访客
        bool isNewVisitor = todayVisitors.TryAdd(clientIp, 0);

        // 获取或创建今天的统计记录
        var startOfDay = DateTime.UtcNow.Date;
        var endOfDay = startOfDay.AddDays(1);

        // 使用事务避免并发创建重复记录
        using var uow = db.CreateUnitOfWork();
        var existing = await db.Select<FakaVisitStats>()
            .Where(v => v.Date >= startOfDay && v.Date < endOfDay)
            .ForUpdate()
            .FirstAsync();

        if (existing != null)
        {
            await db.Update<FakaVisitStats>()
                .Where(v => v.Id == existing.Id)
                .Set(v => v.Pv, existing.Pv + 1)
                .Set(v => v.Uv, isNewVisitor ? existing.Uv + 1 : existing.Uv)
                .ExecuteAffrowsAsync();
        }
        else
        {
            try
            {
                await db.Insert(new FakaVisitStats
                {
                    Date = startOfDay,
                    Pv = 1,
                    Uv = 1
                }).ExecuteAffrowsAsync();
            }
            catch
            {
                // 并发插入冲突：回退到更新
                var fallback = await db.Select<FakaVisitStats>()
                    .Where(v => v.Date >= startOfDay && v.Date < endOfDay)
                    .FirstAsync();
                if (fallback != null)
                {
                    await db.Update<FakaVisitStats>()
                        .Where(v => v.Id == fallback.Id)
                        .Set(v => v.Pv, fallback.Pv + 1)
                        .Set(v => v.Uv, isNewVisitor ? fallback.Uv + 1 : fallback.Uv)
                        .ExecuteAffrowsAsync();
                }
            }
        }
        uow.Commit();
    }

    /// <summary>
    /// 清理过期的内存数据（可由定时任务调用）
    /// </summary>
    public static void CleanupOldEntries()
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        foreach (var key in _dailyVisitors.Keys)
        {
            if (key != today)
            {
                _dailyVisitors.TryRemove(key, out _);
            }
        }
    }
}
