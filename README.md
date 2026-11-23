# 🌾 Krushi Prime — Smart Billing… Smart Business…

**🌐 Live demo:** https://rahul96a.github.io/krushi-prime/ (sample data · shop login `9999999999` / `demo@123` · admin `admin` / `Admin@123`)

**Billing and shop management for Krushi Seva Kendras (agri-input shops).** It is multi-tenant, so one deployment serves every shop you sign up. Each new shop gets a **6-month free trial** automatically.

| | |
|---|---|
| **Backend** | .NET 8 Web API · EF Core 8 · JWT · Clean layered architecture (Domain / Infrastructure / Api) |
| **Frontend** | React 19 + TypeScript + Vite + Tailwind 4 · responsive (desktop counter + mobile) · English / मराठी |
| **Database** | **Azure SQL** (serverless, free offer) in production · SQLite for zero-setup local demos |
| **Cloud** | Azure App Service (F1 free) · Azure SQL free offer · Application Insights · Bicep IaC · GitHub Actions |

---

## ✅ Features (mapped to the brochure)

| Brochure promise | What's built |
|---|---|
| **Fast Billing (One Click Bill)** | Keyboard-first POS (F2 search, Enter add, F9 save), barcode / Marathi-name search, auto-FEFO batch pick, live GST preview, Save & Print / Save & WhatsApp |
| **GST & Non-GST Billing** | Tax Invoice with CGST/SGST or IGST split, Bill of Supply / estimate mode, tax-inclusive (MRP) or exclusive pricing, round-off, amount in words, UPI QR on bill, **A4 and 80 mm thermal** print layouts |
| **Smart Stock Management** | Batch + expiry tracking, **FEFO** (expired stock can never be billed), purchase inward with free qty, stock adjustments with audit trail, low-stock and expiry alerts |
| **Udhari हिशोब (Easy / Automatic)** | Credit sales post to the farmer's ledger automatically, partial payments, credit limits, ageing ("days since last payment"), payment receipts by Cash/UPI/Bank/Cheque |
| **WhatsApp Message Support (FREE)** | Bill and udhari-reminder messages in Marathi + English through free click-to-chat links. Optional auto-send via the Meta WhatsApp Cloud API |
| **Customer & Purchase Reports** | Dashboard, sales (day-wise, by payment mode), **GST report (rate-wise, HSN summary CSV for GSTR-1, B2B list, input tax)**, profit, stock valuation, expiry, udhari, customer & supplier ledgers, CSV export everywhere |
| **Full Shop Management** | Products (fertilizer / seed / pesticide categories, HSN, GST, licences on invoice), farmers (village, crops, acreage), suppliers, purchases, staff users with roles (Owner / Manager / Cashier) |
| **6 months free** | Self sign-up or executive onboarding creates the shop with `SubscriptionEnd = today + 6 months`. When that date passes, the shop can still read its data but cannot create new records until the plan is renewed |
| **Vendor (your) console** | Super-admin: all shops, usage (bills, billed amount, last activity), extend/renew plans, suspend, reset owner password, onboard from the field |

## 💳 Online payments – "Pay bill" (Cashfree · NTT DATA)

Farmers pay udhari or a bill balance online (UPI, cards, net-banking) from a link sent on WhatsApp or a QR code at the counter. Each shop connects **its own** merchant account, so money settles straight to the shop's bank. Confirmed payments are posted to the farmer ledger and the invoice automatically, exactly once.

| | Cashfree | NTT DATA (Atom AIPAY) | Demo simulator |
|---|---|---|---|
| How it works | Payment Links API (`/pg/links`, api-version 2025-01-01) | Server AUTH → `atomTokenId` → hosted checkout (`atomcheckout.js`) | Practice page, no real money |
| Confirmation | Webhook `PAYMENT_LINK_EVENT`, HMAC-SHA256 verified, then status re-fetched from Cashfree · plus "Check" button | Encrypted `encData` posted to the return URL (AES-256 / PBKDF2-SHA512) | Button |
| Setup | Settings → Online payments → App ID + Secret; add the webhook URL in the Cashfree dashboard | Settings → Online payments → Merchant ID, password, product, request/response keys | One click |
| Verified in this build | Requests reach Cashfree sandbox (needs your sandbox keys for a full test) | Real UAT token created and checkout opened (sandbox merchant 317159) | Full flow |

