import { useEffect, useMemo, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, qs } from '../lib/api'
import type { Customer, PaymentMode, Product, SaleListItem, ShopSettings, WhatsAppResult } from '../lib/types'
import { CATEGORIES } from '../lib/types'
import { dmy, inr, lineTax, num, qty as fq } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { Badge, Button, Card, Field, Modal, openWhatsApp, PageHeader, Spinner, useLoad, useToast } from '../components/ui'
import { CustomerForm } from './Customers'

interface Line { key: number; product: Product; batchId: number | null; quantity: number; rate: number; discount: number }

const MODES: { mode: PaymentMode; label: string; icon: string }[] = [
  { mode: 'Cash', label: 'Cash', icon: '💵' }, { mode: 'Upi', label: 'UPI', icon: '📱' }, { mode: 'Card', label: 'Card', icon: '💳' }, { mode: 'Credit', label: 'Udhari', icon: '🤝' },
]

export default function Billing() {
  const { t } = useI18n()
  const toast = useToast()
  const nav = useNavigate()
  const searchRef = useRef<HTMLInputElement>(null)
  const { data: products, loading, reload: reloadProducts } = useLoad(() => api.get<Product[]>('/api/products?batches=true'))
  const { data: shop } = useLoad(() => api.get<{ settings: ShopSettings }>('/api/settings/shop'))

  const [search, setSearch] = useState('')
  const [cat, setCat] = useState<string>('')
  const [hi, setHi] = useState(0)
  const [lines, setLines] = useState<Line[]>([])
  const [customer, setCustomer] = useState<Customer | null>(null)
  const [custQuery, setCustQuery] = useState('')
  const [custResults, setCustResults] = useState<Customer[]>([])
  const [walkIn, setWalkIn] = useState({ name: '', mobile: '' })
  const [isGst, setIsGst] = useState(true)
  const [interState, setInterState] = useState(false)
  const [mode, setMode] = useState<PaymentMode>('Cash')
  const [paid, setPaid] = useState<string>('')
  const [notes, setNotes] = useState('')
  const [saving, setSaving] = useState(false)
  const [addCust, setAddCust] = useState(false)

  const inclusive = shop?.settings.pricesIncludeTax ?? true
  const gstAllowed = shop?.settings.isGstRegistered ?? true

  useEffect(() => {
    const h = (e: KeyboardEvent) => {
      if (e.key === 'F2') { e.preventDefault(); searchRef.current?.focus() }
      if (e.key === 'F9') { e.preventDefault(); document.getElementById('save-bill')?.click() }
    }
    window.addEventListener('keydown', h)
    searchRef.current?.focus()
    return () => window.removeEventListener('keydown', h)
  }, [])

  useEffect(() => {
    if (custQuery.trim().length < 2) { setCustResults([]); return }
    const id = setTimeout(() => api.get<Customer[]>(`/api/customers${qs({ q: custQuery })}`).then(setCustResults).catch(() => {}), 250)
    return () => clearTimeout(id)
  }, [custQuery])

  const results = useMemo(() => {
    if (!products) return []
    const q = search.trim().toLowerCase()
    return products.filter(p => (!cat || p.category === cat) && (!q || p.name.toLowerCase().includes(q) || p.localName?.includes(search.trim()) || p.company?.toLowerCase().includes(q) || p.barcode === search.trim()))
      .slice(0, 30)
  }, [products, search, cat])

  const add = (p: Product) => {
    setLines(ls => {
      const existing = ls.find(l => l.product.id === p.id && l.batchId === null)
      if (existing) return ls.map(l => (l === existing ? { ...l, quantity: l.quantity + 1 } : l))
      return [...ls, { key: Date.now() + Math.random(), product: p, batchId: null, quantity: 1, rate: p.salePrice, discount: 0 }]
    })
    setSearch(''); setHi(0); searchRef.current?.focus()
  }
  const update = (key: number, patch: Partial<Line>) => setLines(ls => ls.map(l => (l.key === key ? { ...l, ...patch } : l)))

  const calc = useMemo(() => {
    const gstOn = isGst && gstAllowed
    const rows = lines.map(l => ({ l, ...lineTax(l.quantity * l.rate - l.discount, l.product.gstRate, inclusive, gstOn) }))
    const taxable = rows.reduce((s, r) => s + r.taxable, 0)
    const tax = rows.reduce((s, r) => s + r.tax, 0)
    const raw = rows.reduce((s, r) => s + r.total, 0)
    const grand = Math.round(raw)
    return { rows, taxable, tax, raw, grand, roundOff: grand - raw, discount: lines.reduce((s, l) => s + l.discount, 0) }
  }, [lines, isGst, gstAllowed, inclusive])

  const paidNum = mode === 'Credit' ? 0 : paid === '' ? calc.grand : Math.min(Number(paid) || 0, calc.grand)
  const balance = calc.grand - paidNum

  const reset = () => { setLines([]); setCustomer(null); setCustQuery(''); setWalkIn({ name: '', mobile: '' }); setMode('Cash'); setPaid(''); setNotes(''); reloadProducts() }

  const save = async (after: 'print' | 'whatsapp' | 'new') => {
    if (!lines.length) return toast.err('Add at least one product / किमान एक उत्पादन जोडा')
    if (balance > 0 && !customer) return toast.err('उधारीसाठी ग्राहक निवडा – select a farmer for credit bill')
    setSaving(true)
    try {
      const res = await api.post<SaleListItem>('/api/sales', {
        customerId: customer?.id ?? null, customerName: customer ? null : walkIn.name || 'Cash Customer', customerMobile: customer ? null : walkIn.mobile || null,
        isGst: isGst && gstAllowed, isInterState: interState, paymentMode: mode, paidAmount: paidNum, notes: notes || null,
        items: lines.map(l => ({ productId: l.product.id, batchId: l.batchId, quantity: l.quantity, rate: l.rate, discount: l.discount })),
      })
      toast.ok(`Bill ${res.invoiceNo} saved · ${inr(res.grandTotal)}`)
      if (after === 'whatsapp') {
        try { openWhatsApp((await api.post<WhatsAppResult>(`/api/whatsapp/invoice/${res.id}`)).url) } catch (e) { toast.err(e) }
        reset()
      } else if (after === 'print') nav(`/sales/${res.id}?print=1`)
      else reset()
    } catch (e) { toast.err(e) } finally { setSaving(false) }
  }

  if (loading) return <Spinner />

  return (
    <div>
      <PageHeader title={`🧾 ${t('billing')}`} subtitle="F2 = search product · Enter = add · F9 = save" actions={lines.length > 0 && <Button variant="ghost" onClick={reset}>Clear</Button>} />
      <div className="grid gap-4 xl:grid-cols-[1fr_380px]">
        <div className="space-y-4">
          {/* product search */}
          <div className="card relative p-3">
            <input ref={searchRef} className="input py-3 text-base" placeholder="🔍 Search product / बारकोड / कंपनी (e.g. urea, DAP, युरिया)…" value={search}
              onChange={e => { setSearch(e.target.value); setHi(0) }}
              onKeyDown={e => {
                if (e.key === 'ArrowDown') { e.preventDefault(); setHi(h => Math.min(h + 1, results.length - 1)) }
                if (e.key === 'ArrowUp') { e.preventDefault(); setHi(h => Math.max(h - 1, 0)) }
                if (e.key === 'Enter' && results[hi] && search) { e.preventDefault(); add(results[hi]) }
              }} />
            <div className="mt-2 flex flex-wrap gap-1.5">
              <button onClick={() => setCat('')} className={`rounded-full px-3 py-1 text-xs font-semibold ${!cat ? 'bg-brand-700 text-white' : 'bg-stone-100'}`}>All</button>
              {CATEGORIES.map(c => <button key={c} onClick={() => setCat(c === cat ? '' : c)} className={`rounded-full px-3 py-1 text-xs font-semibold ${cat === c ? 'bg-brand-700 text-white' : 'bg-stone-100'}`}>{c}</button>)}
            </div>
            {(search || cat) && (
              <div className="mt-2 max-h-72 overflow-y-auto rounded-lg border border-stone-200">
                {results.length === 0 && <div className="p-3 text-sm text-stone-400">No product found</div>}
                {results.map((p, i) => (
                  <button key={p.id} onClick={() => add(p)} onMouseEnter={() => setHi(i)}
                    className={`flex w-full items-center justify-between gap-3 border-b border-stone-100 px-3 py-2 text-left last:border-0 ${i === hi ? 'bg-brand-50' : ''}`}>
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold">{p.name} {p.localName && <span className="font-normal text-stone-500">· {p.localName}</span>}</div>
                      <div className="text-xs text-stone-500">{p.company} · GST {p.gstRate}% · {p.nearestExpiry ? `Exp ${dmy(p.nearestExpiry)}` : 'No expiry'}</div>
                    </div>
                    <div className="text-right">
                      <div className="font-bold text-brand-800">{inr(p.salePrice, 2)}</div>
                      <Badge tone={p.stock <= 0 ? 'red' : p.stock <= p.reorderLevel ? 'amber' : 'green'}>Stock {fq(p.stock)} {p.unit}</Badge>
                    </div>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* cart */}
          <Card title={`Items (${lines.length})`}>
            {lines.length === 0 ? <div className="py-10 text-center text-stone-400">🌾 Search and add products to start billing</div> : (
              <div className="-mx-4 -my-4 overflow-x-auto">
                <table className="tbl">
                  <thead><tr><th>Product</th><th>Batch</th><th className="w-24">Qty</th><th className="w-28">Rate</th><th className="w-24">Disc ₹</th><th className="num">Amount</th><th /></tr></thead>
                  <tbody>
                    {calc.rows.map(({ l, total, tax }) => {
                      const batches = (l.product.batches ?? []).filter(b => b.quantity > 0)
                      const over = l.quantity > l.product.stock
                      return (
                        <tr key={l.key}>
                          <td><div className="font-semibold">{l.product.name}</div><div className="text-xs text-stone-500">HSN {l.product.hsnCode ?? '-'} · GST {l.product.gstRate}%{tax > 0 && ` (₹${num(tax)})`}</div>{over && <div className="text-xs font-semibold text-red-600">Only {fq(l.product.stock)} in stock</div>}</td>
                          <td>
                            <select className="input py-1 text-xs" value={l.batchId ?? ''} onChange={e => update(l.key, { batchId: e.target.value ? Number(e.target.value) : null })}>
                              <option value="">Auto (FEFO)</option>
                              {batches.map(b => <option key={b.id} value={b.id}>{b.batchNo} · {fq(b.quantity)} · {b.expiryDate ? dmy(b.expiryDate) : '—'}</option>)}
                            </select>
                          </td>
                          <td><input type="number" min={0.001} step="any" className="input py-1" value={l.quantity} onChange={e => update(l.key, { quantity: Number(e.target.value) })} /></td>
                          <td><input type="number" min={0} step="any" className="input py-1" value={l.rate} onChange={e => update(l.key, { rate: Number(e.target.value) })} /></td>
                          <td><input type="number" min={0} step="any" className="input py-1" value={l.discount} onChange={e => update(l.key, { discount: Number(e.target.value) })} /></td>
                          <td className="num font-bold">{inr(total, 2)}</td>
                          <td><button className="text-lg text-red-500" onClick={() => setLines(ls => ls.filter(x => x.key !== l.key))}>×</button></td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </Card>
        </div>

        {/* checkout panel */}
        <div className="space-y-4">
          <Card title={`👨‍🌾 ${t('customer')}`} actions={<Button variant="ghost" className="px-2 py-1" onClick={() => setAddCust(true)}>+ New</Button>}>
            {customer ? (
              <div className="flex items-start justify-between rounded-xl bg-brand-50 p-3">
                <div><div className="font-bold">{customer.name}</div><div className="text-xs text-stone-600">{customer.mobile} · {customer.village}</div>
                  {customer.balance > 0 && <div className="mt-1 text-xs font-bold text-red-600">Previous udhari: {inr(customer.balance)}</div>}</div>
                <button className="text-sm text-stone-500" onClick={() => setCustomer(null)}>Change</button>
              </div>
            ) : (
              <div className="space-y-2">
                <div className="relative">
                  <input className="input" placeholder="Search farmer by name / mobile / village" value={custQuery} onChange={e => setCustQuery(e.target.value)} />
                  {custResults.length > 0 && (
                    <div className="absolute z-10 mt-1 max-h-60 w-full overflow-y-auto rounded-lg border border-stone-200 bg-white shadow-lg">
                      {custResults.map(c => (
                        <button key={c.id} className="flex w-full justify-between px-3 py-2 text-left text-sm hover:bg-brand-50" onClick={() => { setCustomer(c); setCustQuery(''); setCustResults([]) }}>
                          <span><b>{c.name}</b> <span className="text-xs text-stone-500">{c.village} · {c.mobile}</span></span>
                          {c.balance > 0 && <span className="text-xs font-bold text-red-600">{inr(c.balance)}</span>}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
                <div className="grid grid-cols-2 gap-2">
                  <input className="input" placeholder="Walk-in name (optional)" value={walkIn.name} onChange={e => setWalkIn({ ...walkIn, name: e.target.value })} />
                  <input className="input" placeholder="Mobile (for WhatsApp)" inputMode="numeric" maxLength={10} value={walkIn.mobile} onChange={e => setWalkIn({ ...walkIn, mobile: e.target.value })} />
                </div>
              </div>
            )}
          </Card>

          <Card>
            <div className="mb-3 grid grid-cols-2 gap-2">
              <button disabled={!gstAllowed} onClick={() => setIsGst(true)} className={`rounded-lg border-2 py-2 text-sm font-bold ${isGst && gstAllowed ? 'border-brand-700 bg-brand-50 text-brand-800' : 'border-stone-200'}`}>GST Tax Invoice</button>
              <button onClick={() => setIsGst(false)} className={`rounded-lg border-2 py-2 text-sm font-bold ${!isGst || !gstAllowed ? 'border-brand-700 bg-brand-50 text-brand-800' : 'border-stone-200'}`}>Non-GST Bill</button>
            </div>
            {isGst && gstAllowed && <label className="mb-3 flex items-center gap-2 text-xs text-stone-600"><input type="checkbox" checked={interState} onChange={e => setInterState(e.target.checked)} /> Inter-state sale (IGST)</label>}

            <dl className="space-y-1.5 text-sm">
              <div className="flex justify-between"><dt className="text-stone-500">Taxable value</dt><dd className="tabular-nums">{inr(calc.taxable, 2)}</dd></div>
              {calc.discount > 0 && <div className="flex justify-between"><dt className="text-stone-500">Discount</dt><dd className="tabular-nums">−{inr(calc.discount, 2)}</dd></div>}
              {calc.tax > 0 && (interState
                ? <div className="flex justify-between"><dt className="text-stone-500">IGST</dt><dd className="tabular-nums">{inr(calc.tax, 2)}</dd></div>
                : <><div className="flex justify-between"><dt className="text-stone-500">CGST</dt><dd className="tabular-nums">{inr(calc.tax / 2, 2)}</dd></div>
                  <div className="flex justify-between"><dt className="text-stone-500">SGST</dt><dd className="tabular-nums">{inr(calc.tax / 2, 2)}</dd></div></>)}
              <div className="flex justify-between"><dt className="text-stone-500">Round off</dt><dd className="tabular-nums">{num(calc.roundOff)}</dd></div>
              <div className="flex justify-between border-t border-dashed border-stone-300 pt-2 text-2xl font-extrabold text-brand-900"><dt>{t('total')}</dt><dd className="tabular-nums">{inr(calc.grand)}</dd></div>
            </dl>

            <div className="mt-4 grid grid-cols-4 gap-1.5">
              {MODES.map(m => (
                <button key={m.mode} onClick={() => { setMode(m.mode); if (m.mode === 'Credit') setPaid('0') ; else setPaid('') }}
                  className={`rounded-lg border-2 py-2 text-xs font-bold ${mode === m.mode ? 'border-brand-700 bg-brand-700 text-white' : 'border-stone-200'}`}>
                  <div className="text-lg">{m.icon}</div>{m.label}
                </button>
              ))}
            </div>
            {mode !== 'Credit' && (
              <Field label={`${t('paid')} (leave blank = full)`} className="mt-3">
                <input type="number" className="input text-lg font-bold" placeholder={String(calc.grand)} value={paid} onChange={e => setPaid(e.target.value)} />
              </Field>
            )}
            {balance > 0 && <div className="mt-2 rounded-lg bg-red-50 px-3 py-2 text-sm font-bold text-red-700">🤝 Udhari on this bill: {inr(balance)}</div>}
            {paid !== '' && Number(paid) > calc.grand && <div className="mt-2 rounded-lg bg-sky-50 px-3 py-2 text-sm font-bold text-sky-700">Return change: {inr(Number(paid) - calc.grand)}</div>}
            <input className="input mt-3" placeholder="Note (optional) – e.g. crop / advice" value={notes} onChange={e => setNotes(e.target.value)} />

            <div className="mt-4 grid gap-2">
              <Button id="save-bill" className="py-3 text-base" loading={saving} onClick={() => save('print')}>🖨️ {t('saveBill')} & Print (F9)</Button>
              <div className="grid grid-cols-2 gap-2">
                <Button variant="whatsapp" loading={saving} onClick={() => save('whatsapp')}>💬 Save & WhatsApp</Button>
                <Button variant="secondary" loading={saving} onClick={() => save('new')}>Save & New</Button>
              </div>
            </div>
          </Card>
        </div>
      </div>

      <Modal open={addCust} onClose={() => setAddCust(false)} title={t('addCustomer')}>
        <CustomerForm onSaved={c => { setCustomer(c); setAddCust(false) }} />
      </Modal>
    </div>
  )
}
