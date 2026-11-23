using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace KrushiPrime.Infrastructure.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default);
    Task<AuthResponse> RegisterShopAsync(RegisterShopRequest req, CancellationToken ct = default);
    Task<Tenant> OnboardTenantAsync(RegisterShopRequest req, CancellationToken ct = default);
    Task<UserInfo> GetUserInfoAsync(int userId, CancellationToken ct = default);
    string HashPassword(string password);
    bool VerifyPassword(AppUser user, string password);
}

public class AuthService : IAuthService
{
    public const string TenantClaim = "tenant_id";
    public const int FreeTrialMonths = 6;   // Launch offer: 6 months free for every Krushi Seva Kendra

    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public AuthService(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    public string HashPassword(string password) => _hasher.HashPassword(null!, password);
    public bool VerifyPassword(AppUser user, string password) => _hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;

    public async Task<AuthResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Username == req.Username.Trim(), ct);
        if (user is null || !VerifyPassword(user, req.Password)) throw new BusinessException("Invalid mobile number / username or password.", 401);
        if (!user.IsActive) throw new BusinessException("Your user account is disabled. Contact the shop owner.", 403);
        if (user.Tenant is { IsActive: false }) throw new BusinessException("This shop account is suspended. Please contact Krushi Prime support.", 403);

        user.LastLoginAt = AppClock.Now;
        await _db.SaveChangesAsync(ct);
        return Issue(user);
    }

    public async Task<AuthResponse> RegisterShopAsync(RegisterShopRequest req, CancellationToken ct = default)
    {
        var tenant = await OnboardTenantAsync(req, ct);
        var owner = await _db.Users.Include(u => u.Tenant).FirstAsync(u => u.TenantId == tenant.Id && u.Role == UserRole.Owner, ct);
        return Issue(owner);
    }

    /// <summary>Creates shop + owner login with the 6-month free trial; optionally seeds a ready product catalogue.</summary>
    public async Task<Tenant> OnboardTenantAsync(RegisterShopRequest req, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Username == req.Mobile, ct))
            throw new BusinessException("This mobile number is already registered. Please login.", 409);

        return await _db.InTransactionAsync(async () =>
        {
        var start = AppClock.Today;
        var tenant = new Tenant
        {
            ShopName = req.ShopName.Trim(), OwnerName = req.OwnerName.Trim(), Mobile = req.Mobile, Village = req.Village,
            Taluka = req.Taluka, District = req.District, Gstin = req.Gstin?.ToUpperInvariant(), IsGstRegistered = !string.IsNullOrWhiteSpace(req.Gstin),
            InvoicePrefix = BuildPrefix(req.ShopName), Plan = SubscriptionPlan.Trial,
            SubscriptionStart = start, SubscriptionEnd = start.AddMonths(FreeTrialMonths),
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        _db.Users.Add(new AppUser { TenantId = tenant.Id, FullName = req.OwnerName.Trim(), Username = req.Mobile, PasswordHash = HashPassword(req.Password), Role = UserRole.Owner });
        if (req.SeedSampleProducts) SeedData.AddStarterCatalogue(_db, tenant.Id);
        await _db.SaveChangesAsync(ct);
        return tenant;
        }, ct);
    }

    private static string BuildPrefix(string shop)
    {
        var letters = new string(shop.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]).Where(char.IsAsciiLetter).Take(3).ToArray()).ToUpperInvariant();
        return letters.Length >= 2 ? letters : "KP";
    }

    public async Task<UserInfo> GetUserInfoAsync(int userId, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Tenant).FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new BusinessException("User not found.", 404);
        return ToInfo(user);
    }

    public static TenantSummary ToSummary(Tenant t)
    {
        var days = (int)Math.Ceiling((t.SubscriptionEnd.Date - AppClock.Today).TotalDays);
        return new TenantSummary(t.Id, t.ShopName, t.Plan.ToString(), t.SubscriptionEnd, Math.Max(days, 0), t.IsActive, days < 0);
    }

    private static UserInfo ToInfo(AppUser u) => new(u.Id, u.FullName, u.Username, u.Role.ToString(), u.Tenant is null ? null : ToSummary(u.Tenant));

    private AuthResponse Issue(AppUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"]!));
        var expires = DateTime.UtcNow.AddHours(double.TryParse(_cfg["Jwt:ExpiryHours"], out var h) ? h : 12);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("full_name", user.FullName),
        };
        if (user.TenantId is int tid) claims.Add(new Claim(TenantClaim, tid.ToString()));

        var token = new JwtSecurityToken(_cfg["Jwt:Issuer"], _cfg["Jwt:Audience"], claims, expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, ToInfo(user));
    }
}
