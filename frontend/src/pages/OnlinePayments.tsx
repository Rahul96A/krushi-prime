import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api } from '../lib/api'
import type { PaymentLink } from '../lib/types'
import { dmyt, inr } from '../lib/format'
import { useAuth } from '../lib/auth'
import { Badge, Button, Card, Empty, PageHeader, Spinner, Stat, openWhatsApp, useLoad, useToast } from '../components/ui'
import { linkTone, providerLabel, PayLinkModal } from '../components/PayLink'

export default function OnlinePayments() {
  const toast = useToast()
  const nav = useNavigate()
  const { can } = useAuth()
  const { data, loading, reload, setData } = useLoad(() => api.get<PaymentLink[]>('/api/online-payments'))
  const [show, setShow] = useState<PaymentLink | null>(null)
  const [busy, setBusy] = useState<number | null>(null)
  const rows = data ?? []
  const paid = rows.filter(r => r.status === 'Paid')

  const refresh = async (r: PaymentLink) => {
    setBusy(r.id)
    try { const u = await api.post<PaymentLink>(`/api/online-payments/${r.id}/refresh`); setData(rows.map(x => (x.id === u.id ? u : x))); toast.ok(`Status: ${u.status}`) } catch (e) { toast.err(e) } finally { setBusy(null) }
  }
  const markReceived = async (r: PaymentLink) => {
    setBusy(r.id)
    try { await api.post(`/api/online-payments/${r.id}/mark-received`, { reference: null }); toast.ok(`${inr(r.amount)} received on UPI – udhari updated`); reload() } catch (e) { toast.err(e) } finally { setBusy(null) }
  }
  const simulate = async (r: PaymentLink) => {
    setBusy(r.id)
    try { await api.post(`/api/online-payments/${r.publicId}/simulate`); toast.ok(`${inr(r.amount)} received (demo) – ledger updated`); reload() } catch (e) { toast.err(e) } finally { setBusy(null) }
  }

  return (
    <div>
      <PageHeader title="💳 Online Payments" subtitle="Pay-bill links for udhari & bills – UPI, cards, net-banking via Cashfree or NTT DATA. Paid amounts post to the farmer ledger automatically."
        actions={<><Button variant="secondary" onClick={reload}>↻ Refresh</Button><Button onClick={() => nav('/udhari')}>+ Send pay link</Button></>} />
      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label="Links sent" value={rows.filter(r => r.purpose !== 'Subscription').length} icon="🔗" />
        <Stat label="Collected online" value={inr(paid.filter(r => r.purpose !== 'Subscription').reduce((s, r) => s + r.amountPaid, 0))} tone="gold" icon="💰" />
        <Stat label="Awaiting payment" value={inr(rows.filter(r => r.status === 'Created' && r.purpose !== 'Subscription').reduce((s, r) => s + r.amount, 0))} tone="blue" icon="⏳" />
        <Stat label="Paid links" value={paid.length} tone="green" icon="✅" />
      </div>
      <Card>
        {loading ? <Spinner /> : rows.length === 0 ? <Empty text="No pay links yet – open Udhari or a bill and tap “🔗 Pay link”" /> : (
          <div className="-mx-4 overflow-x-auto">
            <table className="tbl">
              <thead><tr><th>Created</th><th>Payer</th><th>For</th><th>Gateway</th><th className="num">Amount</th><th>Status</th><th /></tr></thead>
              <tbody>{rows.map(r => (
                <tr key={r.id}>
                  <td className="text-xs">{dmyt(r.createdAt)}</td>
                  <td className="font-semibold">{r.payerName}<div className="text-xs font-normal text-stone-500">{r.payerMobile}</div></td>
                  <td className="text-xs">{r.purpose}<div className="text-stone-500">{r.description}</div></td>
                  <td className="text-xs">{providerLabel(r.provider)}{r.provider === 'UpiDirect' ? <div>Live UPI</div> : r.provider !== 'Simulator' && <div>{r.liveMode ? 'Live' : 'Test'}</div>}</td>
                  <td className="num font-bold">{inr(r.amount, 2)}</td>
                  <td><Badge tone={linkTone(r.status)}>{r.status === 'Created' ? 'Awaiting' : r.status}</Badge>{r.paidAt && <div className="text-[11px] text-stone-500">{dmyt(r.paidAt)}</div>}{r.lastMessage && r.status !== 'Paid' && <div className="text-[11px] text-stone-500">{r.lastMessage}</div>}</td>
                  <td className="whitespace-nowrap text-right">
                    {r.status === 'Created' && <>
                      <Button variant="ghost" className="px-2 py-1" onClick={() => setShow(r)}>Share</Button>
                      {r.whatsAppUrl && <Button variant="ghost" className="px-2 py-1 text-[#128C7E]" onClick={() => openWhatsApp(r.whatsAppUrl)}>💬</Button>}
                      {r.provider === 'Cashfree' && <Button variant="ghost" className="px-2 py-1" loading={busy === r.id} onClick={() => refresh(r)}>Check</Button>}
                      {r.provider === 'UpiDirect' && can('Owner', 'Manager') && <Button variant="gold" className="px-2 py-1" loading={busy === r.id} onClick={() => markReceived(r)}>✓ Mark received</Button>}
                      {r.provider === 'Simulator' && can('Owner', 'Manager') && <Button variant="gold" className="px-2 py-1" loading={busy === r.id} onClick={() => simulate(r)}>Simulate paid</Button>}
                    </>}
                  </td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
      </Card>
      <PayLinkModal link={show} onClose={() => setShow(null)} />
    </div>
  )
}
