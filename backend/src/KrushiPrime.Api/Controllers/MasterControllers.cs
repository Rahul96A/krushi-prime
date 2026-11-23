using KrushiPrime.Api.Infrastructure;
using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Persistence;
using KrushiPrime.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KrushiPrime.Api.Controllers;

[ApiController, Route("api/products"), Authorize(Policy = Policies.ShopUser)]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBillingService _billing;
    public ProductsController(AppDbContext db, IBillingService billing) { _db = db; _billing = billing; }

    internal static ProductDto Map(Product p, bool withBatches) => new(p.Id, p.Name, p.LocalName, p.Category, p.Company, p.HsnCode, p.GstRate, p.Unit,
        p.PackSize, p.Barcode, p.SalePrice, p.Mrp, p.PurchasePrice, p.ReorderLevel, p.TrackExpiry, p.IsActive,
        p.Batches.Sum(b => b.Quantity), p.Batches.Where(b => b.Quantity > 0 && b.ExpiryDate != null).Min(b => b.ExpiryDate),
        withBatches ? p.Batches.Where(b => b.Quantity != 0).OrderBy(b => b.ExpiryDate ?? DateTime.MaxValue)
            .Select(b => new BatchDto(b.Id, b.BatchNo, b.MfgDate, b.ExpiryDate, b.Quantity, b.PurchasePrice, b.SalePrice, b.Mrp)).ToList() : null);

    [HttpGet]
    public async Task<List<ProductDto>> List([FromQuery] string? q, [FromQuery] ProductCategory? category, [FromQuery] bool includeInactive = false, [FromQuery] bool batches = false, CancellationToken ct = default)
    {
        var query = _db.Products.AsNoTracking().Include(p => p.Batches).AsQueryable();
        if (!includeInactive) query = query.Where(p => p.IsActive);
        if (category is not null) query = query.Where(p => p.Category == category);
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(p => EF.Functions.Like(p.Name, "%" + q + "%") || (p.LocalName != null && EF.Functions.Like(p.LocalName, "%" + q + "%")) || (p.Company != null && EF.Functions.Like(p.Company, "%" + q + "%")) || p.Barcode == q || p.HsnCode == q);
        }
        return (await query.OrderBy(p => p.Name).Take(500).ToListAsync(ct)).Select(p => Map(p, batches)).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ProductDto> Get(int id, CancellationToken ct)
        => Map(await _db.Products.AsNoTracking().Include(p => p.Batches).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new BusinessException("Product not found.", 404), true);

    [HttpPost, Authorize(Policy = Policies.ShopManager)]
    public async Task<ProductDto> Create(ProductUpsert req, CancellationToken ct)
    {
        var p = new Product();
        Apply(p, req);
        if (req.OpeningQty is > 0)
        {
            p.Batches.Add(new ProductBatch { BatchNo = string.IsNullOrWhiteSpace(req.OpeningBatchNo) ? "OPENING" : req.OpeningBatchNo.Trim().ToUpperInvariant(), ExpiryDate = req.OpeningExpiry,
                Quantity = req.OpeningQty.Value, PurchasePrice = req.PurchasePrice, SalePrice = req.SalePrice, Mrp = req.Mrp });
        }
        _db.Products.Add(p);
        await _db.SaveChangesAsync(ct);
        if (req.OpeningQty is > 0)
        {
            _db.StockMovements.Add(new StockMovement { ProductId = p.Id, BatchId = p.Batches[0].Id, Date = AppClock.Now, Type = StockMovementType.Opening, Quantity = req.OpeningQty.Value, Reference = "OPENING" });
            await _db.SaveChangesAsync(ct);
        }
        return Map(p, true);
    }

    [HttpPut("{id:int}"), Authorize(Policy = Policies.ShopManager)]
    public async Task<ProductDto> Update(int id, ProductUpsert req, CancellationToken ct)
    {
        var p = await _db.Products.Include(x => x.Batches).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Product not found.", 404);
        Apply(p, req);
        await _db.SaveChangesAsync(ct);
        return Map(p, true);
    }

    [HttpDelete("{id:int}"), Authorize(Policy = Policies.ShopManager)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Product not found.", 404);
        p.IsActive = false;   // soft delete keeps invoice history intact
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("adjust-stock"), Authorize(Policy = Policies.ShopManager)]
    public async Task<ProductDto> Adjust(StockAdjustRequest req, CancellationToken ct)
    {
        await _billing.AdjustStockAsync(req, ct);
        return await Get(req.ProductId, ct);
    }

    [HttpGet("{id:int}/movements")]
    public Task<List<StockMovement>> Movements(int id, CancellationToken ct)
        => _db.StockMovements.AsNoTracking().Where(m => m.ProductId == id).OrderByDescending(m => m.Date).Take(200).ToListAsync(ct);

    private static void Apply(Product p, ProductUpsert r)
    {
        p.Name = r.Name.Trim(); p.LocalName = r.LocalName; p.Category = r.Category; p.Company = r.Company; p.HsnCode = r.HsnCode;
        p.GstRate = r.GstRate; p.Unit = r.Unit; p.PackSize = r.PackSize; p.Barcode = string.IsNullOrWhiteSpace(r.Barcode) ? null : r.Barcode.Trim();
        p.SalePrice = r.SalePrice; p.Mrp = r.Mrp; p.PurchasePrice = r.PurchasePrice; p.ReorderLevel = r.ReorderLevel; p.TrackExpiry = r.TrackExpiry; p.IsActive = r.IsActive;
    }
}

