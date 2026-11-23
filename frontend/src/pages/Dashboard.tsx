import { Link, useNavigate } from 'react-router-dom'
import { Bar, BarChart, CartesianGrid, Cell, Pie, PieChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { api } from '../lib/api'
import type { Dashboard as D } from '../lib/types'
import { inr, dmyt } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { useAuth } from '../lib/auth'
import { Badge, Button, Card, Empty, PageHeader, Spinner, Stat, statusTone, useLoad } from '../components/ui'

const COLORS = ['#15803d', '#eab308', '#0ea5e9', '#f97316', '#8b5cf6', '#ef4444', '#14b8a6', '#78716c']

export default function Dashboard() {
  const { t } = useI18n()
  const { user } = useAuth()
  const nav = useNavigate()
  const { data: d, loading } = useLoad(() => api.get<D>('/api/reports/dashboard'))

  if (loading || !d) return <Spinner />
  const hour = new Date().getHours()
  const greet = hour < 12 ? 'सुप्रभात' : hour < 17 ? 'नमस्कार' : 'शुभ संध्या'

  return (
    <div className="space-y-5">
      <PageHeader title={`${greet}, ${user?.fullName.split(' ')[0]} 🙏`} subtitle={new Date().toLocaleDateString('en-IN', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}
        actions={<><Button onClick={() => nav('/billing')}>🧾 {t('billing')} (F2)</Button><Button variant="secondary" onClick={() => nav('/udhari')}>🤝 {t('udhari')}</Button></>} />

      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label={t('todaySales')} value={inr(d.todaySales)} sub={`${d.todayBills} bills`} icon="🧾" />
        <Stat label={t('todayCollection')} value={inr(d.todayCollection)} sub="Cash + UPI + udhari recovered" tone="blue" icon="💰" />
        <Stat label={t('monthSales')} value={inr(d.monthSales)} sub={`Gross profit ${inr(d.monthProfit)}`} tone="gold" icon="📈" />
        <Stat label={t('totalUdhari')} value={inr(d.totalUdhari)} sub={`${d.udhariCustomers} farmers`} tone="red" icon="🤝" />
      </div>

      {(d.lowStockCount > 0 || d.expiringSoonCount > 0 || d.expiredCount > 0) && (
        <div className="flex flex-wrap gap-2">
          {d.lowStockCount > 0 && <Link to="/stock" className="rounded-xl bg-amber-100 px-4 py-2 text-sm font-semibold text-amber-800">⚠️ {d.lowStockCount} products low on stock – reorder</Link>}
          {d.expiringSoonCount > 0 && <Link to="/stock?tab=expiry" className="rounded-xl bg-orange-100 px-4 py-2 text-sm font-semibold text-orange-800">⏳ {d.expiringSoonCount} batch{d.expiringSoonCount > 1 ? "es" : ""} expiring within 30 days</Link>}
          {d.expiredCount > 0 && <Link to="/stock?tab=expiry" className="rounded-xl bg-red-100 px-4 py-2 text-sm font-semibold text-red-700">⛔ {d.expiredCount} expired batches in stock</Link>}
        </div>
      )}

      <div className="grid gap-5 lg:grid-cols-3">
        <Card title="Last 7 days sales" className="lg:col-span-2">
          <div className="h-64">
            <ResponsiveContainer>
              <BarChart data={d.last7Days.map(x => ({ ...x, day: new Date(x.date).toLocaleDateString('en-IN', { weekday: 'short', day: 'numeric' }) }))}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} />
                <XAxis dataKey="day" fontSize={12} />
                <YAxis fontSize={12} tickFormatter={v => `₹${(v / 1000).toFixed(0)}k`} />
                <Tooltip formatter={v => inr(Number(v))} />
                <Bar dataKey="sales" fill="#15803d" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Card>
        <Card title="Category-wise (this month)">
          {d.categorySales.length === 0 ? <Empty /> : (
            <>
              <div className="h-44">
                <ResponsiveContainer>
                  <PieChart><Pie data={d.categorySales} dataKey="amount" nameKey="name" innerRadius={45} outerRadius={75} paddingAngle={2}>
                    {d.categorySales.map((_, i) => <Cell key={i} fill={COLORS[i % COLORS.length]} />)}
                  </Pie><Tooltip formatter={v => inr(Number(v))} /></PieChart>
                </ResponsiveContainer>
              </div>
              <ul className="mt-2 space-y-1 text-sm">
                {d.categorySales.map((c, i) => <li key={c.name} className="flex justify-between"><span><span className="mr-2 inline-block h-2.5 w-2.5 rounded-full" style={{ background: COLORS[i % COLORS.length] }} />{c.name}</span><b>{inr(c.amount)}</b></li>)}
              </ul>
            </>
          )}
        </Card>
      </div>

      <div className="grid gap-5 lg:grid-cols-3">
        <Card title="Recent bills" className="lg:col-span-2" actions={<Link to="/sales" className="text-sm font-semibold text-brand-700">View all →</Link>}>
          <div className="-mx-4 -my-4 overflow-x-auto">
            <table className="tbl">
              <thead><tr><th>Bill No</th><th>Customer</th><th>Time</th><th className="num">Total</th><th>Status</th></tr></thead>
              <tbody>
                {d.recentInvoices.map(i => (
                  <tr key={i.id} className="cursor-pointer" onClick={() => nav(`/sales/${i.id}`)}>
                    <td className="font-semibold text-brand-800">{i.invoiceNo}</td><td>{i.customerName}</td><td className="text-xs text-stone-500">{dmyt(i.invoiceDate)}</td>
                    <td className="num font-bold">{inr(i.grandTotal)}</td><td><Badge tone={statusTone(i.status)}>{i.status}</Badge></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Card>
        <Card title="Top products (month)">
          {d.topProducts.length === 0 ? <Empty /> : (
            <ol className="space-y-3">
              {d.topProducts.map((p, i) => (
                <li key={p.name} className="flex items-center gap-3">
                  <span className="flex h-7 w-7 items-center justify-center rounded-full bg-brand-100 text-xs font-bold text-brand-800">{i + 1}</span>
                  <div className="min-w-0 flex-1"><div className="truncate text-sm font-semibold">{p.name}</div><div className="text-xs text-stone-500">Qty {p.quantity}</div></div>
                  <b className="text-sm">{inr(p.amount)}</b>
                </li>
              ))}
            </ol>
          )}
          <div className="mt-4 rounded-xl bg-stone-50 p-3 text-sm">Stock value (cost): <b>{inr(d.stockValue)}</b></div>
        </Card>
      </div>
    </div>
  )
}
