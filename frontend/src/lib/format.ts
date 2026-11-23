export const inr = (n: number | undefined | null, decimals = 0) =>
  '₹' + (n ?? 0).toLocaleString('en-IN', { minimumFractionDigits: decimals, maximumFractionDigits: decimals })

export const num = (n: number | undefined | null, d = 2) => (n ?? 0).toLocaleString('en-IN', { minimumFractionDigits: d, maximumFractionDigits: d })

export const qty = (n: number) => (Number.isInteger(n) ? String(n) : n.toFixed(3).replace(/0+$/, ''))

export const dmy = (s?: string | null) => {
  if (!s) return '—'
  const d = new Date(s)
  return `${String(d.getDate()).padStart(2, '0')}-${String(d.getMonth() + 1).padStart(2, '0')}-${d.getFullYear()}`
}

export const dmyt = (s?: string | null) => (s ? `${dmy(s)} ${new Date(s).toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' })}` : '—')

export const isoDate = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
export const today = () => isoDate(new Date())
export const monthStart = () => { const d = new Date(); return isoDate(new Date(d.getFullYear(), d.getMonth(), 1)) }

export const daysUntil = (s?: string) => (s ? Math.ceil((new Date(s).setHours(0, 0, 0, 0) - new Date().setHours(0, 0, 0, 0)) / 86400000) : null)

/** Client-side mirror of server TaxCalculator – used for the live bill preview only. Server is the source of truth. */
export function lineTax(gross: number, rate: number, inclusive: boolean, isGst: boolean) {
  const r2 = (v: number) => Math.round((v + Number.EPSILON) * 100) / 100
  gross = r2(gross)
  if (!isGst || rate <= 0) return { taxable: gross, tax: 0, total: gross }
  const taxable = inclusive ? r2((gross * 100) / (100 + rate)) : gross
  const tax = inclusive ? r2(gross - taxable) : r2((gross * rate) / 100)
  return { taxable, tax, total: r2(taxable + tax) }
}

export function downloadCsv(filename: string, rows: Record<string, unknown>[]) {
  if (!rows.length) return
  const headers = Object.keys(rows[0])
  const esc = (v: unknown) => `"${String(v ?? '').replace(/"/g, '""')}"`
  const csv = [headers.join(','), ...rows.map(r => headers.map(h => esc(r[h])).join(','))].join('\n')
  const blob = new Blob(['﻿' + csv], { type: 'text/csv;charset=utf-8' })
  const a = document.createElement('a')
  a.href = URL.createObjectURL(blob)
  a.download = filename
  a.click()
  URL.revokeObjectURL(a.href)
}

const ones = ['', 'One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven', 'Eight', 'Nine', 'Ten', 'Eleven', 'Twelve', 'Thirteen', 'Fourteen', 'Fifteen', 'Sixteen', 'Seventeen', 'Eighteen', 'Nineteen']
const tens = ['', '', 'Twenty', 'Thirty', 'Forty', 'Fifty', 'Sixty', 'Seventy', 'Eighty', 'Ninety']
const two = (n: number) => (n < 20 ? ones[n] : `${tens[Math.floor(n / 10)]} ${ones[n % 10]}`.trim())
const three = (n: number) => (n >= 100 ? `${ones[Math.floor(n / 100)]} Hundred ${two(n % 100)}`.trim() : two(n))

/** Indian numbering words, e.g. 125000 → "One Lakh Twenty Five Thousand Rupees Only". */
export function amountInWords(amount: number) {
  let n = Math.round(amount)
  if (n === 0) return 'Zero Rupees Only'
  const parts: string[] = []
  const crore = Math.floor(n / 10000000); n %= 10000000
  const lakh = Math.floor(n / 100000); n %= 100000
  const thousand = Math.floor(n / 1000); n %= 1000
  if (crore) parts.push(`${three(crore)} Crore`)
  if (lakh) parts.push(`${two(lakh)} Lakh`)
  if (thousand) parts.push(`${two(thousand)} Thousand`)
  if (n) parts.push(three(n))
  return `${parts.join(' ')} Rupees Only`
}
