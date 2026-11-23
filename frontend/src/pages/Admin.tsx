import { useState, type FormEvent } from 'react'
import { api, qs } from '../lib/api'
import { dmy, inr } from '../lib/format'
import { useAuth } from '../lib/auth'
import { Badge, Button, Card, Field, Modal, PageHeader, Spinner, Stat, useLoad, useToast } from '../components/ui'
import { Logo } from '../components/Layout'

interface Row { id: number; shopName: string; ownerName: string; mobile: string; district?: string; plan: string; subscriptionStart: string; subscriptionEnd: string; daysLeft: number; isActive: boolean; invoices: number; totalBilled: number; lastBillAt?: string; createdAt: string }
interface Stats { totalShops: number; activeShops: number; onTrial: number; paid: number; expired: number; expiringIn30Days: number; invoicesToday: number }

/** Vendor console for Krushi Prime executives: onboard shops (6 months free), renew, suspend. */
export default function Admin() {
  const { logout, user } = useAuth()
  const toast = useToast()
  const [q, setQ] = useState('')
  const stats = useLoad(() => api.get<Stats>('/api/admin/stats'))
  const list = useLoad(() => api.get<Row[]>(`/api/admin/tenants${qs({ q })}`), [q])
  const [onboard, setOnboard] = useState(false)
  const [renew, setRenew] = useState<Row | null>(null)
  const reload = () => { stats.reload(); list.reload() }

  const toggle = async (r: Row) => {
    try { await api.put(`/api/admin/tenants/${r.id}/status`, { isActive: !r.isActive }); toast.ok(r.isActive ? 'Shop suspended' : 'Shop activated'); reload() } catch (e) { toast.err(e) }
  }
  const resetPwd = async (r: Row) => {
    try { const res = await api.post<{ username: string; temporaryPassword: string }>(`/api/admin/tenants/${r.id}/reset-password`); toast.ok(`Login ${res.username} · temp password ${res.temporaryPassword}`) } catch (e) { toast.err(e) }
  }

  return (
    <div className="min-h-full bg-stone-50">
      <header className="flex items-center justify-between bg-brand-900 px-6 py-3"><Logo light /><div className="flex items-center gap-3 text-sm text-brand-100">{user?.fullName}<Button variant="gold" className="py-1" onClick={logout}>Logout</Button></div></header>
      <div className="mx-auto max-w-7xl p-6">
        <PageHeader title="Vendor Console" subtitle="Onboard Krushi Seva Kendras · Launch offer: first 6 months FREE" actions={<Button variant="gold" onClick={() => setOnboard(true)}>+ Onboard new shop</Button>} />
        {stats.data && (
          <div className="mb-5 grid grid-cols-2 gap-3 md:grid-cols-3 lg:grid-cols-6">
            <Stat label="Total shops" value={stats.data.totalShops} icon="🏪" />
            <Stat label="Active" value={stats.data.activeShops} tone="blue" icon="✅" />
            <Stat label="On free trial" value={stats.data.onTrial} tone="gold" icon="🎁" />
            <Stat label="Paid" value={stats.data.paid} tone="green" icon="💰" />
            <Stat label="Expiring ≤30d" value={stats.data.expiringIn30Days} tone="red" icon="⏳" />
            <Stat label="Bills today (all)" value={stats.data.invoicesToday} tone="stone" icon="🧾" />
          </div>
        )}
        <Card>
          <input className="input mb-4" placeholder="Search shop / owner / mobile…" value={q} onChange={e => setQ(e.target.value)} />
          {list.loading ? <Spinner /> : (
            <div className="-mx-4 overflow-x-auto">
              <table className="tbl">
                <thead><tr><th>Shop</th><th>Owner</th><th>Plan</th><th>Valid till</th><th className="num">Bills</th><th className="num">Billed</th><th>Last bill</th><th>Status</th><th /></tr></thead>
                <tbody>{list.data?.map(r => (
                  <tr key={r.id}>
                    <td className="font-semibold">{r.shopName}<div className="text-xs font-normal text-stone-500">{r.district} · joined {dmy(r.createdAt)}</div></td>
                    <td>{r.ownerName}<div className="text-xs text-stone-500">{r.mobile}</div></td>
                    <td><Badge tone={r.plan === 'Trial' ? 'amber' : 'green'}>{r.plan}</Badge></td>
                    <td className="text-xs">{dmy(r.subscriptionEnd)}<div>{r.daysLeft < 0 ? <Badge tone="red">Expired</Badge> : <span className="text-stone-500">{r.daysLeft} days left</span>}</div></td>
                    <td className="num">{r.invoices}</td><td className="num">{inr(r.totalBilled)}</td><td className="text-xs">{dmy(r.lastBillAt)}</td>
                    <td><Badge tone={r.isActive ? 'green' : 'red'}>{r.isActive ? 'Active' : 'Suspended'}</Badge></td>
                    <td className="whitespace-nowrap text-right">
                      <Button variant="ghost" className="px-2 py-1" onClick={() => setRenew(r)}>Renew</Button>
                      <Button variant="ghost" className="px-2 py-1" onClick={() => resetPwd(r)}>Reset pwd</Button>
                      <Button variant="ghost" className="px-2 py-1" onClick={() => toggle(r)}>{r.isActive ? 'Suspend' : 'Activate'}</Button>
                    </td>
                  </tr>))}</tbody>
              </table>
            </div>
          )}
        </Card>
      </div>

      <Modal open={onboard} onClose={() => setOnboard(false)} title="Onboard shop – 6 months FREE">
        <OnboardForm onDone={() => { setOnboard(false); reload() }} />
      </Modal>
      <Modal open={!!renew} onClose={() => setRenew(null)} title={`Subscription – ${renew?.shopName}`}>
        {renew && <RenewForm row={renew} onDone={() => { setRenew(null); reload() }} />}
      </Modal>
    </div>
  )
}

