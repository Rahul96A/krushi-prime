import { useState } from 'react'
import { api, qs } from '../lib/api'
import type { DayValue, NameValue, SaleListItem } from '../lib/types'
import { dmy, downloadCsv, inr, monthStart, num, today } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { useAuth } from '../lib/auth'
import { Button, Card, Empty, PageHeader, Spinner, Stat, useLoad } from '../components/ui'

interface SalesR { bills: number; taxable: number; tax: number; grandTotal: number; paid: number; balance: number; byPaymentMode: NameValue[]; daily: DayValue[]; invoices: SaleListItem[] }
interface GstR { taxable: number; cgst: number; sgst: number; igst: number; nonGstSales: number; purchaseTaxCredit: number
  rateWise: { rate: number; taxable: number; cgst: number; sgst: number; igst: number; total: number }[]
  hsnWise: { hsn: string; description: string; uqc: string; quantity: number; rate: number; taxable: number; cgst: number; sgst: number; igst: number; total: number }[]
  b2b: { invoiceNo: string; date: string; customerName: string; gstin: string; taxable: number; tax: number; total: number }[] }
interface ProfitR { revenue: number; cost: number; grossProfit: number; marginPercent: number; byProduct: NameValue[] }

const TABS = [
  { id: 'sales', label: 'Sales summary', roles: ['Owner', 'Manager', 'Cashier'] },
  { id: 'gst', label: 'GST report (GSTR-1)', roles: ['Owner', 'Manager'] },
  { id: 'profit', label: 'Profit', roles: ['Owner'] },
]

export default function Reports() {
  const { t } = useI18n()
  const { can } = useAuth()
  const [tab, setTab] = useState('sales')
  const [range, setRange] = useState({ from: monthStart(), to: today() })
  const tabs = TABS.filter(x => can(...x.roles))

  return (
    <div>
      <PageHeader title={`📈 ${t('reports')}`} actions={<Button variant="secondary" onClick={() => window.print()}>🖨️ Print</Button>} />
      <div className="no-print mb-4 flex flex-wrap items-center gap-2">
        <div className="flex rounded-lg border border-stone-300 bg-white p-0.5 text-sm font-bold">
          {tabs.map(x => <button key={x.id} onClick={() => setTab(x.id)} className={`rounded-md px-4 py-1.5 ${tab === x.id ? 'bg-brand-700 text-white' : ''}`}>{x.label}</button>)}
        </div>
        <input type="date" className="input max-w-[11rem]" value={range.from} onChange={e => setRange({ ...range, from: e.target.value })} />
        <span>→</span>
        <input type="date" className="input max-w-[11rem]" value={range.to} onChange={e => setRange({ ...range, to: e.target.value })} />
      </div>
      <div className="print-area">
        {tab === 'sales' && <SalesReport range={range} />}
        {tab === 'gst' && <GstReport range={range} />}
        {tab === 'profit' && <ProfitReport range={range} />}
      </div>
    </div>
  )
}

type R = { range: { from: string; to: string } }

function SalesReport({ range }: R) {
  const { data, loading } = useLoad(() => api.get<SalesR>(`/api/reports/sales${qs(range)}`), [range])
  if (loading || !data) return <Spinner />
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label="Total sales" value={inr(data.grandTotal)} sub={`${data.bills} bills`} icon="🧾" />
        <Stat label="GST collected" value={inr(data.tax, 2)} tone="blue" icon="🏛️" />
        <Stat label="Received" value={inr(data.paid)} tone="gold" icon="💰" />
        <Stat label="Given on udhari" value={inr(data.balance)} tone="red" icon="🤝" />
      </div>
      <div className="grid gap-4 lg:grid-cols-3">
        <Card title="By payment mode">
          <table className="tbl"><tbody>{data.byPaymentMode.map(m => <tr key={m.name}><td>{m.name}</td><td className="num">{m.quantity} bills</td><td className="num font-bold">{inr(m.amount)}</td></tr>)}</tbody></table>
        </Card>
        <Card title="Day-wise" className="lg:col-span-2" actions={<Button variant="secondary" onClick={() => downloadCsv('daywise-sales.csv', data.daily.map(d => ({ Date: dmy(d.date), Bills: d.bills, Sales: d.sales })))}>⬇ CSV</Button>}>
          <div className="max-h-72 overflow-y-auto"><table className="tbl"><thead><tr><th>Date</th><th className="num">Bills</th><th className="num">Sales</th></tr></thead>
            <tbody>{data.daily.map(d => <tr key={d.date}><td>{dmy(d.date)}</td><td className="num">{d.bills}</td><td className="num font-semibold">{inr(d.sales)}</td></tr>)}</tbody></table></div>
        </Card>
      </div>
    </div>
  )
}