[ApiController, Route("api/customers"), Authorize(Policy = Policies.ShopUser)]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IReportService _reports;
    public CustomersController(AppDbContext db, IReportService reports) { _db = db; _reports = reports; }

    [HttpGet]
    public async Task<List<Customer>> List([FromQuery] string? q, [FromQuery] bool withDueOnly = false, CancellationToken ct = default)
    {
        var query = _db.Customers.AsNoTracking().Where(c => c.IsActive);
        if (withDueOnly) query = query.Where(c => c.Balance > 0);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => EF.Functions.Like(c.Name, "%" + q + "%") || (c.Mobile != null && EF.Functions.Like(c.Mobile, "%" + q + "%")) || (c.Village != null && EF.Functions.Like(c.Village, "%" + q + "%")));
        return await query.OrderBy(c => c.Name).Take(500).ToListAsync(ct);
    }

    [HttpGet("{id:int}")]
    public async Task<Customer> Get(int id, CancellationToken ct) => await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new BusinessException("Customer not found.", 404);

    [HttpGet("{id:int}/ledger")]
    public Task<LedgerDto> Ledger(int id, CancellationToken ct) => _reports.CustomerLedgerAsync(id, ct);

    [HttpPost]
    public async Task<Customer> Create(CustomerUpsert r, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(r.Mobile) && await _db.Customers.AnyAsync(c => c.Mobile == r.Mobile, ct))
            throw new BusinessException("A customer with this mobile number already exists.", 409);
        var c = new Customer { OpeningBalance = r.OpeningBalance, Balance = r.OpeningBalance };
        Apply(c, r);
        _db.Customers.Add(c);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    [HttpPut("{id:int}")]
    public async Task<Customer> Update(int id, CustomerUpsert r, CancellationToken ct)
    {
        var c = await _db.Customers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Customer not found.", 404);
        c.Balance += r.OpeningBalance - c.OpeningBalance;
        c.OpeningBalance = r.OpeningBalance;
        Apply(c, r);
        await _db.SaveChangesAsync(ct);
        return c;
    }

    private static void Apply(Customer c, CustomerUpsert r)
    {
        c.Name = r.Name.Trim(); c.Mobile = r.Mobile?.Trim(); c.Village = r.Village; c.Address = r.Address; c.Gstin = r.Gstin?.ToUpperInvariant();
        c.CreditLimit = r.CreditLimit; c.LandAcres = r.LandAcres; c.Crops = r.Crops; c.IsActive = r.IsActive;
    }
}

[ApiController, Route("api/suppliers"), Authorize(Policy = Policies.ShopManager)]
public class SuppliersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IReportService _reports;
    public SuppliersController(AppDbContext db, IReportService reports) { _db = db; _reports = reports; }

    [HttpGet]
    public Task<List<Supplier>> List(CancellationToken ct) => _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync(ct);

    [HttpGet("{id:int}/ledger")]
    public Task<LedgerDto> Ledger(int id, CancellationToken ct) => _reports.SupplierLedgerAsync(id, ct);

    [HttpPost]
    public async Task<Supplier> Create(SupplierUpsert r, CancellationToken ct)
    {
        var s = new Supplier { Name = r.Name.Trim(), Mobile = r.Mobile, Gstin = r.Gstin?.ToUpperInvariant(), Address = r.Address };
        _db.Suppliers.Add(s);
        await _db.SaveChangesAsync(ct);
        return s;
    }

    [HttpPut("{id:int}")]
    public async Task<Supplier> Update(int id, SupplierUpsert r, CancellationToken ct)
    {
        var s = await _db.Suppliers.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new BusinessException("Supplier not found.", 404);
        s.Name = r.Name.Trim(); s.Mobile = r.Mobile; s.Gstin = r.Gstin?.ToUpperInvariant(); s.Address = r.Address; s.IsActive = r.IsActive;
        await _db.SaveChangesAsync(ct);
        return s;
    }
}
