using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KrushiPrime.Domain;
using Microsoft.Extensions.Configuration;

namespace KrushiPrime.Infrastructure.Payments;

/// <summary>Encrypts gateway secrets at rest (AES-256-GCM, key derived from configuration).</summary>
public class SecretProtector
{
    private readonly byte[] _key;
    public SecretProtector(IConfiguration cfg)
        => _key = SHA256.HashData(Encoding.UTF8.GetBytes(cfg["Payments:EncryptionKey"] ?? cfg["Jwt:Key"] ?? throw new InvalidOperationException("Payments:EncryptionKey missing")));

    public string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        var nonce = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(plain);
        var cipher = new byte[data.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, data, cipher, tag);
        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray());
    }

    public string? Unprotect(string? enc)
    {
        if (string.IsNullOrEmpty(enc)) return null;
        var all = Convert.FromBase64String(enc);
        var plain = new byte[all.Length - 28];
        using var aes = new AesGcm(_key, 16);
        aes.Decrypt(all[..12], all[28..], all[12..28], plain);
        return Encoding.UTF8.GetString(plain);
    }

    public static string Mask(string? value) => string.IsNullOrEmpty(value) ? "" : value.Length <= 4 ? "••••" : $"••••{value[^4..]}";
}

/// <summary>
/// NTT DATA Payment Services (Atom) AIPAY crypto: AES-256-CBC, key = PBKDF2-HMAC-SHA512(key, salt=key, 65536 iter),
/// fixed IV 0..15, upper-case hex. Byte-compatible with NDPS' official PHP/Java kits.
/// </summary>
public static class AtomAes
{
    private static readonly byte[] Iv = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

    private static Aes Create(string key)
    {
        var aes = Aes.Create();
        aes.Key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(key), 65536, HashAlgorithmName.SHA512, 32);
        aes.IV = Iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }

    public static string Encrypt(string plain, string key)
    {
        using var aes = Create(key);
        return Convert.ToHexString(aes.EncryptCbc(Encoding.UTF8.GetBytes(plain), Iv));
    }

    public static string Decrypt(string hex, string key)
    {
        using var aes = Create(key);
        return Encoding.UTF8.GetString(aes.DecryptCbc(Convert.FromHexString(hex), Iv));
    }
}

public record GatewayCredentials(PaymentProvider Provider, bool Live, string? CashfreeAppId = null, string? CashfreeSecret = null,
    string? NttMerchId = null, string? NttPassword = null, string? NttProductId = null, string? NttRequestKey = null, string? NttResponseKey = null, string? UpiId = null, string? PayeeName = null);

public record GatewayCreateResult(string ProviderRef, string? Url, string? Token, DateTime? ExpiresAt);
public record GatewayStatusResult(PaymentLinkStatus Status, decimal AmountPaid, string? TxnId, string? Message);

public interface IPaymentGateway
{
    PaymentProvider Provider { get; }
    Task<GatewayCreateResult> CreateAsync(GatewayCredentials c, PaymentLink link, string publicBaseUrl, CancellationToken ct);
    Task<GatewayStatusResult?> FetchStatusAsync(GatewayCredentials c, PaymentLink link, CancellationToken ct);
}

/// <summary>Built-in simulator: lets you demo the full online-payment flow without a merchant account.</summary>
public class SimulatorGateway : IPaymentGateway
{
    public PaymentProvider Provider => PaymentProvider.Simulator;
    public Task<GatewayCreateResult> CreateAsync(GatewayCredentials c, PaymentLink link, string baseUrl, CancellationToken ct)
        => Task.FromResult(new GatewayCreateResult($"SIM{link.PublicId[..12].ToUpperInvariant()}", null, null, AppClock.Now.AddDays(30)));
    public Task<GatewayStatusResult?> FetchStatusAsync(GatewayCredentials c, PaymentLink link, CancellationToken ct) => Task.FromResult<GatewayStatusResult?>(null);
}

