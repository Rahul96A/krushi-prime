using System.Security.Claims;
using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Persistence;
using KrushiPrime.Infrastructure.Services;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KrushiPrime.Api.Infrastructure;

public static class Policies
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ShopUser = "ShopUser";
    public const string ShopManager = "ShopManager";
    public const string ShopOwner = "ShopOwner";
}

public class HttpCurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal? _p;
    public HttpCurrentUser(IHttpContextAccessor accessor) => _p = accessor.HttpContext?.User;

    public int? UserId => int.TryParse(_p?.FindFirstValue(ClaimTypes.NameIdentifier), out var v) ? v : null;
    public int? TenantId => int.TryParse(_p?.FindFirstValue(AuthService.TenantClaim), out var v) ? v : null;
    public string? UserName => _p?.Identity?.Name;
    public UserRole? Role => Enum.TryParse<UserRole>(_p?.FindFirstValue(ClaimTypes.Role), out var r) ? r : null;
}

/// <summary>Converts business rule violations into RFC 7807 problem responses with friendly messages.</summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _log;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> log) => _log = log;

    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            BusinessException be => (be.StatusCode, be.Message),
            DbUpdateException => (StatusCodes.Status409Conflict, "The data could not be saved because it conflicts with existing records."),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong. Please try again."),
        };
        if (status >= 500) _log.LogError(ex, "Unhandled error on {Path}", ctx.Request.Path);

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = title, Instance = ctx.Request.Path }, ct);
        return true;
    }
}

/// <summary>
/// Licensing gate. Suspended shops are blocked. After the free trial / paid period ends, the shop keeps
/// read-only access (reports, history) but cannot create new bills until renewed.
/// </summary>
public class SubscriptionMiddleware
{
    private readonly RequestDelegate _next;
    public SubscriptionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, ICurrentUser user, AppDbContext db, IMemoryCache cache, IConfiguration cfg)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (user.TenantId is int tid && path.StartsWith("/api", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("/api/subscription", StringComparison.OrdinalIgnoreCase)) // renewal must work after expiry
        {
            var t = await cache.GetOrCreateAsync($"tenant-lic-{tid}", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
                return await db.Tenants.AsNoTracking().Where(x => x.Id == tid).Select(x => new { x.IsActive, x.SubscriptionEnd }).FirstOrDefaultAsync();
            });
            var support = cfg["Support:Phone"] ?? "8668375712";
            if (t is null || !t.IsActive)
            {
                await Deny(ctx, StatusCodes.Status403Forbidden, $"Shop account suspended. Contact Krushi Prime support: {support}");
                return;
            }
            if (t.SubscriptionEnd.Date < AppClock.Today && !HttpMethods.IsGet(ctx.Request.Method))
            {
                await Deny(ctx, StatusCodes.Status402PaymentRequired, $"Your Krushi Prime subscription ended on {t.SubscriptionEnd:dd-MM-yyyy}. Please renew to continue billing. Call {support}.");
                return;
            }
        }
        await _next(ctx);
    }

    private static Task Deny(HttpContext ctx, int status, string msg)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new ProblemDetails { Status = status, Title = msg });
    }
}
