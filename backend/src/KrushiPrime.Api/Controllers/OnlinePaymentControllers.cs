using System.Net;
using System.Text;
using KrushiPrime.Api.Infrastructure;
using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Payments;
using KrushiPrime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KrushiPrime.Api.Controllers;

internal static class UrlHelper
{
    /// <summary>Public base URL used inside payment links and gateway callbacks.</summary>
    public static string BaseUrl(this ControllerBase c, IConfiguration cfg)
        => (string.IsNullOrWhiteSpace(cfg["App:PublicBaseUrl"]) ? $"{c.Request.Scheme}://{c.Request.Host}" : cfg["App:PublicBaseUrl"]!).TrimEnd('/');
}

[ApiController, Route("api/online-payments"), Authorize(Policy = Policies.ShopUser)]
public class OnlinePaymentsController : ControllerBase
{
    private readonly IOnlinePaymentService _svc;
    private readonly IConfiguration _cfg;
    public OnlinePaymentsController(IOnlinePaymentService svc, IConfiguration cfg) { _svc = svc; _cfg = cfg; }

    [HttpGet] public Task<List<PaymentLinkDto>> List(CancellationToken ct) => _svc.ListAsync(this.BaseUrl(_cfg), ct);

    /// <summary>Create a "pay udhari online" link for a farmer (share on WhatsApp).</summary>
    [HttpPost("udhari/{customerId:int}")]
    public Task<PaymentLinkDto> Udhari(int customerId, CreateUdhariLinkRequest req, CancellationToken ct) => _svc.CreateUdhariLinkAsync(customerId, req.Amount, this.BaseUrl(_cfg), ct);

    /// <summary>Create a pay link for the unpaid balance of a bill.</summary>
    [HttpPost("invoice/{invoiceId:int}")]
    public Task<PaymentLinkDto> Invoice(int invoiceId, CancellationToken ct) => _svc.CreateInvoiceLinkAsync(invoiceId, this.BaseUrl(_cfg), ct);

    [HttpPost("{id:int}/refresh")]
    public Task<PaymentLinkDto> Refresh(int id, CancellationToken ct) => _svc.RefreshAsync(id, this.BaseUrl(_cfg), ct);

    public record MarkReceivedRequest(string? Reference);

    /// <summary>Direct UPI: confirm the money arrived in the shop's UPI app.</summary>
    [HttpPost("{id:int}/mark-received"), Authorize(Policy = Policies.ShopManager)]
    public Task<PaymentLinkDto> MarkReceived(int id, MarkReceivedRequest req, CancellationToken ct) => _svc.MarkReceivedAsync(id, req.Reference, this.BaseUrl(_cfg), ct);

    [HttpPost("{publicId}/simulate"), Authorize(Policy = Policies.ShopManager)]
    public Task<PaymentLinkDto> Simulate(string publicId, CancellationToken ct) => _svc.SimulatePaidAsync(publicId, this.BaseUrl(_cfg), ct);
}

[ApiController, Route("api/settings/payments"), Authorize(Policy = Policies.ShopOwner)]
public class PaymentSettingsController : ControllerBase
{
    private readonly IOnlinePaymentService _svc;
    private readonly IConfiguration _cfg;
    public PaymentSettingsController(IOnlinePaymentService svc, IConfiguration cfg) { _svc = svc; _cfg = cfg; }

    [HttpGet] public async Task<PaymentSettingsDto> Get(CancellationToken ct) => (await _svc.GetSettingsAsync(ct)) with { WebhookUrl = $"{this.BaseUrl(_cfg)}/api/webhooks/cashfree" };
    [HttpPut] public async Task<PaymentSettingsDto> Save(PaymentSettingsDto dto, CancellationToken ct) => (await _svc.SaveSettingsAsync(dto, ct)) with { WebhookUrl = $"{this.BaseUrl(_cfg)}/api/webhooks/cashfree" };
}

/// <summary>Shop owner renews Krushi Prime online (allowed even after expiry).</summary>
[ApiController, Route("api/subscription"), Authorize(Policy = Policies.ShopOwner)]
public class SubscriptionController : ControllerBase
{
    private readonly IOnlinePaymentService _svc;
    private readonly IConfiguration _cfg;
    public SubscriptionController(IOnlinePaymentService svc, IConfiguration cfg) { _svc = svc; _cfg = cfg; }

    [HttpGet("plans")] public List<PlanOption> Plans() => _svc.Plans();
    [HttpPost("checkout")] public Task<PaymentLinkDto> Checkout(CheckoutRequest req, CancellationToken ct) => _svc.CreateSubscriptionCheckoutAsync(req.Plan, this.BaseUrl(_cfg), ct);
}

[ApiController, Route("api/webhooks"), AllowAnonymous]
public class WebhooksController : ControllerBase
{
    private readonly IOnlinePaymentService _svc;
    public WebhooksController(IOnlinePaymentService svc) => _svc = svc;

