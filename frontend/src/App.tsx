import { BrowserRouter, MemoryRouter, Navigate, Route, Routes } from 'react-router-dom'
import { isDemo } from './lib/api'
import type { ReactNode } from 'react'
import { AuthProvider, useAuth } from './lib/auth'
import { I18nProvider } from './lib/i18n'
import { Spinner, ToastProvider } from './components/ui'
import Layout, { MoreMenu } from './components/Layout'
import Login from './pages/Login'
import Dashboard from './pages/Dashboard'
import Billing from './pages/Billing'
import InvoiceView from './pages/InvoiceView'
import Sales from './pages/Sales'
import Products from './pages/Products'
import Stock from './pages/Stock'
import Customers from './pages/Customers'
import Udhari from './pages/Udhari'
import Suppliers from './pages/Suppliers'
import Purchases from './pages/Purchases'
import Reports from './pages/Reports'
import Settings from './pages/Settings'
import Admin from './pages/Admin'
import OnlinePayments from './pages/OnlinePayments'

function Guard({ children, roles }: { children: ReactNode; roles?: string[] }) {
  const { user, loading } = useAuth()
  if (loading) return <Spinner />
  if (!user) return <Navigate to="/login" replace />
  if (user.role === 'SuperAdmin' && !roles?.includes('SuperAdmin')) return <Navigate to="/admin" replace />
  if (roles && !roles.includes(user.role)) return <Navigate to="/" replace />
  return <>{children}</>
}

// Demo runs inside a sandboxed frame where URL routing is unavailable → keep routes in memory
const Router = isDemo ? MemoryRouter : BrowserRouter

export default function App() {
  return (
    <I18nProvider>
      <ToastProvider>
        <AuthProvider>
          <Router>
            <Routes>
              <Route path="/login" element={<Login />} />
              <Route path="/admin" element={<Guard roles={['SuperAdmin']}><Admin /></Guard>} />
              <Route element={<Guard><Layout /></Guard>}>
                <Route index element={<Dashboard />} />
                <Route path="billing" element={<Billing />} />
                <Route path="sales" element={<Sales />} />
                <Route path="sales/:id" element={<InvoiceView />} />
                <Route path="products" element={<Products />} />
                <Route path="stock" element={<Stock />} />
                <Route path="customers" element={<Customers />} />
                <Route path="udhari" element={<Udhari />} />
                <Route path="online-payments" element={<OnlinePayments />} />
                <Route path="suppliers" element={<Guard roles={['Owner', 'Manager']}><Suppliers /></Guard>} />
                <Route path="purchases" element={<Guard roles={['Owner', 'Manager']}><Purchases /></Guard>} />
                <Route path="reports" element={<Reports />} />
                <Route path="settings" element={<Settings />} />
                <Route path="more" element={<MoreMenu />} />
              </Route>
              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </Router>
        </AuthProvider>
      </ToastProvider>
    </I18nProvider>
  )
}
