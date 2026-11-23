namespace KrushiPrime.Infrastructure.Services;

public readonly record struct LineTax(decimal Taxable, decimal Cgst, decimal Sgst, decimal Igst, decimal Total)
{
    public decimal Tax => Cgst + Sgst + Igst;
}

/// <summary>Indian GST computation for a single invoice line. Pure and unit-tested.</summary>
public static class TaxCalculator
{
    public static decimal R2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <param name="gross">qty × rate − discount</param>
    /// <param name="inclusive">true when the rate already contains GST (typical MRP-based agri retail)</param>
    /// <param name="isGstBill">false for Bill of Supply / non-GST estimate: no tax split</param>
    public static LineTax Compute(decimal gross, decimal gstRate, bool inclusive, bool isGstBill, bool interState)
    {
        gross = R2(gross);
        if (!isGstBill || gstRate <= 0)
            return new LineTax(gross, 0, 0, 0, gross);

        decimal taxable, tax;
        if (inclusive)
        {
            taxable = R2(gross * 100m / (100m + gstRate));
            tax = gross - taxable;
        }
        else
        {
            taxable = gross;
            tax = R2(gross * gstRate / 100m);
        }

        if (interState)
            return new LineTax(taxable, 0, 0, tax, taxable + tax);

        var cgst = R2(tax / 2);
        return new LineTax(taxable, cgst, tax - cgst, 0, taxable + tax);
    }

    /// <summary>Round invoice to nearest rupee (standard retail practice).</summary>
    public static (decimal GrandTotal, decimal RoundOff) RoundInvoice(decimal total)
    {
        var grand = Math.Round(total, 0, MidpointRounding.AwayFromZero);
        return (grand, grand - total);
    }
}