    /// <summary>Cashfree PAYMENT_LINK_EVENT webhook (configure in Cashfree dashboard → Developers → Webhooks).</summary>
    [HttpPost("cashfree")]
    public async Task<IActionResult> Cashfree(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var raw = await reader.ReadToEndAsync(ct);
        var ok = await _svc.HandleCashfreeWebhookAsync(raw, Request.Headers["x-webhook-timestamp"], Request.Headers["x-webhook-signature"], ct);
        return ok ? Ok() : Unauthorized();
    }
}

/// <summary>Public, mobile-friendly "Pay bill" page that farmers open from WhatsApp. Works for every provider.</summary>
[ApiController, Route("pay"), AllowAnonymous, EnableRateLimiting("public")]
public class PayPageController : ControllerBase
{
    private readonly IOnlinePaymentService _svc;
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly IMemoryCache _cache;
    public PayPageController(IOnlinePaymentService svc, AppDbContext db, IConfiguration cfg, IMemoryCache cache) { _svc = svc; _db = db; _cfg = cfg; _cache = cache; }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private async Task<string> ShopNameAsync(PaymentLink l, CancellationToken ct)
        => l.Purpose == PaymentLinkPurpose.Subscription ? "Krushi Prime" : (await _db.Tenants.AsNoTracking().Where(t => t.Id == l.TenantId).Select(t => t.ShopName).FirstAsync(ct));

    private ContentResult Page(string title, string body, string? head = null) => Content($$"""
        <!DOCTYPE html><html lang="mr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <title>{{E(title)}}</title>{{head}}
        <style>
          body{margin:0;font:16px/1.5 system-ui,-apple-system,"Segoe UI",sans-serif;background:#f0fdf4;color:#14532d;display:flex;min-height:100vh;align-items:center;justify-content:center}
          .card{background:#fff;max-width:420px;width:calc(100% - 32px);border-radius:20px;box-shadow:0 10px 30px rgba(20,83,45,.15);overflow:hidden}
          .top{background:#15803d;color:#fff;padding:20px 24px}.top small{opacity:.85}.top h1{margin:4px 0 0;font-size:20px}
          .body{padding:24px}.amt{font-size:40px;font-weight:800;margin:4px 0 12px}.muted{color:#57534e;font-size:14px}
          .btn{display:block;width:100%;box-sizing:border-box;text-align:center;border:0;border-radius:12px;padding:15px;font-size:17px;font-weight:700;cursor:pointer;text-decoration:none;margin-top:12px}
          .pay{background:#facc15;color:#14532d}.ghost{background:#f5f5f4;color:#44403c}.ok{color:#15803d}.bad{color:#b91c1c}
          .foot{text-align:center;font-size:12px;color:#78716c;padding:0 24px 18px}
        </style></head><body><div class="card">{{body}}<div class="foot">🔒 Secured payment · Powered by Krushi Prime</div></div></body></html>
        """, "text/html; charset=utf-8");

    private string Header(string shop, PaymentLink l) => $"<div class='top'><small>{E(l.Purpose == PaymentLinkPurpose.Subscription ? "Subscription renewal" : "Pay bill online")}</small><h1>{E(shop)}</h1></div>";

