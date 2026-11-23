import { createContext, useCallback, useContext, useEffect, useState, type ButtonHTMLAttributes, type ReactNode } from 'react'
import { createPortal } from 'react-dom'

// ── Button ──
type Variant = 'primary' | 'secondary' | 'danger' | 'ghost' | 'whatsapp' | 'gold'
const variants: Record<Variant, string> = {
  primary: 'bg-brand-700 text-white hover:bg-brand-800 shadow-sm',
  secondary: 'bg-white text-stone-700 border border-stone-300 hover:bg-stone-50',
  danger: 'bg-red-600 text-white hover:bg-red-700',
  ghost: 'text-stone-600 hover:bg-stone-100',
  whatsapp: 'bg-[#25D366] text-white hover:bg-[#1ebe5a]',
  gold: 'bg-harvest-400 text-brand-900 hover:bg-harvest-500 font-bold',
}
export function Button({ variant = 'primary', loading, className = '', children, ...rest }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: Variant; loading?: boolean }) {
  return (
    <button {...rest} disabled={rest.disabled || loading}
      className={`inline-flex items-center justify-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition disabled:cursor-not-allowed disabled:opacity-50 ${variants[variant]} ${className}`}>
      {loading && <span className="h-4 w-4 animate-spin rounded-full border-2 border-current border-t-transparent" />}
      {children}
    </button>
  )
}

// ── Layout helpers ──
export const Card = ({ title, actions, children, className = '' }: { title?: ReactNode; actions?: ReactNode; children: ReactNode; className?: string }) => (
  <section className={`card ${className}`}>
    {(title || actions) && (
      <header className="flex flex-wrap items-center justify-between gap-2 border-b border-stone-100 px-4 py-3">
        <h3 className="font-bold text-stone-800">{title}</h3>
        <div className="flex flex-wrap gap-2">{actions}</div>
      </header>
    )}
    <div className="p-4">{children}</div>
  </section>
)

export const PageHeader = ({ title, subtitle, actions }: { title: ReactNode; subtitle?: ReactNode; actions?: ReactNode }) => (
  <div className="mb-5 flex flex-wrap items-end justify-between gap-3">
    <div>
      <h1 className="text-2xl font-extrabold text-brand-900">{title}</h1>
      {subtitle && <p className="text-sm text-stone-500">{subtitle}</p>}
    </div>
    <div className="flex flex-wrap gap-2 no-print">{actions}</div>
  </div>
)

export const Field = ({ label, children, className = '' }: { label: string; children: ReactNode; className?: string }) => (
  <label className={`block ${className}`}><span className="label">{label}</span>{children}</label>
)

export function Stat({ label, value, sub, tone = 'green', icon }: { label: string; value: ReactNode; sub?: ReactNode; tone?: 'green' | 'gold' | 'red' | 'blue' | 'stone'; icon?: string }) {
  const tones = { green: 'from-brand-600 to-brand-800 text-white', gold: 'from-harvest-400 to-amber-500 text-brand-900', red: 'from-rose-500 to-red-600 text-white', blue: 'from-sky-500 to-blue-600 text-white', stone: 'from-stone-600 to-stone-800 text-white' }
  return (
    <div className={`rounded-2xl bg-gradient-to-br p-4 shadow-sm ${tones[tone]}`}>
      <div className="flex items-center justify-between text-xs font-semibold uppercase tracking-wide opacity-90"><span>{label}</span><span className="text-lg">{icon}</span></div>
      <div className="mt-1 text-2xl font-extrabold tabular-nums">{value}</div>
      {sub && <div className="mt-0.5 text-xs opacity-90">{sub}</div>}
    </div>
  )
}

