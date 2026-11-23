using KrushiPrime.Infrastructure.Payments;
using KrushiPrime.Infrastructure.Persistence;
using KrushiPrime.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KrushiPrime.Infrastructure;

public static class DependencyInjection
{
    /// <param name="cfg">Database:Provider = "SqlServer" (Azure SQL, production) or "Sqlite" (local demo / offline).</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration cfg)
    {
        var provider = cfg["Database:Provider"] ?? "Sqlite";
        services.AddDbContext<AppDbContext>(o =>
        {
            if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
                o.UseSqlServer(cfg.GetConnectionString("Default"), sql =>
                {
                    sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null); // Azure SQL serverless auto-resume
                    sql.CommandTimeout(60);
                });
            else
                o.UseSqlite(cfg.GetConnectionString("Sqlite") ?? "Data Source=krushiprime.db");
        });

        services.Configure<WhatsAppOptions>(cfg.GetSection("WhatsApp"));
        services.AddHttpClient<IWhatsAppService, WhatsAppService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IReportService, ReportService>();

        // Online payments (Cashfree / NTT DATA / demo simulator)
        services.AddSingleton<SecretProtector>();
        services.AddHttpClient<CashfreeGateway>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<NttDataGateway>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<IPaymentGateway>(sp => sp.GetRequiredService<CashfreeGateway>());
        services.AddScoped<IPaymentGateway>(sp => sp.GetRequiredService<NttDataGateway>());
        services.AddScoped<IPaymentGateway, SimulatorGateway>();
        services.AddScoped<IPaymentGateway, UpiDirectGateway>();
        services.AddScoped<IOnlinePaymentService, OnlinePaymentService>();
        return services;
    }
}