/// <summary>
/// Direct UPI (any UPI app: PhonePe, GPay, Paytm, BHIM). Free and instant: money goes straight to the shop's own UPI ID via a
/// standard upi://pay intent / QR. There is no gateway callback, so the shop confirms receipt with "Mark received".
/// </summary>
public class UpiDirectGateway : IPaymentGateway
{
    public PaymentProvider Provider => PaymentProvider.UpiDirect;

    public static string Intent(string vpa, string payee, decimal amount, string note)
        => $"upi://pay?pa={vpa.Trim()}&pn={Uri.EscapeDataString(payee)}&am={amount.ToString("0.00", CultureInfo.InvariantCulture)}&cu=INR&tn={Uri.EscapeDataString(note.Length > 50 ? note[..50] : note)}";

    public Task<GatewayCreateResult> CreateAsync(GatewayCredentials c, PaymentLink link, string baseUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(c.UpiId) || !c.UpiId.Contains('@'))
            throw new BusinessException("Set the shop's UPI ID (e.g. 98xxxxxxxx@ybl) in Settings → Billing preferences first.");
        var reference = $"KP{link.PublicId[..8].ToUpperInvariant()}";
        return Task.FromResult(new GatewayCreateResult(reference, Intent(c.UpiId.Trim(), c.PayeeName ?? "Krushi Seva Kendra", link.Amount, $"{reference} {link.PayerName}"), null, AppClock.Now.AddDays(30)));
    }

    public Task<GatewayStatusResult?> FetchStatusAsync(GatewayCredentials c, PaymentLink link, CancellationToken ct) => Task.FromResult<GatewayStatusResult?>(null);
}

/// <summary>Cashfree Payments – Payment Links API (UPI, cards, net-banking, wallets). Webhook: PAYMENT_LINK_EVENT.</summary>
public class CashfreeGateway : IPaymentGateway
{
    public const string ApiVersion = "2025-01-01";
    private readonly HttpClient _http;
    public CashfreeGateway(HttpClient http) => _http = http;
    public PaymentProvider Provider => PaymentProvider.Cashfree;

    private static string Base(bool live) => live ? "https://api.cashfree.com/pg" : "https://sandbox.cashfree.com/pg";

    private HttpRequestMessage Req(HttpMethod m, GatewayCredentials c, string path, object? body = null)
    {
        if (string.IsNullOrWhiteSpace(c.CashfreeAppId) || string.IsNullOrWhiteSpace(c.CashfreeSecret))
            throw new BusinessException("Cashfree App ID and Secret Key are not configured (Settings → Online payments).");
        var r = new HttpRequestMessage(m, Base(c.Live) + path);
        r.Headers.Add("x-client-id", c.CashfreeAppId);
        r.Headers.Add("x-client-secret", c.CashfreeSecret);
        r.Headers.Add("x-api-version", ApiVersion);
        r.Headers.Add("x-request-id", Guid.NewGuid().ToString("N"));
        if (body is not null) r.Content = JsonContent.Create(body);
        return r;
    }

    public async Task<GatewayCreateResult> CreateAsync(GatewayCredentials c, PaymentLink link, string baseUrl, CancellationToken ct)
    {
        var phone = new string((link.PayerMobile ?? "").Where(char.IsDigit).ToArray());
        if (phone.Length > 10) phone = phone[^10..];
        if (phone.Length != 10) throw new BusinessException("A 10-digit mobile number is required to create a Cashfree payment link.");

        var linkId = "KP" + link.PublicId[..30].ToUpperInvariant();
        var expiry = AppClock.Now.AddDays(link.Purpose == PaymentLinkPurpose.Subscription ? 2 : 30);
        var body = new
        {
            link_id = linkId,
            link_amount = link.Amount,
            link_currency = "INR",
            link_purpose = link.Description.Length > 450 ? link.Description[..450] : link.Description,
            customer_details = new { customer_name = link.PayerName, customer_phone = phone },
            link_partial_payments = false,
            link_expiry_time = expiry.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture) + "+05:30",
            link_notify = new { send_sms = false, send_email = false },
            link_auto_reminders = false,
            link_notes = new Dictionary<string, string> { ["kp_public_id"] = link.PublicId, ["purpose"] = link.Purpose.ToString() },
            link_meta = new { return_url = $"{baseUrl}/pay/{link.PublicId}/done", notify_url = $"{baseUrl}/api/webhooks/cashfree", upi_intent = false },
        };
        using var resp = await _http.SendAsync(Req(HttpMethod.Post, c, "/links", body), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new BusinessException($"Cashfree: {ErrorMessage(json)}", 502);
        using var doc = JsonDocument.Parse(json);
        return new GatewayCreateResult(linkId, doc.RootElement.GetProperty("link_url").GetString(), null, expiry);
    }

