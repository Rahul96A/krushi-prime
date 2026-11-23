using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KrushiPrime.Infrastructure.Services;

public record DayValue(DateTime Date, decimal Sales, int Bills);
public record NameValue(string Name, decimal Quantity, decimal Amount);
public record DashboardDto(decimal TodaySales, int TodayBills, decimal TodayCollection, decimal MonthSales, decimal MonthProfit,
    decimal TotalUdhari, int UdhariCustomers, int LowStockCount, int ExpiringSoonCount, int ExpiredCount, decimal StockValue,
    List<DayValue> Last7Days, List<NameValue> TopProducts, List<NameValue> CategorySales, List<SaleListItem> RecentInvoices);

public record SalesReportDto(DateTime From, DateTime To, int Bills, decimal Taxable, decimal Tax, decimal GrandTotal, decimal Paid, decimal Balance,
    List<NameValue> ByPaymentMode, List<DayValue> Daily, List<SaleListItem> Invoices);

public record GstRateRow(decimal Rate, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal Total);
public record GstHsnRow(string Hsn, string Description, string Uqc, decimal Quantity, decimal Rate, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal Total);
public record GstB2bRow(string InvoiceNo, DateTime Date, string CustomerName, string Gstin, decimal Taxable, decimal Tax, decimal Total);
public record GstReportDto(DateTime From, DateTime To, decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal NonGstSales,
    List<GstRateRow> RateWise, List<GstHsnRow> HsnWise, List<GstB2bRow> B2b, decimal PurchaseTaxCredit);

public record StockRow(int ProductId, string Name, string? LocalName, string Category, string? Company, string Unit, decimal Stock,
    decimal ReorderLevel, decimal PurchaseValue, decimal SaleValue, bool IsLow, DateTime? NearestExpiry);
public record ExpiryRow(int ProductId, string Name, string BatchNo, DateTime ExpiryDate, decimal Quantity, int DaysLeft, decimal Value);
public record UdhariRow(int CustomerId, string Name, string? Mobile, string? Village, decimal Balance, DateTime? LastInvoiceDate, DateTime? LastPaymentDate, int DaysSinceLastPayment);
public record ProfitReportDto(DateTime From, DateTime To, decimal Revenue, decimal Cost, decimal GrossProfit, decimal MarginPercent, List<NameValue> ByProduct);
public record PurchaseListItem(int Id, string BillNo, DateTime BillDate, string SupplierName, decimal TaxAmount, decimal GrandTotal, decimal PaidAmount, int ItemCount);

public interface IReportService
{
    Task<DashboardDto> DashboardAsync(CancellationToken ct = default);
    Task<SalesReportDto> SalesAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<GstReportDto> GstAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<StockRow>> StockAsync(CancellationToken ct = default);
    Task<List<ExpiryRow>> ExpiryAsync(int days, CancellationToken ct = default);
    Task<List<UdhariRow>> UdhariAsync(CancellationToken ct = default);
    Task<ProfitReportDto> ProfitAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<List<PurchaseListItem>> PurchasesAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<LedgerDto> CustomerLedgerAsync(int customerId, CancellationToken ct = default);
    Task<LedgerDto> SupplierLedgerAsync(int supplierId, CancellationToken ct = default);
}

public class ReportService : IReportService
{
    private readonly AppDbContext _db;
    public ReportService(AppDbContext db) => _db = db;

    public static SaleListItem ToListItem(SalesInvoice i) => new(i.Id, i.InvoiceNo, i.InvoiceDate, i.CustomerName, i.CustomerMobile, i.IsGst,
        i.GrandTotal, i.PaidAmount, i.BalanceAmount, i.PaymentMode.ToString(), i.Status.ToString(), i.Items.Count);

    private IQueryable<SalesInvoice> ActiveSales(DateTime from, DateTime to)
        => _db.SalesInvoices.AsNoTracking().Where(i => i.Status != InvoiceStatus.Cancelled && i.InvoiceDate >= from.Date && i.InvoiceDate < to.Date.AddDays(1));

