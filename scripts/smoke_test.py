#!/usr/bin/env python3
"""End-to-end smoke test for the Krushi Prime API. Usage: python3 smoke_test.py [base_url]"""
import json, sys, urllib.request, urllib.error, random

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5080"
TOKEN = None

def call(method, path, body=None, expect=200, token=None):
    req = urllib.request.Request(BASE + path, method=method, data=json.dumps(body).encode() if body is not None else None,
                                 headers={"Content-Type": "application/json", **({"Authorization": "Bearer " + (token or TOKEN)} if (token or TOKEN) else {})})
    try:
        with urllib.request.urlopen(req) as r:
            status, raw = r.status, r.read()
    except urllib.error.HTTPError as e:
        status, raw = e.code, e.read()
    data = json.loads(raw) if raw else None
    ok = status == expect if isinstance(expect, int) else status in expect
    print(("  PASS " if ok else "  FAIL ") + f"{method} {path} → {status}" + ("" if ok else f"  {data}"))
    if not ok: sys.exit(1)
    return data

print("Auth")
TOKEN = call("POST", "/api/auth/login", {"username": "9999999999", "password": "demo@123"})["token"]
call("POST", "/api/auth/login", {"username": "9999999999", "password": "wrong"}, 401, token="x")

print("Masters")
products = call("GET", "/api/products?batches=true")
urea = next(p for p in products if p["name"].startswith("Urea"))
pest = next(p for p in products if p["category"] == "Pesticide")
seed = next(p for p in products if p["category"] == "Seed")
mob = f"9{random.randint(100000000, 999999999)}"
cust = call("POST", "/api/customers", {"name": "Test Farmer", "mobile": mob, "village": "Shirur", "openingBalance": 500, "creditLimit": 0})
assert cust["balance"] == 500

print("Sales: GST bill with udhari, FEFO, tax split")
stock_before = urea["stock"]
sale = call("POST", "/api/sales", {"customerId": cust["id"], "isGst": True, "isInterState": False, "paymentMode": "Cash", "paidAmount": 1000,
    "items": [{"productId": urea["id"], "quantity": 2, "rate": urea["salePrice"]}, {"productId": pest["id"], "quantity": 1, "rate": pest["salePrice"]},
              {"productId": seed["id"], "quantity": 1, "rate": seed["salePrice"]}]}, 201)
inv = call("GET", f"/api/sales/{sale['id']}")["invoice"]
expected = round(2 * urea["salePrice"] + pest["salePrice"] + seed["salePrice"])
assert inv["grandTotal"] == expected, (inv["grandTotal"], expected)
assert abs(inv["cgstAmount"] - inv["sgstAmount"]) <= 0.01 and inv["igstAmount"] == 0
seed_line = next(i for i in inv["items"] if i["productId"] == seed["id"])
assert seed_line["cgst"] == 0, "Seeds are 0% GST"
assert inv["balanceAmount"] == expected - 1000 and inv["status"] == "Partial"
assert call("GET", f"/api/products/{urea['id']}")["stock"] == stock_before - 2
c = call("GET", f"/api/customers/{cust['id']}")
assert c["balance"] == 500 + expected - 1000
print(f"  invoice {inv['invoiceNo']} total ₹{inv['grandTotal']} CGST {inv['cgstAmount']} SGST {inv['sgstAmount']}")

print("Validation rules")
call("POST", "/api/sales", {"isGst": True, "paymentMode": "Credit", "paidAmount": 0, "items": [{"productId": urea["id"], "quantity": 1, "rate": 100}]}, 400)
call("POST", "/api/sales", {"isGst": True, "paymentMode": "Cash", "paidAmount": 0, "customerName": "Walk-in", "items": [{"productId": urea["id"], "quantity": 99999, "rate": 100}]}, 400)
nongst = call("POST", "/api/sales", {"isGst": False, "paymentMode": "Upi", "paidAmount": 99999, "customerName": "Walk-in", "items": [{"productId": pest["id"], "quantity": 1, "rate": 300}]}, 201)
assert nongst["grandTotal"] == 300 and nongst["status"] == "Paid"

print("Udhari payment + ledger + WhatsApp")
call("POST", "/api/payments", {"partyType": "Customer", "partyId": cust["id"], "amount": 200, "mode": "Upi", "reference": "UPI123"})
ledger = call("GET", f"/api/customers/{cust['id']}/ledger")
assert ledger["closingBalance"] == call("GET", f"/api/customers/{cust['id']}")["balance"]
wa = call("POST", f"/api/whatsapp/invoice/{sale['id']}")
assert wa["url"].startswith("https://wa.me/91" + mob)
call("POST", f"/api/whatsapp/reminder/{cust['id']}")

print("Cancel invoice restores stock and udhari")
bal = call("GET", f"/api/customers/{cust['id']}")["balance"]
call("POST", f"/api/sales/{sale['id']}/cancel", {"reason": "Wrong item"})
assert call("GET", f"/api/products/{urea['id']}")["stock"] == stock_before
assert call("GET", f"/api/customers/{cust['id']}")["balance"] == bal - inv["balanceAmount"]

print("Purchase inward")
sup = call("GET", "/api/suppliers")[0]
call("POST", "/api/purchases", {"supplierId": sup["id"], "billNo": "T-1", "isInterState": False, "otherCharges": 0, "paidAmount": 0, "paymentMode": "Credit",
    "items": [{"productId": urea["id"], "batchNo": "NEW1", "expiryDate": "2028-01-01", "quantity": 10, "freeQuantity": 1, "rate": 240, "gstRate": 5}]})
assert call("GET", f"/api/products/{urea['id']}")["stock"] == stock_before + 11

print("Reports")
for r in ["dashboard", "sales", "gst", "stock", "expiry?days=60", "udhari", "profit", "purchases"]:
    call("GET", f"/api/reports/{r}")

print("Self registration (6-month free trial) & licensing")
shop_mobile = f"8{random.randint(100000000, 999999999)}"
reg = call("POST", "/api/auth/register-shop", {"shopName": "Jai Kisan Krushi Kendra", "ownerName": "Test Owner", "mobile": shop_mobile, "password": "secret123"}, token="x")
assert reg["user"]["tenant"]["plan"] == "Trial" and reg["user"]["tenant"]["daysLeft"] >= 180
own_products = call("GET", "/api/products", token=reg["token"])
assert len(own_products) > 0 and all(p["stock"] == 0 for p in own_products), "tenant isolation / starter catalogue"
call("GET", "/api/admin/tenants", expect=403, token=reg["token"])

admin = call("POST", "/api/auth/login", {"username": "admin", "password": "Admin@123"}, token="x")["token"]
tenants = call("GET", "/api/admin/tenants", token=admin)
new_t = next(t for t in tenants if t["mobile"] == shop_mobile)
call("PUT", f"/api/admin/tenants/{new_t['id']}/subscription", {"plan": "Trial", "subscriptionEnd": "2020-01-01"}, token=admin)
call("POST", "/api/customers", {"name": "X", "openingBalance": 0, "creditLimit": 0}, 402, token=reg["token"])
call("GET", "/api/reports/dashboard", token=reg["token"])  # read-only still allowed
call("PUT", f"/api/admin/tenants/{new_t['id']}/subscription", {"plan": "Pro", "extendMonths": 12}, token=admin)
call("POST", "/api/customers", {"name": "X", "openingBalance": 0, "creditLimit": 0}, 200, token=reg["token"])
print("\nALL SMOKE TESTS PASSED ✅")
