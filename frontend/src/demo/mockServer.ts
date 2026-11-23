/* eslint-disable @typescript-eslint/no-explicit-any */
/**
 * Browser-only demo backend. Used when the app is built with VITE_DEMO=1 (e.g. for a shareable web demo link
 * before Azure is provisioned). It mirrors the .NET API contract and its business rules (GST, FEFO, udhari),
 * starting from a snapshot of the seeded demo shop. Data lives in the browser (localStorage).
 */
import snapshot from './snapshot.json'
import { safeStorage } from '../lib/api'

type Any = any
const KEY = 'kp.demo.state.v2'
const DEMO_UPI = '8888595653@ybl' // vendor's PhonePe for live UPI demos – editable in Settings → Billing preferences
const r2 = (v: number) => Math.round((v + Number.EPSILON) * 100) / 100
const DAY = 86400000

// ── dates: normalise .NET strings to ISO and shift the snapshot so "today" is always today ──
const pad = (n: number) => String(n).padStart(2, '0')
const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
const startOfDay = (d: Date) => new Date(d.getFullYear(), d.getMonth(), d.getDate())
const now = () => new Date()
const today = () => startOfDay(now())
const dateKey = /(date|At|Start|End)$/i

function prepare(raw: Any): Any {
  const shiftDays = Math.round((today().getTime() - new Date(raw.snapshotDate + 'T00:00:00').getTime()) / DAY)
  const walk = (o: Any): Any => {
    if (Array.isArray(o)) return o.map(walk)
    if (o && typeof o === 'object') {
      const out: Any = {}
      for (const [k, v] of Object.entries(o)) {
        if (typeof v === 'string' && dateKey.test(k) && /^\d{4}-\d{2}-\d{2}/.test(v)) {
          const d = new Date(v.replace(' ', 'T').replace(/(\.\d{3})\d+/, '$1'))
          out[k] = iso(new Date(d.getTime() + shiftDays * DAY))
        } else out[k] = walk(v)
      }
      return out
    }
    return o
  }
  const s = walk(raw)
  delete s.snapshotDate
  s.shop.upiId = DEMO_UPI
  s.paySettings = { provider: 'UpiDirect', liveMode: true }
  s.seq = 100000
  return s
}

let state: Any = (() => {
  try { const saved = localStorage.getItem(KEY); if (saved) return JSON.parse(saved) } catch { /* storage unavailable */ }
  return prepare(snapshot)
})()
const persist = () => { try { localStorage.setItem(KEY, JSON.stringify(state)) } catch { /* quota / private mode */ } }
export const resetDemo = () => { try { localStorage.removeItem(KEY) } catch { /* ignore */ } state = prepare(snapshot) }
const nextId = () => ++state.seq

class HttpError extends Error { status: number; constructor(status: number, msg: string) { super(msg); this.status = status } }
const bad = (msg: string, status = 400): never => { throw new HttpError(status, msg) }

// ── auth ──
const session = () => safeStorage.getItem('kp.token')
const daysLeft = () => Math.ceil((startOfDay(new Date(state.shop.subscriptionEnd)).getTime() - today().getTime()) / DAY)
const tenantSummary = () => ({ id: 1, shopName: state.shop.shopName, plan: state.shop.plan === 0 ? 'Trial' : typeof state.shop.plan === 'string' ? state.shop.plan : ['Trial', 'Basic', 'Pro', 'Enterprise'][state.shop.plan] ?? 'Trial', subscriptionEnd: state.shop.subscriptionEnd, daysLeft: Math.max(daysLeft(), 0), isActive: true, isExpired: daysLeft() < 0 })
const owner = () => ({ id: 2, fullName: state.shop.ownerName, username: '9999999999', role: 'Owner', tenant: tenantSummary() })
const admin = { id: 1, fullName: 'Krushi Prime Admin', username: 'admin', role: 'SuperAdmin' }
const me = () => (session() === 'demo-admin' ? admin : owner())

// ── helpers mirroring the server ──
const product = (id: number) => state.products.find((p: Any) => p.id === id) ?? bad('Product not found.', 404)
const customer = (id: number) => state.customers.find((c: Any) => c.id === id) ?? bad('Customer not found.', 404)
const stockOf = (p: Any) => r2(p.batches.reduce((s: number, b: Any) => s + b.quantity, 0))
const nearestExpiry = (p: Any) => p.batches.filter((b: Any) => b.quantity > 0 && b.expiryDate).map((b: Any) => b.expiryDate).sort()[0] ?? null
const mapProduct = (p: Any, withBatches: boolean) => ({ ...p, stock: stockOf(p), nearestExpiry: nearestExpiry(p), batches: withBatches ? p.batches.filter((b: Any) => b.quantity !== 0).sort((a: Any, b: Any) => (a.expiryDate ?? '9') < (b.expiryDate ?? '9') ? -1 : 1) : undefined })
const listItem = (i: Any) => ({ id: i.id, invoiceNo: i.invoiceNo, invoiceDate: i.invoiceDate, customerName: i.customerName, customerMobile: i.customerMobile, isGst: i.isGst, grandTotal: i.grandTotal, paidAmount: i.paidAmount, balanceAmount: i.balanceAmount, paymentMode: i.paymentMode, status: i.status, itemCount: i.items.length })
const inRange = (d: string, from: Date, to: Date) => { const x = new Date(d); return x >= from && x < new Date(to.getTime() + DAY) }
const activeSales = (from: Date, to: Date) => state.invoices.filter((i: Any) => i.status !== 'Cancelled' && inRange(i.invoiceDate, from, to))
const like = (v: string | null | undefined, q: string) => !!v && v.toLowerCase().includes(q.toLowerCase())
const fy = (d: Date) => { const s = d.getMonth() >= 3 ? d.getFullYear() : d.getFullYear() - 1; return `${s}-${pad((s + 1) % 100)}` }
const sum = (a: Any[], f: (x: Any) => number) => r2(a.reduce((s, x) => s + (f(x) || 0), 0))
const groupBy = <T,>(a: T[], k: (x: T) => string) => a.reduce((m, x) => { (m[k(x)] ??= []).push(x); return m }, {} as Record<string, T[]>)

