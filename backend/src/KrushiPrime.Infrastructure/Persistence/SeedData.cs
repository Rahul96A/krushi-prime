using KrushiPrime.Domain;
using KrushiPrime.Infrastructure.Contracts;
using KrushiPrime.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KrushiPrime.Infrastructure.Persistence;

public static class SeedData
{
    private record Item(string Name, string Local, ProductCategory Cat, string Company, string Hsn, decimal Gst, string Unit, string Pack, decimal Purchase, decimal Sale, decimal Mrp, decimal Reorder);

    // Starter catalogue every new Krushi Seva Kendra gets (prices & GST editable; verify current GST notifications).
    private static readonly Item[] Catalogue =
    {
        new("Urea (Neem Coated) 45kg", "युरिया ४५ किलो", ProductCategory.Fertilizer, "IFFCO", "3102", 5, "Bag", "45 Kg", 242, 266.50m, 266.50m, 50),
        new("DAP 18:46:0 50kg", "डीएपी ५० किलो", ProductCategory.Fertilizer, "Coromandel", "3105", 5, "Bag", "50 Kg", 1290, 1350, 1350, 30),
        new("NPK 10:26:26 50kg", "१०:२६:२६ ५० किलो", ProductCategory.Fertilizer, "Mahadhan", "3105", 5, "Bag", "50 Kg", 1400, 1470, 1470, 20),
        new("MOP Potash 50kg", "पोटॅश ५० किलो", ProductCategory.Fertilizer, "IPL", "3104", 5, "Bag", "50 Kg", 1600, 1700, 1700, 15),
        new("SSP Single Super Phosphate 50kg", "सिंगल सुपर फॉस्फेट", ProductCategory.Fertilizer, "RCF", "3103", 5, "Bag", "50 Kg", 500, 560, 580, 25),
        new("NPK 19:19:19 Water Soluble 1kg", "१९:१९:१९ विद्राव्य खत", ProductCategory.Fertilizer, "Mahadhan", "3105", 5, "Kg", "1 Kg", 145, 180, 195, 20),
        new("Imidacloprid 17.8% SL 250ml", "इमिडाक्लोप्रिड", ProductCategory.Pesticide, "Bayer", "3808", 18, "Btl", "250 ml", 280, 350, 390, 10),
        new("Emamectin Benzoate 5% SG 100g", "इमामेक्टिन बेंझोएट", ProductCategory.Pesticide, "Syngenta", "3808", 18, "Pkt", "100 g", 230, 290, 320, 10),
        new("Chlorpyrifos 20% EC 1L", "क्लोरोपायरीफॉस", ProductCategory.Pesticide, "Dhanuka", "3808", 18, "Btl", "1 Ltr", 360, 450, 490, 8),
        new("Mancozeb 75% WP 1kg", "मॅन्कोझेब", ProductCategory.Fungicide, "UPL", "3808", 18, "Pkt", "1 Kg", 390, 480, 520, 8),
        new("Glyphosate 41% SL 1L", "ग्लायफोसेट तणनाशक", ProductCategory.Herbicide, "Dhanuka", "3808", 18, "Btl", "1 Ltr", 420, 520, 560, 8),
        new("Humic Acid 98% 1L", "ह्युमिक ॲसिड", ProductCategory.GrowthPromoter, "Biostadt", "3105", 5, "Btl", "1 Ltr", 300, 400, 450, 10),
        new("Cotton BG-II Hybrid Seed 475g", "कापूस बियाणे", ProductCategory.Seed, "Rasi Seeds", "1207", 0, "Pkt", "475 g", 820, 901, 901, 30),
        new("Soybean JS-335 Seed 30kg", "सोयाबीन बियाणे", ProductCategory.Seed, "Mahabeej", "1201", 0, "Bag", "30 Kg", 2950, 3300, 3450, 10),
        new("Onion Seed N-53 500g", "कांदा बियाणे", ProductCategory.Seed, "Panchganga", "1209", 0, "Pkt", "500 g", 1150, 1400, 1500, 5),
        new("Battery Sprayer 16L", "बॅटरी स्प्रे पंप", ProductCategory.Equipment, "Neptune", "8424", 5, "Nos", "16 Ltr", 2300, 2900, 3200, 2),
        new("Ammonium Sulphate 50kg", "अमोनियम सल्फेट", ProductCategory.Fertilizer, "GSFC", "3102", 5, "Bag", "50 Kg", 780, 850, 875, 15),
        new("NPK 12:61:0 Water Soluble 1kg", "१२:६१:० विद्राव्य खत", ProductCategory.Fertilizer, "SQM", "3105", 5, "Kg", "1 Kg", 175, 220, 240, 20),
        new("NPK 0:52:34 Water Soluble 1kg", "०:५२:३४ विद्राव्य खत", ProductCategory.Fertilizer, "Yara", "3105", 5, "Kg", "1 Kg", 190, 240, 260, 20),
        new("Calcium Nitrate 25kg", "कॅल्शियम नायट्रेट", ProductCategory.Fertilizer, "Yara", "3102", 5, "Bag", "25 Kg", 1650, 1850, 1950, 5),
        new("Zinc Sulphate 33% 5kg", "झिंक सल्फेट", ProductCategory.Fertilizer, "Aries", "2833", 5, "Bag", "5 Kg", 380, 460, 500, 10),
        new("Sulphur 90% WDG 3kg", "सल्फर ९०%", ProductCategory.Fertilizer, "Coromandel", "2802", 5, "Pkt", "3 Kg", 290, 360, 390, 10),
        new("Thiamethoxam 25% WG 100g", "थायामेथोक्झाम", ProductCategory.Pesticide, "Syngenta", "3808", 18, "Pkt", "100 g", 240, 310, 340, 10),
        new("Chlorantraniliprole 18.5% SC 60ml", "क्लोरँट्रानिलिप्रोल", ProductCategory.Pesticide, "FMC", "3808", 18, "Btl", "60 ml", 640, 780, 850, 8),
        new("Lambda Cyhalothrin 5% EC 500ml", "लॅम्डा सायहॅलोथ्रिन", ProductCategory.Pesticide, "Gharda", "3808", 18, "Btl", "500 ml", 310, 390, 420, 8),
        new("Carbendazim 50% WP 500g", "कार्बेन्डाझिम", ProductCategory.Fungicide, "Crystal", "3808", 18, "Pkt", "500 g", 210, 270, 300, 8),
        new("Tebuconazole 25.9% EC 250ml", "टेब्युकोनाझोल", ProductCategory.Fungicide, "Bayer", "3808", 18, "Btl", "250 ml", 330, 420, 460, 6),
        new("2,4-D Amine Salt 58% SL 1L", "२,४-डी तणनाशक", ProductCategory.Herbicide, "UPL", "3808", 18, "Btl", "1 Ltr", 280, 350, 380, 6),
        new("Seaweed Extract Liquid 500ml", "समुद्री शेवाळ अर्क", ProductCategory.GrowthPromoter, "Biostadt", "3101", 5, "Btl", "500 ml", 260, 340, 380, 8),
        new("Tomato Hybrid Seed 10g", "टोमॅटो बियाणे", ProductCategory.Seed, "Syngenta", "1209", 0, "Pkt", "10 g", 950, 1150, 1250, 10),
        new("Maize Hybrid Seed 4kg", "मका बियाणे", ProductCategory.Seed, "Pioneer", "1005", 0, "Pkt", "4 Kg", 1380, 1550, 1650, 10),
        new("Wheat Seed Lok-1 40kg", "गहू बियाणे", ProductCategory.Seed, "Mahabeej", "1001", 0, "Bag", "40 Kg", 1700, 1950, 2050, 5),
        new("Tur Seed BDN-711 2kg", "तूर बियाणे", ProductCategory.Seed, "Mahabeej", "0713", 0, "Pkt", "2 Kg", 330, 400, 430, 10),
        new("Knapsack Hand Sprayer 16L", "हात पंप", ProductCategory.Equipment, "Aspee", "8424", 5, "Nos", "16 Ltr", 1050, 1350, 1500, 2),
    };

