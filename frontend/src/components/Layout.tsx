import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../lib/auth'
import { useI18n } from '../lib/i18n'
import { dmy } from '../lib/format'
import { isDemo } from '../lib/api'

const NAV: { to: string; key: string; icon: string; roles?: string[] }[] = [
  { to: '/', key: 'dashboard', icon: '📊' },
  { to: '/billing', key: 'billing', icon: '🧾' },
  { to: '/sales', key: 'sales', icon: '📄' },
  { to: '/products', key: 'products', icon: '🌾' },
  { to: '/stock', key: 'stock', icon: '📦' },
  { to: '/customers', key: 'customers', icon: '👨‍🌾' },
  { to: '/udhari', key: 'udhari', icon: '🤝' },
  { to: '/online-payments', key: 'onlinePayments', icon: '💳' },
  { to: '/purchases', key: 'purchases', icon: '🚚', roles: ['Owner', 'Manager'] },
  { to: '/suppliers', key: 'suppliers', icon: '🏭', roles: ['Owner', 'Manager'] },
  { to: '/reports', key: 'reports', icon: '📈' },
  { to: '/settings', key: 'settings', icon: '⚙️' },
]

export const Logo = ({ light }: { light?: boolean }) => (
  <div className="flex items-center gap-2">
    <svg viewBox="0 0 64 64" className="h-9 w-9 shrink-0"><rect width="64" height="64" rx="14" fill={light ? '#fff' : '#15803d'} /><path d="M32 52V28" stroke={light ? '#15803d' : '#fff'} strokeWidth="4" strokeLinecap="round" /><path d="M32 34c-10 0-16-6-17-16 10 0 16 6 17 16z" fill="#4ade80" /><path d="M32 30c1-10 7-17 18-17-1 11-7 17-18 17z" fill="#facc15" /></svg>
    <div className="leading-none">
      <div className={`text-lg font-extrabold ${light ? 'text-white' : 'text-brand-800'}`}>Krushi <span className={light ? 'text-harvest-400' : 'text-brand-600'}>Prime</span></div>
      <div className={`text-[10px] font-medium ${light ? 'text-brand-100' : 'text-stone-500'}`}>Smart Billing… Smart Business…</div>
    </div>
  </div>
)

export default function Layout() {
  const { user, logout, can } = useAuth()
  const { t, lang, setLang } = useI18n()
  const nav = useNavigate()
  const tenant = user?.tenant
  const items = NAV.filter(n => !n.roles || can(...n.roles))

  return (
    <div className="flex h-full">
      <aside className="no-print hidden w-60 shrink-0 flex-col bg-brand-900 text-brand-50 md:flex">
        <div className="p-4"><Logo light /></div>
        <nav className="flex-1 space-y-0.5 overflow-y-auto px-2">
          {items.map(n => (
            <NavLink key={n.to} to={n.to} end={n.to === '/'}
              className={({ isActive }) => `flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition ${isActive ? 'bg-harvest-400 text-brand-900' : 'hover:bg-brand-800'}`}>
              <span className="text-base">{n.icon}</span>{t(n.key)}
            </NavLink>
          ))}
        </nav>
        <div className="border-t border-brand-800 p-3 text-xs text-brand-200">Support: 📞 8668375712</div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="no-print flex items-center justify-between gap-3 border-b border-stone-200 bg-white px-4 py-2.5">
          <div className="md:hidden"><Logo /></div>
          <div className="hidden min-w-0 md:block">
            <div className="truncate font-bold text-brand-900">{tenant?.shopName}</div>
            <div className="text-xs text-stone-500">{user?.fullName} · {user?.role}</div>
          </div>
          <div className="flex items-center gap-2">
            {tenant && (
              tenant.isExpired
                ? <span className="rounded-full bg-red-100 px-3 py-1 text-xs font-bold text-red-700">Subscription expired · Renew: 8668375712</span>
                : tenant.plan === 'Trial'
                  ? <span className="hidden rounded-full bg-harvest-400/30 px-3 py-1 text-xs font-bold text-amber-800 sm:inline">🎁 {tenant.daysLeft} {t('trialLeft')}</span>
                  : <span className="hidden rounded-full bg-brand-100 px-3 py-1 text-xs font-bold text-brand-800 sm:inline">{tenant.plan} · till {dmy(tenant.subscriptionEnd)}</span>
            )}
            {isDemo && <button title="Reset sample data" onClick={async () => { (await import('../demo/mockServer')).resetDemo(); window.location.reload() }} className="rounded-lg bg-sky-100 px-2 py-1 text-xs font-bold text-sky-800">LIVE DEMO · ↺ Reset</button>}
            <button onClick={() => setLang(lang === 'en' ? 'mr' : 'en')} className="rounded-lg border border-stone-300 px-2 py-1 text-xs font-bold">{lang === 'en' ? 'मराठी' : 'English'}</button>
            <button onClick={() => nav('/billing')} className="rounded-lg bg-brand-700 px-3 py-1.5 text-xs font-bold text-white md:hidden">+ Bill</button>
            <button onClick={logout} className="rounded-lg px-2 py-1 text-xs font-semibold text-stone-500 hover:bg-stone-100">{t('logout')}</button>
          </div>
        </header>
        <main className="flex-1 overflow-y-auto p-4 pb-24 md:p-6 md:pb-6"><Outlet /></main>
        <nav className="no-print fixed inset-x-0 bottom-0 z-40 flex justify-around border-t border-stone-200 bg-white py-1 md:hidden">
          {items.filter(n => ['/', '/billing', '/sales', '/customers', '/products'].includes(n.to)).map(n => (
            <NavLink key={n.to} to={n.to} end={n.to === '/'} className={({ isActive }) => `flex flex-col items-center px-2 py-1 text-[10px] font-semibold ${isActive ? 'text-brand-700' : 'text-stone-500'}`}>
              <span className="text-lg">{n.icon}</span>{t(n.key)}
            </NavLink>
          ))}
          <NavLink to="/more" className="flex flex-col items-center px-2 py-1 text-[10px] font-semibold text-stone-500"><span className="text-lg">☰</span>More</NavLink>
        </nav>
      </div>
    </div>
  )
}

export function MoreMenu() {
  const { t } = useI18n()
  const { can } = useAuth()
  return (
    <div className="grid grid-cols-3 gap-3">
      {NAV.filter(n => !n.roles || can(...n.roles)).map(n => (
        <NavLink key={n.to} to={n.to} className="card flex flex-col items-center gap-1 p-4 text-center text-sm font-semibold"><span className="text-2xl">{n.icon}</span>{t(n.key)}</NavLink>
      ))}
    </div>
  )
}