function lineTax(gross: number, rate: number, inclusive: boolean, isGst: boolean, inter: boolean) {
  gross = r2(gross)
  if (!isGst || rate <= 0) return { taxable: gross, cgst: 0, sgst: 0, igst: 0, total: gross }
  const taxable = inclusive ? r2((gross * 100) / (100 + rate)) : gross
  const tax = inclusive ? r2(gross - taxable) : r2((gross * rate) / 100)
  if (inter) return { taxable, cgst: 0, sgst: 0, igst: tax, total: r2(taxable + tax) }
  const cgst = r2(tax / 2)
  return { taxable, cgst, sgst: r2(tax - cgst), igst: 0, total: r2(taxable + tax) }
}

function allocate(p: Any, line: Any) {
  const t = today()
  if (line.batchId) {
    const b = p.batches.find((x: Any) => x.id === line.batchId) ?? bad(`Batch not found for ${p.name}.`)
    if (b.expiryDate && new Date(b.expiryDate) < t) bad(`Batch ${b.batchNo} of ${p.name} has expired. Expired stock cannot be sold.`)
    if (b.quantity < line.quantity) bad(`Only ${b.quantity} ${p.unit} available in batch ${b.batchNo} of ${p.name}.`)
    return [[b, line.quantity]]
  }
  let remaining = line.quantity
  const out: Any[] = []
  for (const b of p.batches.filter((x: Any) => x.quantity > 0 && (!x.expiryDate || new Date(x.expiryDate) >= t)).sort((a: Any, b: Any) => (a.expiryDate ?? '9999') < (b.expiryDate ?? '9999') ? -1 : 1)) {
    if (remaining <= 0) break
    const take = Math.min(b.quantity, remaining)
    out.push([b, take]); remaining = r2(remaining - take)
  }
  if (remaining > 0) bad(`Insufficient stock for ${p.name}. Available: ${r2(line.quantity - remaining)} ${p.unit}, required: ${line.quantity}.`)
  return out
}

function createSale(req: Any) {
  if (!req.items?.length) bad('Add at least one item to the bill.')
  const shop = state.shop
  const isGst = req.isGst && shop.isGstRegistered
  const cust = req.customerId ? customer(req.customerId) : null
  const date = now()
  const inv: Any = {
    id: nextId(), invoiceDate: iso(date), customerId: cust?.id ?? null, customerName: cust?.name ?? (req.customerName || 'Cash Customer'),
    customerMobile: cust?.mobile ?? req.customerMobile ?? null, customerVillage: cust?.village ?? null, customerGstin: cust?.gstin ?? null,
    isGst, isInterState: isGst && !!req.isInterState, paymentMode: req.paymentMode, notes: req.notes ?? null, items: [],
  }
  const moves: Any[] = []
  for (const line of req.items) {
    const p = product(line.productId)
    for (const [b, q] of allocate(p, line)) {
      const discount = r2((line.discount || 0) * (q / line.quantity))
      const t = lineTax(q * line.rate - discount, p.gstRate, shop.pricesIncludeTax, isGst, inv.isInterState)
      moves.push([b, q])
      inv.items.push({ id: nextId(), productId: p.id, batchId: b.id, productName: p.name, hsnCode: p.hsnCode, batchNo: b.batchNo, expiryDate: b.expiryDate, unit: p.unit,
        quantity: q, rate: line.rate, discountAmount: discount, taxableValue: t.taxable, gstRate: isGst ? p.gstRate : 0, cgst: t.cgst, sgst: t.sgst, igst: t.igst, total: t.total, costPrice: b.purchasePrice || p.purchasePrice })
    }
  }
  inv.subTotal = sum(inv.items, x => x.taxableValue); inv.discount = sum(inv.items, x => x.discountAmount)
  inv.cgstAmount = sum(inv.items, x => x.cgst); inv.sgstAmount = sum(inv.items, x => x.sgst); inv.igstAmount = sum(inv.items, x => x.igst)
  const raw = sum(inv.items, x => x.total)
  inv.grandTotal = Math.round(raw); inv.roundOff = r2(inv.grandTotal - raw)
  const paid = Math.min(Math.max(req.paymentMode === 'Credit' ? 0 : Number(req.paidAmount) || 0, 0), inv.grandTotal)
  inv.paidAmount = paid; inv.balanceAmount = r2(inv.grandTotal - paid)
  inv.status = inv.balanceAmount === 0 ? 'Paid' : paid === 0 ? 'Unpaid' : 'Partial'
  if (inv.balanceAmount > 0) {
    if (!cust) bad('उधारी बिलासाठी ग्राहक निवडा. Select a registered customer for a credit (udhari) bill.')
    if (cust.creditLimit > 0 && cust.balance + inv.balanceAmount > cust.creditLimit) bad(`Credit limit exceeded for ${cust.name}.`)
    cust.balance = r2(cust.balance + inv.balanceAmount)
  }
  for (const [b, q] of moves) b.quantity = r2(b.quantity - q)
  inv.invoiceNo = `${shop.invoicePrefix}/${fy(date)}/${String(shop.nextInvoiceNo).padStart(4, '0')}`
  shop.nextInvoiceNo++
  state.invoices.push(inv)
  return inv
}

function cancelSale(id: number, reason: string) {
  const inv = state.invoices.find((i: Any) => i.id === id) ?? bad('Invoice not found.', 404)
  if (inv.status === 'Cancelled') bad('Invoice is already cancelled.')
  for (const it of inv.items) { const b = state.products.flatMap((p: Any) => p.batches).find((x: Any) => x.id === it.batchId); if (b) b.quantity = r2(b.quantity + it.quantity) }
  if (inv.customerId && inv.balanceAmount) { const c = customer(inv.customerId); c.balance = r2(c.balance - inv.balanceAmount) }
  Object.assign(inv, { status: 'Cancelled', cancelReason: reason, cancelledAt: iso(now()) })
  return inv
}

