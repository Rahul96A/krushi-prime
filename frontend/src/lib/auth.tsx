import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from 'react'
import { api, isDemo, loginPath, tokenStore } from './api'
import type { AuthResponse, UserInfo } from './types'

interface AuthCtx {
  user: UserInfo | null
  loading: boolean
  login: (username: string, password: string) => Promise<UserInfo>
  register: (data: Record<string, unknown>) => Promise<UserInfo>
  logout: () => void
  refresh: () => Promise<void>
  can: (...roles: string[]) => boolean
}

const Ctx = createContext<AuthCtx>(null!)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserInfo | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    if (!tokenStore.get()) { setUser(null); return }
    try { setUser(await api.get<UserInfo>('/api/auth/me')) } catch { tokenStore.clear(); setUser(null) }
  }, [])

  useEffect(() => { refresh().finally(() => setLoading(false)) }, [refresh])

  const accept = (r: AuthResponse) => { tokenStore.set(r.token); setUser(r.user); return r.user }

  const value: AuthCtx = {
    user, loading, refresh,
    login: async (username, password) => accept(await api.post<AuthResponse>('/api/auth/login', { username, password })),
    register: async data => accept(await api.post<AuthResponse>('/api/auth/register-shop', data)),
    logout: () => { tokenStore.clear(); setUser(null); if (!isDemo) window.location.href = loginPath },
    can: (...roles) => !!user && roles.includes(user.role),
  }
  return <Ctx.Provider value={value}>{children}</Ctx.Provider>
}

export const useAuth = () => useContext(Ctx)
