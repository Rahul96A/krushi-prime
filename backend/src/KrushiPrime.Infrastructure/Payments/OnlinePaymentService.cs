using System.Text.Json;
using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Persistence;
using KrushiPrime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace KrushiPrime.Infrastructure.Payments;

public record PaymentLinkDto(int Id, string PublicId, string Purpose, string Provider, bool LiveMode, string PayerName, string? PayerMobile, string Description,
    decimal Amount, decimal AmountPaid, string Status, string PayUrl, string? WhatsAppUrl, DateTime CreatedAt, DateTime? PaidAt, DateTime? ExpiresAt, string? LastMessage, int? CustomerId, int? SalesInvoiceId, string? UpiUri = null);

public record PaymentSettingsDto(PaymentProvider Provider, bool LiveMode, string? CashfreeAppId, string? CashfreeSecret, string? NttMerchId, string? NttPassword,
    string? NttProductId, string? NttRequestKey, string? NttResponseKey, string? WebhookUrl = null);

public record PlanOption(string Plan, int Months, decimal Price, string Label);
public record CreateUdhariLinkRequest(decimal? Amount);
public record CheckoutRequest(SubscriptionPlan Plan);

public interface IOnlinePaymentService
{
    Task<PaymentLinkDto> CreateUdhariLinkAsync(int customerId, decimal? amount, string baseUrl, CancellationToken ct = default);
    Task<PaymentLinkDto> CreateInvoiceLinkAsync(int invoiceId, string baseUrl, CancellationToken ct = default);
    Task<PaymentLinkDto> CreateSubscriptionCheckoutAsync(SubscriptionPlan plan, string baseUrl, CancellationToken ct = default);
    Task<List<PaymentLinkDto>> ListAsync(string baseUrl, CancellationToken ct = default);
    Task<PaymentLinkDto> RefreshAsync(int id, string baseUrl, CancellationToken ct = default);
    Task<PaymentLinkDto> SimulatePaidAsync(string publicId, string baseUrl, CancellationToken ct = default);
    Task<PaymentLinkDto> MarkReceivedAsync(int id, string? reference, string baseUrl, CancellationToken ct = default);
    Task<PaymentLink?> FindPublicAsync(string publicId, CancellationToken ct = default);
    Task<PaymentLink> RefreshPublicAsync(string publicId, CancellationToken ct = default);
    Task<bool> HandleCashfreeWebhookAsync(string rawBody, string? timestamp, string? signature, CancellationToken ct = default);
    Task<PaymentLink> HandleNttReturnAsync(string publicId, string encData, CancellationToken ct = default);
    Task<GatewayCredentials> CredentialsForAsync(PaymentLink link, CancellationToken ct = default);
    Task<PaymentSettingsDto> GetSettingsAsync(CancellationToken ct = default);
    Task<PaymentSettingsDto> SaveSettingsAsync(PaymentSettingsDto dto, CancellationToken ct = default);
    List<PlanOption> Plans();
}