export function Badge({ children, tone = 'stone' }: { children: ReactNode; tone?: 'green' | 'red' | 'amber' | 'blue' | 'stone' }) {
  const t = { green: 'bg-brand-100 text-brand-800', red: 'bg-red-100 text-red-700', amber: 'bg-amber-100 text-amber-800', blue: 'bg-sky-100 text-sky-800', stone: 'bg-stone-100 text-stone-700' }
  return <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold ${t[tone]}`}>{children}</span>
}

export const statusTone = (s: string) => (s === 'Paid' ? 'green' : s === 'Partial' ? 'amber' : s === 'Cancelled' ? 'stone' : 'red') as 'green' | 'amber' | 'stone' | 'red'

export const Empty = ({ text = 'No records found' }: { text?: string }) => <div className="py-10 text-center text-sm text-stone-400">🌱 {text}</div>
export const Spinner = () => <div className="flex justify-center py-10"><span className="h-8 w-8 animate-spin rounded-full border-4 border-brand-600 border-t-transparent" /></div>

// ── Modal ──
export function Modal({ open, onClose, title, children, wide }: { open: boolean; onClose: () => void; title: ReactNode; children: ReactNode; wide?: boolean }) {
  useEffect(() => {
    const h = (e: KeyboardEvent) => e.key === 'Escape' && onClose()
    if (open) window.addEventListener('keydown', h)
    return () => window.removeEventListener('keydown', h)
  }, [open, onClose])
  if (!open) return null
  // Portal: modals opened from table rows must not inherit row styles (e.g. nowrap) or row click handlers
  return createPortal(
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-black/40 p-4 text-left whitespace-normal no-print" onMouseDown={onClose}>
      <div className={`card mt-10 w-full ${wide ? 'max-w-4xl' : 'max-w-lg'}`} onMouseDown={e => e.stopPropagation()}>
        <div className="flex items-center justify-between border-b border-stone-100 px-5 py-3">
          <h3 className="text-lg font-bold text-brand-900">{title}</h3>
          <button className="text-2xl leading-none text-stone-400 hover:text-stone-700" onClick={onClose}>×</button>
        </div>
        <div className="p-5">{children}</div>
      </div>
    </div>,
    document.body,
  )
}

// ── Toasts ──
type Toast = { id: number; text: string; tone: 'ok' | 'err' }
const ToastCtx = createContext<{ ok: (t: string) => void; err: (e: unknown) => void }>(null!)
export function ToastProvider({ children }: { children: ReactNode }) {
  const [list, setList] = useState<Toast[]>([])
  const push = useCallback((text: string, tone: Toast['tone']) => {
    const id = Date.now() + Math.random()
    setList(l => [...l, { id, text, tone }])
    setTimeout(() => setList(l => l.filter(x => x.id !== id)), 4500)
  }, [])
  const value = { ok: (t: string) => push(t, 'ok'), err: (e: unknown) => push(e instanceof Error ? e.message : String(e), 'err') }
  return (
    <ToastCtx.Provider value={value}>
      {children}
      <div className="fixed bottom-20 right-4 z-[60] flex flex-col gap-2 md:bottom-4 no-print">
        {list.map(t => (
          <div key={t.id} className={`max-w-sm rounded-xl px-4 py-3 text-sm font-medium text-white shadow-lg ${t.tone === 'ok' ? 'bg-brand-700' : 'bg-red-600'}`}>
            {t.tone === 'ok' ? '✅ ' : '⚠️ '}{t.text}
          </div>
        ))}
      </div>
    </ToastCtx.Provider>
  )
}
export const useToast = () => useContext(ToastCtx)

// ── Data hook ──
export function useLoad<T>(fn: () => Promise<T>, deps: unknown[] = []) {
  const [data, setData] = useState<T | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [tick, setTick] = useState(0)
  useEffect(() => {
    let alive = true
    setLoading(true)
    fn().then(d => { if (alive) { setData(d); setError(null) } }).catch(e => alive && setError(e.message)).finally(() => alive && setLoading(false))
    return () => { alive = false }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, tick])
  return { data, loading, error, reload: () => setTick(t => t + 1), setData }
}

/** Opens WhatsApp click-to-chat. Uses a real link click (window.open is blocked in some embedded viewers). */
export const openWhatsApp = (url?: string) => {
  if (!url) return
  const a = document.createElement('a')
  a.href = url; a.target = '_blank'; a.rel = 'noopener noreferrer'
  document.body.appendChild(a); a.click(); a.remove()
}
