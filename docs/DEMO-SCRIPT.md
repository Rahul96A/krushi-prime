# Krushi Prime – Client Demo Script (10–12 minutes)

**Before you leave for the client:** run `scripts/run-demo.ps1`, open http://localhost:5080, log in once, and keep your phone handy for the WhatsApp step. To start from a clean demo, delete `krushiprime.db` and start the app again.

---

## 1. Opening (1 min) — Login screen
> "नमस्कार! हे Krushi Prime – खास कृषी सेवा केंद्रासाठी बनवलेले बिलिंग सॉफ्टवेअर. पहिले **६ महिने पूर्णपणे मोफत**, installation मोफत, WhatsApp मोफत."

- Point to the brochure features on the left. They are what the shop will actually use.
- Click **Use demo shop** → **Login**.

## 2. Dashboard (1 min) — "Your shop on one screen"
- Today's sales, today's collection, this month's sales and profit, **total udhari**.
- Alerts: **low stock** and **batch expiring in 30 days**. *"No more expired pesticide sitting on the shelf."*
- Switch the language with **मराठी** (top right) to show the Marathi interface.

## 3. Fast billing (3 min) — the moment that sells
1. Press **F2** and type `urea`, then press Enter. Type `imida`, Enter. Type `soybean`, Enter.
   - *"Search works by name, company, barcode, or the Marathi name."*
2. In the Batch column, point out **Auto (FEFO)**: the system sells the batch that expires first and **never bills expired stock**.
3. Customer: type `ramesh` and select him. His **previous udhari** appears straight away.
4. Point out the GST split: fertilizer 5%, pesticide 18%, seed 0%. CGST/SGST are calculated automatically, and **Non-GST Bill** is one click away.
5. Choose **Cash** and enter `3000`. The red box shows the **udhari on this bill**.
6. Click **Save Bill & Print**. Show the A4 invoice with **licence numbers, HSN, batch/expiry and the UPI QR code**, then switch to **Thermal 80mm**.
7. Click **WhatsApp**. The bill opens in WhatsApp, ready to send to the farmer.

## 4. Udhari हिशोब (2 min)
- Open **Udhari** to see every farmer's dues, last payment date and ageing badge.
- Click **💬 Remind** to send a polite Marathi + English reminder with the UPI ID.
- Click **💰 Receive** and record ₹1,000 by UPI. The balance updates at once.
- Open **Farmers**, click the name, and show the **ledger** with every bill and payment and the running balance.

## 4b. Online "Pay bill" (1 min) — the wow moment
- In **Udhari**, tap **🔗 Pay link** for a farmer. A QR code and a **Send on WhatsApp** button appear.
- Scan the QR with your phone. The farmer sees a clean pay page with the shop's name and the amount.
- (Demo simulator) Tap **Pay**, then open **Online Payments**: the status shows **Paid** and the farmer's udhari has dropped automatically.
- *"With Cashfree or NTT DATA connected, the farmer pays by UPI and the money comes straight to your bank. No more chasing udhari."*

## 5. Stock & purchases (1.5 min)
- **Stock & Expiry → Expiry alerts**: batch-wise expiry list.
- **Purchases → New purchase**: choose the supplier, add a product, enter batch, expiry and quantity, then save. Stock and batches update automatically.

## 6. Reports & GST (1.5 min)
- **Reports → GST report**: rate-wise summary, the **HSN summary CSV ready for GSTR-1**, and the B2B list. *"Hand this to your CA; nothing has to be typed again."*
- **Profit**: product-wise margin (visible to the owner only).

## 7. Close (1 min)
- **Settings**: shop details, licences, UPI ID, staff logins (Cashier and Manager roles).
- *"Everything runs on Microsoft Azure cloud, so it works on a laptop or a mobile and your data is backed up."*
- **Offer:** 6 months free → Full shop management · Free installation & setup · WhatsApp messaging FREE · 1 year support FREE.
- **Sign them up on the spot:** log out → **Register Shop – 6 Months FREE** → enter the shop name, owner and mobile. The shop is live in 10 seconds with a ready product list.
  (Or onboard them from **Admin Console → + Onboard new shop**.)

---

## Handling common questions
| Question | Answer |
|---|---|
| "Internet गेले तर?" | It works on mobile data or a hotspot, and the bill page is light. Offline mode is on the roadmap. |
| "माझा जुना data?" | We import your product and farmer lists from Excel during free setup. |
| "6 महिन्यांनंतर?" | Your data stays safe. Renew to keep billing; reports stay readable even if you don't. |
| "Printer?" | Any A4 printer or any 80 mm thermal printer (USB or Bluetooth) that works with the browser's print dialog. |
| "Staff चोरी/चूक?" | Each staff member gets their own login. Cashiers cannot cancel bills or see profit. Every stock change is logged. |
| "GST rates बदलले तर?" | The GST rate is editable per product and applies from the next bill. |

## Vendor console (for you)
Log in as `admin` to see every shop, its trial days left, number of bills and last activity. From there you can **Renew** (Basic/Pro, extend 1–24 months), **Suspend** a shop, or **Reset** the owner's password.