function createPurchase(req: Any) {
  const sup = state.suppliers.find((s: Any) => s.id === req.supplierId) ?? bad('Supplier not found.', 404)
  if (!req.items?.length) bad('Add at least one item.')
  let sub = 0, tax = 0
  for (const l of req.items) {
    const p = product(l.productId)
    const no = (l.batchNo || 'NA').trim().toUpperCase()
    let b = p.batches.find((x: Any) => x.batchNo === no)
    if (!b) { b = { id: nextId(), batchNo: no, quantity: 0 }; p.batches.push(b) }
    Object.assign(b, { expiryDate: l.expiryDate ?? b.expiryDate ?? null, quantity: r2(b.quantity + l.quantity + (l.freeQuantity || 0)), purchasePrice: l.rate, salePrice: l.salePrice || p.salePrice, mrp: l.mrp || p.mrp })
    p.purchasePrice = l.rate; if (l.salePrice > 0) p.salePrice = l.salePrice; if (l.mrp > 0) p.mrp = l.mrp
    const t = r2(l.quantity * l.rate); sub += t; tax += r2((t * l.gstRate) / 100)
  }
  const grand = Math.round(sub + tax + (req.otherCharges || 0))
  const paid = Math.min(Math.max(req.paidAmount || 0, 0), grand)
  sup.balance = r2(sup.balance + grand - paid)
  const pi = { id: nextId(), supplierId: sup.id, billNo: req.billNo, billDate: req.billDate ? `${req.billDate}T12:00:00` : iso(now()), subTotal: r2(sub), taxAmount: r2(tax), grandTotal: grand, paidAmount: paid, itemCount: req.items.length, paymentMode: req.paymentMode }
  state.purchases.push(pi)
  return pi
}

function recordPayment(req: Any) {
  const party = req.partyType === 'Customer' ? customer(req.partyId) : state.suppliers.find((s: Any) => s.id === req.partyId) ?? bad('Supplier not found.', 404)
  party.balance = r2(party.balance - Number(req.amount))
  const pay = { id: nextId(), partyType: req.partyType, partyId: req.partyId, amount: Number(req.amount), mode: req.mode === 'Credit' ? 'Cash' : req.mode, reference: req.reference || null, note: req.note || null, date: iso(now()) }
  state.payments.push(pay)
  return pay
}

function ledger(kind: 'Customer' | 'Supplier', id: number) {
  const party = kind === 'Customer' ? customer(id) : state.suppliers.find((s: Any) => s.id === id) ?? bad('Supplier not found.', 404)
  const docs = kind === 'Customer'
    ? state.invoices.filter((i: Any) => i.customerId === id && i.status !== 'Cancelled').flatMap((i: Any) => [[i.invoiceDate, 'Sale', i.invoiceNo, i.grandTotal, 0], [i.invoiceDate, 'Paid at billing', i.invoiceNo, 0, i.paidAmount]])
    : state.purchases.filter((p: Any) => p.supplierId === id).flatMap((p: Any) => [[p.billDate, 'Purchase', p.billNo, p.grandTotal, 0], [p.billDate, 'Paid on bill', p.billNo, 0, p.paidAmount]])
  const rows = docs.filter((x: Any) => x[3] || x[4])
    .concat(state.payments.filter((p: Any) => p.partyType === kind && p.partyId === id).map((p: Any) => [p.date, `Payment (${p.mode})`, p.reference ?? '', 0, p.amount]))
    .sort((a: Any, b: Any) => (a[0] < b[0] ? -1 : 1))
  const opening = kind === 'Customer' ? party.openingBalance : 0
  let bal = opening
  const entries = rows.map((x: Any) => { bal = r2(bal + x[3] - x[4]); return { date: x[0], type: x[1], reference: x[2], debit: x[3], credit: x[4], balance: bal } })
  return { partyId: id, name: party.name, mobile: party.mobile, openingBalance: opening, closingBalance: bal, entries }
}

const catOf = (pid: number) => state.products.find((p: Any) => p.id === pid)?.category ?? 'Other'

function stockReport() {
  return state.products.filter((p: Any) => p.isActive).sort((a: Any, b: Any) => a.name.localeCompare(b.name)).map((p: Any) => {
    const qty = stockOf(p)
    return { productId: p.id, name: p.name, localName: p.localName, category: p.category, company: p.company, unit: p.unit, stock: qty, reorderLevel: p.reorderLevel,
      purchaseValue: sum(p.batches.filter((b: Any) => b.quantity > 0), (b: Any) => b.quantity * (b.purchasePrice || p.purchasePrice)), saleValue: r2(qty * p.salePrice), isLow: qty <= p.reorderLevel, nearestExpiry: nearestExpiry(p) }
  })
}

function dashboard() {
  const t = today(), monthStart = new Date(t.getFullYear(), t.getMonth(), 1)
  const from = monthStart < new Date(t.getTime() - 6 * DAY) ? monthStart : new Date(t.getTime() - 6 * DAY)
  const sales = activeSales(from, t)
  const todays = sales.filter((s: Any) => startOfDay(new Date(s.invoiceDate)).getTime() === t.getTime())
  const month = sales.filter((s: Any) => new Date(s.invoiceDate) >= monthStart)
  const items = month.flatMap((s: Any) => s.items)
  const exp = state.products.flatMap((p: Any) => p.batches).filter((b: Any) => b.quantity > 0 && b.expiryDate).map((b: Any) => startOfDay(new Date(b.expiryDate)))
  const stock = stockReport()
  const debt = state.customers.filter((c: Any) => c.balance > 0)
  return {
    todaySales: sum(todays, s => s.grandTotal), todayBills: todays.length,
    todayCollection: sum(todays, s => s.paidAmount) + sum(state.payments.filter((p: Any) => p.partyType === 'Customer' && new Date(p.date) >= t), p => p.amount),
    monthSales: sum(month, s => s.grandTotal), monthProfit: sum(items, i => i.taxableValue - i.costPrice * i.quantity),
    totalUdhari: sum(debt, c => c.balance), udhariCustomers: debt.length, lowStockCount: stock.filter((s: Any) => s.isLow).length,
    expiringSoonCount: exp.filter((e: Date) => e >= t && e.getTime() <= t.getTime() + 30 * DAY).length, expiredCount: exp.filter((e: Date) => e < t).length,
    stockValue: sum(stock, s => s.purchaseValue),
    last7Days: [...Array(7)].map((_, k) => { const d = new Date(t.getTime() - (6 - k) * DAY); const ds = sales.filter((s: Any) => startOfDay(new Date(s.invoiceDate)).getTime() === d.getTime()); return { date: iso(d), sales: sum(ds, s => s.grandTotal), bills: ds.length } }),
    topProducts: Object.entries(groupBy(items, (i: Any) => i.productName)).map(([name, g]) => ({ name, quantity: sum(g, x => x.quantity), amount: sum(g, x => x.total) })).sort((a, b) => b.amount - a.amount).slice(0, 5),
    categorySales: Object.entries(groupBy(items, (i: Any) => catOf(i.productId))).map(([name, g]) => ({ name, quantity: sum(g, x => x.quantity), amount: sum(g, x => x.total) })).sort((a, b) => b.amount - a.amount),
    recentInvoices: [...state.invoices].sort((a: Any, b: Any) => b.id - a.id).slice(0, 6).map(listItem),
  }
}

