using KrushiPrime.Api.Infrastructure;
using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Persistence;
using KrushiPrime.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrushiPrime.Api.Controllers;

[ApiController, Route("api/reports"), Authorize(Policy = Policies.ShopUser)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _r;
    public ReportsController(IReportService r) => _r = r;

    private static (DateTime, DateTime) Range(DateTime? from, DateTime? to)
        => (from ?? new DateTime(AppClock.Today.Year, AppClock.Today.Month, 1), to ?? AppClock.Today);

    [HttpGet("dashboard")] public Task<DashboardDto> Dashboard(CancellationToken ct) => _r.DashboardAsync(ct);
    [HttpGet("sales")] public Task<SalesReportDto> Sales(DateTime? from, DateTime? to, CancellationToken ct) { var (f, t) = Range(from, to); return _r.SalesAsync(f, t, ct); }
    [HttpGet("gst"), Authorize(Policy = Policies.ShopManager)] public Task<GstReportDto> Gst(DateTime? from, DateTime? to, CancellationToken ct) { var (f, t) = Range(from, to); return _r.GstAsync(f, t, ct); }
    [HttpGet("profit"), Authorize(Policy = Policies.ShopOwner)] public Task<ProfitReportDto> Profit(DateTime? from, DateTime? to, CancellationToken ct) { var (f, t) = Range(from, to); return _r.ProfitAsync(f, t, ct); }
    [HttpGet("purchases"), Authorize(Policy = Policies.ShopManager)] public Task<List<PurchaseListItem>> Purchases(DateTime? from, DateTime? to, CancellationToken ct) { var (f, t) = Range(from, to); return _r.PurchasesAsync(f, t, ct); }
    [HttpGet("stock")] public Task<List<StockRow>> Stock(CancellationToken ct) => _r.StockAsync(ct);
    [HttpGet("expiry")] public Task<List<ExpiryRow>> Expiry(int days = 60, CancellationToken ct = default) => _r.ExpiryAsync(days, ct);
    [HttpGet("udhari")] public Task<List<UdhariRow>> Udhari(CancellationToken ct) => _r.UdhariAsync(ct);
}

[ApiController, Route("api/settings"), Authorize(Policy = Policies.ShopUser)]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IAuthService _auth;
    public SettingsController(AppDbContext db, ICurrentUser user, IAuthService auth) { _db = db; _user = user; _auth = auth; }

    internal static ShopSettingsDto ToDto(Tenant t) => new(t.ShopName, t.OwnerName, t.Mobile, t.Email, t.AddressLine, t.Village, t.Taluka, t.District,
        t.State, t.StateCode, t.Pincode, t.Gstin, t.IsGstRegistered, t.FertilizerLicenseNo, t.PesticideLicenseNo, t.SeedLicenseNo, t.UpiId,
        t.InvoicePrefix, t.InvoiceFooter, t.PricesIncludeTax, t.AllowNegativeStock);

    private Task<Tenant> Shop(CancellationToken ct) => _db.Tenants.FirstAsync(t => t.Id == _user.TenantId, ct);

    [HttpGet("shop")]
    public async Task<object> Get(CancellationToken ct)
    {
        var t = await Shop(ct);
        return new { Settings = ToDto(t), Subscription = AuthService.ToSummary(t) };
    }

    [HttpPut("shop"), Authorize(Policy = Policies.ShopOwner)]
    public async Task<ShopSettingsDto> Update(ShopSettingsDto r, CancellationToken ct)
    {
        var t = await Shop(ct);
        t.ShopName = r.ShopName; t.OwnerName = r.OwnerName; t.Mobile = r.Mobile; t.Email = r.Email; t.AddressLine = r.AddressLine; t.Village = r.Village;
        t.Taluka = r.Taluka; t.District = r.District; t.State = r.State; t.StateCode = r.StateCode; t.Pincode = r.Pincode; t.Gstin = r.Gstin?.ToUpperInvariant();
        t.IsGstRegistered = r.IsGstRegistered; t.FertilizerLicenseNo = r.FertilizerLicenseNo; t.PesticideLicenseNo = r.PesticideLicenseNo; t.SeedLicenseNo = r.SeedLicenseNo;
        t.UpiId = r.UpiId; t.InvoicePrefix = string.IsNullOrWhiteSpace(r.InvoicePrefix) ? "KP" : r.InvoicePrefix.Trim().ToUpperInvariant(); t.InvoiceFooter = r.InvoiceFooter;
        t.PricesIncludeTax = r.PricesIncludeTax; t.AllowNegativeStock = r.AllowNegativeStock;
        await _db.SaveChangesAsync(ct);
        return ToDto(t);
    }

    public record UserRow(int Id, string FullName, string Username, string Role, bool IsActive, DateTime? LastLoginAt);

    [HttpGet("users"), Authorize(Policy = Policies.ShopOwner)]
    public Task<List<UserRow>> Users(CancellationToken ct) => _db.Users.AsNoTracking().Where(u => u.TenantId == _user.TenantId)
        .OrderBy(u => u.Role).Select(u => new UserRow(u.Id, u.FullName, u.Username, u.Role.ToString(), u.IsActive, u.LastLoginAt)).ToListAsync(ct);

    [HttpPost("users"), Authorize(Policy = Policies.ShopOwner)]
    public async Task<UserRow> AddUser(CreateUserRequest r, CancellationToken ct)
    {
        if (r.Role is UserRole.SuperAdmin or UserRole.Owner) throw new BusinessException("You can add Manager or Cashier users only.");
        if (await _db.Users.AnyAsync(u => u.Username == r.Username, ct)) throw new BusinessException("Username already taken.", 409);
        var u = new AppUser { TenantId = _user.TenantId, FullName = r.FullName, Username = r.Username.Trim(), Role = r.Role, PasswordHash = _auth.HashPassword(r.Password) };
        _db.Users.Add(u);
        await _db.SaveChangesAsync(ct);
        return new UserRow(u.Id, u.FullName, u.Username, u.Role.ToString(), true, null);
    }

    [HttpPut("users/{id:int}/status"), Authorize(Policy = Policies.ShopOwner)]
    public async Task<IActionResult> UserStatus(int id, SetStatusRequest r, CancellationToken ct)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _user.TenantId, ct) ?? throw new BusinessException("User not found.", 404);
        if (u.Role == UserRole.Owner) throw new BusinessException("Owner account cannot be disabled.");
        u.IsActive = r.IsActive;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
