using System.Reflection;
using KrushiPrime.Domain;
using Microsoft.EntityFrameworkCore;

namespace KrushiPrime.Infrastructure.Persistence;

public interface ICurrentUser
{
    int? UserId { get; }
    int? TenantId { get; }
    string? UserName { get; }
    UserRole? Role { get; }
    bool IsSuperAdmin => Role == UserRole.SuperAdmin;
}

/// <summary>Used for seeding, background jobs and tests.</summary>
public class SystemUser : ICurrentUser
{
    public int? UserId { get; set; }
    public int? TenantId { get; set; }
    public string? UserName { get; set; } = "system";
    public UserRole? Role { get; set; } = UserRole.SuperAdmin;
}

public class AppDbContext : DbContext
{
    private readonly ICurrentUser _user;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUser user) : base(options) => _user = user;

    /// <summary>Evaluated per query by EF; drives the multi-tenant global filter.</summary>
    public int? CurrentTenantId => _user.TenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();
    public DbSet<PurchaseInvoiceItem> PurchaseInvoiceItems => Set<PurchaseInvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();
    public DbSet<PaymentLink> PaymentLinks => Set<PaymentLink>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        if (Database.IsSqlite())
            b.Properties<decimal>().HaveConversion<double>();   // SQLite cannot ORDER BY / SUM decimals natively
        else
            b.Properties<decimal>().HavePrecision(18, 3);
        b.Properties<string>().HaveMaxLength(400);
    }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Tenant>(e =>
        {
            e.Property(x => x.NextInvoiceNo).IsConcurrencyToken();
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.HasIndex(x => x.Mobile);
        });

        mb.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Product>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.Name });
            e.HasIndex(x => new { x.TenantId, x.Barcode });
            e.HasMany(x => x.Batches).WithOne(x => x.Product!).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<ProductBatch>().HasIndex(x => new { x.TenantId, x.ProductId, x.BatchNo });
        mb.Entity<StockMovement>().HasIndex(x => new { x.TenantId, x.ProductId, x.Date });
        mb.Entity<Customer>().HasIndex(x => new { x.TenantId, x.Mobile });
        mb.Entity<Customer>().HasIndex(x => new { x.TenantId, x.Name });
        mb.Entity<Supplier>().HasIndex(x => new { x.TenantId, x.Name });

        mb.Entity<SalesInvoice>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.InvoiceNo }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.InvoiceDate });
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SalesInvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Notes).HasMaxLength(1000);
        });

        mb.Entity<PurchaseInvoice>(e =>
        {
            e.HasIndex(x => new { x.TenantId, x.BillDate });
            e.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.PurchaseInvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Payment>().HasIndex(x => new { x.TenantId, x.PartyType, x.PartyId });
        mb.Entity<MessageLog>().Property(x => x.Body).HasMaxLength(4000);
        mb.Entity<PaymentLink>(e =>
        {
            e.HasIndex(x => x.PublicId).IsUnique();
            e.Property(x => x.AmountPaid).IsConcurrencyToken(); // duplicate webhooks can never double-post
            e.HasIndex(x => x.ProviderRef);
            e.HasIndex(x => new { x.TenantId, x.Status });
            e.Property(x => x.ProviderUrl).HasMaxLength(1000);
            e.Property(x => x.LastMessage).HasMaxLength(1000);
        });
        mb.Entity<Tenant>(e =>
        {
            foreach (var p in new[] { nameof(Tenant.CashfreeSecretEnc), nameof(Tenant.NttPasswordEnc), nameof(Tenant.NttRequestKeyEnc), nameof(Tenant.NttResponseKeyEnc) })
                e.Property(p).HasMaxLength(1000);
        });

        // Multi-tenant isolation: every ITenantEntity gets "TenantId == CurrentTenantId".
        var method = typeof(AppDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var type in mb.Model.GetEntityTypes().Select(t => t.ClrType).Where(t => typeof(ITenantEntity).IsAssignableFrom(t)))
            method.MakeGenericMethod(type).Invoke(this, new object[] { mb });
    }

    private void ApplyTenantFilter<T>(ModelBuilder mb) where T : class, ITenantEntity
        => mb.Entity<T>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        var now = AppClock.Now;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = _user.UserName;
                if (entry.Entity is ITenantEntity te && te.TenantId == 0)
                    te.TenantId = _user.TenantId ?? throw new BusinessException("Tenant context missing.", 403);
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = _user.UserName;
            }
        }
        return base.SaveChangesAsync(ct);
    }
}