const uqc = (u: string) => ({ KG: 'KGS', LTR: 'LTR', ML: 'MLT', BAG: 'BAG', PKT: 'PAC', GM: 'GMS' } as Record<string, string>)[u.toUpperCase()] ?? 'NOS'

function range(q: URLSearchParams) {
  const t = today()
  return [q.get('from') ? new Date(q.get('from') + 'T00:00:00') : new Date(t.getFullYear(), t.getMonth(), 1), q.get('to') ? new Date(q.get('to') + 'T00:00:00') : t]
}

function waLink(mobile: string, msg: string) {
  const d = mobile.replace(/\D/g, '')
  return { mode: 'Link', sent: false, url: `https://wa.me/${d.length === 10 ? '91' + d : d}?text=${encodeURIComponent(msg)}`, message: msg }
}

// ── online payments (demo: built-in simulator) ──
const plans = [{ plan: 'Basic', months: 12, price: 2999, label: 'Basic – 1 year' }, { plan: 'Pro', months: 12, price: 4999, label: 'Pro – 1 year (+ online payments)' }]
const payState = () => (state.payLinks ??= [], state.paySettings ??= { provider: 'UpiDirect', liveMode: true }, state)
const upiIntent = (amount: number, note: string) =>
  `upi://pay?pa=${String(state.shop.upiId).trim()}&pn=${encodeURIComponent(state.shop.shopName)}&am=${amount.toFixed(2)}&cu=INR&tn=${encodeURIComponent(note.slice(0, 50))}`
function linkDto(l: Any) {
  const d = (l.payerMobile ?? '').replace(/\D/g, '')
  const msg = l.provider === 'UpiDirect'
    ? `नमस्कार ${l.payerName} जी 🙏\n${state.shop.shopName}कडील ${l.description}: *₹${l.amount.toLocaleString('en-IN')}*\n\nPhonePe / GPay / Paytm ने भरा 👇\nUPI ID: *${state.shop.upiId}*\nAmount: ₹${l.amount}\nRef: ${l.reference}\n\nधन्यवाद!`
    : `नमस्कार ${l.payerName} जी 🙏\n${state.shop.shopName}कडील ${l.description}: *₹${l.amount.toLocaleString('en-IN')}*\n\nUPI / कार्ड / नेट बँकिंगने ऑनलाइन भरा 👇\n${l.payUrl}`
  return { ...l, whatsAppUrl: d.length >= 10 && l.status === 'Created' && l.purpose !== 'Subscription' ? `https://wa.me/91${d.slice(-10)}?text=${encodeURIComponent(msg)}` : undefined }
}
function newLink(x: Any) {
  const st = payState()
  if (st.paySettings.provider === 'None') bad('Online payments are not set up. Owner: open Settings → Online payments and choose a gateway.')
  const publicId = crypto.randomUUID().replace(/-/g, '')
  const provider = x.purpose === 'Subscription' ? 'Simulator' : st.paySettings.provider
  if (provider === 'UpiDirect' && !String(state.shop.upiId ?? '').includes('@')) bad('Set your UPI ID in Settings → Billing preferences first.')
  const reference = 'KP' + publicId.slice(0, 8).toUpperCase()
  const l: Any = { id: nextId(), publicId, reference, provider, liveMode: provider === 'UpiDirect', amountPaid: 0, status: 'Created',
    payUrl: `https://your-krushiprime-site/pay/${publicId.slice(0, 12)} (demo)`, createdAt: iso(now()), expiresAt: iso(new Date(Date.now() + 30 * DAY)), ...x }
  if (provider === 'UpiDirect') l.upiUri = upiIntent(l.amount, `${reference} ${l.payerName}`)
  st.payLinks.unshift(l)
  return linkDto(l)
}
function simulatePaid(publicId: string) {
  const l = payState().payLinks.find((x: Any) => x.publicId === publicId) ?? bad('Payment link not found.', 404)
  if (l.status === 'Paid') return linkDto(l)
  Object.assign(l, { status: 'Paid', amountPaid: l.amount, paidAt: iso(now()), lastMessage: l.provider === 'UpiDirect' ? 'Received on UPI (confirmed by shop)' : 'Demo payment (simulator)' })
  if (l.purpose === 'Subscription') {
    const base = new Date(state.shop.subscriptionEnd) < today() ? today() : new Date(state.shop.subscriptionEnd)
    state.shop.subscriptionEnd = iso(new Date(base.getFullYear(), base.getMonth() + l.planMonths, base.getDate())); state.shop.plan = l.plan
  } else if (l.customerId) {
    recordPayment({ partyType: 'Customer', partyId: l.customerId, amount: l.amount, mode: 'Upi', reference: l.reference ?? `SIMTXN${Math.floor(Math.random() * 900000 + 100000)}`, note: `Online payment (${l.purpose})` })
    if (l.salesInvoiceId) { const inv = state.invoices.find((i: Any) => i.id === l.salesInvoiceId); if (inv) { inv.paidAmount = r2(inv.paidAmount + l.amount); inv.balanceAmount = Math.max(0, r2(inv.balanceAmount - l.amount)); inv.status = inv.balanceAmount === 0 ? 'Paid' : 'Partial' } }
  }
  return linkDto(l)
}

