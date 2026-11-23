using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using KrushiPrime.Api.Infrastructure;
using KrushiPrime.Infrastructure;
using KrushiPrime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

if (string.IsNullOrWhiteSpace(cfg["Jwt:Key"]) || cfg["Jwt:Key"]!.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be configured (min 32 chars). Use App Service settings / Key Vault in production.");

// ── Services ──────────────────────────────────────────────
builder.Services.AddInfrastructure(cfg);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddMemoryCache();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = cfg["Jwt:Issuer"],
        ValidateAudience = true, ValidAudience = cfg["Jwt:Audience"],
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromMinutes(2),
        NameClaimType = System.Security.Claims.ClaimTypes.Name, RoleClaimType = System.Security.Claims.ClaimTypes.Role,
    };
});
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Policies.SuperAdmin, p => p.RequireRole("SuperAdmin"));
    o.AddPolicy(Policies.ShopUser, p => p.RequireRole("Owner", "Manager", "Cashier").RequireClaim("tenant_id"));
    o.AddPolicy(Policies.ShopManager, p => p.RequireRole("Owner", "Manager").RequireClaim("tenant_id"));
    o.AddPolicy(Policies.ShopOwner, p => p.RequireRole("Owner").RequireClaim("tenant_id"));
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins((cfg["Cors:Origins"] ?? "http://localhost:5173").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    .AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("public", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    // Azure App Service terminates TLS at its front end; trust X-Forwarded-* so payment callback URLs are https
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});
builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(cfg["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Krushi Prime API", Version = "v1", Description = "Smart Billing… Smart Business… — Krushi Seva Kendra ERP" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() } });
});

var app = builder.Build();

// ── Database & seed ───────────────────────────────────────
await SeedData.InitializeAsync(app.Services, cfg, app.Logger);
_ = Task.Run(async () =>
{
    try { await SeedData.SeedDemoAsync(app.Services, cfg, app.Logger); }
    catch (Exception ex) { app.Logger.LogError(ex, "Demo data seeding failed"); }
});

// ── Pipeline ──────────────────────────────────────────────
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseResponseCompression();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});
if (!app.Environment.IsDevelopment()) app.UseHsts();

if (cfg.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.DocumentTitle = "Krushi Prime API");
}

app.UseDefaultFiles();   // React build can be served from wwwroot → single free App Service
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<SubscriptionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
