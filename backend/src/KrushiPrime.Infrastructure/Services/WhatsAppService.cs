using System.Net.Http.Json;
using System.Text;
using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KrushiPrime.Infrastructure.Services;

public class WhatsAppOptions
{
    /// <summary>"Link" (free click-to-chat, default) or "CloudApi" (Meta WhatsApp Business Cloud API, auto-send).</summary>
    public string Mode { get; set; } = "Link";
    public string? PhoneNumberId { get; set; }
    public string? AccessToken { get; set; }
    public string ApiVersion { get; set; } = "v21.0";
    public string CountryCode { get; set; } = "91";
}

public interface IWhatsAppService
{
    Task<WhatsAppResult> SendInvoiceAsync(int invoiceId, CancellationToken ct = default);
    Task<WhatsAppResult> SendUdhariReminderAsync(int customerId, CancellationToken ct = default);
}

/// <summary>
/// Sends invoices and udhari reminders on WhatsApp. "Link" mode is 100% free: returns a wa.me link that opens WhatsApp
/// with the message pre-filled. "CloudApi" mode sends automatically through Meta's WhatsApp Cloud API.
/// </summary>
public class WhatsAppService : IWhatsAppService
{
    private readonly AppDbContext _db;
    private readonly HttpClient _http;
    private readonly WhatsAppOptions _opt;
    private readonly ILogger<WhatsAppService> _log;

    public WhatsAppService(AppDbContext db, HttpClient http, IOptions<WhatsAppOptions> opt, ILogger<WhatsAppService> log)
    { _db = db; _http = http; _opt = opt.Value; _log = log; }

    public async Task<WhatsAppResult> SendInvoiceAsync(int invoiceId, CancellationToken ct = default)
    {
        var inv = await _db.SalesInvoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == invoiceId, ct) ?? throw new BusinessException("Invoice not found.", 404);
        var shop = await _db.Tenants.FirstAsync(t => t.Id == inv.TenantId, ct);
        if (string.IsNullOrWhiteSpace(inv.CustomerMobile)) throw new BusinessException("Customer mobile number is not available on this bill.");

        decimal? outstanding = null;
        if (inv.CustomerId is int cid) outstanding = (await _db.Customers.FirstAsync(c => c.Id == cid, ct)).Balance;

        var sb = new StringBuilder();
        sb.AppendLine($"🌾 *{shop.ShopName}*");
        if (!string.IsNullOrWhiteSpace(shop.Village)) sb.AppendLine(shop.Village);
        sb.AppendLine($"{(inv.IsGst ? "Tax Invoice" : "Bill")}: *{inv.InvoiceNo}*  |  {inv.InvoiceDate:dd-MM-yyyy}");
        sb.AppendLine($"ग्राहक: {inv.CustomerName}");
        sb.AppendLine("------------------------------");
        foreach (var i in inv.Items) sb.AppendLine($"• {i.ProductName} × {i.Quantity:0.###} {i.Unit} = ₹{i.Total:N2}");
        sb.AppendLine("------------------------------");
        if (inv.IsGst) sb.AppendLine($"GST: ₹{inv.CgstAmount + inv.SgstAmount + inv.IgstAmount:N2}");
        sb.AppendLine($"*एकूण / Total: ₹{inv.GrandTotal:N0}*");
        sb.AppendLine($"जमा / Paid: ₹{inv.PaidAmount:N0}");
        if (inv.BalanceAmount > 0) sb.AppendLine($"या बिलाची उधारी: ₹{inv.BalanceAmount:N0}");
        if (outstanding is > 0) sb.AppendLine($"एकूण बाकी / Total due: *₹{outstanding:N0}*");
        if (!string.IsNullOrWhiteSpace(shop.UpiId) && outstanding is > 0) sb.AppendLine($"UPI: {shop.UpiId}");
        sb.AppendLine();
        sb.Append(shop.InvoiceFooter ?? "धन्यवाद 🙏");

        return await DispatchAsync(inv.CustomerMobile!, sb.ToString(), "Invoice", ct);
    }

    public async Task<WhatsAppResult> SendUdhariReminderAsync(int customerId, CancellationToken ct = default)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == customerId, ct) ?? throw new BusinessException("Customer not found.", 404);
        if (string.IsNullOrWhiteSpace(c.Mobile)) throw new BusinessException("Customer mobile number is not saved.");
        if (c.Balance <= 0) throw new BusinessException($"{c.Name} has no pending udhari.");
        var shop = await _db.Tenants.FirstAsync(t => t.Id == c.TenantId, ct);

        var msg = new StringBuilder()
            .AppendLine($"नमस्कार {c.Name} जी 🙏")
            .AppendLine()
            .AppendLine($"*{shop.ShopName}* कडे आपली उधारी रक्कम *₹{c.Balance:N0}* बाकी आहे.")
            .AppendLine("कृपया सोयीनुसार लवकरात लवकर भरणा करावा.")
            .AppendLine()
            .AppendLine($"Dear {c.Name}, your pending amount at {shop.ShopName} is ₹{c.Balance:N0}. Kindly clear it at the earliest.")
            .AppendLine(string.IsNullOrWhiteSpace(shop.UpiId) ? "" : $"UPI: {shop.UpiId}")
            .Append($"📞 {shop.Mobile}")
            .ToString();
        return await DispatchAsync(c.Mobile!, msg, "Reminder", ct);
    }

    private async Task<WhatsAppResult> DispatchAsync(string mobile, string message, string purpose, CancellationToken ct)
    {
        var digits = new string(mobile.Where(char.IsDigit).ToArray());
        var to = digits.Length == 10 ? _opt.CountryCode + digits : digits;
        var log = new MessageLog { To = to, Purpose = purpose, Body = message };
        WhatsAppResult result;

        if (string.Equals(_opt.Mode, "CloudApi", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(_opt.AccessToken) && !string.IsNullOrWhiteSpace(_opt.PhoneNumberId))
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"https://graph.facebook.com/{_opt.ApiVersion}/{_opt.PhoneNumberId}/messages")
                {
                    Content = JsonContent.Create(new { messaging_product = "whatsapp", to, type = "text", text = new { preview_url = false, body = message } })
                };
                req.Headers.Authorization = new("Bearer", _opt.AccessToken);
                var resp = await _http.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode) throw new InvalidOperationException(body);
                log.Status = "Sent"; log.ProviderMessageId = body.Length > 390 ? body[..390] : body;
                result = new WhatsAppResult("CloudApi", true, null, message, null);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "WhatsApp Cloud API send failed; falling back to click-to-chat link");
                log.Status = "Failed"; log.Error = ex.Message.Length > 390 ? ex.Message[..390] : ex.Message;
                result = new WhatsAppResult("Link", false, BuildLink(to, message), message, "Auto-send failed, opening WhatsApp instead.");
            }
        }
        else
        {
            log.Status = "Link";
            result = new WhatsAppResult("Link", false, BuildLink(to, message), message, null);
        }

        _db.MessageLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public static string BuildLink(string to, string message) => $"https://wa.me/{to}?text={Uri.EscapeDataString(message)}";
}
