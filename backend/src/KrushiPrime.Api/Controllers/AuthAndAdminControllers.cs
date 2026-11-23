using KrushiPrime.Api.Infrastructure;
using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Persistence;
using KrushiPrime.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KrushiPrime.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    public AuthController(IAuthService auth, AppDbContext db, ICurrentUser user) { _auth = auth; _db = db; _user = user; }

    [HttpPost("login"), EnableRateLimiting("auth")]
    public Task<AuthResponse> Login(LoginRequest req, CancellationToken ct) => _auth.LoginAsync(req, ct);

    /// <summary>Self sign-up for a Krushi Seva Kendra → instant 6-month free trial.</summary>
    [HttpPost("register-shop"), EnableRateLimiting("auth")]
    public Task<AuthResponse> Register(RegisterShopRequest req, CancellationToken ct) => _auth.RegisterShopAsync(req, ct);

    [HttpGet("me"), Authorize]
    public Task<UserInfo> Me(CancellationToken ct) => _auth.GetUserInfoAsync(_user.UserId!.Value, ct);

    [HttpPost("change-password"), Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest req, CancellationToken ct)
    {
        var u = await _db.Users.FirstAsync(x => x.Id == _user.UserId, ct);
        if (!_auth.VerifyPassword(u, req.CurrentPassword)) throw new BusinessException("Current password is incorrect.");
        u.PasswordHash = _auth.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

/// <summary>Krushi Prime vendor console: onboard shops, manage trials & renewals.</summary>
[ApiController, Route("api/admin"), Authorize(Policy = Policies.SuperAdmin)]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuthService _auth;
    private readonly IMemoryCache _cache;
    public AdminController(AppDbContext db, IAuthService auth, IMemoryCache cache) { _db = db; _auth = auth; _cache = cache; }

    public record TenantRow(int Id, string ShopName, string OwnerName, string Mobile, string? District, string Plan, DateTime SubscriptionStart,
        DateTime SubscriptionEnd, int DaysLeft, bool IsActive, int Invoices, decimal TotalBilled, DateTime? LastBillAt, DateTime CreatedAt);

    [HttpGet("stats")]
    public async Task<object> Stats(CancellationToken ct)
    {
        var tenants = await _db.Tenants.AsNoTracking().ToListAsync(ct);
        var today = AppClock.Today;
        return new
        {
            TotalShops = tenants.Count,
            ActiveShops = tenants.Count(t => t.IsActive && t.SubscriptionEnd >= today),
            OnTrial = tenants.Count(t => t.Plan == SubscriptionPlan.Trial && t.SubscriptionEnd >= today),
            Paid = tenants.Count(t => t.Plan != SubscriptionPlan.Trial && t.SubscriptionEnd >= today),
            Expired = tenants.Count(t => t.SubscriptionEnd < today),
            ExpiringIn30Days = tenants.Count(t => t.SubscriptionEnd >= today && t.SubscriptionEnd <= today.AddDays(30)),
            InvoicesToday = await _db.SalesInvoices.IgnoreQueryFilters().CountAsync(i => i.InvoiceDate >= today, ct),
        };
    }

    [HttpGet("tenants")]
    public async Task<List<TenantRow>> Tenants([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Tenants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(t => EF.Functions.Like(t.ShopName, "%" + q + "%") || EF.Functions.Like(t.Mobile, "%" + q + "%") || EF.Functions.Like(t.OwnerName, "%" + q + "%"));
        var tenants = await query.OrderByDescending(t => t.Id).ToListAsync(ct);
        var stats = (await _db.SalesInvoices.IgnoreQueryFilters().AsNoTracking().Where(i => i.Status != InvoiceStatus.Cancelled)
                .Select(i => new { i.TenantId, i.GrandTotal, i.InvoiceDate }).ToListAsync(ct))
            .GroupBy(i => i.TenantId).ToDictionary(g => g.Key, g => (Count: g.Count(), Total: g.Sum(x => x.GrandTotal), Last: g.Max(x => x.InvoiceDate)));
        return tenants.Select(t =>
        {
            var s = AuthService.ToSummary(t);
            stats.TryGetValue(t.Id, out var st);
            return new TenantRow(t.Id, t.ShopName, t.OwnerName, t.Mobile, t.District, t.Plan.ToString(), t.SubscriptionStart, t.SubscriptionEnd,
                s.IsExpired ? -1 : s.DaysLeft, t.IsActive, st.Count, st.Total, st.Count == 0 ? null : st.Last, t.CreatedAt);
        }).ToList();
    }

    /// <summary>Onboard a shop from the field (sales executive) – gets 6 months free automatically.</summary>
    [HttpPost("tenants")]
    public async Task<TenantSummary> Create(RegisterShopRequest req, CancellationToken ct) => AuthService.ToSummary(await _auth.OnboardTenantAsync(req, ct));

    [HttpPut("tenants/{id:int}/subscription")]
    public async Task<TenantSummary> Subscription(int id, UpdateSubscriptionRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FindAsync(new object[] { id }, ct) ?? throw new BusinessException("Shop not found.", 404);
        t.Plan = req.Plan;
        if (req.SubscriptionEnd is DateTime end) t.SubscriptionEnd = end.Date;
        else if (req.ExtendMonths is int m and > 0)
            t.SubscriptionEnd = (t.SubscriptionEnd < AppClock.Today ? AppClock.Today : t.SubscriptionEnd).AddMonths(m);
        if (req.Notes is not null) t.Notes = req.Notes;
        await _db.SaveChangesAsync(ct);
        _cache.Remove($"tenant-lic-{id}");
        return AuthService.ToSummary(t);
    }

    [HttpPut("tenants/{id:int}/status")]
    public async Task<TenantSummary> Status(int id, SetStatusRequest req, CancellationToken ct)
    {
        var t = await _db.Tenants.FindAsync(new object[] { id }, ct) ?? throw new BusinessException("Shop not found.", 404);
        t.IsActive = req.IsActive;
        await _db.SaveChangesAsync(ct);
        _cache.Remove($"tenant-lic-{id}");
        return AuthService.ToSummary(t);
    }

    [HttpPost("tenants/{id:int}/reset-password")]
    public async Task<object> ResetOwnerPassword(int id, CancellationToken ct)
    {
        var owner = await _db.Users.FirstOrDefaultAsync(u => u.TenantId == id && u.Role == UserRole.Owner, ct) ?? throw new BusinessException("Owner not found.", 404);
        var temp = $"Kp@{Random.Shared.Next(100000, 999999)}";
        owner.PasswordHash = _auth.HashPassword(temp);
        await _db.SaveChangesAsync(ct);
        return new { owner.Username, TemporaryPassword = temp };
    }
}
