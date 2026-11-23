import { useMemo, useState } from 'react'
import { api } from '../lib/api'
import type { PaymentMode, Product, Supplier } from '../lib/types'
import { dmy, inr, today } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { Button, Card, Empty, Field, Modal, PageHeader, Spinner, useLoad, useToast } from '../components/ui'

interface PurchaseRow { id: number; billNo: string; billDate: string; supplierName: string; taxAmount: number; grandTotal: number; paidAmount: number; itemCount: number }
interface PLine { key: number; productId: number; batchNo: string; expiryDate: string; quantity: number; freeQuantity: number; rate: number; gstRate: number; salePrice: number; mrp: number }

export default function Purchases() {
  const { t } = useI18n()
  const list = useLoad(() => api.get<PurchaseRow[]>('/api/purchases'))
  const [open, setOpen] = useState(false)
  return (
    <div>
      <PageHeader title={`🚚 ${t('purchases')}`} subtitle="Stock inward from distributors – batches & expiry are created automatically" actions={<Button onClick={() => setOpen(true)}>+ New purchase</Button>} />
      <Card>
        {list.loading ? <Spinner /> : !list.data?.length ? <Empty /> : (
          <div className="-mx-4 overflow-x-auto">
            <table className="tbl">
              <thead><tr><th>Bill No</th><th>Date</th><th>Supplier</th><th className="num">Items</th><th className="num">GST</th><th className="num">Total</th><th className="num">Paid</th></tr></thead>
              <tbody>{list.data.map(p => (
                <tr key={p.id}><td className="font-semibold">{p.billNo}</td><td>{dmy(p.billDate)}</td><td>{p.supplierName}</td><td className="num">{p.itemCount}</td>
                  <td className="num">{inr(p.taxAmount, 2)}</td><td className="num font-bold">{inr(p.grandTotal)}</td><td className="num">{inr(p.paidAmount)}</td></tr>))}</tbody>
            </table>
          </div>
        )}
      </Card>
      <Modal open={open} onClose={() => setOpen(false)} title="New purchase bill" wide>
        {open && <PurchaseForm onDone={() => { setOpen(false); list.reload() }} />}
      </Modal>
    </div>
  )
}

function PurchaseForm({ onDone }: { onDone: () => void }) {
  const toast = useToast()
  const { data: suppliers } = useLoad(() => api.get<Supplier[]>('/api/suppliers'))
  const { data: products } = useLoad(() => api.get<Product[]>('/api/products'))
  const [h, setH] = useState({ supplierId: 0, billNo: '', billDate: today(), isInterState: false, otherCharges: 0, paidAmount: 0, paymentMode: 'Credit' as PaymentMode, notes: '' })
  const [lines, setLines] = useState<PLine[]>([])
  const [pick, setPick] = useState('')
  const [busy, setBusy] = useState(false)

  const addLine = (id: number) => {
    const p = products?.find(x => x.id === id)
    if (!p) return
    setLines(l => [...l, { key: Date.now(), productId: p.id, batchNo: '', expiryDate: '', quantity: 1, freeQuantity: 0, rate: +(p.purchasePrice / (1 + p.gstRate / 100)).toFixed(2), gstRate: p.gstRate, salePrice: p.salePrice, mrp: p.mrp }])
    setPick('')
  }
  const upd = (key: number, patch: Partial<PLine>) => setLines(l => l.map(x => (x.key === key ? { ...x, ...patch } : x)))
  const totals = useMemo(() => {
    const sub = lines.reduce((s, l) => s + l.quantity * l.rate, 0)
    const tax = lines.reduce((s, l) => s + (l.quantity * l.rate * l.gstRate) / 100, 0)
    return { sub, tax, grand: Math.round(sub + tax + h.otherCharges) }
  }, [lines, h.otherCharges])

  const save = async () => {
    if (!h.supplierId || !h.billNo || !lines.length) return toast.err('Select supplier, enter bill no and add items')
    setBusy(true)
    try {
      await api.post('/api/purchases', { ...h, items: lines.map(l => ({ ...l, expiryDate: l.expiryDate || null })) })
      toast.ok('Purchase saved – stock updated'); onDone()
    } catch (e) { toast.err(e) } finally { setBusy(false) }
  }

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-4">
        <Field label="Supplier *" className="sm:col-span-2"><select className="input" value={h.supplierId} onChange={e => setH({ ...h, supplierId: Number(e.target.value) })}><option value={0}>Select…</option>{suppliers?.map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></Field>
        <Field label="Supplier bill no *"><input className="input" value={h.billNo} onChange={e => setH({ ...h, billNo: e.target.value })} /></Field>
        <Field label="Bill date"><input type="date" className="input" value={h.billDate} onChange={e => setH({ ...h, billDate: e.target.value })} /></Field>
      </div>
      <select className="input" value={pick} onChange={e => addLine(Number(e.target.value))}><option value="">+ Add product…</option>{products?.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select>
      {lines.length > 0 && (
        <div className="overflow-x-auto">
          <table className="tbl text-xs">
            <thead><tr><th>Product</th><th>Batch</th><th>Expiry</th><th>Qty</th><th>Free</th><th>Rate (excl. GST)</th><th>GST%</th><th>Sale ₹</th><th>MRP</th><th className="num">Amount</th><th /></tr></thead>
            <tbody>{lines.map(l => (
              <tr key={l.key}>
                <td className="font-semibold">{products?.find(p => p.id === l.productId)?.name}</td>
                <td><input className="input w-24 py-1" value={l.batchNo} onChange={e => upd(l.key, { batchNo: e.target.value })} /></td>
                <td><input type="date" className="input w-36 py-1" value={l.expiryDate} onChange={e => upd(l.key, { expiryDate: e.target.value })} /></td>
                {(['quantity', 'freeQuantity', 'rate', 'gstRate', 'salePrice', 'mrp'] as const).map(k => (
                  <td key={k}><input type="number" step="any" className="input w-20 py-1" value={l[k]} onChange={e => upd(l.key, { [k]: Number(e.target.value) })} /></td>
                ))}
                <td className="num font-semibold">{inr(l.quantity * l.rate * (1 + l.gstRate / 100), 2)}</td>
                <td><button className="text-red-500" onClick={() => setLines(x => x.filter(y => y.key !== l.key))}>×</button></td>
              </tr>))}</tbody>
          </table>
        </div>
      )}
      <div className="grid gap-3 sm:grid-cols-4">
        <Field label="Other charges (freight)"><input type="number" className="input" value={h.otherCharges} onChange={e => setH({ ...h, otherCharges: Number(e.target.value) })} /></Field>
        <Field label="Paid now ₹"><input type="number" className="input" value={h.paidAmount} onChange={e => setH({ ...h, paidAmount: Number(e.target.value), paymentMode: Number(e.target.value) > 0 ? 'Bank' : 'Credit' })} /></Field>
        <label className="flex items-center gap-2 self-end pb-2 text-sm"><input type="checkbox" checked={h.isInterState} onChange={e => setH({ ...h, isInterState: e.target.checked })} /> Inter-state (IGST)</label>
        <div className="self-end rounded-xl bg-brand-50 p-3 text-right text-sm">Taxable {inr(totals.sub, 2)} · GST {inr(totals.tax, 2)}<div className="text-xl font-extrabold text-brand-900">{inr(totals.grand)}</div></div>
      </div>
      <div className="flex justify-end"><Button loading={busy} onClick={save}>Save purchase & update stock</Button></div>
    </div>
  )
}
