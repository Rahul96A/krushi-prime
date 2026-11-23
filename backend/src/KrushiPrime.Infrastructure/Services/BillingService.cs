using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KrushiPrime.Infrastructure.Services;

public interface IBillingService
{
    Task<SalesInvoice> CreateSaleAsync(CreateSaleRequest req, CancellationToken ct = default);
    Task<SalesInvoice> CancelSaleAsync(int id, string reason, CancellationToken ct = default);
    Task<PurchaseInvoice> CreatePurchaseAsync(CreatePurchaseRequest req, CancellationToken ct = default);
    Task<Payment> RecordPaymentAsync(CreatePaymentRequest req, CancellationToken ct = default);
    Task AdjustStockAsync(StockAdjustRequest req, CancellationToken ct = default);
}

/// <summary>
/// The transactional heart of Krushi Prime: GST billing with FEFO batch allocation (first-expiry-first-out),
/// udhari (credit) posting, purchases/inward stock, payments and stock adjustments. All operations are atomic.
/// </summary>
public class BillingService : IBillingService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public BillingService(AppDbContext db, ICurrentUser user) { _db = db; _user = user; }

    private async Task<Tenant> CurrentTenantAsync(CancellationToken ct)
        => await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _user.TenantId, ct) ?? throw new BusinessException("Shop not found.", 404);

    // ─────────────────────────── SALES ───────────────────────────
    public Task<SalesInvoice> CreateSaleAsync(CreateSaleRequest req, CancellationToken ct = default) => _db.InTransactionAsync(async () =>
    {
        if (req.Items is not { Count: > 0 }) throw new BusinessException("Add at least one item to the bill.");

        var tenant = await CurrentTenantAsync(ct);
        var isGst = req.IsGst && tenant.IsGstRegistered;

        Customer? customer = null;
        if (req.CustomerId is int cid)
            customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == cid, ct) ?? throw new BusinessException("Customer not found.", 404);

        var productIds = req.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Include(p => p.Batches).Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        var date = req.InvoiceDate?.Date.Add(AppClock.Now.TimeOfDay) ?? AppClock.Now;
        var invoice = new SalesInvoice
        {
            InvoiceDate = date,
            CustomerId = customer?.Id,
            CustomerName = customer?.Name ?? (string.IsNullOrWhiteSpace(req.CustomerName) ? "Cash Customer" : req.CustomerName.Trim()),
            CustomerMobile = customer?.Mobile ?? req.CustomerMobile,
            CustomerVillage = customer?.Village ?? req.CustomerVillage,
            CustomerGstin = customer?.Gstin,
            IsGst = isGst,
            IsInterState = isGst && req.IsInterState,
            PaymentMode = req.PaymentMode,
            Notes = req.Notes,
        };

        foreach (var line in req.Items)
        {
            if (!products.TryGetValue(line.ProductId, out var product)) throw new BusinessException($"Product #{line.ProductId} not found.", 404);
            foreach (var (batch, qty) in AllocateBatches(product, line, tenant.AllowNegativeStock))
            {
                var share = line.Quantity == 0 ? 0 : qty / line.Quantity;
                var discount = TaxCalculator.R2(line.Discount * share);
                var tax = TaxCalculator.Compute(qty * line.Rate - discount, product.GstRate, tenant.PricesIncludeTax, isGst, invoice.IsInterState);

                batch.Quantity -= qty;
                invoice.Items.Add(new SalesInvoiceItem
                {
                    ProductId = product.Id, BatchId = batch.Id == 0 ? null : batch.Id, ProductName = product.Name, HsnCode = product.HsnCode,
                    BatchNo = batch.BatchNo, ExpiryDate = batch.ExpiryDate, Unit = product.Unit, Quantity = qty, Rate = line.Rate,
                    DiscountAmount = discount, TaxableValue = tax.Taxable, GstRate = isGst ? product.GstRate : 0,
                    Cgst = tax.Cgst, Sgst = tax.Sgst, Igst = tax.Igst, Total = tax.Total,
                    CostPrice = batch.PurchasePrice > 0 ? batch.PurchasePrice : product.PurchasePrice,
                });
                _db.StockMovements.Add(new StockMovement { ProductId = product.Id, BatchId = batch.Id == 0 ? null : batch.Id, Date = date, Type = StockMovementType.Sale, Quantity = -qty });
            }
        }

        invoice.SubTotal = invoice.Items.Sum(i => i.TaxableValue);
        invoice.Discount = invoice.Items.Sum(i => i.DiscountAmount);
        invoice.CgstAmount = invoice.Items.Sum(i => i.Cgst);
        invoice.SgstAmount = invoice.Items.Sum(i => i.Sgst);
        invoice.IgstAmount = invoice.Items.Sum(i => i.Igst);
        (invoice.GrandTotal, invoice.RoundOff) = TaxCalculator.RoundInvoice(invoice.Items.Sum(i => i.Total));

        var paid = Math.Clamp(req.PaymentMode == PaymentMode.Credit ? 0 : req.PaidAmount, 0, invoice.GrandTotal);
        invoice.PaidAmount = paid;
        invoice.BalanceAmount = invoice.GrandTotal - paid;
        invoice.Status = invoice.BalanceAmount == 0 ? InvoiceStatus.Paid : paid == 0 ? InvoiceStatus.Unpaid : InvoiceStatus.Partial;

        if (invoice.BalanceAmount > 0)
        {
            if (customer is null) throw new BusinessException("उधारी बिलासाठी ग्राहक निवडा. Select a registered customer for a credit (udhari) bill.");
            if (customer.CreditLimit > 0 && customer.Balance + invoice.BalanceAmount > customer.CreditLimit)
                throw new BusinessException($"Credit limit exceeded for {customer.Name}. Outstanding ₹{customer.Balance:N0}, limit ₹{customer.CreditLimit:N0}.");
            customer.Balance += invoice.BalanceAmount;
        }

        // Invoice numbering: PREFIX/FY/0001, guarded by optimistic concurrency on Tenant.NextInvoiceNo
        invoice.InvoiceNo = $"{tenant.InvoicePrefix}/{AppClock.FinancialYear(date)}/{tenant.NextInvoiceNo:D4}";
        tenant.NextInvoiceNo++;
        foreach (var m in _db.ChangeTracker.Entries<StockMovement>().Where(e => e.State == EntityState.Added)) m.Entity.Reference = invoice.InvoiceNo;

        _db.SalesInvoices.Add(invoice);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new BusinessException("Another bill was saved at the same moment. Please press Save again.", 409);
        }
        return invoice;
    }, ct);

    /// <summary>FEFO: picks non-expired batches with the earliest expiry first; splits a line across batches if needed.</summary>
    internal static List<(ProductBatch Batch, decimal Qty)> AllocateBatches(Product product, SaleLineRequest line, bool allowNegative)
    {
        var result = new List<(ProductBatch, decimal)>();
        if (line.BatchId is int bid)
        {
            var batch = product.Batches.FirstOrDefault(b => b.Id == bid) ?? throw new BusinessException($"Batch not found for {product.Name}.");
            if (batch.ExpiryDate is DateTime ex && ex.Date < AppClock.Today)
                throw new BusinessException($"Batch {batch.BatchNo} of {product.Name} expired on {ex:dd-MM-yyyy}. Expired stock cannot be sold.");
            if (batch.Quantity < line.Quantity && !allowNegative)
                throw new BusinessException($"Only {batch.Quantity:0.###} {product.Unit} available in batch {batch.BatchNo} of {product.Name}.");
            result.Add((batch, line.Quantity));
            return result;
        }

        var remaining = line.Quantity;
        var candidates = product.Batches
            .Where(b => b.Quantity > 0 && (b.ExpiryDate == null || b.ExpiryDate.Value.Date >= AppClock.Today))
            .OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue).ThenBy(b => b.Id);

        foreach (var b in candidates)
        {
            if (remaining <= 0) break;
            var take = Math.Min(b.Quantity, remaining);
            result.Add((b, take));
            remaining -= take;
        }

        if (remaining > 0)
        {
            if (!allowNegative)
            {
                var available = line.Quantity - remaining;
                throw new BusinessException($"Insufficient stock for {product.Name}. Available: {available:0.###} {product.Unit}, required: {line.Quantity:0.###}.");
            }
            var fallback = product.Batches.OrderByDescending(b => b.Id).FirstOrDefault();
            if (fallback is null)
            {
                fallback = new ProductBatch { TenantId = product.TenantId, BatchNo = "NA", PurchasePrice = product.PurchasePrice, SalePrice = product.SalePrice, Mrp = product.Mrp };
                product.Batches.Add(fallback);
            }
            var idx = result.FindIndex(r => r.Item1 == fallback);
            if (idx >= 0) result[idx] = (fallback, result[idx].Item2 + remaining); else result.Add((fallback, remaining));
        }
        return result;
    }

    public Task<SalesInvoice> CancelSaleAsync(int id, string reason, CancellationToken ct = default) => _db.InTransactionAsync(async () =>
    {
        var inv = await _db.SalesInvoices.Include(i => i.Items).FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new BusinessException("Invoice not found.", 404);
        if (inv.Status == InvoiceStatus.Cancelled) throw new BusinessException("Invoice is already cancelled.");

        var batchIds = inv.Items.Where(i => i.BatchId != null).Select(i => i.BatchId!.Value).ToList();
        var batches = await _db.ProductBatches.Where(b => batchIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
        foreach (var item in inv.Items)
        {
            if (item.BatchId is int bid && batches.TryGetValue(bid, out var b)) b.Quantity += item.Quantity;
            _db.StockMovements.Add(new StockMovement { ProductId = item.ProductId, BatchId = item.BatchId, Date = AppClock.Now, Type = StockMovementType.SaleCancel, Quantity = item.Quantity, Reference = inv.InvoiceNo, Note = reason });
        }

        if (inv.CustomerId is int cid && inv.BalanceAmount != 0)
        {
            var c = await _db.Customers.FirstAsync(x => x.Id == cid, ct);
            c.Balance -= inv.BalanceAmount;
        }

        inv.Status = InvoiceStatus.Cancelled;
        inv.CancelledAt = AppClock.Now;
        inv.CancelReason = reason;
        await _db.SaveChangesAsync(ct);
        return inv;
    }, ct);

    // ─────────────────────────── PURCHASES ───────────────────────────
    public Task<PurchaseInvoice> CreatePurchaseAsync(CreatePurchaseRequest req, CancellationToken ct = default) => _db.InTransactionAsync(async () =>
    {
        if (req.Items is not { Count: > 0 }) throw new BusinessException("Add at least one item.");

        var supplier = await _db.Suppliers.FirstOrDefaultAsync(s => s.Id == req.SupplierId, ct) ?? throw new BusinessException("Supplier not found.", 404);
        var ids = req.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Include(p => p.Batches).Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var date = req.BillDate ?? AppClock.Now;

        var pi = new PurchaseInvoice
        {
            SupplierId = supplier.Id, BillNo = req.BillNo.Trim(), BillDate = date, IsInterState = req.IsInterState,
            OtherCharges = req.OtherCharges, PaymentMode = req.PaymentMode, Notes = req.Notes,
        };

        var pendingMovements = new List<(StockMovement Move, ProductBatch Batch)>();
        foreach (var l in req.Items)
        {
            if (!products.TryGetValue(l.ProductId, out var p)) throw new BusinessException($"Product #{l.ProductId} not found.", 404);
            var batchNo = string.IsNullOrWhiteSpace(l.BatchNo) ? "NA" : l.BatchNo.Trim().ToUpperInvariant();
            var batch = p.Batches.FirstOrDefault(b => b.BatchNo == batchNo);
            if (batch is null)
            {
                batch = new ProductBatch { BatchNo = batchNo };
                p.Batches.Add(batch);
            }
            batch.MfgDate = l.MfgDate ?? batch.MfgDate;
            batch.ExpiryDate = l.ExpiryDate ?? batch.ExpiryDate;
            batch.Quantity += l.Quantity + l.FreeQuantity;
            batch.PurchasePrice = l.Rate;
            batch.SalePrice = l.SalePrice ?? (batch.SalePrice > 0 ? batch.SalePrice : p.SalePrice);
            batch.Mrp = l.Mrp ?? (batch.Mrp > 0 ? batch.Mrp : p.Mrp);

            p.PurchasePrice = l.Rate;
            if (l.SalePrice is > 0) p.SalePrice = l.SalePrice.Value;
            if (l.Mrp is > 0) p.Mrp = l.Mrp.Value;

            var taxable = TaxCalculator.R2(l.Quantity * l.Rate);
            var tax = TaxCalculator.R2(taxable * l.GstRate / 100m);
            pi.Items.Add(new PurchaseInvoiceItem
            {
                ProductId = p.Id, ProductName = p.Name, BatchNo = batchNo, MfgDate = l.MfgDate, ExpiryDate = l.ExpiryDate,
                Quantity = l.Quantity, FreeQuantity = l.FreeQuantity, Rate = l.Rate, GstRate = l.GstRate, TaxAmount = tax,
                Total = taxable + tax, SalePrice = batch.SalePrice, Mrp = batch.Mrp,
            });
            var move = new StockMovement { ProductId = p.Id, Date = date, Type = StockMovementType.Purchase, Quantity = l.Quantity + l.FreeQuantity, Reference = $"PUR {pi.BillNo}" };
            _db.StockMovements.Add(move);
            pendingMovements.Add((move, batch));
        }

        pi.SubTotal = pi.Items.Sum(i => i.Total - i.TaxAmount);
        pi.TaxAmount = pi.Items.Sum(i => i.TaxAmount);
        pi.GrandTotal = Math.Round(pi.SubTotal + pi.TaxAmount + pi.OtherCharges, 0, MidpointRounding.AwayFromZero);
        pi.PaidAmount = Math.Clamp(req.PaidAmount, 0, pi.GrandTotal);
        supplier.Balance += pi.GrandTotal - pi.PaidAmount;

        _db.PurchaseInvoices.Add(pi);
        await _db.SaveChangesAsync(ct);

        // link generated batch ids back to purchase lines & movements
        for (var i = 0; i < pi.Items.Count; i++)
        {
            pi.Items[i].BatchId = pendingMovements[i].Batch.Id;
            pendingMovements[i].Move.BatchId = pendingMovements[i].Batch.Id;
        }
        await _db.SaveChangesAsync(ct);
        return pi;
    }, ct);

    // ─────────────────────────── PAYMENTS (UDHARI) ───────────────────────────
    public Task<Payment> RecordPaymentAsync(CreatePaymentRequest req, CancellationToken ct = default) => _db.InTransactionAsync(async () =>
    {
        if (req.PartyType == PartyType.Customer)
        {
            var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == req.PartyId, ct) ?? throw new BusinessException("Customer not found.", 404);
            c.Balance -= req.Amount;
        }
        else
        {
            var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == req.PartyId, ct) ?? throw new BusinessException("Supplier not found.", 404);
            s.Balance -= req.Amount;
        }

        var payment = new Payment
        {
            PartyType = req.PartyType, PartyId = req.PartyId, Amount = req.Amount, Mode = req.Mode == PaymentMode.Credit ? PaymentMode.Cash : req.Mode,
            Reference = req.Reference, Note = req.Note, Date = req.Date ?? AppClock.Now,
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);
        return payment;
    }, ct);

    // ─────────────────────────── STOCK ───────────────────────────
    public async Task AdjustStockAsync(StockAdjustRequest req, CancellationToken ct = default)
    {
        var p = await _db.Products.Include(x => x.Batches).FirstOrDefaultAsync(x => x.Id == req.ProductId, ct) ?? throw new BusinessException("Product not found.", 404);
        ProductBatch batch;
        if (req.BatchId is int bid)
            batch = p.Batches.FirstOrDefault(b => b.Id == bid) ?? throw new BusinessException("Batch not found.");
        else
        {
            var no = string.IsNullOrWhiteSpace(req.NewBatchNo) ? "NA" : req.NewBatchNo.Trim().ToUpperInvariant();
            batch = p.Batches.FirstOrDefault(b => b.BatchNo == no) ?? new ProductBatch { BatchNo = no, ExpiryDate = req.NewBatchExpiry, PurchasePrice = p.PurchasePrice, SalePrice = p.SalePrice, Mrp = p.Mrp };
            if (batch.Id == 0) p.Batches.Add(batch);
        }
        if (batch.Quantity + req.QuantityChange < 0) throw new BusinessException("Stock cannot go below zero.");
        batch.Quantity += req.QuantityChange;
        await _db.SaveChangesAsync(ct);
        _db.StockMovements.Add(new StockMovement { ProductId = p.Id, BatchId = batch.Id, Date = AppClock.Now, Type = StockMovementType.Adjustment, Quantity = req.QuantityChange, Note = req.Note, Reference = "ADJ" });
        await _db.SaveChangesAsync(ct);
    }
}