/// <summary>
/// "Pay bill" online: creates payment links for farmers' udhari / invoice balances (paid into the shop's own Cashfree or NTT DATA
/// merchant account) and for Krushi Prime subscription renewals (paid to the platform account). Confirmed payments are posted
/// automatically to the farmer ledger (idempotent), or extend the shop's plan.
/// </summary>
public class OnlinePaymentService : IOnlinePaymentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IEnumerable<IPaymentGateway> _gateways;
    private readonly SecretProtector _protector;
    private readonly IConfiguration _cfg;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly ILogger<OnlinePaymentService> _log;

    public OnlinePaymentService(AppDbContext db, ICurrentUser user, IEnumerable<IPaymentGateway> gateways, SecretProtector protector, IConfiguration cfg,
        DbContextOptions<AppDbContext> options, ILogger<OnlinePaymentService> log)
    { _db = db; _user = user; _gateways = gateways; _protector = protector; _cfg = cfg; _options = options; _log = log; }

    private IPaymentGateway Gateway(PaymentProvider p) => _gateways.First(g => g.Provider == p);
    private bool SimulatorAllowed => _cfg.GetValue("Payments:AllowSimulator", true);

    public static string PayUrl(string baseUrl, PaymentLink l) => $"{baseUrl}/pay/{l.PublicId}";

    private PaymentLinkDto Map(PaymentLink l, string baseUrl, string? shopName = null)
    {
        string? wa = null;
        var digits = new string((l.PayerMobile ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length >= 10 && l.Purpose != PaymentLinkPurpose.Subscription && l.Status == PaymentLinkStatus.Created)
        {
            var msg = $"नमस्कार {l.PayerName} जी 🙏\n{shopName ?? "आपल्या कृषी सेवा केंद्रा"}कडील {l.Description}: *₹{l.Amount:N0}*\n\nUPI / कार्ड / नेट बँकिंगने ऑनलाइन भरा 👇\n{PayUrl(baseUrl, l)}\n\nPay securely online. धन्यवाद!";
            wa = WhatsAppService.BuildLink(digits.Length == 10 ? "91" + digits : digits, msg);
        }
        return new PaymentLinkDto(l.Id, l.PublicId, l.Purpose.ToString(), l.Provider.ToString(), l.LiveMode, l.PayerName, l.PayerMobile, l.Description, l.Amount, l.AmountPaid,
            l.Status.ToString(), PayUrl(baseUrl, l), wa, l.CreatedAt, l.PaidAt, l.ExpiresAt, l.LastMessage, l.CustomerId, l.SalesInvoiceId, l.Provider == PaymentProvider.UpiDirect ? l.ProviderUrl : null);
    }

    // ───────────────────────── credentials ─────────────────────────
    private GatewayCredentials FromTenant(Tenant t) => new(t.PaymentProvider, t.PaymentLiveMode, t.CashfreeAppId, _protector.Unprotect(t.CashfreeSecretEnc),
        t.NttMerchId, _protector.Unprotect(t.NttPasswordEnc), t.NttProductId, _protector.Unprotect(t.NttRequestKeyEnc), _protector.Unprotect(t.NttResponseKeyEnc), t.UpiId, t.ShopName);

    /// <summary>Krushi Prime's own merchant account (for subscription renewals), from configuration / Key Vault.</summary>
    private GatewayCredentials Platform()
    {
        var s = _cfg.GetSection("Payments:Platform");
        var provider = Enum.TryParse<PaymentProvider>(s["Provider"], true, out var p) ? p : PaymentProvider.Simulator;
        return new GatewayCredentials(provider, s.GetValue("Live", false), s["CashfreeAppId"], s["CashfreeSecret"], s["NttMerchId"], s["NttPassword"], s["NttProductId"], s["NttRequestKey"], s["NttResponseKey"]);
    }

    public async Task<GatewayCredentials> CredentialsForAsync(PaymentLink link, CancellationToken ct = default)
    {
        if (link.Purpose == PaymentLinkPurpose.Subscription) return Platform();
        var t = await _db.Tenants.AsNoTracking().FirstAsync(x => x.Id == link.TenantId, ct);
        return FromTenant(t);
    }

    private async Task<(Tenant Shop, GatewayCredentials Creds)> ShopCredentialsAsync(CancellationToken ct)
    {
        var t = await _db.Tenants.FirstAsync(x => x.Id == _user.TenantId, ct);
        if (t.PaymentProvider == PaymentProvider.None)
            throw new BusinessException("Online payments are not set up. Owner: open Settings → Online payments and choose Cashfree, NTT DATA or Demo simulator.");
        if (t.PaymentProvider == PaymentProvider.Simulator && !SimulatorAllowed)
            throw new BusinessException("Demo simulator is disabled on this server. Configure Cashfree or NTT DATA.");
        if (t.PaymentProvider == PaymentProvider.UpiDirect && string.IsNullOrWhiteSpace(t.UpiId))
            throw new BusinessException("Set the shop's UPI ID in Settings → Billing preferences to use Direct UPI.");
        return (t, FromTenant(t));
    }

    // ───────────────────────── create links ─────────────────────────
    private async Task<PaymentLinkDto> CreateAsync(PaymentLink link, GatewayCredentials creds, string baseUrl, string? shopName, CancellationToken ct)
    {
        link.Provider = creds.Provider;
        link.LiveMode = creds.Live && creds.Provider != PaymentProvider.Simulator;
        link.Status = PaymentLinkStatus.Created;
        var r = await Gateway(creds.Provider).CreateAsync(creds, link, baseUrl, ct);
        link.ProviderRef = r.ProviderRef;
        link.ProviderUrl = r.Url;
        link.ProviderToken = r.Token;
        link.ExpiresAt = r.ExpiresAt;
        _db.PaymentLinks.Add(link);
        await _db.SaveChangesAsync(ct);
        return Map(link, baseUrl, shopName);
    }

    public async Task<PaymentLinkDto> CreateUdhariLinkAsync(int customerId, decimal? amount, string baseUrl, CancellationToken ct = default)
    {
        var (shop, creds) = await ShopCredentialsAsync(ct);
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == customerId, ct) ?? throw new BusinessException("Customer not found.", 404);
        var amt = Math.Round(amount ?? c.Balance, 2);
        if (amt <= 0) throw new BusinessException($"{c.Name} has no pending udhari.");
        if (amt > c.Balance) throw new BusinessException($"Amount is more than the outstanding ₹{c.Balance:N0}.");
        return await CreateAsync(new PaymentLink
        {
            Purpose = PaymentLinkPurpose.Udhari, CustomerId = c.Id, PayerName = c.Name, PayerMobile = c.Mobile, Amount = amt,
            Description = $"उधारी भरणा / Udhari payment – {shop.ShopName}",
        }, creds, baseUrl, shop.ShopName, ct);
    }

    public async Task<PaymentLinkDto> CreateInvoiceLinkAsync(int invoiceId, string baseUrl, CancellationToken ct = default)
    {
        var (shop, creds) = await ShopCredentialsAsync(ct);
        var inv = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Id == invoiceId, ct) ?? throw new BusinessException("Invoice not found.", 404);
        if (inv.Status == InvoiceStatus.Cancelled) throw new BusinessException("Invoice is cancelled.");
        if (inv.BalanceAmount <= 0) throw new BusinessException("This bill is already fully paid.");
        return await CreateAsync(new PaymentLink
        {
            Purpose = PaymentLinkPurpose.Invoice, CustomerId = inv.CustomerId, SalesInvoiceId = inv.Id, PayerName = inv.CustomerName,
            PayerMobile = inv.CustomerMobile, Amount = inv.BalanceAmount, Description = $"Bill {inv.InvoiceNo} – {shop.ShopName}",
        }, creds, baseUrl, shop.ShopName, ct);
    }

    public List<PlanOption> Plans()
    {
        var plans = _cfg.GetSection("Payments:Plans").GetChildren()
            .Select(s => new PlanOption(s.Key, s.GetValue("Months", 12), s.GetValue("Price", 0m), s["Label"] ?? s.Key)).Where(p => p.Price > 0).ToList();
        return plans.Count > 0 ? plans : new()
        {
            new("Basic", 12, 2999, "Basic – 1 year (billing, stock, udhari, WhatsApp)"),
            new("Pro", 12, 4999, "Pro – 1 year (+ online payments, multi-user, priority support)"),
        };
    }

    public async Task<PaymentLinkDto> CreateSubscriptionCheckoutAsync(SubscriptionPlan plan, string baseUrl, CancellationToken ct = default)
    {
        var option = Plans().FirstOrDefault(p => p.Plan.Equals(plan.ToString(), StringComparison.OrdinalIgnoreCase)) ?? throw new BusinessException("This plan is not available for online purchase.");
        var shop = await _db.Tenants.FirstAsync(x => x.Id == _user.TenantId, ct);
        var creds = Platform();
        if (creds.Provider == PaymentProvider.Simulator && !SimulatorAllowed) throw new BusinessException("Online renewal is not configured yet. Please call 8668375712.");
        return await CreateAsync(new PaymentLink
        {
            Purpose = PaymentLinkPurpose.Subscription, Plan = plan, PlanMonths = option.Months, PayerName = shop.OwnerName, PayerMobile = shop.Mobile,
            Amount = option.Price, Description = $"Krushi Prime {option.Label} – {shop.ShopName}",
        }, creds, baseUrl, null, ct);
    }

    public async Task<List<PaymentLinkDto>> ListAsync(string baseUrl, CancellationToken ct = default)
    {
        var shop = await _db.Tenants.AsNoTracking().FirstAsync(x => x.Id == _user.TenantId, ct);
        var rows = await _db.PaymentLinks.AsNoTracking().OrderByDescending(x => x.Id).Take(300).ToListAsync(ct);
        return rows.Select(r => Map(r, baseUrl, shop.ShopName)).ToList();
    }

    // ───────────────────────── status / confirmation ─────────────────────────
    public async Task<PaymentLinkDto> RefreshAsync(int id, string baseUrl, CancellationToken ct = default)
    {
        var link = await _db.PaymentLinks.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Payment link not found.", 404);
        await PollAsync(link, ct);
        return Map(link, baseUrl);
    }

    public Task<PaymentLink?> FindPublicAsync(string publicId, CancellationToken ct = default)
        => _db.PaymentLinks.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.PublicId == publicId, ct);

    public async Task<PaymentLink> RefreshPublicAsync(string publicId, CancellationToken ct = default)
    {
        var link = await FindPublicAsync(publicId, ct) ?? throw new BusinessException("Payment link not found.", 404);
        await PollAsync(link, ct);
        return link;
    }

    private async Task PollAsync(PaymentLink link, CancellationToken ct)
    {
        if (link.Status is PaymentLinkStatus.Paid or PaymentLinkStatus.Cancelled) return;
        var s = await Gateway(link.Provider).FetchStatusAsync(await CredentialsForAsync(link, ct), link, ct);
        if (s is null) return;
        await ApplyAsync(link, s, ct);
    }

    public async Task<PaymentLinkDto> SimulatePaidAsync(string publicId, string baseUrl, CancellationToken ct = default)
    {
        var link = await FindPublicAsync(publicId, ct) ?? throw new BusinessException("Payment link not found.", 404);
        if (link.Provider != PaymentProvider.Simulator || !SimulatorAllowed) throw new BusinessException("Only demo-simulator links can be simulated.");
        await ApplyAsync(link, new GatewayStatusResult(PaymentLinkStatus.Paid, link.Amount, $"SIMTXN{Random.Shared.Next(100000, 999999)}", "Demo payment (simulator)"), ct);
        return Map(link, baseUrl);
    }

    /// <summary>Shop confirms a Direct-UPI payment it can see in its UPI app (PhonePe / GPay business notification).</summary>
    public async Task<PaymentLinkDto> MarkReceivedAsync(int id, string? reference, string baseUrl, CancellationToken ct = default)
    {
        var link = await _db.PaymentLinks.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Payment link not found.", 404);
        if (link.Provider != PaymentProvider.UpiDirect) throw new BusinessException("Gateway payments are confirmed automatically.");
        await ApplyAsync(link, new GatewayStatusResult(PaymentLinkStatus.Paid, link.Amount, string.IsNullOrWhiteSpace(reference) ? link.ProviderRef : reference.Trim(), "Received on UPI (confirmed by shop)"), ct);
        return Map(link, baseUrl);
    }

    public async Task<bool> HandleCashfreeWebhookAsync(string rawBody, string? timestamp, string? signature, CancellationToken ct = default)
    {
        string? linkId = null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("link_id", out var l)) linkId = l.GetString();
                else if (data.TryGetProperty("order", out var o) && o.TryGetProperty("order_tags", out var tags) && tags.TryGetProperty("link_id", out var lt)) linkId = lt.GetString();
            }
        }
        catch (JsonException) { return false; }
        if (string.IsNullOrEmpty(linkId)) return true; // not a payment-link event; acknowledge

        var link = await _db.PaymentLinks.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.ProviderRef == linkId && x.Provider == PaymentProvider.Cashfree, ct);
        if (link is null) { _log.LogWarning("Cashfree webhook for unknown link {LinkId}", linkId); return true; }

        var creds = await CredentialsForAsync(link, ct);
        if (!CashfreeGateway.VerifySignature(rawBody, timestamp, signature, creds.CashfreeSecret ?? ""))
        {
            _log.LogWarning("Rejected Cashfree webhook with invalid signature for {LinkId}", linkId);
            return false;
        }
        // Never trust the payload amount: re-fetch the authoritative status from Cashfree.
        var status = await Gateway(PaymentProvider.Cashfree).FetchStatusAsync(creds, link, ct);
        if (status is not null) await ApplyAsync(link, status, ct);
        return true;
    }

    public async Task<PaymentLink> HandleNttReturnAsync(string publicId, string encData, CancellationToken ct = default)
    {
        var link = await FindPublicAsync(publicId, ct) ?? throw new BusinessException("Payment link not found.", 404);
        if (link.Provider != PaymentProvider.NttData) throw new BusinessException("Invalid payment response.");
        var creds = await CredentialsForAsync(link, ct);
        (string? MerchTxnId, PaymentLinkStatus Status, decimal Amount, string? AtomTxnId, string? Message) r;
        try { r = NttDataGateway.ParseResponse(encData, creds.NttResponseKey ?? ""); }
        catch (Exception ex) { _log.LogWarning(ex, "NTT DATA response could not be decrypted for {PublicId}", publicId); throw new BusinessException("Invalid payment response."); }
        if (r.MerchTxnId != link.ProviderRef) throw new BusinessException("Payment response does not match this bill.");

        if (r.Status == PaymentLinkStatus.Paid)
            await ApplyAsync(link, new GatewayStatusResult(PaymentLinkStatus.Paid, r.Amount > 0 ? Math.Min(r.Amount, link.Amount) : link.Amount, r.AtomTxnId, r.Message), ct);
        else
        {
            link.LastMessage = $"Last attempt failed: {r.Message}";
            link.ProviderToken = null; // a new token is generated on the next attempt
            await _db.SaveChangesAsync(ct);
        }
        return link;
    }

    /// <summary>Idempotently applies a confirmed gateway status: posts money to the ledger / extends subscription exactly once.</summary>
    private async Task ApplyAsync(PaymentLink link, GatewayStatusResult s, CancellationToken ct)
    {
        link.LastMessage = s.Message;
        var newlyPaid = s.Status is PaymentLinkStatus.Paid or PaymentLinkStatus.PartiallyPaid ? Math.Max(0, Math.Min(s.AmountPaid, link.Amount) - link.AmountPaid) : 0;
        if (s.Status != PaymentLinkStatus.Created) link.Status = s.Status;
        if (newlyPaid <= 0) { await _db.SaveChangesAsync(ct); return; }

        link.AmountPaid += newlyPaid;
        link.ProviderTxnId = s.TxnId ?? link.ProviderTxnId;
        link.PaidAt = AppClock.Now;
        try { await _db.SaveChangesAsync(ct); } // persist first: a retry/duplicate webhook sees AmountPaid and never double-posts
        catch (DbUpdateConcurrencyException) { _log.LogInformation("Payment {Link} already applied by a concurrent notification", link.PublicId); return; }

        // Post in the shop's tenant context (webhooks are anonymous)
        var sys = new SystemUser { TenantId = link.TenantId, UserName = $"online:{link.Provider}", Role = UserRole.Owner };
        await using var db = new AppDbContext(_options, sys);
        if (link.Purpose == PaymentLinkPurpose.Subscription)
        {
            var t = await db.Tenants.FirstAsync(x => x.Id == link.TenantId, ct);
            t.Plan = link.Plan ?? SubscriptionPlan.Basic;
            t.SubscriptionEnd = (t.SubscriptionEnd < AppClock.Today ? AppClock.Today : t.SubscriptionEnd).AddMonths(link.PlanMonths ?? 12);
            t.IsActive = true;
            t.Notes = $"{t.Notes}\n{AppClock.Now:dd-MM-yyyy} renewed online ₹{newlyPaid:N0} ({link.Provider} {link.ProviderTxnId})".Trim();
            await db.SaveChangesAsync(ct);
            return;
        }

        if (link.CustomerId is int cid)
        {
            var billing = new BillingService(db, sys);
            var payment = await billing.RecordPaymentAsync(new CreatePaymentRequest(PartyType.Customer, cid, newlyPaid, PaymentMode.Upi,
                link.ProviderTxnId ?? link.ProviderRef, $"Online payment via {link.Provider} ({link.Purpose})", null), ct);
            if (link.SalesInvoiceId is int iid)
            {
                var inv = await db.SalesInvoices.FirstAsync(x => x.Id == iid, ct);
                inv.PaidAmount += newlyPaid;
                inv.BalanceAmount = Math.Max(0, inv.BalanceAmount - newlyPaid);
                inv.Status = inv.BalanceAmount == 0 ? InvoiceStatus.Paid : InvoiceStatus.Partial;
                payment.SalesInvoiceId = iid;
                await db.SaveChangesAsync(ct);
            }
            var tracked = await _db.PaymentLinks.IgnoreQueryFilters().FirstAsync(x => x.Id == link.Id, ct);
            tracked.RecordedPaymentId = payment.Id;
            await _db.SaveChangesAsync(ct);
        }
    }

    // ───────────────────────── settings ─────────────────────────
    public async Task<PaymentSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var t = await _db.Tenants.AsNoTracking().FirstAsync(x => x.Id == _user.TenantId, ct);
        return new PaymentSettingsDto(t.PaymentProvider, t.PaymentLiveMode, t.CashfreeAppId, SecretProtector.Mask(_protector.Unprotect(t.CashfreeSecretEnc)),
            t.NttMerchId, SecretProtector.Mask(_protector.Unprotect(t.NttPasswordEnc)), t.NttProductId,
            SecretProtector.Mask(_protector.Unprotect(t.NttRequestKeyEnc)), SecretProtector.Mask(_protector.Unprotect(t.NttResponseKeyEnc)));
    }

    public async Task<PaymentSettingsDto> SaveSettingsAsync(PaymentSettingsDto d, CancellationToken ct = default)
    {
        var t = await _db.Tenants.FirstAsync(x => x.Id == _user.TenantId, ct);
        if (d.Provider == PaymentProvider.Simulator && !SimulatorAllowed) throw new BusinessException("Demo simulator is disabled on this server.");
        if (d.Provider == PaymentProvider.UpiDirect && string.IsNullOrWhiteSpace(t.UpiId)) throw new BusinessException("Save your UPI ID in Billing preferences first, then choose Direct UPI.");
        string? Keep(string? incoming, string? current) => string.IsNullOrWhiteSpace(incoming) || incoming.StartsWith("••••") ? current : _protector.Protect(incoming.Trim());
        t.PaymentProvider = d.Provider;
        t.PaymentLiveMode = d.LiveMode;
        t.CashfreeAppId = d.CashfreeAppId?.Trim();
        t.CashfreeSecretEnc = Keep(d.CashfreeSecret, t.CashfreeSecretEnc);
        t.NttMerchId = d.NttMerchId?.Trim();
        t.NttProductId = d.NttProductId?.Trim();
        t.NttPasswordEnc = Keep(d.NttPassword, t.NttPasswordEnc);
        t.NttRequestKeyEnc = Keep(d.NttRequestKey, t.NttRequestKeyEnc);
        t.NttResponseKeyEnc = Keep(d.NttResponseKey, t.NttResponseKeyEnc);
        await _db.SaveChangesAsync(ct);
        return await GetSettingsAsync(ct);
    }
}
