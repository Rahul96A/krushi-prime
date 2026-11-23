import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { api } from '../lib/api'
import { dmy, downloadCsv, inr, qty } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { Badge, Button, Card, Empty, PageHeader, Spinner, Stat, useLoad } from '../components/ui'

interface StockRow { productId: number; name: string; localName?: string; category: string; company?: string; unit: string; stock: number; reorderLevel: number; purchaseValue: number; saleValue: number; isLow: boolean; nearestExpiry?: string }
interface ExpiryRow { productId: number; name: string; batchNo: string; expiryDate: string; quantity: number; daysLeft: number; value: number }

export default function Stock() {
  const { t } = useI18n()
  const [sp, setSp] = useSearchParams()
  const tab = sp.get('tab') ?? 'stock'
  const [lowOnly, setLowOnly] = useState(false)
  const [days, setDays] = useState(60)
  const stock = useLoad(() => api.get<StockRow[]>('/api/reports/stock'))
  const expiry = useLoad(() => api.get<ExpiryRow[]>(`/api/reports/expiry?days=${days}`), [days])
  const rows = (stock.data ?? []).filter(r => !lowOnly || r.isLow)

  return (
    <div>
      <PageHeader title={`📦 ${t('stock')}`} actions={
        <div className="flex rounded-lg border border-stone-300 bg-white p-0.5 text-sm font-bold">
          {['stock', 'expiry'].map(x => <button key={x} onClick={() => setSp({ tab: x })} className={`rounded-md px-4 py-1.5 ${tab === x ? 'bg-brand-700 text-white' : ''}`}>{x === 'stock' ? 'Stock report' : 'Expiry alerts'}</button>)}
        </div>} />

      {tab === 'stock' ? (
        <>
          <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
            <Stat label="Products" value={stock.data?.length ?? 0} icon="🌾" />
            <Stat label="Stock value (cost)" value={inr(stock.data?.reduce((s, r) => s + r.purchaseValue, 0))} tone="blue" icon="💼" />
            <Stat label="Stock value (sale)" value={inr(stock.data?.reduce((s, r) => s + r.saleValue, 0))} tone="gold" icon="🏷️" />
            <Stat label={t('lowStock')} value={stock.data?.filter(r => r.isLow).length ?? 0} tone="red" icon="⚠️" />
          </div>
          <Card actions={<><label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={lowOnly} onChange={e => setLowOnly(e.target.checked)} /> Low stock only</label>
            <Button variant="secondary" onClick={() => downloadCsv('stock.csv', rows.map(r => ({ Product: r.name, Category: r.category, Company: r.company ?? '', Stock: r.stock, Unit: r.unit, Reorder: r.reorderLevel, CostValue: r.purchaseValue, SaleValue: r.saleValue })))}>⬇ CSV</Button>
            <Button variant="secondary" onClick={() => window.print()}>🖨️</Button></>}>
            {stock.loading ? <Spinner /> : rows.length === 0 ? <Empty /> : (
              <div className="print-area -mx-4 overflow-x-auto">
                <table className="tbl">
                  <thead><tr><th>Product</th><th>Category</th><th className="num">Stock</th><th className="num">Reorder at</th><th className="num">Cost value</th><th className="num">Sale value</th><th>Nearest expiry</th></tr></thead>
                  <tbody>{rows.map(r => (
                    <tr key={r.productId}>
                      <td className="font-semibold">{r.name}<div className="text-xs font-normal text-stone-500">{r.company}</div></td><td>{r.category}</td>
                      <td className="num"><Badge tone={r.stock <= 0 ? 'red' : r.isLow ? 'amber' : 'green'}>{qty(r.stock)} {r.unit}</Badge></td><td className="num">{qty(r.reorderLevel)}</td>
                      <td className="num">{inr(r.purchaseValue)}</td><td className="num">{inr(r.saleValue)}</td><td className="text-xs">{dmy(r.nearestExpiry)}</td>
                    </tr>))}</tbody>
                </table>
              </div>
            )}
          </Card>
        </>
      ) : (
        <Card title="Batches expiring soon (sell first – FEFO is automatic in billing)" actions={
          <select className="input max-w-[11rem]" value={days} onChange={e => setDays(Number(e.target.value))}>{[30, 60, 90, 180].map(d => <option key={d} value={d}>Next {d} days</option>)}</select>}>
          {expiry.loading ? <Spinner /> : !expiry.data?.length ? <Empty text="No batches expiring in this period 👍" /> : (
            <div className="-mx-4 overflow-x-auto">
              <table className="tbl">
                <thead><tr><th>Product</th><th>Batch</th><th>Expiry</th><th className="num">Qty</th><th className="num">Value</th><th>Status</th></tr></thead>
                <tbody>{expiry.data.map(r => (
                  <tr key={r.productId + r.batchNo}>
                    <td className="font-semibold">{r.name}</td><td>{r.batchNo}</td><td>{dmy(r.expiryDate)}</td><td className="num">{qty(r.quantity)}</td><td className="num">{inr(r.value)}</td>
                    <td>{r.daysLeft < 0 ? <Badge tone="red">Expired {-r.daysLeft}d ago</Badge> : r.daysLeft <= 30 ? <Badge tone="amber">{r.daysLeft} days left</Badge> : <Badge>{r.daysLeft} days</Badge>}</td>
                  </tr>))}</tbody>
              </table>
            </div>
          )}
        </Card>
      )}
    </div>
  )
}
