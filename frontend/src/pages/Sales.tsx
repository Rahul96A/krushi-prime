import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api, qs } from '../lib/api'
import type { Paged, SaleListItem } from '../lib/types'
import { dmyt, downloadCsv, inr, monthStart, today } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { Badge, Button, Card, Empty, PageHeader, Spinner, statusTone, useLoad } from '../components/ui'

export default function Sales() {
  const { t } = useI18n()
  const nav = useNavigate()
  const [sp] = useSearchParams()
  const [f, setF] = useState({ from: sp.get('q') ? '' : monthStart(), to: today(), q: sp.get('q') ?? '', status: '' })
  const [page, setPage] = useState(1)
  const { data, loading } = useLoad(() => api.get<Paged<SaleListItem>>(`/api/sales${qs({ ...f, page, pageSize: 50 })}`), [f, page])
  const rows = data?.items ?? []
  const pages = data ? Math.ceil(data.total / data.pageSize) : 1

  return (
    <div>
      <PageHeader title={`📄 ${t('sales')}`} subtitle={data ? `${data.total} bills` : ''}
        actions={<><Button variant="secondary" onClick={() => downloadCsv(`invoices-${f.from}-${f.to}.csv`, rows.map(r => ({ InvoiceNo: r.invoiceNo, Date: dmyt(r.invoiceDate), Customer: r.customerName, Mobile: r.customerMobile ?? '', GST: r.isGst ? 'Yes' : 'No', Total: r.grandTotal, Paid: r.paidAmount, Balance: r.balanceAmount, Mode: r.paymentMode, Status: r.status })))}>⬇ CSV</Button>
          <Button onClick={() => nav('/billing')}>+ {t('billing')}</Button></>} />
      <Card>
        <div className="mb-4 grid gap-2 sm:grid-cols-4">
          <input type="date" className="input" value={f.from} onChange={e => { setF({ ...f, from: e.target.value }); setPage(1) }} />
          <input type="date" className="input" value={f.to} onChange={e => { setF({ ...f, to: e.target.value }); setPage(1) }} />
          <input className="input" placeholder="Bill no / customer / mobile" value={f.q} onChange={e => { setF({ ...f, q: e.target.value }); setPage(1) }} />
          <select className="input" value={f.status} onChange={e => { setF({ ...f, status: e.target.value }); setPage(1) }}>
            <option value="">All status</option>{['Paid', 'Partial', 'Unpaid', 'Cancelled'].map(s => <option key={s}>{s}</option>)}
          </select>
        </div>
        {loading ? <Spinner /> : rows.length === 0 ? <Empty /> : (
          <div className="-mx-4 overflow-x-auto">
            <table className="tbl">
              <thead><tr><th>Bill No</th><th>Date</th><th>Customer</th><th>Type</th><th className="num">Total</th><th className="num">Paid</th><th className="num">Udhari</th><th>Mode</th><th>Status</th></tr></thead>
              <tbody>
                {rows.map(r => (
                  <tr key={r.id} className="cursor-pointer" onClick={() => nav(`/sales/${r.id}`)}>
                    <td className="font-semibold text-brand-800">{r.invoiceNo}</td><td className="text-xs">{dmyt(r.invoiceDate)}</td>
                    <td>{r.customerName}<div className="text-xs text-stone-500">{r.customerMobile}</div></td><td>{r.isGst ? <Badge tone="blue">GST</Badge> : <Badge>Non-GST</Badge>}</td>
                    <td className="num font-bold">{inr(r.grandTotal)}</td><td className="num">{inr(r.paidAmount)}</td>
                    <td className={`num ${r.balanceAmount > 0 ? 'font-bold text-red-600' : ''}`}>{inr(r.balanceAmount)}</td><td>{r.paymentMode}</td><td><Badge tone={statusTone(r.status)}>{r.status}</Badge></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {pages > 1 && (
          <div className="mt-4 flex items-center justify-end gap-2 text-sm">
            <Button variant="secondary" disabled={page <= 1} onClick={() => setPage(p => p - 1)}>‹ Prev</Button><span>Page {page} / {pages}</span>
            <Button variant="secondary" disabled={page >= pages} onClick={() => setPage(p => p + 1)}>Next ›</Button>
          </div>
        )}
      </Card>
    </div>
  )
}