    public async Task<DashboardDto> DashboardAsync(CancellationToken ct = default)
    {
        var today = AppClock.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var from = monthStart < today.AddDays(-6) ? monthStart : today.AddDays(-6);
        var sales = await ActiveSales(from, today).Include(i => i.Items).ToListAsync(ct);

        var todays = sales.Where(s => s.InvoiceDate.Date == today).ToList();
        var month = sales.Where(s => s.InvoiceDate >= monthStart).ToList();
        var todaysPayments = await _db.Payments.AsNoTracking().Where(p => p.PartyType == PartyType.Customer && p.Date >= today).Select(p => p.Amount).ToListAsync(ct);

        var udhari = await _db.Customers.AsNoTracking().Where(c => c.Balance > 0).Select(c => c.Balance).ToListAsync(ct);
        var stock = await StockAsync(ct);
        var batches = await _db.ProductBatches.AsNoTracking().Where(b => b.Quantity > 0 && b.ExpiryDate != null).Select(b => b.ExpiryDate!.Value).ToListAsync(ct);

        var monthItems = month.SelectMany(s => s.Items).ToList();
        var productCats = await _db.Products.AsNoTracking().Select(p => new { p.Id, p.Category }).ToDictionaryAsync(p => p.Id, p => p.Category, ct);

        var recent = await _db.SalesInvoices.AsNoTracking().Include(i => i.Items).OrderByDescending(i => i.Id).Take(6).ToListAsync(ct);

        return new DashboardDto(
            todays.Sum(s => s.GrandTotal), todays.Count, todays.Sum(s => s.PaidAmount) + todaysPayments.Sum(),
            month.Sum(s => s.GrandTotal), TaxCalculator.R2(monthItems.Sum(i => i.TaxableValue - i.CostPrice * i.Quantity)),
            udhari.Sum(), udhari.Count, stock.Count(s => s.IsLow),
            batches.Count(e => e.Date >= today && e.Date <= today.AddDays(30)), batches.Count(e => e.Date < today), TaxCalculator.R2(stock.Sum(s => s.PurchaseValue)),
            Enumerable.Range(0, 7).Select(d => today.AddDays(d - 6)).Select(d => new DayValue(d, sales.Where(s => s.InvoiceDate.Date == d).Sum(s => s.GrandTotal), sales.Count(s => s.InvoiceDate.Date == d))).ToList(),
            monthItems.GroupBy(i => i.ProductName).Select(g => new NameValue(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.Total))).OrderByDescending(x => x.Amount).Take(5).ToList(),
            monthItems.GroupBy(i => productCats.TryGetValue(i.ProductId, out var c) ? c.ToString() : "Other").Select(g => new NameValue(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.Total))).OrderByDescending(x => x.Amount).ToList(),
            recent.Select(ToListItem).ToList());
    }

    public async Task<SalesReportDto> SalesAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var list = await ActiveSales(from, to).Include(i => i.Items).OrderBy(i => i.InvoiceDate).ToListAsync(ct);
        return new SalesReportDto(from.Date, to.Date, list.Count, list.Sum(i => i.SubTotal), list.Sum(i => i.CgstAmount + i.SgstAmount + i.IgstAmount),
            list.Sum(i => i.GrandTotal), list.Sum(i => i.PaidAmount), list.Sum(i => i.BalanceAmount),
            list.GroupBy(i => i.PaymentMode.ToString()).Select(g => new NameValue(g.Key, g.Count(), g.Sum(x => x.GrandTotal))).ToList(),
            list.GroupBy(i => i.InvoiceDate.Date).Select(g => new DayValue(g.Key, g.Sum(x => x.GrandTotal), g.Count())).ToList(),
            list.Select(ToListItem).ToList());
    }

    public async Task<GstReportDto> GstAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var list = await ActiveSales(from, to).Include(i => i.Items).ToListAsync(ct);
        var gst = list.Where(i => i.IsGst).ToList();
        var items = gst.SelectMany(i => i.Items).ToList();

        var purchaseTax = await _db.PurchaseInvoices.AsNoTracking().Where(p => p.BillDate >= from.Date && p.BillDate < to.Date.AddDays(1)).Select(p => p.TaxAmount).ToListAsync(ct);

        return new GstReportDto(from.Date, to.Date, items.Sum(i => i.TaxableValue), items.Sum(i => i.Cgst), items.Sum(i => i.Sgst), items.Sum(i => i.Igst),
            list.Where(i => !i.IsGst).Sum(i => i.GrandTotal),
            items.GroupBy(i => i.GstRate).OrderBy(g => g.Key).Select(g => new GstRateRow(g.Key, g.Sum(x => x.TaxableValue), g.Sum(x => x.Cgst), g.Sum(x => x.Sgst), g.Sum(x => x.Igst), g.Sum(x => x.Total))).ToList(),
            items.GroupBy(i => new { Hsn = i.HsnCode ?? "-", i.GstRate, i.Unit }).Select(g => new GstHsnRow(g.Key.Hsn, g.First().ProductName, UqcFor(g.Key.Unit),
                g.Sum(x => x.Quantity), g.Key.GstRate, g.Sum(x => x.TaxableValue), g.Sum(x => x.Cgst), g.Sum(x => x.Sgst), g.Sum(x => x.Igst), g.Sum(x => x.Total))).OrderBy(r => r.Hsn).ToList(),
            gst.Where(i => !string.IsNullOrWhiteSpace(i.CustomerGstin)).Select(i => new GstB2bRow(i.InvoiceNo, i.InvoiceDate, i.CustomerName, i.CustomerGstin!, i.SubTotal, i.CgstAmount + i.SgstAmount + i.IgstAmount, i.GrandTotal)).ToList(),
            purchaseTax.Sum());
    }

    private static string UqcFor(string unit) => unit.ToUpperInvariant() switch
    {
        "KG" => "KGS", "LTR" or "LITRE" => "LTR", "ML" => "MLT", "BAG" => "BAG", "PKT" or "PACKET" => "PAC", "GM" or "GRAM" => "GMS", _ => "NOS"
    };

    public async Task<List<StockRow>> StockAsync(CancellationToken ct = default)
    {
        var products = await _db.Products.AsNoTracking().Include(p => p.Batches).Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync(ct);
        return products.Select(p =>
        {
            var qty = p.Batches.Sum(b => b.Quantity);
            var pv = p.Batches.Where(b => b.Quantity > 0).Sum(b => b.Quantity * (b.PurchasePrice > 0 ? b.PurchasePrice : p.PurchasePrice));
            var exp = p.Batches.Where(b => b.Quantity > 0 && b.ExpiryDate != null).Min(b => b.ExpiryDate);
            return new StockRow(p.Id, p.Name, p.LocalName, p.Category.ToString(), p.Company, p.Unit, qty, p.ReorderLevel, TaxCalculator.R2(pv), TaxCalculator.R2(qty * p.SalePrice), qty <= p.ReorderLevel, exp);
        }).ToList();
    }

    public async Task<List<ExpiryRow>> ExpiryAsync(int days, CancellationToken ct = default)
    {
        var limit = AppClock.Today.AddDays(days);
        var rows = await _db.ProductBatches.AsNoTracking().Include(b => b.Product)
            .Where(b => b.Quantity > 0 && b.ExpiryDate != null && b.ExpiryDate <= limit).ToListAsync(ct);
        return rows.OrderBy(b => b.ExpiryDate).Select(b => new ExpiryRow(b.ProductId, b.Product!.Name, b.BatchNo, b.ExpiryDate!.Value, b.Quantity,
            (int)(b.ExpiryDate!.Value.Date - AppClock.Today).TotalDays, TaxCalculator.R2(b.Quantity * b.PurchasePrice))).ToList();
    }

    public async Task<List<UdhariRow>> UdhariAsync(CancellationToken ct = default)
    {
        var customers = await _db.Customers.AsNoTracking().Where(c => c.Balance > 0).ToListAsync(ct);
        var ids = customers.Select(c => c.Id).ToList();
        var lastPay = (await _db.Payments.AsNoTracking().Where(p => p.PartyType == PartyType.Customer && ids.Contains(p.PartyId)).Select(p => new { p.PartyId, p.Date }).ToListAsync(ct))
            .GroupBy(p => p.PartyId).ToDictionary(g => g.Key, g => g.Max(x => x.Date));
        var lastInv = (await _db.SalesInvoices.AsNoTracking().Where(i => i.CustomerId != null && ids.Contains(i.CustomerId.Value) && i.Status != InvoiceStatus.Cancelled).Select(i => new { i.CustomerId, i.InvoiceDate }).ToListAsync(ct))
            .GroupBy(i => i.CustomerId!.Value).ToDictionary(g => g.Key, g => g.Max(x => x.InvoiceDate));

        return customers.Select(c =>
        {
            DateTime? lp = lastPay.TryGetValue(c.Id, out var p) ? p : null;
            DateTime? li = lastInv.TryGetValue(c.Id, out var i) ? i : null;
            var since = (lp ?? li ?? c.CreatedAt).Date;
            return new UdhariRow(c.Id, c.Name, c.Mobile, c.Village, c.Balance, li, lp, (int)(AppClock.Today - since).TotalDays);
        }).OrderByDescending(r => r.Balance).ToList();
    }

    public async Task<ProfitReportDto> ProfitAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var items = (await ActiveSales(from, to).Include(i => i.Items).ToListAsync(ct)).SelectMany(i => i.Items).ToList();
        var revenue = TaxCalculator.R2(items.Sum(i => i.TaxableValue));
        var cost = TaxCalculator.R2(items.Sum(i => i.CostPrice * i.Quantity));
        return new ProfitReportDto(from.Date, to.Date, revenue, cost, revenue - cost, revenue == 0 ? 0 : Math.Round((revenue - cost) * 100 / revenue, 2),
            items.GroupBy(i => i.ProductName).Select(g => new NameValue(g.Key, g.Sum(x => x.Quantity), TaxCalculator.R2(g.Sum(x => x.TaxableValue - x.CostPrice * x.Quantity)))).OrderByDescending(x => x.Amount).ToList());
    }

    public async Task<List<PurchaseListItem>> PurchasesAsync(DateTime from, DateTime to, CancellationToken ct = default)
    {
        var list = await _db.PurchaseInvoices.AsNoTracking().Include(p => p.Supplier).Include(p => p.Items)
            .Where(p => p.BillDate >= from.Date && p.BillDate < to.Date.AddDays(1)).OrderByDescending(p => p.BillDate).ToListAsync(ct);
        return list.Select(p => new PurchaseListItem(p.Id, p.BillNo, p.BillDate, p.Supplier?.Name ?? "", p.TaxAmount, p.GrandTotal, p.PaidAmount, p.Items.Count)).ToList();
    }

    public async Task<LedgerDto> CustomerLedgerAsync(int customerId, CancellationToken ct = default)
    {
        var c = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, ct) ?? throw new BusinessException("Customer not found.", 404);
        var invoices = await _db.SalesInvoices.AsNoTracking().Where(i => i.CustomerId == customerId && i.Status != InvoiceStatus.Cancelled).ToListAsync(ct);
        var payments = await _db.Payments.AsNoTracking().Where(p => p.PartyType == PartyType.Customer && p.PartyId == customerId).ToListAsync(ct);

        var raw = invoices.SelectMany(i => new[]
            {
                (i.InvoiceDate, "Sale", i.InvoiceNo, i.GrandTotal, 0m),
                (i.InvoiceDate, "Paid at billing", i.InvoiceNo, 0m, i.PaidAmount),
            }).Where(x => x.Item4 != 0 || x.Item5 != 0)
            .Concat(payments.Select(p => (p.Date, $"Payment ({p.Mode})", p.Reference ?? "", 0m, p.Amount)))
            .OrderBy(x => x.Item1).ToList();
        return BuildLedger(c.Id, c.Name, c.Mobile, c.OpeningBalance, raw);
    }

    public async Task<LedgerDto> SupplierLedgerAsync(int supplierId, CancellationToken ct = default)
    {
        var s = await _db.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId, ct) ?? throw new BusinessException("Supplier not found.", 404);
        var bills = await _db.PurchaseInvoices.AsNoTracking().Where(p => p.SupplierId == supplierId).ToListAsync(ct);
        var payments = await _db.Payments.AsNoTracking().Where(p => p.PartyType == PartyType.Supplier && p.PartyId == supplierId).ToListAsync(ct);
        var raw = bills.SelectMany(b => new[]
            {
                (b.BillDate, "Purchase", b.BillNo, b.GrandTotal, 0m),
                (b.BillDate, "Paid on bill", b.BillNo, 0m, b.PaidAmount),
            }).Where(x => x.Item4 != 0 || x.Item5 != 0)
            .Concat(payments.Select(p => (p.Date, $"Payment ({p.Mode})", p.Reference ?? "", 0m, p.Amount)))
            .OrderBy(x => x.Item1).ToList();
        return BuildLedger(s.Id, s.Name, s.Mobile, 0, raw);
    }

    private static LedgerDto BuildLedger(int id, string name, string? mobile, decimal opening, List<(DateTime, string, string, decimal, decimal)> raw)
    {
        var bal = opening;
        var entries = new List<LedgerEntry>();
        foreach (var (date, type, reference, debit, credit) in raw)
        {
            bal += debit - credit;
            entries.Add(new LedgerEntry(date, type, reference, debit, credit, bal));
        }
        return new LedgerDto(id, name, mobile, opening, bal, entries);
    }
}
