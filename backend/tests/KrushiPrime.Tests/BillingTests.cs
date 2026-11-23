using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Services;

namespace KrushiPrime.Tests;

public class TaxCalculatorTests
{
    [Fact]
    public void Inclusive_5pct_intra_state_splits_cgst_sgst()
    {
        var t = TaxCalculator.Compute(1050m, 5, inclusive: true, isGstBill: true, interState: false);
        Assert.Equal(1000m, t.Taxable);
        Assert.Equal(25m, t.Cgst);
        Assert.Equal(25m, t.Sgst);
        Assert.Equal(1050m, t.Total);
    }

    [Fact]
    public void Exclusive_18pct_inter_state_is_igst()
    {
        var t = TaxCalculator.Compute(1000m, 18, inclusive: false, isGstBill: true, interState: true);
        Assert.Equal(180m, t.Igst);
        Assert.Equal(0m, t.Cgst);
        Assert.Equal(1180m, t.Total);
    }

    [Fact]
    public void Non_gst_bill_has_no_tax()
    {
        var t = TaxCalculator.Compute(500m, 18, inclusive: true, isGstBill: false, interState: false);
        Assert.Equal(0m, t.Tax);
        Assert.Equal(500m, t.Total);
    }

    [Fact]
    public void Odd_paise_are_balanced_between_cgst_and_sgst()
    {
        var t = TaxCalculator.Compute(266.50m, 5, inclusive: true, isGstBill: true, interState: false);
        Assert.Equal(t.Total, t.Taxable + t.Cgst + t.Sgst);
        Assert.True(Math.Abs(t.Cgst - t.Sgst) <= 0.01m);
    }

    [Theory]
    [InlineData(1233.49, 1233, -0.49)]
    [InlineData(1233.50, 1234, 0.50)]
    public void Invoice_rounds_to_nearest_rupee(decimal total, decimal grand, decimal roundOff)
    {
        var (g, r) = TaxCalculator.RoundInvoice(total);
        Assert.Equal(grand, g);
        Assert.Equal(roundOff, r);
    }
}

public class FefoAllocationTests
{
    private static Product ProductWithBatches() => new()
    {
        Id = 1, Name = "Imidacloprid", Unit = "Btl",
        Batches =
        {
            new ProductBatch { Id = 10, BatchNo = "LATE", Quantity = 10, ExpiryDate = AppClock.Today.AddMonths(12) },
            new ProductBatch { Id = 11, BatchNo = "EARLY", Quantity = 3, ExpiryDate = AppClock.Today.AddDays(20) },
            new ProductBatch { Id = 12, BatchNo = "EXPIRED", Quantity = 50, ExpiryDate = AppClock.Today.AddDays(-1) },
        }
    };

    [Fact]
    public void Picks_earliest_expiry_first_and_splits_across_batches()
    {
        var alloc = BillingService.AllocateBatches(ProductWithBatches(), new SaleLineRequest(1, null, 5, 100), allowNegative: false);
        Assert.Equal(2, alloc.Count);
        Assert.Equal("EARLY", alloc[0].Batch.BatchNo);
        Assert.Equal(3, alloc[0].Qty);
        Assert.Equal("LATE", alloc[1].Batch.BatchNo);
        Assert.Equal(2, alloc[1].Qty);
    }

    [Fact]
    public void Never_sells_expired_stock()
    {
        var ex = Assert.Throws<BusinessException>(() => BillingService.AllocateBatches(ProductWithBatches(), new SaleLineRequest(1, null, 20, 100), false));
        Assert.Contains("Insufficient stock", ex.Message);
        Assert.Throws<BusinessException>(() => BillingService.AllocateBatches(ProductWithBatches(), new SaleLineRequest(1, 12, 1, 100), false));
    }
}
