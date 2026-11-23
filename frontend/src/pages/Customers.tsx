import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, qs } from '../lib/api'
import type { Customer, Ledger, PaymentMode, WhatsAppResult } from '../lib/types'
import { dmy, downloadCsv, inr } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { Button, Card, Empty, Field, Modal, openWhatsApp, PageHeader, Spinner, useLoad, useToast } from '../components/ui'
import { PayLinkButton } from '../components/PayLink'

export function CustomerForm({ initial, onSaved }: { initial?: Customer; onSaved: (c: Customer) => void }) {
  const toast = useToast()
  const [f, setF] = useState({ name: initial?.name ?? '', mobile: initial?.mobile ?? '', village: initial?.village ?? '', address: initial?.address ?? '', gstin: initial?.gstin ?? '', openingBalance: initial?.openingBalance ?? 0, creditLimit: initial?.creditLimit ?? 0, landAcres: initial?.landAcres ?? '', crops: initial?.crops ?? '', isActive: true })
  const [busy, setBusy] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setBusy(true)
    try {
      const body = { ...f, landAcres: f.landAcres === '' ? null : Number(f.landAcres), mobile: f.mobile || null, gstin: f.gstin || null }
      const c = initial ? await api.put<Customer>(`/api/customers/${initial.id}`, body) : await api.post<Customer>('/api/customers', body)
      toast.ok('Farmer saved'); onSaved(c)
    } catch (err) { toast.err(err) } finally { setBusy(false) }
  }
  const s = (k: string) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.type === 'number' ? Number(e.target.value) : e.target.value })
  return (
    <form onSubmit={submit} className="grid gap-3 sm:grid-cols-2">
      <Field label="Name *" className="sm:col-span-2"><input className="input" required value={f.name} onChange={s('name')} autoFocus /></Field>
      <Field label="Mobile"><input className="input" inputMode="numeric" maxLength={10} pattern="[6-9][0-9]{9}" value={f.mobile} onChange={s('mobile')} /></Field>
      <Field label="Village"><input className="input" value={f.village} onChange={s('village')} /></Field>
      <Field label="Crops"><input className="input" value={f.crops} onChange={s('crops')} placeholder="Onion, Soybean" /></Field>
      <Field label="Land (acres)"><input className="input" type="number" step="any" value={f.landAcres} onChange={s('landAcres')} /></Field>
      <Field label="Opening udhari ₹"><input className="input" type="number" step="any" value={f.openingBalance} onChange={s('openingBalance')} /></Field>
      <Field label="Credit limit ₹ (0 = no limit)"><input className="input" type="number" step="any" value={f.creditLimit} onChange={s('creditLimit')} /></Field>
      <Field label="GSTIN (B2B)" className="sm:col-span-2"><input className="input uppercase" maxLength={15} value={f.gstin} onChange={s('gstin')} /></Field>
      <div className="flex justify-end sm:col-span-2"><Button loading={busy}>Save</Button></div>
    </form>
  )
}

export function PaymentForm({ partyType, partyId, due, onDone }: { partyType: 'Customer' | 'Supplier'; partyId: number; due: number; onDone: () => void }) {
  const toast = useToast()
  const [f, setF] = useState({ amount: due > 0 ? due : 0, mode: 'Cash' as PaymentMode, reference: '', note: '' })
  const [busy, setBusy] = useState(false)
  const submit = async (e: FormEvent) => {
    e.preventDefault(); setBusy(true)
    try { await api.post('/api/payments', { partyType, partyId, ...f }); toast.ok(`Payment of ${inr(f.amount)} recorded`); onDone() } catch (err) { toast.err(err) } finally { setBusy(false) }
  }
  return (
    <form onSubmit={submit} className="space-y-3">
      <div className="rounded-xl bg-red-50 p-3 text-center"><div className="text-xs font-semibold text-red-600">Current due</div><div className="text-2xl font-extrabold text-red-700">{inr(due)}</div></div>
      <Field label="Amount ₹"><input type="number" step="any" min={0.01} required className="input text-lg font-bold" value={f.amount} onChange={e => setF({ ...f, amount: Number(e.target.value) })} /></Field>
      <div className="grid grid-cols-4 gap-1.5">{(['Cash', 'Upi', 'Bank', 'Cheque'] as PaymentMode[]).map(m => <button type="button" key={m} onClick={() => setF({ ...f, mode: m })} className={`rounded-lg border-2 py-2 text-xs font-bold ${f.mode === m ? 'border-brand-700 bg-brand-700 text-white' : 'border-stone-200'}`}>{m === 'Upi' ? 'UPI' : m}</button>)}</div>
      <Field label="Reference (UTR / cheque no)"><input className="input" value={f.reference} onChange={e => setF({ ...f, reference: e.target.value })} /></Field>
      <Field label="Note"><input className="input" value={f.note} onChange={e => setF({ ...f, note: e.target.value })} /></Field>
      <div className="flex justify-end"><Button loading={busy}>Save payment</Button></div>
    </form>
  )
}