* **Where it appears:** a 🔗 Pay link button on Udhari, the farmer ledger and invoices. There's also an **Online Payments** page (status, check, share, simulate) and **Renew online** for the Krushi Prime subscription, which works even after a plan expires.
* **Subscription renewals** go to *your* (vendor) account: set `Payments:Platform:*` and edit the prices in `Payments:Plans`. The amounts shipped (₹2,999 / ₹4,999) are placeholders.
* **Security:** gateway secrets are encrypted at rest (AES-256-GCM) and the UI only ever shows them masked. Cashfree webhooks with a bad signature are rejected, a concurrency token stops duplicate notifications from double-posting, and the public pay page is rate-limited.
* `Payments:AllowSimulator` is on in Development and turned off in Production unless demo data is enabled. Turn it off before real shops go live.

## 🚀 Demo in 2 minutes (your laptop)

Prerequisites: **.NET 8 SDK** and **Node 20+**.

```powershell
# Windows
./scripts/run-demo.ps1
```
```bash
# macOS / Linux
./scripts/run-demo.sh
```

Open **http://localhost:5080**. A SQLite database with a demo shop is created on first start. The demo shop has about 90 bills from the last 3 weeks, farmers with udhari, 16 real-world products and a batch that is close to expiry.

| Login | Username | Password |
|---|---|---|
| Demo shop owner | `9999999999` | `demo@123` |
| Vendor super-admin | `admin` | `Admin@123` |

To reset the demo, stop the app, delete `backend/src/KrushiPrime.Api/krushiprime.db` and start again.

👉 For a step-by-step walkthrough to use in front of a shop owner, see **[docs/DEMO-SCRIPT.md](docs/DEMO-SCRIPT.md)**.

### Shareable web demo (no server needed)
```bash
cd frontend && npm run build:demo   # → dist-demo/index.html: one self-contained page with sample data that runs fully in the browser
```

### Developer mode (hot reload)
```bash
cd backend/src/KrushiPrime.Api && dotnet run        # API + Swagger → http://localhost:5080/swagger
cd frontend && npm install && npm run dev            # UI → http://localhost:5173 (proxies /api)
```

## ☁️ Deploy to Azure (free tier)

### ⭐ Recommended: ₹0/month — Azure Container Apps + Azure SQL free offer (Cloud Shell, no local tools)
1. Build the package: `dotnet publish backend/src/KrushiPrime.Api -c Release -o publish` (with the React build in `wwwroot`) → zip `publish/*` as `app.zip`; compile `infra/containerapps.bicep` to `containerapps.json`; add `scripts/deploy-azure-free.sh` as `deploy.sh` (a ready-made `krushiprime-azure-free.zip` is provided with each release).
2. portal.azure.com → **Cloud Shell (Bash)** → upload the zip → `unzip krushiprime-azure-free.zip -d kp && bash kp/deploy.sh`

How it stays free: Container Apps consumption gives a monthly free grant (180k vCPU-s, 360k GiB-s, 2M requests) and the app **scales to zero** when idle. No container registry is needed, because the stock `mcr.microsoft.com/dotnet/aspnet:8.0` image runs the published app from an Azure Files share. Logs are capped inside the 5 GB free allowance. The trade-off is that the first visit after a quiet period takes about 10–40 s to wake up. Updates: `bash kp/update.sh <resource-group>`.

### Option A — App Service script (F1 free has 60 CPU-min/day; B1 is paid)
```bash
az login
./scripts/deploy-azure.sh krushiprime-rg centralindia
```
The script provisions everything in `infra/main.bicep`, builds the app, deploys it, and prints the URL and the generated secrets.

### Option B — GitHub Actions
1. Deploy the infrastructure once: `az deployment group create -g <rg> -f infra/main.bicep -p infra/main.parameters.json -p sqlAdminPassword=… jwtKey=… superAdminPassword=…`
2. In the repo, add the secret `AZURE_WEBAPP_PUBLISH_PROFILE` and the variable `AZURE_WEBAPP_NAME`.
3. Push to `main`. `.github/workflows/ci-cd.yml` builds the UI, runs the tests, publishes and deploys.

### Option C — Docker (any cloud / on-prem server)
```bash
docker compose up --build      # app + SQL Server 2022 → http://localhost:8080
```

### What the free tier really means (read before going live)
| Resource | Free allowance | Watch out |
|---|---|---|
| App Service **F1** | 1 GB RAM, 60 CPU-minutes/day | Sleeps when idle, so the first hit after a pause is slow. No custom domain or SSL binding. **Move to B1 for paying shops** (change `appServiceSku`) |
| Azure SQL **free offer** | 100,000 vCore-seconds + 32 GB per database each month, for the life of the subscription | Roughly 28 vCore-hours a month, shared by all shops in this one database. With the default `AutoPause`, the database **stops until next month** when the allowance runs out. Set `sqlFreeLimitExhaustionBehavior=BillOverUsage` once real shops bill every day |
| Application Insights | 5 GB ingestion/month | The template caps the daily quota at 1 GB |
| WhatsApp (Link mode) | Unlimited, free | The shop taps Send in WhatsApp. Auto-send through the Cloud API is billed by Meta per conversation |

