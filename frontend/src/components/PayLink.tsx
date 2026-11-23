import { useEffect, useState } from 'react'
import QRCode from 'qrcode'
import { api } from '../lib/api'
import type { PaymentLink } from '../lib/types'
import { inr } from '../lib/format'
import { Badge, Button, Modal, openWhatsApp, useToast } from './ui'

export const providerLabel = (p: string) => (p === 'NttData' ? 'NTT DATA' : p === 'Simulator' ? 'Demo simulator' : p === 'UpiDirect' ? 'Direct UPI' : p)
export const linkTone = (s: string) => (s === 'Paid' ? 'green' : s === 'PartiallyPaid' ? 'amber' : s === 'Created' ? 'blue' : 'red') as 'green' | 'amber' | 'blue' | 'red'

/** Shows a freshly created "pay bill" link: QR for the counter, WhatsApp share for remote farmers. */
export function PayLinkModal({ link, onClose }: { link: PaymentLink | null; onClose: () => void }) {
  const toast = useToast()
  const [qr, setQr] = useState('')
  useEffect(() => { if (link) QRCode.toDataURL(link.upiUri ?? link.payUrl, { margin: 1, width: 240 }).then(setQr) }, [link])
  if (!link) return null
  const copy = async () => { try { await navigator.clipboard.writeText(link.payUrl); toast.ok('Link copied') } catch { toast.err('Copy not allowed here – long-press the link to copy') } }
  return (
    <Modal open={!!link} onClose={onClose} title="🔗 Online pay link ready">
      <div className="space-y-4 text-center">
        <div><div className="text-sm text-stone-500">{link.payerName} · {link.description}</div><div className="text-3xl font-extrabold text-brand-900">{inr(link.amount, 2)}</div>
          <Badge tone={link.provider === 'UpiDirect' ? 'green' : 'blue'}>{providerLabel(link.provider)}{link.provider === 'UpiDirect' ? ' · LIVE money' : link.provider !== 'Simulator' && (link.liveMode ? ' · LIVE' : ' · Test mode')}</Badge></div>
        {qr && <img src={qr} alt="Pay link QR" className="mx-auto h-48 w-48 rounded-xl border border-stone-200" />}
        {link.upiUri
          ? <p className="text-xs text-stone-600"><b>Scan with PhonePe / GPay / Paytm</b>: the amount fills in automatically and the money goes straight to your UPI ID <b>{decodeURIComponent(link.upiUri.split('pa=')[1]?.split('&')[0] ?? '')}</b>. When it arrives, tap <b>Mark received</b> in Online Payments.</p>
          : <p className="text-xs text-stone-500">Farmer scans this at the counter, or send it on WhatsApp. UPI / cards / net-banking. Udhari is updated automatically when paid.</p>}
        {link.upiUri && <a href={link.upiUri} className="block rounded-lg bg-brand-700 py-2 text-sm font-bold text-white">📱 Open UPI app on this phone</a>}
        {!link.payUrl.endsWith('(demo)') && <a href={link.payUrl} target="_blank" rel="noopener noreferrer" className="block break-all rounded-lg bg-stone-100 p-2 text-xs text-brand-800">{link.payUrl}</a>}
        <div className="grid grid-cols-2 gap-2">
          {link.whatsAppUrl ? <Button variant="whatsapp" onClick={() => openWhatsApp(link.whatsAppUrl)}>💬 Send on WhatsApp</Button> : <Button variant="secondary" disabled>No mobile number</Button>}
          {!link.payUrl.endsWith('(demo)') ? <Button variant="secondary" onClick={copy}>📋 Copy link</Button> : <Button variant="secondary" onClick={onClose}>Done</Button>}
        </div>
      </div>
    </Modal>
  )
}

/** Button that creates a udhari or invoice pay link and opens the share modal. */
export function PayLinkButton({ kind, id, label = '🔗 Pay link', className = '', variant = 'ghost', due }: { kind: 'udhari' | 'invoice'; id: number; label?: string; className?: string; variant?: 'ghost' | 'secondary' | 'gold'; due?: number }) {
  const toast = useToast()
  const [busy, setBusy] = useState(false)
  const [link, setLink] = useState<PaymentLink | null>(null)
  const [ask, setAsk] = useState(false)
  const [amount, setAmount] = useState<string>('')
  const create = async (amt?: number) => {
    setBusy(true)
    try { setLink(await api.post<PaymentLink>(`/api/online-payments/${kind}/${id}`, kind === 'udhari' ? { amount: amt ?? null } : {})); setAsk(false) } catch (e) { toast.err(e) } finally { setBusy(false) }
  }
  return <>
    <Button variant={variant} className={className} loading={busy} onClick={() => (kind === 'udhari' ? (setAmount(due ? String(due) : ''), setAsk(true)) : create())}>{label}</Button>
    <Modal open={ask} onClose={() => setAsk(false)} title="Amount to collect">
      <div className="space-y-3">
        <input type="number" min={1} step="any" className="input text-2xl font-bold" value={amount} placeholder="Full udhari" onChange={e => setAmount(e.target.value)} autoFocus />
        <div className="flex flex-wrap gap-2">{[1, 10, 500, 1000].map(v => <button key={v} type="button" onClick={() => setAmount(String(v))} className="rounded-full bg-stone-100 px-3 py-1 text-sm font-semibold">₹{v}</button>)}
          {due ? <button type="button" onClick={() => setAmount(String(due))} className="rounded-full bg-brand-100 px-3 py-1 text-sm font-semibold text-brand-800">Full {inr(due)}</button> : null}</div>
        <p className="text-xs text-stone-500">Tip for a live demo: use ₹1 and pay it from your own phone.</p>
        <Button className="w-full" loading={busy} onClick={() => create(amount ? Number(amount) : undefined)}>Create pay link</Button>
      </div>
    </Modal>
    <PayLinkModal link={link} onClose={() => setLink(null)} />
  </>
}