// ── router ──
type Handler = (m: RegExpMatchArray, q: URLSearchParams, body: Any) => Any
const routes: [string, RegExp, Handler][] = [
  ['POST', /^\/api\/auth\/login$/, (_m, _q, b) => {
    const u = String(b.username).trim()
    if (u === 'admin' && b.password === 'Admin@123') return { token: 'demo-admin', expiresAt: iso(new Date(Date.now() + DAY)), user: admin }
    if (u === '9999999999' && b.password === 'demo@123') return { token: 'demo-owner', expiresAt: iso(new Date(Date.now() + DAY)), user: owner() }
    return bad('Invalid mobile number / username or password.', 401)
  }],
  ['POST', /^\/api\/auth\/register-shop$/, (_m, _q, b) => {
    Object.assign(state.shop, { shopName: b.shopName, ownerName: b.ownerName, mobile: b.mobile, village: b.village || state.shop.village, district: b.district || state.shop.district })
    return { token: 'demo-owner', expiresAt: iso(new Date(Date.now() + DAY)), user: owner() }
  }],
  ['GET', /^\/api\/auth\/me$/, () => (session() ? me() : bad('Unauthorized', 401))],
  ['POST', /^\/api\/auth\/change-password$/, () => null],

  ['GET', /^\/api\/products$/, (_m, q) => {
    const s = q.get('q')?.trim(), cat = q.get('category')
    return state.products.filter((p: Any) => (q.get('includeInactive') === 'true' || p.isActive) && (!cat || p.category === cat)
      && (!s || like(p.name, s) || like(p.localName, s) || like(p.company, s) || p.barcode === s || p.hsnCode === s))
      .sort((a: Any, b: Any) => a.name.localeCompare(b.name)).map((p: Any) => mapProduct(p, q.get('batches') === 'true'))
  }],
  ['GET', /^\/api\/products\/(\d+)$/, m => mapProduct(product(+m[1]), true)],
  ['POST', /^\/api\/products$/, (_m, _q, b) => {
    const p: Any = { ...b, id: nextId(), batches: [] }
    if (b.openingQty > 0) p.batches.push({ id: nextId(), batchNo: (b.openingBatchNo || 'OPENING').toUpperCase(), expiryDate: b.openingExpiry ? `${b.openingExpiry}T00:00:00` : null, quantity: b.openingQty, purchasePrice: b.purchasePrice, salePrice: b.salePrice, mrp: b.mrp })
    delete p.openingQty; delete p.openingBatchNo; delete p.openingExpiry
    state.products.push(p); return mapProduct(p, true)
  }],
  ['PUT', /^\/api\/products\/(\d+)$/, (m, _q, b) => { const p = product(+m[1]); const { openingQty: _o, openingBatchNo: _n, openingExpiry: _e, id: _i, ...rest } = b; Object.assign(p, rest); return mapProduct(p, true) }],
  ['DELETE', /^\/api\/products\/(\d+)$/, m => { product(+m[1]).isActive = false; return null }],
  ['POST', /^\/api\/products\/adjust-stock$/, (_m, _q, b) => {
    const p = product(b.productId)
    let bt = b.batchId ? p.batches.find((x: Any) => x.id === b.batchId) : p.batches.find((x: Any) => x.batchNo === (b.newBatchNo || 'NA').toUpperCase())
    if (!bt) { bt = { id: nextId(), batchNo: (b.newBatchNo || 'NA').toUpperCase(), expiryDate: b.newBatchExpiry ? `${b.newBatchExpiry}T00:00:00` : null, quantity: 0, purchasePrice: p.purchasePrice, salePrice: p.salePrice, mrp: p.mrp }; p.batches.push(bt) }
    if (bt.quantity + b.quantityChange < 0) bad('Stock cannot go below zero.')
    bt.quantity = r2(bt.quantity + b.quantityChange); return mapProduct(p, true)
  }],

  ['GET', /^\/api\/customers$/, (_m, q) => {
    const s = q.get('q')?.trim()
    return state.customers.filter((c: Any) => c.isActive && (q.get('withDueOnly') !== 'true' || c.balance > 0) && (!s || like(c.name, s) || like(c.mobile, s) || like(c.village, s)))
      .sort((a: Any, b: Any) => a.name.localeCompare(b.name))
  }],
  ['GET', /^\/api\/customers\/(\d+)$/, m => customer(+m[1])],
  ['GET', /^\/api\/customers\/(\d+)\/ledger$/, m => ledger('Customer', +m[1])],
  ['POST', /^\/api\/customers$/, (_m, _q, b) => {
    if (b.mobile && state.customers.some((c: Any) => c.mobile === b.mobile)) bad('A customer with this mobile number already exists.', 409)
    const c = { ...b, id: nextId(), balance: b.openingBalance || 0, isActive: true }; state.customers.push(c); return c
  }],
  ['PUT', /^\/api\/customers\/(\d+)$/, (m, _q, b) => { const c = customer(+m[1]); c.balance = r2(c.balance + (b.openingBalance || 0) - (c.openingBalance || 0)); Object.assign(c, { ...b, balance: c.balance }); return c }],

  ['GET', /^\/api\/suppliers$/, () => state.suppliers.filter((s: Any) => s.isActive)],
  ['GET', /^\/api\/suppliers\/(\d+)\/ledger$/, m => ledger('Supplier', +m[1])],
  ['POST', /^\/api\/suppliers$/, (_m, _q, b) => { const s = { ...b, id: nextId(), balance: 0, isActive: true }; state.suppliers.push(s); return s }],
  ['PUT', /^\/api\/suppliers\/(\d+)$/, (m, _q, b) => Object.assign(state.suppliers.find((s: Any) => s.id === +m[1]), b)],

  ['GET', /^\/api\/sales$/, (_m, q) => {
    let list = [...state.invoices]
    if (q.get('from')) list = list.filter(i => new Date(i.invoiceDate) >= new Date(q.get('from') + 'T00:00:00'))
    if (q.get('to')) list = list.filter(i => new Date(i.invoiceDate) < new Date(new Date(q.get('to') + 'T00:00:00').getTime() + DAY))
    if (q.get('status')) list = list.filter(i => i.status === q.get('status'))
    const s = q.get('q'); if (s) list = list.filter(i => like(i.invoiceNo, s) || like(i.customerName, s) || like(i.customerMobile, s))
    list.sort((a, b) => b.id - a.id)
    const page = +(q.get('page') ?? 1), size = +(q.get('pageSize') ?? 50)
    return { items: list.slice((page - 1) * size, page * size).map(listItem), total: list.length, page, pageSize: size }
  }],
  ['GET', /^\/api\/sales\/(\d+)$/, m => {
    const inv = state.invoices.find((i: Any) => i.id === +m[1]) ?? bad('Invoice not found.', 404)
    return { invoice: inv, shop: state.shop, customerOutstanding: inv.customerId ? customer(inv.customerId).balance : undefined }
  }],
  ['POST', /^\/api\/sales$/, (_m, _q, b) => listItem(createSale(b))],
  ['POST', /^\/api\/sales\/(\d+)\/cancel$/, (m, _q, b) => listItem(cancelSale(+m[1], b.reason))],

  ['GET', /^\/api\/purchases$/, () => [...state.purchases].sort((a: Any, b: Any) => (a.billDate < b.billDate ? 1 : -1)).map((p: Any) => ({ ...p, supplierName: state.suppliers.find((s: Any) => s.id === p.supplierId)?.name ?? '' }))],
  ['POST', /^\/api\/purchases$/, (_m, _q, b) => createPurchase(b)],
  ['POST', /^\/api\/payments$/, (_m, _q, b) => recordPayment(b)],

  ['POST', /^\/api\/whatsapp\/invoice\/(\d+)$/, m => {
    const inv = state.invoices.find((i: Any) => i.id === +m[1]) ?? bad('Invoice not found.', 404)
    if (!inv.customerMobile) bad('Customer mobile number is not available on this bill.')
    const due = inv.customerId ? customer(inv.customerId).balance : 0
    const msg = [`🌾 *${state.shop.shopName}*`, `${inv.isGst ? 'Tax Invoice' : 'Bill'}: *${inv.invoiceNo}*`, `ग्राहक: ${inv.customerName}`, '------------------------------',
      ...inv.items.map((i: Any) => `• ${i.productName} × ${i.quantity} ${i.unit} = ₹${i.total.toFixed(2)}`), '------------------------------',
      `*एकूण / Total: ₹${inv.grandTotal.toLocaleString('en-IN')}*`, `जमा / Paid: ₹${inv.paidAmount.toLocaleString('en-IN')}`,
      ...(due > 0 ? [`एकूण बाकी / Total due: *₹${due.toLocaleString('en-IN')}*`, `UPI: ${state.shop.upiId}`] : []), '', state.shop.invoiceFooter].join('\n')
    return waLink(inv.customerMobile, msg)
  }],
  ['POST', /^\/api\/whatsapp\/reminder\/(\d+)$/, m => {
    const c = customer(+m[1])
    if (!c.mobile) bad('Customer mobile number is not saved.')
    if (c.balance <= 0) bad(`${c.name} has no pending udhari.`)
    return waLink(c.mobile, `नमस्कार ${c.name} जी 🙏\n\n*${state.shop.shopName}* कडे आपली उधारी रक्कम *₹${c.balance.toLocaleString('en-IN')}* बाकी आहे.\nकृपया सोयीनुसार लवकरात लवकर भरणा करावा.\n\nUPI: ${state.shop.upiId}\n📞 ${state.shop.mobile}`)
  }],

  ['GET', /^\/api\/reports\/dashboard$/, () => dashboard()],
  ['GET', /^\/api\/reports\/stock$/, () => stockReport()],
  ['GET', /^\/api\/reports\/expiry$/, (_m, q) => {
    const limit = today().getTime() + +(q.get('days') ?? 60) * DAY
    return state.products.flatMap((p: Any) => p.batches.filter((b: Any) => b.quantity > 0 && b.expiryDate && new Date(b.expiryDate).getTime() <= limit).map((b: Any) => ({
      productId: p.id, name: p.name, batchNo: b.batchNo, expiryDate: b.expiryDate, quantity: b.quantity, value: r2(b.quantity * b.purchasePrice),
      daysLeft: Math.round((startOfDay(new Date(b.expiryDate)).getTime() - today().getTime()) / DAY) }))).sort((a: Any, b: Any) => (a.expiryDate < b.expiryDate ? -1 : 1))
  }],
  ['GET', /^\/api\/reports\/udhari$/, () => state.customers.filter((c: Any) => c.balance > 0).map((c: Any) => {
    const lp = state.payments.filter((p: Any) => p.partyType === 'Customer' && p.partyId === c.id).map((p: Any) => p.date).sort().pop()
    const li = state.invoices.filter((i: Any) => i.customerId === c.id && i.status !== 'Cancelled').map((i: Any) => i.invoiceDate).sort().pop()
    const since = startOfDay(new Date(lp ?? li ?? c.createdAt))
    return { customerId: c.id, name: c.name, mobile: c.mobile, village: c.village, balance: c.balance, lastInvoiceDate: li, lastPaymentDate: lp, daysSinceLastPayment: Math.round((today().getTime() - since.getTime()) / DAY) }
  }).sort((a: Any, b: Any) => b.balance - a.balance)],
  ['GET', /^\/api\/reports\/sales$/, (_m, q) => {
    const [f, t] = range(q); const list = activeSales(f, t).sort((a: Any, b: Any) => (a.invoiceDate < b.invoiceDate ? -1 : 1))
    return { bills: list.length, taxable: sum(list, i => i.subTotal), tax: sum(list, i => i.cgstAmount + i.sgstAmount + i.igstAmount), grandTotal: sum(list, i => i.grandTotal),
      paid: sum(list, i => i.paidAmount), balance: sum(list, i => i.balanceAmount),
      byPaymentMode: Object.entries(groupBy(list, (i: Any) => i.paymentMode)).map(([name, g]) => ({ name, quantity: g.length, amount: sum(g, x => x.grandTotal) })),
      daily: Object.entries(groupBy(list, (i: Any) => i.invoiceDate.slice(0, 10))).map(([d, g]) => ({ date: d + 'T00:00:00', sales: sum(g, x => x.grandTotal), bills: g.length })), invoices: list.map(listItem) }
  }],
  ['GET', /^\/api\/reports\/gst$/, (_m, q) => {
    const [f, t] = range(q); const list = activeSales(f, t); const gst = list.filter((i: Any) => i.isGst); const items = gst.flatMap((i: Any) => i.items)
    return {
      taxable: sum(items, i => i.taxableValue), cgst: sum(items, i => i.cgst), sgst: sum(items, i => i.sgst), igst: sum(items, i => i.igst), nonGstSales: sum(list.filter((i: Any) => !i.isGst), i => i.grandTotal),
      purchaseTaxCredit: sum(state.purchases.filter((p: Any) => inRange(p.billDate, f, t)), p => p.taxAmount),
      rateWise: Object.entries(groupBy(items, (i: Any) => String(i.gstRate))).map(([rate, g]) => ({ rate: +rate, taxable: sum(g, x => x.taxableValue), cgst: sum(g, x => x.cgst), sgst: sum(g, x => x.sgst), igst: sum(g, x => x.igst), total: sum(g, x => x.total) })).sort((a, b) => a.rate - b.rate),
      hsnWise: Object.values(groupBy(items, (i: Any) => `${i.hsnCode}|${i.gstRate}|${i.unit}`)).map((g: Any[]) => ({ hsn: g[0].hsnCode ?? '-', description: g[0].productName, uqc: uqc(g[0].unit), quantity: sum(g, x => x.quantity), rate: g[0].gstRate, taxable: sum(g, x => x.taxableValue), cgst: sum(g, x => x.cgst), sgst: sum(g, x => x.sgst), igst: sum(g, x => x.igst), total: sum(g, x => x.total) })).sort((a, b) => (a.hsn < b.hsn ? -1 : 1)),
      b2b: gst.filter((i: Any) => i.customerGstin).map((i: Any) => ({ invoiceNo: i.invoiceNo, date: i.invoiceDate, customerName: i.customerName, gstin: i.customerGstin, taxable: i.subTotal, tax: r2(i.cgstAmount + i.sgstAmount + i.igstAmount), total: i.grandTotal })),
    }
  }],
  ['GET', /^\/api\/reports\/profit$/, (_m, q) => {
    const [f, t] = range(q); const items = activeSales(f, t).flatMap((i: Any) => i.items)
    const revenue = sum(items, i => i.taxableValue), cost = sum(items, i => i.costPrice * i.quantity)
    return { revenue, cost, grossProfit: r2(revenue - cost), marginPercent: revenue ? r2(((revenue - cost) * 100) / revenue) : 0,
      byProduct: Object.entries(groupBy(items, (i: Any) => i.productName)).map(([name, g]) => ({ name, quantity: sum(g, x => x.quantity), amount: sum(g, x => x.taxableValue - x.costPrice * x.quantity) })).sort((a, b) => b.amount - a.amount) }
  }],

  ['GET', /^\/api\/settings\/shop$/, () => ({ settings: state.shop, subscription: tenantSummary() })],
  ['PUT', /^\/api\/settings\/shop$/, (_m, _q, b) => Object.assign(state.shop, b)],
  ['GET', /^\/api\/settings\/users$/, () => state.users],
  ['POST', /^\/api\/settings\/users$/, (_m, _q, b) => { const u = { id: nextId(), fullName: b.fullName, username: b.username, role: b.role, isActive: true }; state.users.push(u); return u }],
  ['PUT', /^\/api\/settings\/users\/(\d+)\/status$/, (m, _q, b) => { const u = state.users.find((x: Any) => x.id === +m[1]); if (u) u.isActive = b.isActive; return null }],

  ['GET', /^\/api\/settings\/payments$/, () => ({ ...payState().paySettings, webhookUrl: 'https://<your-site>/api/webhooks/cashfree' })],
  ['PUT', /^\/api\/settings\/payments$/, (_m, _q, b) => { const st = payState(); st.paySettings = { provider: b.provider, liveMode: b.liveMode, cashfreeAppId: b.cashfreeAppId, nttMerchId: b.nttMerchId, nttProductId: b.nttProductId,
    cashfreeSecret: b.cashfreeSecret ? '••••' + String(b.cashfreeSecret).slice(-4) : undefined, nttPassword: b.nttPassword ? '••••' : undefined, nttRequestKey: b.nttRequestKey ? '••••' : undefined, nttResponseKey: b.nttResponseKey ? '••••' : undefined }
    if (b.provider === 'Cashfree' || b.provider === 'NttData') bad('This web demo cannot reach real gateways. Use "Direct UPI" or "Demo simulator" here; Cashfree / NTT DATA work in the installed app.')
    if (b.provider === 'UpiDirect' && !String(state.shop.upiId ?? '').includes('@')) bad('Save your UPI ID in Billing preferences first, then choose Direct UPI.')
    return { ...st.paySettings } }],
  ['GET', /^\/api\/online-payments$/, () => payState().payLinks.map(linkDto)],
  ['POST', /^\/api\/online-payments\/(\d+)\/mark-received$/, m => {
    const l = payState().payLinks.find((x: Any) => x.id === +m[1]) ?? bad('Payment link not found.', 404)
    if (l.provider !== 'UpiDirect') bad('Gateway payments are confirmed automatically.')
    return simulatePaid(l.publicId)
  }],
  ['POST', /^\/api\/online-payments\/udhari\/(\d+)$/, (m, _q, b) => {
    const c = customer(+m[1]); if (c.balance <= 0) bad(`${c.name} has no pending udhari.`)
    const amt = b.amount ? r2(Number(b.amount)) : c.balance
    if (amt <= 0) bad('Enter a valid amount.'); if (amt > c.balance) bad(`Amount is more than the outstanding ₹${c.balance.toLocaleString('en-IN')}.`)
    return newLink({ purpose: 'Udhari', customerId: c.id, payerName: c.name, payerMobile: c.mobile, amount: amt, description: `उधारी भरणा / Udhari payment – ${state.shop.shopName}` })
  }],
  ['POST', /^\/api\/online-payments\/invoice\/(\d+)$/, m => {
    const inv = state.invoices.find((i: Any) => i.id === +m[1]) ?? bad('Invoice not found.', 404)
    if (inv.balanceAmount <= 0) bad('This bill is already fully paid.')
    return newLink({ purpose: 'Invoice', customerId: inv.customerId, salesInvoiceId: inv.id, payerName: inv.customerName, payerMobile: inv.customerMobile, amount: inv.balanceAmount, description: `Bill ${inv.invoiceNo} – ${state.shop.shopName}` })
  }],
  ['POST', /^\/api\/online-payments\/(\d+)\/refresh$/, m => linkDto(payState().payLinks.find((x: Any) => x.id === +m[1]) ?? bad('Payment link not found.', 404))],
  ['POST', /^\/api\/online-payments\/(\w+)\/simulate$/, m => simulatePaid(m[1])],
  ['GET', /^\/api\/subscription\/plans$/, () => plans],
  ['POST', /^\/api\/subscription\/checkout$/, (_m, _q, b) => {
    const p = plans.find(x => x.plan === b.plan) ?? bad('Plan not available.')
    const l = newLink({ purpose: 'Subscription', plan: p.plan, planMonths: p.months, payerName: state.shop.ownerName, payerMobile: state.shop.mobile, amount: p.price, description: `Krushi Prime ${p.label}` })
    simulatePaid(l.publicId)
    return l
  }],

  ['GET', /^\/api\/admin\/stats$/, () => {
    const t = today(), list = state.tenants, end = (x: Any) => startOfDay(new Date(x.subscriptionEnd))
    return { totalShops: list.length, activeShops: list.filter((x: Any) => x.isActive && end(x) >= t).length, onTrial: list.filter((x: Any) => x.plan === 'Trial' && end(x) >= t).length,
      paid: list.filter((x: Any) => x.plan !== 'Trial' && end(x) >= t).length, expired: list.filter((x: Any) => end(x) < t).length,
      expiringIn30Days: list.filter((x: Any) => end(x) >= t && end(x).getTime() <= t.getTime() + 30 * DAY).length,
      invoicesToday: state.invoices.filter((i: Any) => new Date(i.invoiceDate) >= t).length }
  }],
  ['GET', /^\/api\/admin\/tenants$/, (_m, q) => {
    const s = q.get('q')
    return state.tenants.filter((x: Any) => !s || like(x.shopName, s) || like(x.mobile, s) || like(x.ownerName, s)).map((x: Any) => {
      const dl = Math.ceil((startOfDay(new Date(x.subscriptionEnd)).getTime() - today().getTime()) / DAY)
      const own = x.id === 1 ? state.invoices.filter((i: Any) => i.status !== 'Cancelled') : null
      return { ...x, daysLeft: dl < 0 ? -1 : dl, ...(own ? { invoices: own.length, totalBilled: Math.round(sum(own, i => i.grandTotal)) } : {}) }
    }).sort((a: Any, b: Any) => b.id - a.id)
  }],
  ['POST', /^\/api\/admin\/tenants$/, (_m, _q, b) => {
    const t = today(), end = new Date(t.getFullYear(), t.getMonth() + 6, t.getDate())
    const x = { id: nextId(), shopName: b.shopName, ownerName: b.ownerName, mobile: b.mobile, district: b.district, plan: 'Trial', subscriptionStart: iso(t), subscriptionEnd: iso(end), isActive: true, invoices: 0, totalBilled: 0, lastBillAt: null, createdAt: iso(now()) }
    state.tenants.push(x); return x
  }],
  ['PUT', /^\/api\/admin\/tenants\/(\d+)\/subscription$/, (m, _q, b) => {
    const x = state.tenants.find((t: Any) => t.id === +m[1]) ?? bad('Shop not found.', 404)
    const base = new Date(x.subscriptionEnd) < today() ? today() : new Date(x.subscriptionEnd)
    x.plan = b.plan; if (b.extendMonths) x.subscriptionEnd = iso(new Date(base.getFullYear(), base.getMonth() + b.extendMonths, base.getDate()))
    return x
  }],
  ['PUT', /^\/api\/admin\/tenants\/(\d+)\/status$/, (m, _q, b) => { const x = state.tenants.find((t: Any) => t.id === +m[1]); x.isActive = b.isActive; return x }],
  ['POST', /^\/api\/admin\/tenants\/(\d+)\/reset-password$/, m => ({ username: state.tenants.find((t: Any) => t.id === +m[1])?.mobile, temporaryPassword: `Kp@${Math.floor(100000 + Math.random() * 900000)}` })],
]

/** Same contract as fetch-based request(): resolves with JSON data or throws {status, message}. */
export async function mockRequest(method: string, url: string, body?: unknown): Promise<Any> {
  await new Promise(r => setTimeout(r, 120)) // feel like a network call
  const [path, query] = url.split('?')
  const q = new URLSearchParams(query ?? '')
  for (const [m, re, h] of routes) {
    const match = path.match(re)
    if (m === method && match) {
      try {
        const res = h(match, q, body ?? {})
        if (method !== 'GET') persist()
        return structuredClone(res ?? null)
      } catch (e) {
        if (e instanceof HttpError) throw e
        throw new HttpError(500, 'Something went wrong. Please try again.')
      }
    }
  }
  throw new HttpError(404, `Not available in demo: ${method} ${path}`)
}
export { HttpError as DemoHttpError }
