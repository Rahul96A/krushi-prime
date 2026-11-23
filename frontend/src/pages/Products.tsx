import { useState, type FormEvent } from 'react'
import { api, qs } from '../lib/api'
import type { Product } from '../lib/types'
import { CATEGORIES, GST_RATES, UNITS } from '../lib/types'
import { dmy, downloadCsv, inr, qty } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { useAuth } from '../lib/auth'
import { Badge, Button, Card, Empty, Field, Modal, PageHeader, Spinner, useLoad, useToast } from '../components/ui'

const blank = { name: '', localName: '', category: 'Fertilizer', company: '', hsnCode: '', gstRate: 5, unit: 'Bag', packSize: '', barcode: '', salePrice: 0, mrp: 0, purchasePrice: 0, reorderLevel: 5, trackExpiry: true, isActive: true, openingQty: 0, openingBatchNo: '', openingExpiry: '' }

export default function Products() {
  const { t } = useI18n()
  const { can } = useAuth()
  const toast = useToast()
  const [q, setQ] = useState('')
  const [cat, setCat] = useState('')
  const { data, loading, reload } = useLoad(() => api.get<Product[]>(`/api/products${qs({ q, category: cat, batches: true })}`), [q, cat])
  const [edit, setEdit] = useState<(typeof blank & { id?: number }) | null>(null)
  const [adjust, setAdjust] = useState<Product | null>(null)
  const [saving, setSaving] = useState(false)

  const save = async (e: FormEvent) => {
    e.preventDefault()
    if (!edit) return
    setSaving(true)
    try {
      const body = { ...edit, openingQty: edit.id ? null : edit.openingQty || null, openingExpiry: edit.openingExpiry || null, barcode: edit.barcode || null }
      edit.id ? await api.put(`/api/products/${edit.id}`, body) : await api.post('/api/products', body)
      toast.ok('Product saved'); setEdit(null); reload()
    } catch (err) { toast.err(err) } finally { setSaving(false) }
  }

  const set = (k: string, v: unknown) => setEdit(e => (e ? { ...e, [k]: v } : e))

  return (
    <div>
      <PageHeader title={`🌾 ${t('products')}`} subtitle={`${data?.length ?? 0} products`}
        actions={<><Button variant="secondary" onClick={() => data && downloadCsv('products.csv', data.map(p => ({ Name: p.name, Marathi: p.localName ?? '', Category: p.category, Company: p.company ?? '', HSN: p.hsnCode ?? '', GST: p.gstRate, Unit: p.unit, SalePrice: p.salePrice, MRP: p.mrp, Stock: p.stock })))}>⬇ CSV</Button>
          {can('Owner', 'Manager') && <Button onClick={() => setEdit({ ...blank })}>+ {t('addProduct')}</Button>}</>} />
      <Card>
        <div className="mb-4 grid gap-2 sm:grid-cols-3">
          <input className="input sm:col-span-2" placeholder={`${t('search')}…`} value={q} onChange={e => setQ(e.target.value)} />
          <select className="input" value={cat} onChange={e => setCat(e.target.value)}><option value="">All categories</option>{CATEGORIES.map(c => <option key={c}>{c}</option>)}</select>
        </div>
        {loading ? <Spinner /> : !data?.length ? <Empty /> : (
          <div className="-mx-4 overflow-x-auto">
            <table className="tbl">
              <thead><tr><th>Product</th><th>Category</th><th>HSN / GST</th><th className="num">Sale ₹</th><th className="num">MRP</th><th className="num">Stock</th><th>Nearest expiry</th><th /></tr></thead>
              <tbody>
                {data.map(p => (
                  <tr key={p.id}>
                    <td><div className="font-semibold">{p.name}</div><div className="text-xs text-stone-500">{p.localName} · {p.company}</div></td>
                    <td><Badge tone="green">{p.category}</Badge></td><td className="text-xs">{p.hsnCode} · {p.gstRate}%</td>
                    <td className="num">{inr(p.salePrice, 2)}</td><td className="num text-stone-500">{inr(p.mrp, 2)}</td>
                    <td className="num"><Badge tone={p.stock <= 0 ? 'red' : p.stock <= p.reorderLevel ? 'amber' : 'green'}>{qty(p.stock)} {p.unit}</Badge></td>
                    <td className="text-xs">{dmy(p.nearestExpiry)}</td>
                    <td className="whitespace-nowrap text-right">
                      {can('Owner', 'Manager') && <>
                        <Button variant="ghost" className="px-2 py-1" onClick={() => setAdjust(p)}>± Stock</Button>
                        <Button variant="ghost" className="px-2 py-1" onClick={() => setEdit({ ...blank, ...p, localName: p.localName ?? '', company: p.company ?? '', hsnCode: p.hsnCode ?? '', packSize: p.packSize ?? '', barcode: p.barcode ?? '', id: p.id })}>Edit</Button>
                      </>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      <Modal open={!!edit} onClose={() => setEdit(null)} title={edit?.id ? 'Edit product' : t('addProduct')} wide>
        {edit && (
          <form onSubmit={save} className="grid gap-3 sm:grid-cols-3">
            <Field label="Product name *" className="sm:col-span-2"><input className="input" required value={edit.name} onChange={e => set('name', e.target.value)} /></Field>
            <Field label="Marathi name"><input className="input" value={edit.localName} onChange={e => set('localName', e.target.value)} /></Field>
            <Field label="Category"><select className="input" value={edit.category} onChange={e => set('category', e.target.value)}>{CATEGORIES.map(c => <option key={c}>{c}</option>)}</select></Field>
            <Field label="Company / Brand"><input className="input" value={edit.company} onChange={e => set('company', e.target.value)} /></Field>
            <Field label="Barcode"><input className="input" value={edit.barcode} onChange={e => set('barcode', e.target.value)} /></Field>
            <Field label="HSN code"><input className="input" value={edit.hsnCode} onChange={e => set('hsnCode', e.target.value)} /></Field>
            <Field label="GST %"><select className="input" value={edit.gstRate} onChange={e => set('gstRate', Number(e.target.value))}>{GST_RATES.map(r => <option key={r} value={r}>{r}%</option>)}</select></Field>
            <Field label="Unit"><select className="input" value={edit.unit} onChange={e => set('unit', e.target.value)}>{UNITS.map(u => <option key={u}>{u}</option>)}</select></Field>
            <Field label="Purchase price ₹"><input type="number" step="any" className="input" value={edit.purchasePrice} onChange={e => set('purchasePrice', Number(e.target.value))} /></Field>
            <Field label="Sale price ₹ *"><input type="number" step="any" required className="input" value={edit.salePrice} onChange={e => set('salePrice', Number(e.target.value))} /></Field>
            <Field label="MRP ₹"><input type="number" step="any" className="input" value={edit.mrp} onChange={e => set('mrp', Number(e.target.value))} /></Field>
            <Field label="Pack size"><input className="input" value={edit.packSize} onChange={e => set('packSize', e.target.value)} placeholder="50 Kg / 250 ml" /></Field>
            <Field label="Reorder level"><input type="number" className="input" value={edit.reorderLevel} onChange={e => set('reorderLevel', Number(e.target.value))} /></Field>
            <label className="flex items-center gap-2 self-end pb-2 text-sm"><input type="checkbox" checked={edit.trackExpiry} onChange={e => set('trackExpiry', e.target.checked)} /> Track batch expiry</label>
            {!edit.id && <>
              <div className="sm:col-span-3 mt-2 border-t border-stone-100 pt-3 text-xs font-bold uppercase text-stone-500">Opening stock (optional)</div>
              <Field label="Opening qty"><input type="number" step="any" className="input" value={edit.openingQty} onChange={e => set('openingQty', Number(e.target.value))} /></Field>
              <Field label="Batch no"><input className="input" value={edit.openingBatchNo} onChange={e => set('openingBatchNo', e.target.value)} /></Field>
              <Field label="Expiry date"><input type="date" className="input" value={edit.openingExpiry} onChange={e => set('openingExpiry', e.target.value)} /></Field>
            </>}
            <div className="sm:col-span-3 flex justify-end gap-2 pt-2">
              {edit.id && <Button type="button" variant="danger" className="mr-auto" onClick={async () => { await api.del(`/api/products/${edit.id}`); toast.ok('Product deactivated'); setEdit(null); reload() }}>Deactivate</Button>}
              <Button type="button" variant="secondary" onClick={() => setEdit(null)}>Cancel</Button><Button loading={saving}>{t('save')}</Button>
            </div>
          </form>
        )}
      </Modal>

      <Modal open={!!adjust} onClose={() => setAdjust(null)} title={`Stock adjustment – ${adjust?.name}`}>
        {adjust && <AdjustForm p={adjust} onDone={() => { setAdjust(null); reload() }} />}
      </Modal>
    </div>
  )
}

function AdjustForm({ p, onDone }: { p: Product; onDone: () => void }) {
  const toast = useToast()
  const [f, setF] = useState({ batchId: p.batches?.[0]?.id ?? 0, change: 0, note: '', newBatchNo: '', newBatchExpiry: '' })
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    try {
      await api.post('/api/products/adjust-stock', { productId: p.id, batchId: f.batchId || null, quantityChange: f.change, note: f.note, newBatchNo: f.newBatchNo || null, newBatchExpiry: f.newBatchExpiry || null })
      toast.ok('Stock updated'); onDone()
    } catch (err) { toast.err(err) }
  }
  return (
    <form onSubmit={submit} className="space-y-3">
      <table className="tbl"><thead><tr><th>Batch</th><th>Expiry</th><th className="num">Qty</th></tr></thead>
        <tbody>{(p.batches ?? []).map(b => <tr key={b.id}><td>{b.batchNo}</td><td>{dmy(b.expiryDate)}</td><td className="num">{qty(b.quantity)}</td></tr>)}</tbody></table>
      <Field label="Batch"><select className="input" value={f.batchId} onChange={e => setF({ ...f, batchId: Number(e.target.value) })}>
        {(p.batches ?? []).map(b => <option key={b.id} value={b.id}>{b.batchNo}</option>)}<option value={0}>+ New batch</option></select></Field>
      {f.batchId === 0 && <div className="grid grid-cols-2 gap-2"><input className="input" placeholder="Batch no" value={f.newBatchNo} onChange={e => setF({ ...f, newBatchNo: e.target.value })} /><input type="date" className="input" value={f.newBatchExpiry} onChange={e => setF({ ...f, newBatchExpiry: e.target.value })} /></div>}
      <Field label="Quantity change (+ add / − remove)"><input type="number" step="any" className="input" required value={f.change} onChange={e => setF({ ...f, change: Number(e.target.value) })} /></Field>
      <Field label="Reason"><input className="input" required value={f.note} onChange={e => setF({ ...f, note: e.target.value })} placeholder="Damaged / physical count / opening" /></Field>
      <div className="flex justify-end"><Button>Update stock</Button></div>
    </form>
  )
}
