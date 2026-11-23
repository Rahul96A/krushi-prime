import { useState, type FormEvent } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { Button, Field, useToast } from '../components/ui'
import { Logo } from '../components/Layout'

const FEATURES = [
  ['🧾', 'Fast Billing', 'One-click GST / Non-GST bill'], ['📦', 'Smart Stock', 'Batch & expiry tracking (FEFO)'],
  ['🤝', 'Udhari हिशोब', 'Automatic farmer ledger'], ['💬', 'WhatsApp', 'Bills & reminders – FREE'],
  ['📊', 'GST Reports', 'HSN & rate-wise, GSTR-1 ready'], ['🔒', 'Cloud & Secure', 'Microsoft Azure powered'],
]

export default function Login() {
  const { user, login, register } = useAuth()
  const toast = useToast()
  const nav = useNavigate()
  const [mode, setMode] = useState<'login' | 'register'>('login')
  const [busy, setBusy] = useState(false)
  const [f, setF] = useState({ username: '', password: '', shopName: '', ownerName: '', mobile: '', village: '', district: '', gstin: '' })
  const set = (k: keyof typeof f) => (e: React.ChangeEvent<HTMLInputElement>) => setF({ ...f, [k]: e.target.value })

  if (user) return <Navigate to={user.role === 'SuperAdmin' ? '/admin' : '/'} replace />

  const submit = async (e: FormEvent) => {
    e.preventDefault()
    setBusy(true)
    try {
      const u = mode === 'login'
        ? await login(f.username, f.password)
        : await register({ shopName: f.shopName, ownerName: f.ownerName, mobile: f.mobile, password: f.password, village: f.village, district: f.district, gstin: f.gstin || null, seedSampleProducts: true })
      if (mode === 'register') toast.ok('Welcome! Your 6-month FREE trial has started 🎉')
      nav(u.role === 'SuperAdmin' ? '/admin' : '/')
    } catch (err) { toast.err(err) } finally { setBusy(false) }
  }

  return (
    <div className="grid min-h-full lg:grid-cols-2">
      <div className="relative hidden flex-col justify-between overflow-hidden bg-gradient-to-br from-brand-800 via-brand-700 to-brand-900 p-10 text-white lg:flex">
        <div className="absolute -right-24 -top-24 h-96 w-96 rounded-full bg-harvest-400/20 blur-3xl" />
        <Logo light />
        <div className="relative">
          <p className="text-lg font-semibold text-harvest-400">कृषी सेवा केंद्रासाठी</p>
          <h1 className="mt-1 text-5xl font-extrabold leading-tight">सर्वोत्तम बिलिंग सोल्यूशन</h1>
          <p className="mt-2 text-xl text-brand-100">जलद । अचूक । प्रोफेशनल</p>
          <div className="mt-8 grid grid-cols-2 gap-3">
            {FEATURES.map(([i, a, b]) => (
              <div key={a} className="rounded-xl bg-white/10 p-3 backdrop-blur"><div className="text-xl">{i}</div><div className="font-bold">{a}</div><div className="text-xs text-brand-100">{b}</div></div>
            ))}
          </div>
        </div>
        <div className="relative flex items-center justify-between rounded-2xl bg-harvest-400 p-4 text-brand-900">
          <div><div className="text-2xl font-extrabold">🎁 6 महिने मोफत!</div><div className="text-sm font-semibold">Free installation · WhatsApp FREE · 1 year support</div></div>
          <div className="text-right"><div className="text-xs font-bold">आजच संपर्क करा</div><div className="text-xl font-extrabold">📞 8668375712</div></div>
        </div>
      </div>

      <div className="flex items-center justify-center p-6">
        <form onSubmit={submit} className="w-full max-w-md space-y-4">
          <div className="lg:hidden"><Logo /></div>
          <div className="flex rounded-xl bg-stone-200 p-1">
            {(['login', 'register'] as const).map(m => (
              <button type="button" key={m} onClick={() => setMode(m)} className={`flex-1 rounded-lg py-2 text-sm font-bold ${mode === m ? 'bg-white text-brand-800 shadow' : 'text-stone-600'}`}>
                {m === 'login' ? 'Login / लॉगिन' : 'Register Shop – 6 Months FREE'}
              </button>
            ))}
          </div>

          {mode === 'login' ? (
            <>
              <h2 className="text-2xl font-extrabold text-brand-900">स्वागत आहे 🙏</h2>
              <Field label="Mobile number / Username"><input className="input" value={f.username} onChange={set('username')} autoFocus required /></Field>
              <Field label="Password"><input className="input" type="password" value={f.password} onChange={set('password')} required /></Field>
              <Button className="w-full py-3" loading={busy}>Login</Button>
              <div className="rounded-xl border border-dashed border-brand-300 bg-brand-50 p-3 text-xs text-brand-800">
                <b>Demo shop:</b> 9999999999 / demo@123 &nbsp;·&nbsp; <b>Vendor admin:</b> admin / Admin@123
                <div className="mt-2 flex gap-2">
                  <button type="button" className="rounded bg-white px-2 py-1 font-semibold shadow-sm" onClick={() => setF({ ...f, username: '9999999999', password: 'demo@123' })}>Use demo shop</button>
                  <button type="button" className="rounded bg-white px-2 py-1 font-semibold shadow-sm" onClick={() => setF({ ...f, username: 'admin', password: 'Admin@123' })}>Use admin</button>
                </div>
              </div>
            </>
          ) : (
            <>
              <h2 className="text-2xl font-extrabold text-brand-900">आपल्या कृषी व्यवसायाला डिजिटल बनवा!</h2>
              <Field label="Shop name / दुकानाचे नाव"><input className="input" value={f.shopName} onChange={set('shopName')} required placeholder="e.g. Jai Kisan Krushi Seva Kendra" /></Field>
              <div className="grid grid-cols-2 gap-3">
                <Field label="Owner name"><input className="input" value={f.ownerName} onChange={set('ownerName')} required /></Field>
                <Field label="Mobile (login)"><input className="input" value={f.mobile} onChange={set('mobile')} required pattern="[6-9][0-9]{9}" inputMode="numeric" maxLength={10} /></Field>
                <Field label="Village / Town"><input className="input" value={f.village} onChange={set('village')} /></Field>
                <Field label="District"><input className="input" value={f.district} onChange={set('district')} /></Field>
              </div>
              <Field label="GSTIN (optional)"><input className="input uppercase" value={f.gstin} onChange={set('gstin')} maxLength={15} /></Field>
              <Field label="Create password (min 6)"><input className="input" type="password" value={f.password} onChange={set('password')} required minLength={6} /></Field>
              <Button variant="gold" className="w-full py-3" loading={busy}>Start 6-month FREE trial →</Button>
              <p className="text-center text-xs text-stone-500">Ready product list of fertilizers, seeds & pesticides is added automatically.</p>
            </>
          )}
        </form>
      </div>
    </div>
  )
}
