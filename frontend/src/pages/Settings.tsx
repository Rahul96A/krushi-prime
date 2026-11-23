import { useEffect, useState, type FormEvent } from 'react'
import { api, isDemo } from '../lib/api'
import type { PaymentLink, PaymentSettings, PlanOption, ShopSettings, TenantSummary } from '../lib/types'
import { dmy, dmyt } from '../lib/format'
import { useAuth } from '../lib/auth'
import { Badge, Button, Card, Field, Modal, PageHeader, Spinner, useLoad, useToast } from '../components/ui'
import { inr } from '../lib/format'

interface UserRow { id: number; fullName: string; username: string; role: string; isActive: boolean; lastLoginAt?: string }

export default function Settings() {
  const { can, refresh } = useAuth()
  const toast = useToast()
  const { data, loading } = useLoad(() => api.get<{ settings: ShopSettings; subscription: TenantSummary }>('/api/settings/shop'))
  const [s, setS] = useState<ShopSettings | null>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => { if (data) setS(data.settings) }, [data])
  const owner = can('Owner')

  const save = async (e: FormEvent) => {
    e.preventDefault(); setBusy(true)
    try { await api.put('/api/settings/shop', s); toast.ok('Settings saved'); refresh() } catch (err) { toast.err(err) } finally { setBusy(false) }
  }
  if (loading || !s || !data) return <Spinner />
  const f = (k: keyof ShopSettings, label: string, cls = '') => (
    <Field label={label} className={cls}><input className="input" disabled={!owner} value={(s[k] as string) ?? ''} onChange={e => setS({ ...s, [k]: e.target.value })} /></Field>
  )
  const sub = data.subscription

  return (
    <div className="space-y-5">
      <PageHeader title="⚙️ Settings" />
      <div className={`rounded-2xl p-5 ${sub.isExpired ? 'bg-red-50' : 'bg-gradient-to-r from-brand-700 to-brand-900 text-white'}`}>
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <div className="text-sm font-semibold opacity-80">Krushi Prime subscription</div>
            <div className="text-2xl font-extrabold">{sub.plan === 'Trial' ? '🎁 Free Trial (6 months)' : `${sub.plan} plan`}</div>
            <div className="text-sm">Valid till <b>{dmy(sub.subscriptionEnd)}</b> · {sub.isExpired ? 'Expired' : `${sub.daysLeft} days left`}</div>
          </div>
          <div className="rounded-xl bg-harvest-400 px-4 py-3 text-brand-900"><div className="text-xs font-bold">Renew / upgrade</div><div className="text-lg font-extrabold">📞 8668375712</div></div>
        </div>
        {owner && <RenewOnline />}
      </div>

      <form onSubmit={save} className="space-y-5">
        <Card title="Shop profile (printed on invoice)">
          <div className="grid gap-3 sm:grid-cols-3">
            {f('shopName', 'Shop name', 'sm:col-span-2')}{f('ownerName', 'Owner name')}
            {f('mobile', 'Mobile')}{f('email', 'Email')}{f('addressLine', 'Address')}
            {f('village', 'Village / Town')}{f('taluka', 'Taluka')}{f('district', 'District')}
            {f('state', 'State')}{f('stateCode', 'State code')}{f('pincode', 'Pincode')}
          </div>
        </Card>
        <Card title="GST & licences (mandatory for agri-input dealers)">
          <div className="grid gap-3 sm:grid-cols-3">
            {f('gstin', 'GSTIN')}
            <label className="flex items-center gap-2 self-end pb-2 text-sm"><input type="checkbox" disabled={!owner} checked={s.isGstRegistered} onChange={e => setS({ ...s, isGstRegistered: e.target.checked })} /> GST registered (enable tax invoices)</label>
            <div />
            {f('fertilizerLicenseNo', 'Fertilizer licence no')}{f('pesticideLicenseNo', 'Insecticide licence no')}{f('seedLicenseNo', 'Seed licence no')}
          </div>
        </Card>
        <Card title="Billing preferences">
          <div className="grid gap-3 sm:grid-cols-3">
            {f('invoicePrefix', 'Invoice prefix (e.g. SSK)')}{f('upiId', 'UPI ID (QR on bill)')}{f('invoiceFooter', 'Invoice footer / thank-you note')}
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" disabled={!owner} checked={s.pricesIncludeTax} onChange={e => setS({ ...s, pricesIncludeTax: e.target.checked })} /> Sale prices include GST (MRP-style)</label>
            <label className="flex items-center gap-2 text-sm"><input type="checkbox" disabled={!owner} checked={s.allowNegativeStock} onChange={e => setS({ ...s, allowNegativeStock: e.target.checked })} /> Allow billing without stock</label>
          </div>
        </Card>
        {owner && <div className="flex justify-end"><Button loading={busy}>Save settings</Button></div>}
      </form>

      {owner && <OnlinePaymentsCard />}
      {owner && <UsersCard />}
      <PasswordCard />
    </div>
  )
}