function GstReport({ range }: R) {
  const { data, loading } = useLoad(() => api.get<GstR>(`/api/reports/gst${qs(range)}`), [range])
  if (loading || !data) return <Spinner />
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        <Stat label="Taxable value" value={inr(data.taxable)} icon="📄" />
        <Stat label="CGST" value={inr(data.cgst, 2)} tone="blue" />
        <Stat label="SGST" value={inr(data.sgst, 2)} tone="blue" />
        <Stat label="IGST" value={inr(data.igst, 2)} tone="stone" />
        <Stat label="Input tax (purchases)" value={inr(data.purchaseTaxCredit, 2)} tone="gold" sub={`Non-GST sales ${inr(data.nonGstSales)}`} />
      </div>
      <Card title="Rate-wise summary">
        <table className="tbl"><thead><tr><th>GST %</th><th className="num">Taxable</th><th className="num">CGST</th><th className="num">SGST</th><th className="num">IGST</th><th className="num">Total</th></tr></thead>
          <tbody>{data.rateWise.map(r => <tr key={r.rate}><td>{r.rate}%</td><td className="num">{num(r.taxable)}</td><td className="num">{num(r.cgst)}</td><td className="num">{num(r.sgst)}</td><td className="num">{num(r.igst)}</td><td className="num font-bold">{num(r.total)}</td></tr>)}</tbody></table>
      </Card>
      <Card title="HSN-wise summary (Table 12, GSTR-1)" actions={<Button variant="secondary" onClick={() => downloadCsv(`hsn-summary-${range.from}-${range.to}.csv`, data.hsnWise.map(h => ({ HSN: h.hsn, Description: h.description, UQC: h.uqc, 'Total Quantity': h.quantity, Rate: h.rate, 'Taxable Value': h.taxable, 'Integrated Tax': h.igst, 'Central Tax': h.cgst, 'State/UT Tax': h.sgst, 'Total Value': h.total })))}>⬇ CSV for GST portal</Button>}>
        {data.hsnWise.length === 0 ? <Empty /> : (
          <div className="-mx-4 overflow-x-auto"><table className="tbl"><thead><tr><th>HSN</th><th>Description</th><th>UQC</th><th className="num">Qty</th><th className="num">Rate</th><th className="num">Taxable</th><th className="num">CGST</th><th className="num">SGST</th><th className="num">IGST</th><th className="num">Total</th></tr></thead>
            <tbody>{data.hsnWise.map(h => <tr key={h.hsn + h.rate + h.uqc}><td className="font-semibold">{h.hsn}</td><td className="text-xs">{h.description}</td><td>{h.uqc}</td><td className="num">{h.quantity}</td><td className="num">{h.rate}%</td><td className="num">{num(h.taxable)}</td><td className="num">{num(h.cgst)}</td><td className="num">{num(h.sgst)}</td><td className="num">{num(h.igst)}</td><td className="num font-bold">{num(h.total)}</td></tr>)}</tbody></table></div>
        )}
      </Card>
      <Card title={`B2B invoices (${data.b2b.length})`}>
        {data.b2b.length === 0 ? <Empty text="No B2B (GSTIN) sales in this period" /> : (
          <table className="tbl"><thead><tr><th>Invoice</th><th>Date</th><th>Customer</th><th>GSTIN</th><th className="num">Taxable</th><th className="num">Tax</th><th className="num">Total</th></tr></thead>
            <tbody>{data.b2b.map(b => <tr key={b.invoiceNo}><td>{b.invoiceNo}</td><td>{dmy(b.date)}</td><td>{b.customerName}</td><td>{b.gstin}</td><td className="num">{num(b.taxable)}</td><td className="num">{num(b.tax)}</td><td className="num">{num(b.total)}</td></tr>)}</tbody></table>
        )}
      </Card>
    </div>
  )
}

function ProfitReport({ range }: R) {
  const { data, loading } = useLoad(() => api.get<ProfitR>(`/api/reports/profit${qs(range)}`), [range])
  if (loading || !data) return <Spinner />
  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label="Revenue (excl. GST)" value={inr(data.revenue)} icon="💹" />
        <Stat label="Cost of goods" value={inr(data.cost)} tone="stone" icon="📦" />
        <Stat label="Gross profit" value={inr(data.grossProfit)} tone="gold" icon="🏆" />
        <Stat label="Margin" value={`${data.marginPercent}%`} tone="blue" icon="📐" />
      </div>
      <Card title="Product-wise profit" actions={<Button variant="secondary" onClick={() => downloadCsv('profit.csv', data.byProduct.map(p => ({ Product: p.name, Qty: p.quantity, Profit: p.amount })))}>⬇ CSV</Button>}>
        <table className="tbl"><thead><tr><th>Product</th><th className="num">Qty sold</th><th className="num">Profit</th></tr></thead>
          <tbody>{data.byProduct.map(p => <tr key={p.name}><td>{p.name}</td><td className="num">{p.quantity}</td><td className={`num font-bold ${p.amount < 0 ? 'text-red-600' : 'text-brand-700'}`}>{inr(p.amount)}</td></tr>)}</tbody></table>
      </Card>
    </div>
  )
}
