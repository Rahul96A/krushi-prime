const BASE = import.meta.env.VITE_API_BASE ?? ''
/** Demo build (VITE_DEMO=1): no server, runs on the in-browser mock backend with sample data. */
export const isDemo = import.meta.env.VITE_DEMO === '1'
export const loginPath = isDemo ? '#/login' : '/login'
const TOKEN_KEY = 'kp.token'

/** localStorage that never throws (private mode / sandboxed frames); falls back to memory. */
const mem = new Map<string, string>()
export const safeStorage = {
  getItem: (k: string): string | null => { try { return localStorage.getItem(k) } catch { return mem.get(k) ?? null } },
  setItem: (k: string, v: string) => { try { localStorage.setItem(k, v) } catch { mem.set(k, v) } },
  removeItem: (k: string) => { try { localStorage.removeItem(k) } catch { mem.delete(k) } },
}

export const tokenStore = {
  get: () => safeStorage.getItem(TOKEN_KEY),
  set: (t: string) => safeStorage.setItem(TOKEN_KEY, t),
  clear: () => safeStorage.removeItem(TOKEN_KEY),
}

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) { super(message); this.status = status }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  if (isDemo) {
    const { mockRequest } = await import('../demo/mockServer')
    try { return await mockRequest(method, path, body) as T } catch (e) {
      const err = e as { status?: number; message: string }
      throw new ApiError(err.status ?? 500, err.message)
    }
  }
  const token = tokenStore.get()
  const res = await fetch(BASE + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  })
  if (res.status === 401 && token && !path.includes('/auth/login')) {
    tokenStore.clear()
    window.location.href = loginPath
  }
  const text = await res.text()
  const data = text ? JSON.parse(text) : undefined
  if (!res.ok) {
    const validation = data?.errors ? Object.values(data.errors as Record<string, string[]>).flat().join(' ') : ''
    throw new ApiError(res.status, validation || data?.title || `Request failed (${res.status})`)
  }
  return data as T
}

export const api = {
  get: <T>(p: string) => request<T>('GET', p),
  post: <T>(p: string, b?: unknown) => request<T>('POST', p, b ?? {}),
  put: <T>(p: string, b?: unknown) => request<T>('PUT', p, b ?? {}),
  del: <T>(p: string) => request<T>('DELETE', p),
}

export const qs = (o: Record<string, string | number | boolean | undefined | null>) => {
  const s = Object.entries(o).filter(([, v]) => v !== undefined && v !== null && v !== '').map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`).join('&')
  return s ? `?${s}` : ''
}