function UsersCard() {
  const toast = useToast()
  const { data, reload } = useLoad(() => api.get<UserRow[]>('/api/settings/users'))
  const [open, setOpen] = useState(false)
  const [f, setF] = useState({ fullName: '', username: '', password: '', role: 'Cashier' })
  const add = async (e: FormEvent) => {
    e.preventDefault()
    try { await api.post('/api/settings/users', f); toast.ok('User added'); setOpen(false); reload() } catch (err) { toast.err(err) }
  }
  return (
    <Card title="Staff users" actions={<Button variant="secondary" onClick={() => setOpen(true)}>+ Add staff</Button>}>
      <table className="tbl"><thead><tr><th>Name</th><th>Login</th><th>Role</th><th>Last login</th><th>Status</th></tr></thead>
        <tbody>{data?.map(u => (
          <tr key={u.id}><td className="font-semibold">{u.fullName}</td><td>{u.username}</td><td><Badge tone="blue">{u.role}</Badge></td><td className="text-xs">{dmyt(u.lastLoginAt)}</td>
            <td>{u.role === 'Owner' ? <Badge tone="green">Owner</Badge> : <button onClick={async () => { await api.put(`/api/settings/users/${u.id}/status`, { isActive: !u.isActive }); reload() }}><Badge tone={u.isActive ? 'green' : 'red'}>{u.isActive ? 'Active' : 'Disabled'}</Badge></button>}</td></tr>))}</tbody></table>
      <Modal open={open} onClose={() => setOpen(false)} title="Add staff user">
        <form onSubmit={add} className="space-y-3">
          <Field label="Full name"><input className="input" required value={f.fullName} onChange={e => setF({ ...f, fullName: e.target.value })} /></Field>
          <Field label="Login (mobile / username)"><input className="input" required value={f.username} onChange={e => setF({ ...f, username: e.target.value })} /></Field>
          <Field label="Password"><input className="input" type="password" required minLength={6} value={f.password} onChange={e => setF({ ...f, password: e.target.value })} /></Field>
          <Field label="Role"><select className="input" value={f.role} onChange={e => setF({ ...f, role: e.target.value })}><option value="Cashier">Cashier – billing & udhari</option><option value="Manager">Manager – + purchases, stock, GST</option></select></Field>
          <div className="flex justify-end"><Button>Add user</Button></div>
        </form>
      </Modal>
    </Card>
  )
}

function PasswordCard() {
  const toast = useToast()
  const [f, setF] = useState({ currentPassword: '', newPassword: '' })
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    try { await api.post('/api/auth/change-password', f); toast.ok('Password changed'); setF({ currentPassword: '', newPassword: '' }) } catch (err) { toast.err(err) }
  }
  return (
    <Card title="Change my password">
      <form onSubmit={submit} className="grid gap-3 sm:grid-cols-3">
        <input className="input" type="password" placeholder="Current password" required value={f.currentPassword} onChange={e => setF({ ...f, currentPassword: e.target.value })} />
        <input className="input" type="password" placeholder="New password (min 6)" minLength={6} required value={f.newPassword} onChange={e => setF({ ...f, newPassword: e.target.value })} />
        <Button variant="secondary">Update password</Button>
      </form>
    </Card>
  )
}

function RenewOnline() {
  const toast = useToast()
  const { data: plans } = useLoad(() => api.get<PlanOption[]>('/api/subscription/plans'))
  const [busy, setBusy] = useState('')
  const checkout = async (plan: string) => {
    setBusy(plan)
    try {
      const link = await api.post<PaymentLink>('/api/subscription/checkout', { plan })
      if (isDemo) { toast.ok(`Demo: ${plan} plan activated after a simulated payment of ${inr(link.amount)}`); setTimeout(() => window.location.reload(), 1200); return }
      const a = document.createElement('a'); a.href = link.payUrl; a.target = '_blank'; a.rel = 'noopener'; document.body.appendChild(a); a.click(); a.remove()
      toast.ok('Payment page opened – your plan is extended automatically after payment')
    } catch (e) { toast.err(e) } finally { setBusy('') }
  }
  if (!plans?.length) return null
  return (
    <div className="mt-4 flex flex-wrap gap-2 border-t border-white/20 pt-4">
      <span className="self-center text-sm font-semibold">Renew online:</span>
      {plans.map(p => <Button key={p.plan} variant="gold" loading={busy === p.plan} onClick={() => checkout(p.plan)}>{p.label} · {inr(p.price)}</Button>)}
    </div>
  )
}