export function LedgerView({ ledger }: { ledger: Ledger }) {
  return (
    <div>
      <div className="mb-3 flex flex-wrap justify-between gap-2 text-sm">
        <span>Opening: <b>{inr(ledger.openingBalance)}</b></span>
        <span className={ledger.closingBalance > 0 ? 'font-bold text-red-700' : 'font-bold text-brand-700'}>Closing balance: {inr(ledger.closingBalance)}</span>
      </div>
      <div className="max-h-96 overflow-y-auto">
        <table className="tbl">
          <thead><tr><th>Date</th><th>Particulars</th><th>Ref</th><th className="num">Debit</th><th className="num">Credit</th><th className="num">Balance</th></tr></thead>
          <tbody>
            {ledger.entries.length === 0 && <tr><td colSpan={6}><Empty /></td></tr>}
            {ledger.entries.map((e, i) => (
              <tr key={i}><td className="text-xs">{dmy(e.date)}</td><td>{e.type}</td><td className="text-xs">{e.reference}</td>
                <td className="num">{e.debit ? inr(e.debit) : ''}</td><td className="num text-brand-700">{e.credit ? inr(e.credit) : ''}</td><td className="num font-semibold">{inr(e.balance)}</td></tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

export async function sendReminder(id: number, toast: ReturnType<typeof useToast>) {
  try { const r = await api.post<WhatsAppResult>(`/api/whatsapp/reminder/${id}`); r.sent ? toast.ok('Reminder sent on WhatsApp ✅') : openWhatsApp(r.url) } catch (e) { toast.err(e) }
}

export default function Customers() {
  const { t } = useI18n()
  const toast = useToast()
  const nav = useNavigate()
  const [q, setQ] = useState('')
  const { data, loading, reload } = useLoad(() => api.get<Customer[]>(`/api/customers${qs({ q })}`), [q])
  const [form, setForm] = useState<{ c?: Customer } | null>(null)
  const [detail, setDetail] = useState<Customer | null>(null)
  const [pay, setPay] = useState<Customer | null>(null)
  const ledger = useLoad(() => (detail ? api.get<Ledger>(`/api/customers/${detail.id}/ledger`) : Promise.resolve(null)), [detail?.id])

  return (
    <div>
      <PageHeader title={`👨‍🌾 ${t('customers')}`} subtitle={`${data?.length ?? 0} farmers`}
        actions={<><Button variant="secondary" onClick={() => data && downloadCsv('farmers.csv', data.map(c => ({ Name: c.name, Mobile: c.mobile ?? '', Village: c.village ?? '', Crops: c.crops ?? '', Udhari: c.balance })))}>⬇ CSV</Button>
          <Button onClick={() => setForm({})}>+ {t('addCustomer')}</Button></>} />
      <Card>
        <input className="input mb-4" placeholder="Search by name, mobile or village…" value={q} onChange={e => setQ(e.target.value)} />
        {loading ? <Spinner /> : !data?.length ? <Empty /> : (
          <div className="-mx-4 overflow-x-auto">
            <table className="tbl">
              <thead><tr><th>Farmer</th><th>Village</th><th>Crops</th><th className="num">Udhari</th><th /></tr></thead>
              <tbody>{data.map(c => (
                <tr key={c.id}>
                  <td><button className="text-left font-semibold text-brand-800 hover:underline" onClick={() => setDetail(c)}>{c.name}</button><div className="text-xs text-stone-500">{c.mobile}</div></td>
                  <td>{c.village}</td><td className="text-xs">{c.crops}</td>
                  <td className={`num font-bold ${c.balance > 0 ? 'text-red-600' : 'text-brand-700'}`}>{inr(c.balance)}</td>
                  <td className="whitespace-nowrap text-right">
                    {c.balance > 0 && <Button variant="ghost" className="px-2 py-1" onClick={() => setPay(c)}>💰 Receive</Button>}
                    {c.balance > 0 && c.mobile && <Button variant="ghost" className="px-2 py-1 text-[#128C7E]" onClick={() => sendReminder(c.id, toast)}>💬 Remind</Button>}
                    <Button variant="ghost" className="px-2 py-1" onClick={() => setForm({ c })}>Edit</Button>
                  </td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
      </Card>

      <Modal open={!!form} onClose={() => setForm(null)} title={form?.c ? 'Edit farmer' : t('addCustomer')}>
        <CustomerForm initial={form?.c} onSaved={() => { setForm(null); reload() }} />
      </Modal>
      <Modal open={!!pay} onClose={() => setPay(null)} title={`${t('receivePayment')} – ${pay?.name}`}>
        {pay && <PaymentForm partyType="Customer" partyId={pay.id} due={pay.balance} onDone={() => { setPay(null); reload() }} />}
      </Modal>
      <Modal open={!!detail} onClose={() => setDetail(null)} title={`Ledger – ${detail?.name}`} wide>
        {detail && (
          <div>
            <div className="mb-3 flex flex-wrap gap-2">
              <Button onClick={() => { setPay(detail); setDetail(null) }}>💰 {t('receivePayment')}</Button>
              {detail.mobile && <Button variant="whatsapp" onClick={() => sendReminder(detail.id, toast)}>💬 {t('sendReminder')}</Button>}
              {detail.mobile && detail.balance > 0 && <PayLinkButton kind="udhari" id={detail.id} due={detail.balance} variant="gold" label="🔗 Online pay link" />}
              <Button variant="secondary" onClick={() => nav(`/sales?q=${encodeURIComponent(detail.name)}`)}>View bills</Button>
            </div>
            {ledger.loading || !ledger.data ? <Spinner /> : <LedgerView ledger={ledger.data} />}
          </div>
        )}
      </Modal>
    </div>
  )
}
