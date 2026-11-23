import { useEffect, useState } from 'react'
import { useNavigate, useParams, useSearchParams } from 'react-router-dom'
import QRCode from 'qrcode'
import { api, safeStorage } from '../lib/api'
import type { Invoice, ShopSettings, WhatsAppResult } from '../lib/types'
import { amountInWords, dmy, dmyt, inr, num, qty } from '../lib/format'
import { useAuth } from '../lib/auth'
import { Badge, Button, Modal, openWhatsApp, Spinner, statusTone, useLoad, useToast } from '../components/ui'
import { PayLinkButton } from '../components/PayLink'

interface Resp { invoice: Invoice; shop: ShopSettings; customerOutstanding?: number }

export default function InvoiceView() {
  const { id } = useParams()
  const [sp] = useSearchParams()
  const nav = useNavigate()
  const toast = useToast()
  const { can } = useAuth()
  const { data, loading, reload } = useLoad(() => api.get<Resp>(`/api/sales/${id}`), [id])
  const [format, setFormat] = useState<'a4' | 'thermal'>(() => (safeStorage.getItem('kp.printFormat') as 'a4' | 'thermal') || 'a4')
  const [qr, setQr] = useState<string>('')
  const [cancelOpen, setCancelOpen] = useState(false)
  const [reason, setReason] = useState('')

  useEffect(() => {
    if (!data?.shop.upiId) return
    const amount = data.invoice.balanceAmount > 0 ? data.invoice.balanceAmount : data.invoice.grandTotal
    QRCode.toDataURL(`upi://pay?pa=${data.shop.upiId}&pn=${encodeURIComponent(data.shop.shopName)}&am=${amount}&cu=INR&tn=${encodeURIComponent(data.invoice.invoiceNo)}`, { margin: 1, width: 160 }).then(setQr)
  }, [data])

  useEffect(() => {
    if (data && sp.get('print') === '1') { const t = setTimeout(() => window.print(), 600); return () => clearTimeout(t) }
  }, [data, sp])

  if (loading || !data) return <Spinner />
  const { invoice: inv, shop } = data
  const setFmt = (f: 'a4' | 'thermal') => { setFormat(f); safeStorage.setItem('kp.printFormat', f) }

  const whatsapp = async () => {
    try { const r = await api.post<WhatsAppResult>(`/api/whatsapp/invoice/${inv.id}`); r.sent ? toast.ok('Sent on WhatsApp ✅') : openWhatsApp(r.url) } catch (e) { toast.err(e) }
  }
  const cancel = async () => {
    try { await api.post(`/api/sales/${inv.id}/cancel`, { reason }); toast.ok('Invoice cancelled, stock restored'); setCancelOpen(false); reload() } catch (e) { toast.err(e) }
  }

  const tax = inv.cgstAmount + inv.sgstAmount + inv.igstAmount
  const title = inv.isGst ? 'TAX INVOICE' : 'BILL / ESTIMATE'
  const licenses = [shop.fertilizerLicenseNo && `Fert. Lic: ${shop.fertilizerLicenseNo}`, shop.pesticideLicenseNo && `Pest. Lic: ${shop.pesticideLicenseNo}`, shop.seedLicenseNo && `Seed Lic: ${shop.seedLicenseNo}`].filter(Boolean)

  return (
    <div>
      <div className="no-print mb-4 flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Button variant="ghost" onClick={() => nav(-1)}>← Back</Button>
          <h1 className="text-xl font-extrabold text-brand-900">{inv.invoiceNo}</h1>
          <Badge tone={statusTone(inv.status)}>{inv.status}</Badge>
        </div>
        <div className="flex flex-wrap gap-2">
          <div className="flex rounded-lg border border-stone-300 p-0.5 text-xs font-bold">
            <button onClick={() => setFmt('a4')} className={`rounded-md px-3 py-1.5 ${format === 'a4' ? 'bg-brand-700 text-white' : ''}`}>A4</button>
            <button onClick={() => setFmt('thermal')} className={`rounded-md px-3 py-1.5 ${format === 'thermal' ? 'bg-brand-700 text-white' : ''}`}>Thermal 80mm</button>
          </div>
          <Button onClick={() => window.print()}>🖨️ Print</Button>
          {inv.customerMobile && <Button variant="whatsapp" onClick={whatsapp}>💬 WhatsApp</Button>}
          {inv.balanceAmount > 0 && inv.customerId && inv.status !== 'Cancelled' && <PayLinkButton kind="invoice" id={inv.id} variant="gold" label={`🔗 Pay link ₹${inv.balanceAmount.toLocaleString('en-IN')}`} />}
          <Button variant="secondary" onClick={() => nav('/billing')}>+ New Bill</Button>
          {can('Owner', 'Manager') && inv.status !== 'Cancelled' && <Button variant="danger" onClick={() => setCancelOpen(true)}>Cancel bill</Button>}
        </div>
      </div>
      {inv.status === 'Cancelled' && <div className="no-print mb-3 rounded-xl bg-red-50 p-3 text-sm font-semibold text-red-700">This invoice was cancelled. Reason: {inv.cancelReason}</div>}

      <div className="print-area flex justify-center">
        {format === 'thermal' ? (
          <div className="thermal w-[300px] bg-white p-3 font-mono text-[12px] leading-tight text-black shadow">
            <div className="text-center">
              <div className="text-sm font-bold">{shop.shopName}</div>
              <div>{[shop.addressLine, shop.village, shop.district].filter(Boolean).join(', ')}</div>
              <div>Mob: {shop.mobile}</div>
              {shop.gstin && <div>GSTIN: {shop.gstin}</div>}
              {licenses.map(l => <div key={l as string} className="text-[10px]">{l}</div>)}
              <div className="my-1 border-y border-dashed border-black py-0.5 font-bold">{title}</div>
            </div>
            <div className="flex justify-between"><span>No: {inv.invoiceNo}</span></div>
            <div>Date: {dmyt(inv.invoiceDate)}</div>
            <div>To: {inv.customerName}{inv.customerVillage ? `, ${inv.customerVillage}` : ''}</div>
            <div className="my-1 border-t border-dashed border-black" />
            {inv.items.map(i => (
              <div key={i.id} className="mb-1">
                <div>{i.productName}</div>
                <div className="flex justify-between"><span>{qty(i.quantity)} x {num(i.rate)}{i.batchNo && i.batchNo !== 'NA' ? ` B:${i.batchNo}` : ''}</span><span>{num(i.total)}</span></div>
              </div>
            ))}
            <div className="my-1 border-t border-dashed border-black" />
            {inv.isGst && <><div className="flex justify-between"><span>Taxable</span><span>{num(inv.subTotal)}</span></div>
              {inv.igstAmount > 0 ? <div className="flex justify-between"><span>IGST</span><span>{num(inv.igstAmount)}</span></div>
                : <><div className="flex justify-between"><span>CGST</span><span>{num(inv.cgstAmount)}</span></div><div className="flex justify-between"><span>SGST</span><span>{num(inv.sgstAmount)}</span></div></>}</>}
            <div className="flex justify-between"><span>Round off</span><span>{num(inv.roundOff)}</span></div>
            <div className="flex justify-between border-y border-dashed border-black py-0.5 text-sm font-bold"><span>TOTAL</span><span>₹{num(inv.grandTotal)}</span></div>
            <div className="flex justify-between"><span>Paid ({inv.paymentMode})</span><span>{num(inv.paidAmount)}</span></div>
            {inv.balanceAmount > 0 && <div className="flex justify-between font-bold"><span>Udhari</span><span>{num(inv.balanceAmount)}</span></div>}
            {data.customerOutstanding !== undefined && data.customerOutstanding > 0 && <div className="flex justify-between"><span>Total due</span><span>{num(data.customerOutstanding)}</span></div>}
            {qr && <div className="mt-2 flex flex-col items-center"><img src={qr} className="h-28 w-28" alt="UPI QR" /><div className="text-[10px]">Scan & pay · {shop.upiId}</div></div>}
            <div className="mt-2 text-center">{shop.invoiceFooter}</div>
            <div className="mt-1 text-center text-[9px]">Powered by Krushi Prime · 8668375712</div>
          </div>
        ) : (
          <div className="w-full max-w-[210mm] bg-white p-8 text-[13px] text-stone-900 shadow">
            <div className="flex items-start justify-between border-b-2 border-brand-700 pb-3">
              <div>
                <div className="text-2xl font-extrabold text-brand-800">{shop.shopName}</div>
                <div>{[shop.addressLine, shop.village, shop.taluka, shop.district, shop.pincode].filter(Boolean).join(', ')}</div>
                <div>📞 {shop.mobile}{shop.email ? ` · ${shop.email}` : ''}</div>
                {shop.gstin && <div className="font-semibold">GSTIN: {shop.gstin} · State: {shop.state} ({shop.stateCode})</div>}
                {licenses.length > 0 && <div className="text-xs text-stone-600">{licenses.join(' · ')}</div>}
              </div>
              <div className="text-right">
                <div className="rounded bg-brand-700 px-3 py-1 text-sm font-bold text-white">{title}</div>
                <div className="mt-2">No: <b>{inv.invoiceNo}</b></div>
                <div>Date: <b>{dmy(inv.invoiceDate)}</b></div>
              </div>
            </div>
            <div className="mt-3 grid grid-cols-2 gap-4">
              <div className="rounded-lg bg-stone-50 p-3">
                <div className="text-xs font-bold uppercase text-stone-500">Bill to</div>
                <div className="font-bold">{inv.customerName}</div>
                <div>{inv.customerVillage}</div>
                {inv.customerMobile && <div>📞 {inv.customerMobile}</div>}
                {inv.customerGstin && <div>GSTIN: {inv.customerGstin}</div>}
              </div>
              <div className="rounded-lg bg-stone-50 p-3 text-right">
                <div className="text-xs font-bold uppercase text-stone-500">Payment</div>
                <div>{inv.paymentMode} · <b>{inv.status}</b></div>
                {inv.isGst && <div>Place of supply: {inv.isInterState ? 'Inter-state' : `${shop.state} (${shop.stateCode})`}</div>}
              </div>
            </div>
            <table className="mt-4 w-full border-collapse text-[12px]">
              <thead>
                <tr className="bg-brand-700 text-white">
                  <th className="p-1.5 text-left">#</th><th className="p-1.5 text-left">Product</th><th className="p-1.5">HSN</th><th className="p-1.5">Batch / Exp</th>
                  <th className="p-1.5 text-right">Qty</th><th className="p-1.5 text-right">Rate</th>{inv.isGst && <><th className="p-1.5 text-right">Taxable</th><th className="p-1.5 text-right">GST</th></>}<th className="p-1.5 text-right">Amount</th>
                </tr>
              </thead>
              <tbody>
                {inv.items.map((i, n) => (
                  <tr key={i.id} className="border-b border-stone-200">
                    <td className="p-1.5">{n + 1}</td><td className="p-1.5 font-semibold">{i.productName}{i.discountAmount > 0 && <div className="text-[10px] font-normal text-stone-500">Disc ₹{num(i.discountAmount)}</div>}</td>
                    <td className="p-1.5 text-center">{i.hsnCode}</td><td className="p-1.5 text-center text-[11px]">{i.batchNo}{i.expiryDate && <div>{dmy(i.expiryDate)}</div>}</td>
                    <td className="p-1.5 text-right">{qty(i.quantity)} {i.unit}</td><td className="p-1.5 text-right">{num(i.rate)}</td>
                    {inv.isGst && <><td className="p-1.5 text-right">{num(i.taxableValue)}</td><td className="p-1.5 text-right">{i.gstRate}%<div className="text-[10px]">{num(i.cgst + i.sgst + i.igst)}</div></td></>}
                    <td className="p-1.5 text-right font-semibold">{num(i.total)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
            <div className="mt-4 flex justify-between gap-6">
              <div className="flex-1 space-y-2">
                <div className="rounded-lg border border-stone-200 p-2 text-xs"><b>Amount in words:</b> {amountInWords(inv.grandTotal)}</div>
                {qr && <div className="flex items-center gap-3"><img src={qr} className="h-24 w-24" alt="UPI QR" /><div className="text-xs">Scan to pay via any UPI app<br /><b>{shop.upiId}</b></div></div>}
                {inv.notes && <div className="text-xs">Note: {inv.notes}</div>}
              </div>
              <table className="w-64 text-sm">
                <tbody>
                  {inv.isGst && <tr><td>Taxable value</td><td className="text-right">{num(inv.subTotal)}</td></tr>}
                  {inv.cgstAmount > 0 && <tr><td>CGST</td><td className="text-right">{num(inv.cgstAmount)}</td></tr>}
                  {inv.sgstAmount > 0 && <tr><td>SGST</td><td className="text-right">{num(inv.sgstAmount)}</td></tr>}
                  {inv.igstAmount > 0 && <tr><td>IGST</td><td className="text-right">{num(inv.igstAmount)}</td></tr>}
                  {tax === 0 && inv.discount > 0 && <tr><td>Discount</td><td className="text-right">−{num(inv.discount)}</td></tr>}
                  <tr><td>Round off</td><td className="text-right">{num(inv.roundOff)}</td></tr>
                  <tr className="border-t-2 border-brand-700 text-lg font-extrabold text-brand-900"><td>Grand Total</td><td className="text-right">{inr(inv.grandTotal)}</td></tr>
                  <tr><td>Paid</td><td className="text-right">{inr(inv.paidAmount)}</td></tr>
                  {inv.balanceAmount > 0 && <tr className="font-bold text-red-700"><td>Balance (उधारी)</td><td className="text-right">{inr(inv.balanceAmount)}</td></tr>}
                  {data.customerOutstanding !== undefined && data.customerOutstanding > 0 && <tr className="text-xs"><td>Total outstanding</td><td className="text-right">{inr(data.customerOutstanding)}</td></tr>}
                </tbody>
              </table>
            </div>
            <div className="mt-10 flex items-end justify-between text-xs">
              <div className="max-w-sm text-stone-600">{shop.invoiceFooter}<div className="mt-1">Goods once sold will not be taken back. Use pesticides as per label instructions.</div></div>
              <div className="text-center"><div className="mb-8 font-semibold">For {shop.shopName}</div><div className="border-t border-stone-400 px-6 pt-1">Authorised Signatory</div></div>
            </div>
            <div className="mt-4 text-center text-[10px] text-stone-400">Generated by Krushi Prime – Smart Billing… Smart Business… · 8668375712</div>
          </div>
        )}
      </div>

      <Modal open={cancelOpen} onClose={() => setCancelOpen(false)} title="Cancel invoice">
        <p className="mb-3 text-sm text-stone-600">Stock will be returned and the customer's udhari reversed. This cannot be undone.</p>
        <input className="input" placeholder="Reason (required)" value={reason} onChange={e => setReason(e.target.value)} />
        <div className="mt-4 flex justify-end gap-2"><Button variant="secondary" onClick={() => setCancelOpen(false)}>Close</Button><Button variant="danger" disabled={!reason.trim()} onClick={cancel}>Cancel invoice</Button></div>
      </Modal>
    </div>
  )
}
