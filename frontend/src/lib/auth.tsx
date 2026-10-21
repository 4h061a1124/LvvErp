import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { api, onUnauthorized, tokenStore } from './api'
import { AuthContext, type AuthContextValue, type AuthStatus, type CurrentUser } from './auth-context'

interface LoginResponse {
  accessToken: string
  expiresAt: string
  user: CurrentUser
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [status, setStatus] = useState<AuthStatus>(() => (tokenStore.get() ? 'loading' : 'anonymous'))

  const logout = useCallback(() => {
    tokenStore.clear()
    setUser(null)
    setStatus('anonymous')
    queryClient.clear()
  }, [queryClient])

  useEffect(() => onUnauthorized(logout), [logout])

  // Restore the session from a saved token.
  useEffect(() => {
    if (status !== 'loading') return
    let cancelled = false
    api
      .get<CurrentUser>('/auth/me')
      .then(({ data }) => {
        if (cancelled) return
        setUser(data)
        setStatus('authenticated')
      })
      .catch(() => {
        if (!cancelled) logout()
      })
    return () => {
      cancelled = true
    }
  }, [status, logout])

  const login = useCallback(async (userName: string, password: string) => {
    const { data } = await api.post<LoginResponse>('/auth/login', { userName, password })
    tokenStore.set(data.accessToken)
    setUser(data.user)
    setStatus('authenticated')
  }, [])

  const value = useMemo<AuthContextValue>(
    () => ({
      status,
      user,
      login,
      logout,
      can: (permission) => user?.permissions.includes(permission) ?? false,
    }),
    [status, user, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