> **Recommendation:** use F1 + the free SQL offer for demos and the first 2–3 pilot shops. Before you onboard more shops, move to B1 and `BillOverUsage`. The cost is still small because the database is serverless and pauses at night.

## 🏗️ Architecture

```
 Browser (React SPA, EN/मराठी)  ──HTTPS/JWT──►  Azure App Service (.NET 8)
   POS · Reports · Admin                          ├─ Controllers (REST + Swagger)
                                                  ├─ SubscriptionMiddleware (trial / renewal gate)
                                                  ├─ BillingService  (GST, FEFO, udhari, purchases)
                                                  ├─ ReportService   (dashboard, GST, ledgers)
                                                  ├─ WhatsAppService (wa.me link / Cloud API)
                                                  └─ EF Core ──► Azure SQL (serverless, free offer)
                                                                 one DB, row-level tenant isolation
```

* **Multi-tenancy:** every business row carries a `TenantId`. An EF Core global query filter reads it from the JWT, so one shop cannot read another shop's data, even through a bug in a controller.
* **Consistency:** a bill, its stock movement, the batch quantity change, the udhari posting and the invoice number are saved in one transaction. The transaction works with Azure SQL's retrying execution strategy, and optimistic concurrency on the invoice counter prevents duplicate numbers.
* **Money:** decimals everywhere, GST rounded half away from zero per line, invoice rounded to the nearest rupee.
* **Dates:** business dates are kept in IST (`AppClock`) so App Service in UTC never puts a bill on the wrong day.

```
KrushiPrime/
├─ backend/
│  ├─ src/KrushiPrime.Domain/          entities, enums, IST clock
│  ├─ src/KrushiPrime.Infrastructure/  DbContext, migrations, services, seed data
│  ├─ src/KrushiPrime.Api/             controllers, auth, middleware, Program.cs
│  ├─ tests/KrushiPrime.Tests/         GST + FEFO unit tests (xUnit)
│  └─ db/krushiprime-azure-sql.sql     idempotent schema script for DBAs
├─ frontend/                           React app (src/pages, src/components, src/lib)
├─ infra/main.bicep                    Azure IaC (free tier defaults)
├─ scripts/                            run-demo, deploy-azure, smoke_test.py
├─ docs/DEMO-SCRIPT.md                 sales demo walkthrough
├─ Dockerfile · docker-compose.yml · .github/workflows/ci-cd.yml
```

## ⚙️ Configuration (`appsettings.json` / App Service settings)

| Key | Default | Notes |
|---|---|---|
| `Database:Provider` | `Sqlite` (dev) / `SqlServer` (prod) | Provider switch. Production applies EF migrations automatically on startup |
| `ConnectionStrings:Default` | — | Azure SQL connection string (set by Bicep) |
| `Jwt:Key` | dev key | **Required in production**, 32+ characters. Keep it in Key Vault or a pipeline secret |
| `Seed:DemoData` | `true` | Creates the demo shop. Turn it off when real customers start using the system |
| `Seed:SuperAdminPassword` | `Admin@123` | **Change before going live** |
| `WhatsApp:Mode` | `Link` | `CloudApi` + `PhoneNumberId` + `AccessToken` sends messages automatically |
| `Support:Phone` | `8668375712` | Shown on expiry and suspension messages |

## 🔐 Security checklist
- [x] PBKDF2 password hashing (ASP.NET Core Identity hasher), JWT with expiry, role policies
- [x] Tenant isolation through global query filters; super-admin endpoints are separate
- [x] Rate-limited login and sign-up (10 per minute per IP), HTTPS only, TLS 1.2+, FTPS disabled, security headers
- [x] RFC 7807 errors with no stack traces in production; Swagger turned off in production
- [ ] Before scale: move secrets to Key Vault references, switch SQL to Entra ID / managed identity, add a private endpoint (B1+)

## 🧪 Tests
```bash
cd backend && dotnet test                     # GST maths + FEFO rules
python3 scripts/smoke_test.py                 # 40+ end-to-end API checks against a running instance
```

## 🗺️ Roadmap ideas
Sales returns / credit notes · e-Invoice (IRN) and e-Way bill · Tally export · offline PWA mode · SMS fallback · multi-branch shops · subscription payments through Razorpay · Hindi UI · fertilizer POS (iFMS) reconciliation.

---
**Krushi Prime** · आपला विश्वास – आमची साथ! · 📞 8668375712