function OnboardForm({ onDone }: { onDone: () => void }) {
  const toast = useToast()
  const [f, setF] = useState({ shopName: '', ownerName: '', mobile: '', password: '', village: '', taluka: '', district: '', gstin: '' })
  const s = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value })
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    try { await api.post('/api/admin/tenants', { ...f, gstin: f.gstin || null, seedSampleProducts: true }); toast.ok(`${f.shopName} onboarded · login ${f.mobile}`); onDone() } catch (err) { toast.err(err) }
  }
  return (
    <form onSubmit={submit} className="grid gap-3 sm:grid-cols-2">
      <Field label="Shop name" className="sm:col-span-2"><input className="input" required value={f.shopName} onChange={s('shopName')} /></Field>
      <Field label="Owner name"><input className="input" required value={f.ownerName} onChange={s('ownerName')} /></Field>
      <Field label="Owner mobile (login)"><input className="input" required pattern="[6-9][0-9]{9}" maxLength={10} value={f.mobile} onChange={s('mobile')} /></Field>
      <Field label="Initial password"><input className="input" required minLength={6} value={f.password} onChange={s('password')} /></Field>
      <Field label="GSTIN"><input className="input uppercase" value={f.gstin} onChange={s('gstin')} /></Field>
      <Field label="Village"><input className="input" value={f.village} onChange={s('village')} /></Field>
      <Field label="District"><input className="input" value={f.district} onChange={s('district')} /></Field>
      <div className="flex justify-end sm:col-span-2"><Button variant="gold">Create shop with 6-month trial</Button></div>
    </form>
  )
}

function RenewForm({ row, onDone }: { row: Row; onDone: () => void }) {
  const toast = useToast()
  const [f, setF] = useState({ plan: row.plan === 'Trial' ? 'Basic' : row.plan, extendMonths: 12, notes: '' })
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    try { await api.put(`/api/admin/tenants/${row.id}/subscription`, f); toast.ok('Subscription updated'); onDone() } catch (err) { toast.err(err) }
  }
  return (
    <form onSubmit={submit} className="space-y-3">
      <p className="text-sm">Current: <b>{row.plan}</b> till <b>{dmy(row.subscriptionEnd)}</b></p>
      <Field label="Plan"><select className="input" value={f.plan} onChange={e => setF({ ...f, plan: e.target.value })}>{['Trial', 'Basic', 'Pro', 'Enterprise'].map(p => <option key={p}>{p}</option>)}</select></Field>
      <Field label="Extend by"><select className="input" value={f.extendMonths} onChange={e => setF({ ...f, extendMonths: Number(e.target.value) })}>{[1, 3, 6, 12, 24].map(m => <option key={m} value={m}>{m} month{m > 1 ? 's' : ''}</option>)}</select></Field>
      <Field label="Notes (payment ref)"><input className="input" value={f.notes} onChange={e => setF({ ...f, notes: e.target.value })} /></Field>
      <div className="flex justify-end"><Button>Update</Button></div>
    </form>
  )
}
