namespace KrushiPrime.Domain;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>Marker for rows owned by one Krushi Seva Kendra (tenant). Filtered automatically.</summary>
public interface ITenantEntity
{
    int TenantId { get; set; }
}

public abstract class TenantEntity : BaseEntity, ITenantEntity
{
    public int TenantId { get; set; }
}

// ───────────── Tenancy & licensing ─────────────

/// <summary>One agri-input shop (Krushi Seva Kendra) subscribed to Krushi Prime.</summary>
public class Tenant : BaseEntity
{
    public string ShopName { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string? Email { get; set; }
    public string? AddressLine { get; set; }
    public string? Village { get; set; }
    public string? Taluka { get; set; }
    public string? District { get; set; }
    public string State { get; set; } = "Maharashtra";
    public string StateCode { get; set; } = "27";
    public string? Pincode { get; set; }
    public string? Gstin { get; set; }
    public bool IsGstRegistered { get; set; } = true;
    public string? FertilizerLicenseNo { get; set; }
    public string? PesticideLicenseNo { get; set; }
    public string? SeedLicenseNo { get; set; }
    public string? UpiId { get; set; }
    public string InvoicePrefix { get; set; } = "KP";
    public int NextInvoiceNo { get; set; } = 1;
    public string? InvoiceFooter { get; set; } = "धन्यवाद! पुन्हा भेट द्या. | Thank you, visit again.";
    public bool PricesIncludeTax { get; set; } = true;
    public bool AllowNegativeStock { get; set; }

    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Trial;
    public DateTime SubscriptionStart { get; set; }
    public DateTime SubscriptionEnd { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // ── Online payments (the shop's own merchant account; money settles directly to the shop) ──
    public PaymentProvider PaymentProvider { get; set; } = PaymentProvider.None;
    public bool PaymentLiveMode { get; set; }
    public string? CashfreeAppId { get; set; }
    public string? CashfreeSecretEnc { get; set; }
    public string? NttMerchId { get; set; }
    public string? NttPasswordEnc { get; set; }
    public string? NttProductId { get; set; }
    public string? NttRequestKeyEnc { get; set; }
    public string? NttResponseKeyEnc { get; set; }
}

public enum PaymentProvider { None = 0, Simulator = 1, Cashfree = 2, NttData = 3, UpiDirect = 4 }
public enum PaymentLinkPurpose { Udhari = 0, Invoice = 1, Subscription = 2 }
public enum PaymentLinkStatus { Created = 0, Paid = 1, PartiallyPaid = 2, Failed = 3, Expired = 4, Cancelled = 5 }

/// <summary>An online "pay bill" request: udhari collection, invoice balance, or a shop's Krushi Prime renewal.</summary>
public class PaymentLink : TenantEntity
{
    public string PublicId { get; set; } = Guid.NewGuid().ToString("N");
    public PaymentLinkPurpose Purpose { get; set; }
    public PaymentProvider Provider { get; set; }
    public bool LiveMode { get; set; }
    public int? CustomerId { get; set; }
    public int? SalesInvoiceId { get; set; }
    public SubscriptionPlan? Plan { get; set; }
    public int? PlanMonths { get; set; }
    public string PayerName { get; set; } = "";
    public string? PayerMobile { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentLinkStatus Status { get; set; }
    public string ProviderRef { get; set; } = "";       // Cashfree link_id / NTT merchTxnId
    public string? ProviderUrl { get; set; }            // Cashfree hosted link_url
    public string? ProviderToken { get; set; }          // NTT atomTokenId
    public string? ProviderTxnId { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? RecordedPaymentId { get; set; }
    public string? LastMessage { get; set; }
}

public enum SubscriptionPlan { Trial = 0, Basic = 1, Pro = 2, Enterprise = 3 }

public enum UserRole { SuperAdmin = 0, Owner = 1, Manager = 2, Cashier = 3 }

public class AppUser : BaseEntity
{
    public int? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string FullName { get; set; } = "";
    public string Username { get; set; } = "";   // usually mobile number
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; } = UserRole.Cashier;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}

// ───────────── Master data ─────────────

public enum ProductCategory { Fertilizer = 0, Seed = 1, Pesticide = 2, Herbicide = 3, Fungicide = 4, GrowthPromoter = 5, Equipment = 6, Other = 7 }

public class Product : TenantEntity
{
    public string Name { get; set; } = "";
    public string? LocalName { get; set; }          // Marathi / regional name
    public ProductCategory Category { get; set; }
    public string? Company { get; set; }
    public string? HsnCode { get; set; }
    public decimal GstRate { get; set; }            // 0, 5, 12, 18, 28
    public string Unit { get; set; } = "Nos";       // Bag, Kg, Ltr, Ml, Pkt, Nos
    public string? PackSize { get; set; }           // e.g. "50 Kg", "250 ml"
    public string? Barcode { get; set; }
    public decimal SalePrice { get; set; }
    public decimal Mrp { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool TrackExpiry { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public List<ProductBatch> Batches { get; set; } = new();
}

public class ProductBatch : TenantEntity
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string BatchNo { get; set; } = "";
    public DateTime? MfgDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal Mrp { get; set; }
}

public enum StockMovementType { Opening = 0, Purchase = 1, Sale = 2, SaleCancel = 3, Adjustment = 4, PurchaseReturn = 5 }

public class StockMovement : TenantEntity
{
    public int ProductId { get; set; }
    public int? BatchId { get; set; }
    public DateTime Date { get; set; }
    public StockMovementType Type { get; set; }
    public decimal Quantity { get; set; }           // + in, − out
    public string? Reference { get; set; }
    public string? Note { get; set; }
}

public class Customer : TenantEntity
{
    public string Name { get; set; } = "";
    public string? Mobile { get; set; }
    public string? Village { get; set; }
    public string? Address { get; set; }
    public string? Gstin { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal Balance { get; set; }            // outstanding udhari (+ = customer owes shop)
    public decimal CreditLimit { get; set; }
    public decimal? LandAcres { get; set; }
    public string? Crops { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Supplier : TenantEntity
{
    public string Name { get; set; } = "";
    public string? Mobile { get; set; }
    public string? Gstin { get; set; }
    public string? Address { get; set; }
    public decimal Balance { get; set; }            // + = shop owes supplier
    public bool IsActive { get; set; } = true;
}

// ───────────── Transactions ─────────────

public enum PaymentMode { Cash = 0, Upi = 1, Card = 2, Bank = 3, Credit = 4, Mixed = 5, Cheque = 6 }
public enum InvoiceStatus { Paid = 0, Partial = 1, Unpaid = 2, Cancelled = 3 }

public class SalesInvoice : TenantEntity
{
    public string InvoiceNo { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string CustomerName { get; set; } = "Cash Customer";
    public string? CustomerMobile { get; set; }
    public string? CustomerVillage { get; set; }
    public string? CustomerGstin { get; set; }
    public bool IsGst { get; set; } = true;          // false → Bill of Supply / non-GST estimate
    public bool IsInterState { get; set; }
    public decimal SubTotal { get; set; }            // taxable value
    public decimal Discount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal RoundOff { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public InvoiceStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public List<SalesInvoiceItem> Items { get; set; } = new();
}

public class SalesInvoiceItem : BaseEntity
{
    public int SalesInvoiceId { get; set; }
    public int ProductId { get; set; }
    public int? BatchId { get; set; }
    public string ProductName { get; set; } = "";
    public string? HsnCode { get; set; }
    public string? BatchNo { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string Unit { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }               // as entered (incl. tax if tenant.PricesIncludeTax)
    public decimal DiscountAmount { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal GstRate { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal Igst { get; set; }
    public decimal Total { get; set; }
    public decimal CostPrice { get; set; }          // for profit report
}

public class PurchaseInvoice : TenantEntity
{
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string BillNo { get; set; } = "";
    public DateTime BillDate { get; set; }
    public bool IsInterState { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentMode PaymentMode { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseInvoiceItem> Items { get; set; } = new();
}

public class PurchaseInvoiceItem : BaseEntity
{
    public int PurchaseInvoiceId { get; set; }
    public int ProductId { get; set; }
    public int BatchId { get; set; }
    public string ProductName { get; set; } = "";
    public string BatchNo { get; set; } = "";
    public DateTime? MfgDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal FreeQuantity { get; set; }
    public decimal Rate { get; set; }               // excl. tax
    public decimal GstRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal SalePrice { get; set; }
    public decimal Mrp { get; set; }
}

public enum PartyType { Customer = 0, Supplier = 1 }

/// <summary>Udhari collection from farmer, or payment made to supplier.</summary>
public class Payment : TenantEntity
{
    public PartyType PartyType { get; set; }
    public int PartyId { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public PaymentMode Mode { get; set; }
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public int? SalesInvoiceId { get; set; }
}

public class MessageLog : TenantEntity
{
    public string Channel { get; set; } = "WhatsApp";
    public string To { get; set; } = "";
    public string Purpose { get; set; } = "";       // Invoice, Reminder, Promo
    public string Body { get; set; } = "";
    public string Status { get; set; } = "";        // Link, Sent, Failed
    public string? ProviderMessageId { get; set; }
    public string? Error { get; set; }
}