const PROVIDERS: { id: PaymentSettings['provider']; name: string; note: string }[] = [
  { id: 'None', name: 'Off', note: 'Online payments disabled' },
  { id: 'UpiDirect', name: 'Direct UPI (PhonePe / GPay)', note: 'Free · money straight to your UPI ID · confirm receipt with one tap' },
  { id: 'Cashfree', name: 'Cashfree', note: 'Payment links · UPI, cards, net-banking · settles to your bank' },
  { id: 'NttData', name: 'NTT DATA (Atom)', note: 'AIPAY hosted checkout · UPI, cards, net-banking' },
  { id: 'Simulator', name: 'Demo simulator', note: 'Try the whole flow with no real money' },
]

function OnlinePaymentsCard() {
  const toast = useToast()
  const { data, loading } = useLoad(() => api.get<PaymentSettings>('/api/settings/payments'))
  const [f, setF] = useState<PaymentSettings | null>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => { if (data) setF(data) }, [data])
  if (loading || !f) return null
  const set = (k: keyof PaymentSettings) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value })
  const save = async (e: FormEvent) => {
    e.preventDefault(); setBusy(true)
    try { setF(await api.put<PaymentSettings>('/api/settings/payments', f)); toast.ok('Online payment settings saved') } catch (err) { toast.err(err) } finally { setBusy(false) }
  }
  return (
    <Card title="💳 Online payments (Pay bill)">
      <form onSubmit={save} className="space-y-4">
        <p className="text-sm text-stone-600">Connect <b>your own</b> merchant account. Farmers pay udhari and bills by UPI, card or net-banking from a link on WhatsApp. The money settles directly to your bank, and Krushi Prime records the payment in the ledger automatically.</p>
        <div className="grid gap-2 sm:grid-cols-5">
          {PROVIDERS.map(p => (
            <button type="button" key={p.id} onClick={() => setF({ ...f, provider: p.id })}
              className={`rounded-xl border-2 p-3 text-left ${f.provider === p.id ? 'border-brand-700 bg-brand-50' : 'border-stone-200'}`}>
              <div className="font-bold">{p.name}</div><div className="text-xs text-stone-500">{p.note}</div>
            </button>
          ))}
        </div>
        {(f.provider === 'Cashfree' || f.provider === 'NttData') && (
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={f.liveMode} onChange={e => setF({ ...f, liveMode: e.target.checked })} />
            <b>{f.liveMode ? 'LIVE mode – real money' : 'Test / sandbox mode'}</b> <span className="text-stone-500">(use test keys first, then switch to live keys)</span></label>
        )}
        {f.provider === 'Cashfree' && (
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="App ID (x-client-id)"><input className="input" value={f.cashfreeAppId ?? ''} onChange={set('cashfreeAppId')} /></Field>
            <Field label="Secret key (x-client-secret)"><input className="input" type="password" placeholder="Leave as is to keep the saved key" value={f.cashfreeSecret ?? ''} onChange={set('cashfreeSecret')} /></Field>
            <div className="rounded-lg bg-sky-50 p-3 text-xs text-sky-900 sm:col-span-2">Cashfree dashboard → Developers → Webhooks: add <b className="break-all">{f.webhookUrl}</b> with the <b>Payment Link</b> event, so payments are recorded immediately. Without the webhook, use “Check” on the Online Payments page.</div>
          </div>
        )}
        {f.provider === 'NttData' && (
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label="Merchant ID"><input className="input" value={f.nttMerchId ?? ''} onChange={set('nttMerchId')} /></Field>
            <Field label="Transaction password"><input className="input" type="password" value={f.nttPassword ?? ''} onChange={set('nttPassword')} /></Field>
            <Field label="Product ID"><input className="input" value={f.nttProductId ?? ''} onChange={set('nttProductId')} placeholder="NSE" /></Field>
            <Field label="Request encryption key"><input className="input" type="password" value={f.nttRequestKey ?? ''} onChange={set('nttRequestKey')} /></Field>
            <Field label="Response decryption key"><input className="input" type="password" value={f.nttResponseKey ?? ''} onChange={set('nttResponseKey')} /></Field>
            <div className="self-end rounded-lg bg-sky-50 p-2 text-xs text-sky-900">UAT test values: Merchant 317159 · password Test@123 · product NSE (from NTT DATA's sandbox docs)</div>
          </div>
        )}
        {f.provider === 'UpiDirect' && <div className="rounded-lg bg-brand-50 p-3 text-sm text-brand-900">Real money, zero fees. Pay links and QR codes open PhonePe, GPay or Paytm with the amount filled in, and the money goes to the <b>UPI ID in Billing preferences</b> above. No gateway account or KYC needed. Confirm each payment with “Mark received” once you see it in your UPI app (a gateway confirms automatically).</div>}
        {f.provider === 'Simulator' && <div className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900">Demo mode: pay links open a practice page with a “Pay (demo)” button. No real money moves. Use this for client demos only.</div>}
        <div className="flex justify-end"><Button loading={busy}>Save payment settings</Button></div>
      </form>
    </Card>
  )
}
