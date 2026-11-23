import { useState } from 'react'
import { api } from '../lib/api'
import { dmy, downloadCsv, inr } from '../lib/format'
import { useI18n } from '../lib/i18n'
import { Badge, Button, Card, Empty, Modal, PageHeader, Spinner, Stat, useLoad, useToast } from '../components/ui'
import { PaymentForm, sendReminder } from './Customers'
import { PayLinkButton } from '../components/PayLink'

interface Row { customerId: number; name: string; mobile?: string; village?: string; balance: number; lastInvoiceDate?: string; lastPaymentDate?: string; daysSinceLastPayment: number }

export default function Udhari() {
  const { t } = useI18n()
  const toast = useToast()
  const { data, loading, reload } = useLoad(() => api.get<Row[]>('/api/reports/udhari'))
  const [pay, setPay] = useState<Row | null>(null)
  const [q, setQ] = useState('')
  const rows = (data ?? []).filter(r => !q || r.name.toLowerCase().includes(q.toLowerCase()) || r.village?.toLowerCase().includes(q.toLowerCase()))
  const total = rows.reduce((s, r) => s + r.balance, 0)

  return (
    <div>
      <PageHeader title={`🤝 ${t('udhari')} हिशोब`} subtitle="Outstanding credit from farmers – collect faster with one-tap WhatsApp reminders"
        actions={<Button variant="secondary" onClick={() => downloadCsv('udhari.csv', rows.map(r => ({ Farmer: r.name, Mobile: r.mobile ?? '', Village: r.village ?? '', Balance: r.balance, LastBill: dmy(r.lastInvoiceDate), LastPayment: dmy(r.lastPaymentDate), DaysSincePayment: r.daysSinceLastPayment })))}>⬇ CSV</Button>} />
      <div className="mb-4 grid grid-cols-2 gap-3 lg:grid-cols-4">
        <Stat label={t('totalUdhari')} value={inr(total)} tone="red" icon="🤝" />
        <Stat label="Farmers with dues" value={rows.length} tone="stone" icon="👨‍🌾" />
        <Stat label="Overdue > 30 days" value={inr(rows.filter(r => r.daysSinceLastPayment > 30).reduce((s, r) => s + r.balance, 0))} tone="gold" icon="⏰" />
        <Stat label="Avg due / farmer" value={inr(rows.length ? total / rows.length : 0)} tone="blue" icon="📊" />
      </div>
      <Card>
        <input className="input mb-4" placeholder="Search farmer / village…" value={q} onChange={e => setQ(e.target.value)} />
        {loading ? <Spinner /> : rows.length === 0 ? <Empty text="No pending udhari 🎉" /> : (
          <div className="-mx-4 overflow-x-auto">
            <table className="tbl">
              <thead><tr><th>Farmer</th><th>Village</th><th>Last bill</th><th>Last payment</th><th>Age</th><th className="num">Due</th><th /></tr></thead>
              <tbody>{rows.map(r => (
                <tr key={r.customerId}>
                  <td className="font-semibold">{r.name}<div className="text-xs font-normal text-stone-500">{r.mobile}</div></td><td>{r.village}</td>
                  <td className="text-xs">{dmy(r.lastInvoiceDate)}</td><td className="text-xs">{dmy(r.lastPaymentDate)}</td>
                  <td><Badge tone={r.daysSinceLastPayment > 60 ? 'red' : r.daysSinceLastPayment > 30 ? 'amber' : 'stone'}>{r.daysSinceLastPayment}d</Badge></td>
                  <td className="num text-base font-extrabold text-red-600">{inr(r.balance)}</td>
                  <td className="whitespace-nowrap text-right">
                    <Button variant="ghost" className="px-2 py-1" onClick={() => setPay(r)}>💰 Receive</Button>
                    {r.mobile && <PayLinkButton kind="udhari" id={r.customerId} due={r.balance} className="px-2 py-1" />}
                    {r.mobile && <Button variant="whatsapp" className="px-2 py-1" onClick={() => sendReminder(r.customerId, toast)}>💬 Remind</Button>}
                  </td>
                </tr>))}</tbody>
            </table>
          </div>
        )}
      </Card>
      <Modal open={!!pay} onClose={() => setPay(null)} title={`${t('receivePayment')} – ${pay?.name}`}>
        {pay && <PaymentForm partyType="Customer" partyId={pay.customerId} due={pay.balance} onDone={() => { setPay(null); reload() }} />}
      </Modal>
    </div>
  )
}