    [HttpGet("{publicId}")]
    public async Task<IActionResult> Show(string publicId, CancellationToken ct)
    {
        var l = await _svc.FindPublicAsync(publicId, ct);
        if (l is null) return Page("Not found", "<div class='body'><h2>Link not found</h2><p class='muted'>Please ask the shop for a new payment link.</p></div>");
        var shop = await ShopNameAsync(l, ct);
        var info = $"<p class='muted'>{E(l.PayerName)}<br>{E(l.Description)}</p><div class='amt'>₹{l.Amount:N2}</div>";

        if (l.Status == PaymentLinkStatus.Paid)
            return Page("Paid", $"{Header(shop, l)}<div class='body'>{info}<h2 class='ok'>✅ Paid – धन्यवाद!</h2><p class='muted'>Ref: {E(l.ProviderTxnId)} · {l.PaidAt:dd-MM-yyyy HH:mm}</p></div>");
        if (l.Status is PaymentLinkStatus.Expired or PaymentLinkStatus.Cancelled || (l.ExpiresAt < AppClock.Now && l.Provider != PaymentProvider.NttData))
            return Page("Expired", $"{Header(shop, l)}<div class='body'>{info}<h2 class='bad'>This link has expired</h2><p class='muted'>Please ask the shop for a new link.</p></div>");

        switch (l.Provider)
        {
            case PaymentProvider.Cashfree when !string.IsNullOrEmpty(l.ProviderUrl):
                return Page("Pay", $"{Header(shop, l)}<div class='body'>{info}<a class='btn pay' href='{E(l.ProviderUrl)}'>Pay ₹{l.Amount:N0} · UPI / Card / NetBanking</a><a class='btn ghost' href='/pay/{E(l.PublicId)}/done'>I have paid – check status</a></div>");

            case PaymentProvider.NttData:
            {
                var creds = await _svc.CredentialsForAsync(l, ct);
                if (string.IsNullOrEmpty(l.ProviderToken) || l.ExpiresAt < AppClock.Now)
                {
                    // atomTokenId is short-lived: mint a fresh one for each checkout attempt
                    var gw = HttpContext.RequestServices.GetServices<IPaymentGateway>().First(g => g.Provider == PaymentProvider.NttData);
                    var r = await gw.CreateAsync(creds, l, this.BaseUrl(_cfg), ct);
                    var tracked = await _db.PaymentLinks.IgnoreQueryFilters().FirstAsync(x => x.Id == l.Id, ct);
                    tracked.ProviderRef = r.ProviderRef; tracked.ProviderToken = r.Token; tracked.ExpiresAt = r.ExpiresAt;
                    await _db.SaveChangesAsync(ct);
                    l = tracked;
                }
                var env = creds.Live ? "prod" : "uat";
                var script = $$"""
                    <script src="{{NttDataGateway.CheckoutScript(creds.Live)}}"></script>
                    <script>function openPay(){ new AtomPaynetz({ atomTokenId: "{{E(l.ProviderToken)}}", merchId: "{{E(creds.NttMerchId)}}", custEmail: "noreply@krushiprime.in", custMobile: "{{E(l.PayerMobile)}}", returnUrl: "{{this.BaseUrl(_cfg)}}/pay/{{E(l.PublicId)}}/ntt-return" }, "{{env}}"); }</script>
                    """;
                return Page("Pay", $"{Header(shop, l)}<div class='body'>{info}<button class='btn pay' onclick='openPay()'>Pay ₹{l.Amount:N0} · UPI / Card / NetBanking</button>{(l.LastMessage is { } m && m.StartsWith("Last attempt") ? $"<p class='bad'>{E(m)}</p>" : "")}</div>", script);
            }

            case PaymentProvider.UpiDirect when !string.IsNullOrEmpty(l.ProviderUrl):
            {
                var upi = l.ProviderUrl!;
                var vpa = Uri.UnescapeDataString(upi.Split("pa=")[1].Split('&')[0]);
                var qrScript = $$"""
                    <script src="https://cdnjs.cloudflare.com/ajax/libs/qrcodejs/1.0.0/qrcode.min.js"></script>
                    <script>window.addEventListener('load',function(){ new QRCode(document.getElementById('qr'), { text: "{{upi}}", width: 200, height: 200 }); });</script>
                    """;
                return Page("Pay by UPI", $"{Header(shop, l)}<div class='body'>{info}<a class='btn pay' href='{E(upi)}'>Pay ₹{l.Amount:N0} with PhonePe / GPay / Paytm</a>" +
                    $"<div id='qr' style='display:flex;justify-content:center;margin:18px 0'></div><p class='muted' style='text-align:center'>Or scan with any UPI app<br>UPI ID: <b>{E(vpa)}</b></p>" +
                    "<p class='muted'>After paying, show the success screen to the shop. Your udhari is updated once the shop confirms receipt.</p></div>", qrScript);
            }

            case PaymentProvider.Simulator:
                return Page("Pay (demo)", $"{Header(shop, l)}<div class='body'>{info}<form method='post' action='/pay/{E(l.PublicId)}/simulate'><button class='btn pay'>Pay ₹{l.Amount:N0} (demo – no real money)</button></form><p class='muted'>Demo mode: the shop has not connected a payment gateway yet.</p></div>");
        }
        return Page("Unavailable", "<div class='body'><h2>Payment unavailable</h2></div>");
    }

    /// <summary>Cashfree return_url – re-checks status with Cashfree before showing the result.</summary>
    [HttpGet("{publicId}/done")]
    public async Task<IActionResult> Done(string publicId, CancellationToken ct)
    {
        try { await _svc.RefreshPublicAsync(publicId, ct); } catch (BusinessException) { /* show current state */ }
        return Redirect($"/pay/{publicId}");
    }

    /// <summary>NTT DATA AIPAY posts encData here after checkout.</summary>
    [HttpPost("{publicId}/ntt-return"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> NttReturn(string publicId, [FromForm] string encData, CancellationToken ct)
    {
        try { await _svc.HandleNttReturnAsync(publicId, encData, ct); }
        catch (BusinessException ex) { return Page("Error", $"<div class='body'><h2 class='bad'>{E(ex.Message)}</h2></div>"); }
        return Redirect($"/pay/{publicId}");
    }

    [HttpPost("{publicId}/simulate")]
    public async Task<IActionResult> Simulate(string publicId, CancellationToken ct)
    {
        try { await _svc.SimulatePaidAsync(publicId, this.BaseUrl(_cfg), ct); }
        catch (BusinessException ex) { return Page("Error", $"<div class='body'><h2 class='bad'>{E(ex.Message)}</h2></div>"); }
        return Redirect($"/pay/{publicId}");
    }
}