    public async Task<GatewayStatusResult?> FetchStatusAsync(GatewayCredentials c, PaymentLink link, CancellationToken ct)
    {
        using var resp = await _http.SendAsync(Req(HttpMethod.Get, c, $"/links/{link.ProviderRef}"), ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode) throw new BusinessException($"Cashfree: {ErrorMessage(json)}", 502);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var status = root.TryGetProperty("link_status", out var s) ? s.GetString() : null;
        var paid = root.TryGetProperty("link_amount_paid", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDecimal() : 0;
        return new GatewayStatusResult(MapStatus(status), paid, root.TryGetProperty("cf_link_id", out var id) ? id.ToString() : null, status);
    }

    public static PaymentLinkStatus MapStatus(string? s) => s?.ToUpperInvariant() switch
    {
        "PAID" => PaymentLinkStatus.Paid,
        "PARTIALLY_PAID" => PaymentLinkStatus.PartiallyPaid,
        "EXPIRED" => PaymentLinkStatus.Expired,
        "CANCELLED" => PaymentLinkStatus.Cancelled,
        _ => PaymentLinkStatus.Created,
    };

    /// <summary>x-webhook-signature = Base64(HMAC-SHA256(secret, x-webhook-timestamp + rawBody)).</summary>
    public static bool VerifySignature(string rawBody, string? timestamp, string? signature, string secret)
    {
        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature)) return false;
        var expected = Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + rawBody)));
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }

    private static string ErrorMessage(string json)
    {
        try { using var d = JsonDocument.Parse(json); return d.RootElement.TryGetProperty("message", out var m) ? m.GetString() ?? json : json; }
        catch { return json.Length > 200 ? json[..200] : json; }
    }
}

