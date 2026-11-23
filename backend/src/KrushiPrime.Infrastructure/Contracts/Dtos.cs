using System.ComponentModel.DataAnnotations;
using KrushiPrime.Domain;

namespace KrushiPrime.Infrastructure.Contracts;

// ───── Auth & tenancy ─────
public record LoginRequest([Required] string Username, [Required] string Password);

public record RegisterShopRequest(
    [Required, MaxLength(150)] string ShopName,
    [Required, MaxLength(100)] string OwnerName,
    [Required, RegularExpression(@"^[6-9]\d{9}$", ErrorMessage = "Enter a valid 10-digit mobile number")] string Mobile,
    [Required, MinLength(6)] string Password,
    string? Village, string? Taluka, string? District, string? Gstin,
    bool SeedSampleProducts = true);

public record TenantSummary(int Id, string ShopName, string Plan, DateTime SubscriptionEnd, int DaysLeft, bool IsActive, bool IsExpired);
public record UserInfo(int Id, string FullName, string Username, string Role, TenantSummary? Tenant);
public record AuthResponse(string Token, DateTime ExpiresAt, UserInfo User);

public record UpdateSubscriptionRequest(SubscriptionPlan Plan, int? ExtendMonths, DateTime? SubscriptionEnd, string? Notes);
public record SetStatusRequest(bool IsActive);

public record CreateUserRequest([Required] string FullName, [Required] string Username, [Required, MinLength(6)] string Password, UserRole Role);
public record ChangePasswordRequest([Required] string CurrentPassword, [Required, MinLength(6)] string NewPassword);

public record ShopSettingsDto(
    string ShopName, string OwnerName, string Mobile, string? Email, string? AddressLine, string? Village,
    string? Taluka, string? District, string State, string StateCode, string? Pincode, string? Gstin, bool IsGstRegistered,
    string? FertilizerLicenseNo, string? PesticideLicenseNo, string? SeedLicenseNo, string? UpiId,
    string InvoicePrefix, string? InvoiceFooter, bool PricesIncludeTax, bool AllowNegativeStock);

// ───── Masters ─────
public record ProductUpsert(
    [Required, MaxLength(200)] string Name, string? LocalName, ProductCategory Category, string? Company, string? HsnCode,
    [Range(0, 28)] decimal GstRate, [Required] string Unit, string? PackSize, string? Barcode,
    [Range(0, 10_000_000)] decimal SalePrice, decimal Mrp, decimal PurchasePrice, decimal ReorderLevel, bool TrackExpiry = true,
    bool IsActive = true,
    // optional opening stock (create only)
    decimal? OpeningQty = null, string? OpeningBatchNo = null, DateTime? OpeningExpiry = null);

public record BatchDto(int Id, string BatchNo, DateTime? MfgDate, DateTime? ExpiryDate, decimal Quantity, decimal PurchasePrice, decimal SalePrice, decimal Mrp);

public record ProductDto(int Id, string Name, string? LocalName, ProductCategory Category, string? Company, string? HsnCode, decimal GstRate,
    string Unit, string? PackSize, string? Barcode, decimal SalePrice, decimal Mrp, decimal PurchasePrice, decimal ReorderLevel,
    bool TrackExpiry, bool IsActive, decimal Stock, DateTime? NearestExpiry, List<BatchDto>? Batches);

public record CustomerUpsert([Required, MaxLength(150)] string Name, string? Mobile, string? Village, string? Address, string? Gstin,
    decimal OpeningBalance, decimal CreditLimit, decimal? LandAcres, string? Crops, bool IsActive = true);

public record SupplierUpsert([Required, MaxLength(150)] string Name, string? Mobile, string? Gstin, string? Address, bool IsActive = true);

public record StockAdjustRequest([Required] int ProductId, int? BatchId, [Required] decimal QuantityChange, string? Note,
    string? NewBatchNo = null, DateTime? NewBatchExpiry = null);

// ───── Sales ─────
public record SaleLineRequest([Required] int ProductId, int? BatchId, [Range(0.001, 1_000_000)] decimal Quantity, [Range(0, 10_000_000)] decimal Rate, decimal Discount = 0);

public record CreateSaleRequest(
    int? CustomerId, string? CustomerName, string? CustomerMobile, string? CustomerVillage,
    bool IsGst, bool IsInterState, [MinLength(1)] List<SaleLineRequest> Items,
    decimal PaidAmount, PaymentMode PaymentMode, string? Notes, DateTime? InvoiceDate = null);

public record CancelSaleRequest([Required] string Reason);

public record SaleListItem(int Id, string InvoiceNo, DateTime InvoiceDate, string CustomerName, string? CustomerMobile, bool IsGst,
    decimal GrandTotal, decimal PaidAmount, decimal BalanceAmount, string PaymentMode, string Status, int ItemCount);

// ───── Purchases ─────
public record PurchaseLineRequest([Required] int ProductId, [Required] string BatchNo, DateTime? MfgDate, DateTime? ExpiryDate,
    [Range(0.001, 1_000_000)] decimal Quantity, decimal FreeQuantity, [Range(0, 10_000_000)] decimal Rate, decimal GstRate,
    decimal? SalePrice, decimal? Mrp);

public record CreatePurchaseRequest([Required] int SupplierId, [Required] string BillNo, DateTime? BillDate, bool IsInterState,
    [MinLength(1)] List<PurchaseLineRequest> Items, decimal OtherCharges, decimal PaidAmount, PaymentMode PaymentMode, string? Notes);

// ───── Payments ─────
public record CreatePaymentRequest(PartyType PartyType, [Required] int PartyId, [Range(0.01, 100_000_000)] decimal Amount,
    PaymentMode Mode, string? Reference, string? Note, DateTime? Date);

public record LedgerEntry(DateTime Date, string Type, string Reference, decimal Debit, decimal Credit, decimal Balance);
public record LedgerDto(int PartyId, string Name, string? Mobile, decimal OpeningBalance, decimal ClosingBalance, List<LedgerEntry> Entries);

// ───── Messaging ─────
public record WhatsAppResult(string Mode, bool Sent, string? Url, string Message, string? Error);

// ───── Common ─────
public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