    public static void AddStarterCatalogue(AppDbContext db, int tenantId)
    {
        foreach (var c in Catalogue)
            db.Products.Add(new Product
            {
                TenantId = tenantId, Name = c.Name, LocalName = c.Local, Category = c.Cat, Company = c.Company, HsnCode = c.Hsn, GstRate = c.Gst,
                Unit = c.Unit, PackSize = c.Pack, PurchasePrice = c.Purchase, SalePrice = c.Sale, Mrp = c.Mrp, ReorderLevel = c.Reorder,
                TrackExpiry = c.Cat != ProductCategory.Equipment,
            });
    }

    /// <summary>Creates schema, super-admin and (optionally) a fully populated demo shop for client presentations.</summary>
    public static async Task InitializeAsync(IServiceProvider root, IConfiguration cfg, ILogger logger)
    {
        using var scope = root.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        var sys = new SystemUser();
        await using var db = new AppDbContext(options, sys);

        if (db.Database.IsSqlite()) await db.Database.EnsureCreatedAsync();
        else await db.Database.MigrateAsync();

        var hasher = new PasswordHasher<AppUser>();
        var adminUser = cfg["Seed:SuperAdminUsername"] ?? "admin";
        if (!await db.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin))
        {
            db.Users.Add(new AppUser { FullName = "Krushi Prime Admin", Username = adminUser, Role = UserRole.SuperAdmin, PasswordHash = hasher.HashPassword(null!, cfg["Seed:SuperAdminPassword"] ?? "Admin@123") });
            await db.SaveChangesAsync();
            logger.LogInformation("Super admin '{User}' created", adminUser);
        }

    }

    /// <summary>Populates the demo shops. Runs in the background after startup so the web app comes up fast on cold starts.</summary>
    public static async Task SeedDemoAsync(IServiceProvider root, IConfiguration cfg, ILogger logger)
    {
        if (!cfg.GetValue("Seed:DemoData", true)) return;
        using var scope = root.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
        var sys = new SystemUser();
        await using var db = new AppDbContext(options, sys);
        const string demoMobile = "9999999999";
        if (await db.Users.AnyAsync(u => u.Username == demoMobile)) return;

        logger.LogInformation("Seeding demo Krushi Seva Kendra…");
        var auth = new AuthService(db, cfg);
        var tenant = await auth.OnboardTenantAsync(new RegisterShopRequest("Shree Samarth Krushi Seva Kendra", "Sachin Patil", demoMobile, "demo@123",
            "Shirur", "Shirur", "Pune", "27ABCDE1234F1Z5"));
        tenant.AddressLine = "Main Road, Near Bus Stand";
        tenant.Email = "samarthkrushi@example.com";
        tenant.Pincode = "412210";
        tenant.FertilizerLicenseNo = "FL/PUNE/2024/1187";
        tenant.PesticideLicenseNo = "IL/PUNE/2024/0932";
        tenant.SeedLicenseNo = "SL/PUNE/2024/0455";
        tenant.UpiId = "samarthkrushi@upi";
        tenant.InvoicePrefix = "SSK";
        await db.SaveChangesAsync();

        sys.TenantId = tenant.Id;
        sys.UserName = demoMobile;
        sys.Role = UserRole.Owner;
        var billing = new BillingService(db, sys);
        var rnd = new Random(2026);
        var today = AppClock.Today;

        await SeedShopHistoryAsync(db, billing, rnd, today, days: 60, farmerCount: 40, richness: 1.0);
        await SeedOtherShopsAsync(db, cfg, sys, logger);
        logger.LogInformation("Demo shop ready → login {Mobile} / demo@123", demoMobile);
    }
    private static readonly string[] FirstNames = { "Ramesh", "Suresh", "Ganesh", "Vitthal", "Dnyaneshwar", "Sanjay", "Balasaheb", "Prakash", "Anil", "Mahadev", "Santosh", "Rajendra",
        "Dattatray", "Shivaji", "Nitin", "Sachin", "Kailas", "Bhausaheb", "Popat", "Machhindra", "Sunil", "Dilip", "Ashok", "Vilas", "Tukaram", "Namdev", "Kisan", "Laxman", "Sopan", "Rohidas" };
    private static readonly string[] Surnames = { "Patil", "Jadhav", "Shinde", "Pawar", "More", "Deshmukh", "Kale", "Gaikwad", "Thorat", "Bhosale", "Kadam", "Chavan", "Wagh", "Shelke", "Gawade", "Dhumal", "Pachpute", "Kolhe", "Bhujbal", "Lande" };
    private static readonly string[] Villages = { "Shirur", "Ranjangaon", "Malthan", "Karde", "Takali Haji", "Nhavare", "Pimpalner", "Kendur", "Pabal", "Mandavgan", "Talegaon Dhamdhere", "Vadgaon Rasai" };
    private static readonly string[] CropSets = { "Onion, Soybean", "Sugarcane", "Cotton, Tur", "Onion", "Soybean, Wheat", "Pomegranate", "Tomato", "Maize", "Grapes", "Chilli, Onion", "Bajra, Gram", "Banana" };

    /// <summary>Generates a believable trading history: suppliers, farmers, periodic restocking, daily bills, udhari & collections.</summary>
    private static async Task SeedShopHistoryAsync(AppDbContext db, BillingService billing, Random rnd, DateTime today, int days, int farmerCount, double richness)
    {
        var suppliers = new[]
        {
            new Supplier { Name = "Mahalaxmi Agro Distributors", Mobile = "9822000001", Gstin = "27AAKFM1111B1Z2", Address = "Market Yard, Pune" },
            new Supplier { Name = "Kisan Crop Care Pvt Ltd", Mobile = "9822000002", Gstin = "27AACCK2222C1Z9", Address = "MIDC, Ahilyanagar" },
            new Supplier { Name = "Shetkari Seeds Corporation", Mobile = "9822000003", Gstin = "27AAJCS3333D1Z4", Address = "Gultekdi, Pune" },
            new Supplier { Name = "Deccan Fertilizer Depot", Mobile = "9822000004", Gstin = "27AADFD4444E1Z1", Address = "Station Road, Daund" },
        };
        db.Suppliers.AddRange(suppliers);

        var customers = Enumerable.Range(0, farmerCount).Select(i => new Customer
        {
            Name = $"{FirstNames[(i * 7) % FirstNames.Length]} {Surnames[(i * 3 + i / 5) % Surnames.Length]}",
            Village = Villages[rnd.Next(Villages.Length)], Crops = CropSets[rnd.Next(CropSets.Length)],
            Mobile = $"9{rnd.Next(100000000, 999999999)}", LandAcres = rnd.Next(2, 25), CreditLimit = i % 9 == 0 ? 50000 : 0,
        }).ToList();
        customers[0].Name = "Ramesh Bhausaheb Patil";
        customers[1].Name = "Om Sai Agro Traders"; customers[1].Gstin = "27AAQPO5555F1Z8"; customers[1].Crops = "Retailer (B2B)";
        customers[2].OpeningBalance = customers[2].Balance = 4500;   // carried-forward udhari
        customers[5].OpeningBalance = customers[5].Balance = 12000;
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        var products = await db.Products.OrderBy(p => p.Id).ToListAsync();
        decimal Cost(Product p) => Math.Round(p.PurchasePrice / (1 + p.GstRate / 100m), 2);
        decimal OpeningQty(Product p) => p.Unit == "Bag" ? 150 : p.Category == ProductCategory.Equipment ? 8 : 60;
        DateTime? Expiry(Product p, DateTime from) => p.TrackExpiry ? from.AddMonths(p.Category == ProductCategory.Seed ? 9 : 20) : null;
        Supplier SupplierFor(Product p) => p.Category == ProductCategory.Seed ? suppliers[2] : p.Category == ProductCategory.Fertilizer ? (p.Id % 2 == 0 ? suppliers[3] : suppliers[0]) : suppliers[1];

        // Opening stock
        var start = today.AddDays(-days - 2);
        var billNo = 7000;
        foreach (var grp in products.GroupBy(SupplierFor))
        {
            var lines = grp.Select(p => new PurchaseLineRequest(p.Id, $"B{start:yyMM}{p.Id:D2}", start.AddMonths(-1), Expiry(p, start), OpeningQty(p), 0, Cost(p), p.GstRate, p.SalePrice, p.Mrp)).ToList();
            var total = lines.Sum(l => l.Quantity * l.Rate * (1 + l.GstRate / 100));
            await billing.CreatePurchaseAsync(new CreatePurchaseRequest(grp.Key.Id, $"INV/{billNo++}", start, false, lines, 0, Math.Round(total * 0.8m), PaymentMode.Bank, "Opening stock"));
        }

        // Showcase batches: one near expiry (sold first by FEFO) and one already expired (blocked from sale)
        var pest = products.Where(p => p.Category == ProductCategory.Pesticide).ToList();
        await billing.CreatePurchaseAsync(new CreatePurchaseRequest(suppliers[1].Id, $"INV/{billNo++}", today.AddDays(-90), false, new()
        {
            new PurchaseLineRequest(pest[0].Id, "OLD-IM23", today.AddMonths(-22), today.AddDays(18), 200, 0, 237, 18, pest[0].SalePrice, pest[0].Mrp),
            new PurchaseLineRequest(pest[1].Id, "EXP-EM22", today.AddMonths(-24), today.AddDays(-9), 4, 0, 230, 18, pest[1].SalePrice, pest[1].Mrp),
            new PurchaseLineRequest(pest[2].Id, "NE-CP24", today.AddMonths(-20), today.AddDays(26), 150, 0, 360, 18, pest[2].SalePrice, pest[2].Mrp),
        }, 0, 0, PaymentMode.Credit, null));

        // Popular items sell more often
        var weights = products.Select(p => p.Category switch { ProductCategory.Fertilizer => 5, ProductCategory.Pesticide => 3, ProductCategory.Seed => 2, ProductCategory.Equipment => 1, _ => 2 }).ToArray();
        Product Pick()
        {
            var r = rnd.Next(weights.Sum());
            for (var i = 0; i < products.Count; i++) { r -= weights[i]; if (r < 0) return products[i]; }
            return products[^1];
        }

        for (var d = days; d >= 0; d--)
        {
            var date = today.AddDays(-d);

            // Restock every 8 days (keeps a couple of items deliberately low for the reorder alert)
            if (d % 8 == 0 && d > 0)
            {
                var low = await db.Products.AsNoTracking().Include(p => p.Batches).ToListAsync();
                foreach (var grp in low.Where(p => p.Batches.Sum(b => b.Quantity) < p.ReorderLevel * 3 && p.Id % 11 != 3).GroupBy(SupplierFor))
                {
                    var lines = grp.Select(p => new PurchaseLineRequest(p.Id, $"B{date:yyMMdd}{p.Id:D2}", date.AddMonths(-1), Expiry(p, date),
                        OpeningQty(p) / 2, p.Unit == "Bag" ? 2 : 0, Cost(p), p.GstRate, p.SalePrice, p.Mrp)).ToList();
                    var supplier = suppliers.First(s => s.Id == grp.Key.Id);
                    var total = lines.Sum(l => l.Quantity * l.Rate * (1 + l.GstRate / 100));
                    await billing.CreatePurchaseAsync(new CreatePurchaseRequest(supplier.Id, $"INV/{billNo++}", date, false, lines, rnd.Next(0, 3) * 250,
                        rnd.NextDouble() < 0.5 ? Math.Round(total) : 0, PaymentMode.Bank, null));
                }
            }

            // Busier in Kharif / Rabi sowing weeks & on market day (Sunday)
            var bills = (int)Math.Round(rnd.Next(4, 11) * richness * (date.DayOfWeek == DayOfWeek.Sunday ? 1.5 : 1));
            for (var b = 0; b < bills; b++)
            {
                var credit = rnd.NextDouble() < 0.33;
                var cust = credit || rnd.NextDouble() < 0.55 ? customers[rnd.Next(customers.Count)] : null;
                var picks = Enumerable.Range(0, rnd.Next(1, 5)).Select(_ => Pick()).DistinctBy(p => p.Id).ToList();
                var items = picks.Select(p => new SaleLineRequest(p.Id, null, p.Unit == "Bag" ? rnd.Next(1, 8) : rnd.Next(1, 4), p.SalePrice,
                    rnd.NextDouble() < 0.15 ? Math.Round(p.SalePrice * 0.03m) : 0)).ToList();
                var est = items.Sum(i => i.Quantity * i.Rate - i.Discount);
                var paid = cust is null || !credit ? est * 2 : Math.Round(est * (decimal)rnd.NextDouble() * 0.06m) * 10;
                var mode = credit && paid == 0 ? PaymentMode.Credit : rnd.NextDouble() < 0.5 ? PaymentMode.Upi : rnd.NextDouble() < 0.9 ? PaymentMode.Cash : PaymentMode.Card;
                var gst = cust?.Gstin is not null || rnd.NextDouble() > 0.08;
                try
                {
                    var inv = await billing.CreateSaleAsync(new CreateSaleRequest(cust?.Id, cust is null ? (rnd.NextDouble() < 0.3 ? $"{FirstNames[rnd.Next(FirstNames.Length)]} (walk-in)" : "Cash Customer") : null,
                        null, null, gst, false, items, paid, mode, credit ? "Crop: " + (cust?.Crops ?? "") : null, date));
                    if (rnd.NextDouble() < 0.01) await billing.CancelSaleAsync(inv.Id, "Wrong product billed – re-issued");
                }
                catch (BusinessException) { /* a pick went out of stock – skip */ }
            }

            // Udhari collections (UPI / cash) – a few farmers pay something most days
            var debtors = await db.Customers.AsNoTracking().Where(c => c.Balance > 300).OrderBy(c => c.Id).ToListAsync();
            foreach (var debtor in debtors.Where(_ => rnd.NextDouble() < 0.07))
            {
                var amt = rnd.NextDouble() < 0.25 ? debtor.Balance : Math.Max(100, Math.Round(debtor.Balance * (decimal)(0.2 + rnd.NextDouble() * 0.4) / 100) * 100);
                await billing.RecordPaymentAsync(new CreatePaymentRequest(PartyType.Customer, debtor.Id, Math.Min(amt, debtor.Balance), rnd.NextDouble() < 0.6 ? PaymentMode.Upi : PaymentMode.Cash,
                    rnd.NextDouble() < 0.6 ? $"UPI{rnd.Next(10000000, 99999999)}" : null, "Udhari jama", date));
            }
            // Supplier payments weekly
            if (d % 7 == 3)
                foreach (var s in await db.Suppliers.AsNoTracking().Where(s => s.Balance > 10000).ToListAsync())
                    await billing.RecordPaymentAsync(new CreatePaymentRequest(PartyType.Supplier, s.Id, Math.Round(s.Balance * 0.5m / 1000) * 1000, PaymentMode.Bank, $"NEFT{rnd.Next(100000, 999999)}", null, date));
        }
    }

    /// <summary>A few more subscriber shops so the vendor console looks like a real business (trial, paid, expiring, expired, suspended).</summary>
    private static async Task SeedOtherShopsAsync(AppDbContext db, IConfiguration cfg, SystemUser sys, ILogger logger)
    {
        var shops = new (string Shop, string Owner, string Mobile, string District, SubscriptionPlan Plan, int StartedDaysAgo, int EndsInDays, bool Active)[]
        {
            ("Jai Kisan Krushi Kendra", "Vijay Kolhe", "9890011101", "Ahilyanagar", SubscriptionPlan.Trial, 40, 142, true),
            ("Om Sai Agro Agencies", "Rahul Bhujbal", "9890011102", "Nashik", SubscriptionPlan.Pro, 220, 145, true),
            ("Shetkari Mitra Krushi Seva Kendra", "Amol Lande", "9890011103", "Solapur", SubscriptionPlan.Trial, 168, 12, true),
            ("Gajanan Fertilizers & Seeds", "Pandurang Wagh", "9890011104", "Jalgaon", SubscriptionPlan.Trial, 195, -12, true),
            ("Baliraja Agro Center", "Sagar Chavan", "9890011105", "Beed", SubscriptionPlan.Basic, 300, 65, false),
        };
        var auth = new AuthService(db, cfg);
        var rnd = new Random(77);
        foreach (var s in shops)
        {
            sys.TenantId = null; sys.Role = UserRole.SuperAdmin;
            var t = await auth.OnboardTenantAsync(new RegisterShopRequest(s.Shop, s.Owner, s.Mobile, "demo@123", null, null, s.District, null));
            t.Plan = s.Plan; t.IsActive = s.Active;
            t.SubscriptionStart = AppClock.Today.AddDays(-s.StartedDaysAgo);
            t.SubscriptionEnd = AppClock.Today.AddDays(s.EndsInDays);
            t.CreatedAt = t.SubscriptionStart;
            t.InvoicePrefix = new string(s.Shop.Split(' ').Select(w => w[0]).Take(3).ToArray()).ToUpperInvariant();
            await db.SaveChangesAsync();

            sys.TenantId = t.Id; sys.Role = UserRole.Owner; sys.UserName = s.Mobile;
            var billing = new BillingService(db, sys);
            try
            {
                var endDay = s.EndsInDays < 0 ? -s.EndsInDays : 0;   // expired shops stopped billing when the plan ended
                await SeedShopHistoryAsync(db, billing, rnd, AppClock.Today.AddDays(-endDay), days: 14, farmerCount: 12, richness: 0.4);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Partial seed for {Shop}", s.Shop); }
        }
        sys.TenantId = null; sys.Role = UserRole.SuperAdmin; sys.UserName = "system";
        logger.LogInformation("Seeded {Count} additional subscriber shops (password demo@123)", shops.Length);
    }
}