/// <summary>NTT DATA Payment Services India (formerly Atom) – AIPAY: server-side AUTH → atomTokenId → hosted checkout (atomcheckout.js).</summary>
public class NttDataGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    public NttDataGateway(HttpClient http, IConfiguration cfg) { _http = http; _cfg = cfg; }
    public PaymentProvider Provider => PaymentProvider.NttData;

    public string AuthUrl(bool live) => live
        ? _cfg["Payments:NttData:AuthUrlLive"] ?? "https://payment1.atomtech.in/ots/aipay/auth"
        : _cfg["Payments:NttData:AuthUrlUat"] ?? "https://paynetzuat.atomtech.in/ots/aipay/auth";

    public static string CheckoutScript(bool live) => live
        ? "https://psa.atomtech.in/staticdata/ots/js/atomcheckout.js"
        : "https://pgtest.atomtech.in/staticdata/ots/js/atomcheckout.js";

    public async Task<GatewayCreateResult> CreateAsync(GatewayCredentials c, PaymentLink link, string baseUrl, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(c.NttMerchId) || string.IsNullOrWhiteSpace(c.NttPassword) || string.IsNullOrWhiteSpace(c.NttRequestKey) || string.IsNullOrWhiteSpace(c.NttResponseKey))
            throw new BusinessException("NTT DATA Merchant ID, password and request/response keys are not configured (Settings → Online payments).");

        var txnId = $"KP{link.PublicId[..10].ToUpperInvariant()}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"; // unique per checkout attempt
        var mobile = new string((link.PayerMobile ?? "").Where(char.IsDigit).ToArray());
        var payload = new
        {
            payInstrument = new
            {
                headDetails = new { version = "OTSv1.1", api = "AUTH", platform = "FLASH" },
                merchDetails = new { merchId = c.NttMerchId, userId = "", password = c.NttPassword, merchTxnId = txnId, merchTxnDate = AppClock.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) },
                payDetails = new { amount = link.Amount.ToString("0.00", CultureInfo.InvariantCulture), product = string.IsNullOrWhiteSpace(c.NttProductId) ? "NSE" : c.NttProductId, custAccNo = link.CustomerId?.ToString() ?? "0", txnCurrency = "INR" },
                custDetails = new { custEmail = "noreply@krushiprime.in", custMobile = mobile.Length >= 10 ? mobile[^10..] : "9999999999" },
                extras = new { udf1 = link.PublicId, udf2 = link.Purpose.ToString(), udf3 = "", udf4 = "", udf5 = "" },
            }
        };
        var enc = AtomAes.Encrypt(JsonSerializer.Serialize(payload), c.NttRequestKey);
        using var resp = await _http.PostAsync(AuthUrl(c.Live), new FormUrlEncodedContent(new Dictionary<string, string> { ["encData"] = enc, ["merchId"] = c.NttMerchId }), ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        var encResp = text.Split('&').Select(p => p.Split('=', 2)).FirstOrDefault(p => p[0] == "encData")?.ElementAtOrDefault(1);
        if (encResp is null) throw new BusinessException($"NTT DATA auth failed: {(text.Length > 200 ? text[..200] : text)}", 502);

        using var doc = JsonDocument.Parse(AtomAes.Decrypt(encResp, c.NttResponseKey));
        var root = doc.RootElement;
        var code = root.TryGetProperty("responseDetails", out var rd) && rd.TryGetProperty("txnStatusCode", out var sc) ? sc.GetString() : null;
        if (code != "OTS0000" || !root.TryGetProperty("atomTokenId", out var token))
            throw new BusinessException($"NTT DATA auth failed ({code}): {(rd.ValueKind == JsonValueKind.Object && rd.TryGetProperty("txnMessage", out var msg) ? msg.GetString() : "unknown error")}", 502);
        return new GatewayCreateResult(txnId, null, token.ToString(), AppClock.Now.AddMinutes(30));
    }

    // NTT DATA reports the result by POSTing encData to the return URL (handled in OnlinePaymentService).
    public Task<GatewayStatusResult?> FetchStatusAsync(GatewayCredentials c, PaymentLink link, CancellationToken ct) => Task.FromResult<GatewayStatusResult?>(null);

    /// <summary>Decrypts and interprets the encData posted back by AIPAY checkout.</summary>
    public static (string? MerchTxnId, PaymentLinkStatus Status, decimal Amount, string? AtomTxnId, string? Message) ParseResponse(string encData, string responseKey)
    {
        using var doc = JsonDocument.Parse(AtomAes.Decrypt(encData, responseKey));
        var pi = doc.RootElement.TryGetProperty("payInstrument", out var p) ? p : doc.RootElement;
        string? Get(string obj, string prop) => pi.TryGetProperty(obj, out var o) && o.ValueKind == JsonValueKind.Object && o.TryGetProperty(prop, out var v) ? v.ToString() : null;
        var code = Get("responseDetails", "statusCode");
        var amountText = Get("payDetails", "totalAmount") ?? Get("payDetails", "amount");
        decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);
        var status = code == "OTS0000" ? PaymentLinkStatus.Paid : PaymentLinkStatus.Failed;
        return (Get("merchDetails", "merchTxnId"), status, amount, Get("payDetails", "atomTxnId"), Get("responseDetails", "description") ?? Get("responseDetails", "message") ?? code);
    }
}
