using KrushiPrime.Api.Infrastructure;
using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Persistence;
using KrushiPrime.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrushiPrime.Api.Controllers;

[ApiController, Route("api/sales"), Authorize(Policy = Policies.ShopUser)]
public class SalesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBillingService _billing;
    public SalesController(AppDbContext db, IBillingService billing) { _db = db; _billing = billing; }

    [HttpGet]
    public async Task<PagedResult<SaleListItem>> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? q, [FromQuery] int? customerId,
        [FromQuery] InvoiceStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var query = _db.SalesInvoices.AsNoTracking().Include(i => i.Items).AsQueryable();
        if (from is not null) query = query.Where(i => i.InvoiceDate >= from.Value.Date);
        if (to is not null) query = query.Where(i => i.InvoiceDate < to.Value.Date.AddDays(1));
        if (customerId is not null) query = query.Where(i => i.CustomerId == customerId);
        if (status is not null) query = query.Where(i => i.Status == status);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(i => EF.Functions.Like(i.InvoiceNo, "%" + q + "%") || EF.Functions.Like(i.CustomerName, "%" + q + "%") || (i.CustomerMobile != null && EF.Functions.Like(i.CustomerMobile, "%" + q + "%")));
        pageSize = Math.Clamp(pageSize, 1, 200);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(i => i.Id).Skip((Math.Max(page, 1) - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<SaleListItem>(rows.Select(ReportService.ToListItem).ToList(), total, page, pageSize);
    }

    [HttpGet("{id:int}")]
    public async Task<object> Get(int id, CancellationToken ct)
    {
        var inv = await _db.SalesInvoices.AsNoTracking().Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new BusinessException("Invoice not found.", 404);
        var shop = await _db.Tenants.AsNoTracking().FirstAsync(t => t.Id == inv.TenantId, ct);
        decimal? outstanding = inv.CustomerId is int cid ? (await _db.Customers.AsNoTracking().FirstAsync(c => c.Id == cid, ct)).Balance : null;
        return new { Invoice = inv, Shop = SettingsController.ToDto(shop), CustomerOutstanding = outstanding };
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateSaleRequest req, CancellationToken ct)
    {
        var inv = await _billing.CreateSaleAsync(req, ct);
        return CreatedAtAction(nameof(Get), new { id = inv.Id }, ReportService.ToListItem(inv));
    }

    [HttpPost("{id:int}/cancel"), Authorize(Policy = Policies.ShopManager)]
    public async Task<SaleListItem> Cancel(int id, CancelSaleRequest req, CancellationToken ct) => ReportService.ToListItem(await _billing.CancelSaleAsync(id, req.Reason, ct));
}

[ApiController, Route("api/purchases"), Authorize(Policy = Policies.ShopManager)]
public class PurchasesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBillingService _billing;
    private readonly IReportService _reports;
    public PurchasesController(AppDbContext db, IBillingService billing, IReportService reports) { _db = db; _billing = billing; _reports = reports; }

    [HttpGet]
    public Task<List<PurchaseListItem>> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => _reports.PurchasesAsync(from ?? AppClock.Today.AddMonths(-3), to ?? AppClock.Today, ct);

    [HttpGet("{id:int}")]
    public async Task<PurchaseInvoice> Get(int id, CancellationToken ct)
        => await _db.PurchaseInvoices.AsNoTracking().Include(p => p.Items).Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new BusinessException("Purchase not found.", 404);

    [HttpPost]
    public async Task<object> Create(CreatePurchaseRequest req, CancellationToken ct)
    {
        var p = await _billing.CreatePurchaseAsync(req, ct);
        return new { p.Id, p.BillNo, p.GrandTotal };
    }
}

[ApiController, Route("api/payments"), Authorize(Policy = Policies.ShopUser)]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBillingService _billing;
    public PaymentsController(AppDbContext db, IBillingService billing) { _db = db; _billing = billing; }

    [HttpGet]
    public Task<List<Payment>> List([FromQuery] PartyType? partyType, [FromQuery] int? partyId, CancellationToken ct)
    {
        var q = _db.Payments.AsNoTracking();
        if (partyType is not null) q = q.Where(p => p.PartyType == partyType);
        if (partyId is not null) q = q.Where(p => p.PartyId == partyId);
        return q.OrderByDescending(p => p.Date).Take(300).ToListAsync(ct);
    }

    /// <summary>Record udhari received from a farmer (or payment to a supplier).</summary>
    [HttpPost]
    public Task<Payment> Create(CreatePaymentRequest req, CancellationToken ct)
    {
        if (req.PartyType == PartyType.Supplier && !(User.IsInRole("Owner") || User.IsInRole("Manager")))
            throw new BusinessException("Only owner/manager can record supplier payments.", 403);
        return _billing.RecordPaymentAsync(req, ct);
    }
}

[ApiController, Route("api/whatsapp"), Authorize(Policy = Policies.ShopUser)]
public class WhatsAppController : ControllerBase
{
    private readonly IWhatsAppService _wa;
    public WhatsAppController(IWhatsAppService wa) => _wa = wa;

    [HttpPost("invoice/{invoiceId:int}")]
    public Task<WhatsAppResult> Invoice(int invoiceId, CancellationToken ct) => _wa.SendInvoiceAsync(invoiceId, ct);

    [HttpPost("reminder/{customerId:int}")]
    public Task<WhatsAppResult> Reminder(int customerId, CancellationToken ct) => _wa.SendUdhariReminderAsync(customerId, ct);
}
