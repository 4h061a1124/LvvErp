import { createContext, useContext } from 'react'
import type { Permission } from './permissions'

export interface CurrentUser {
  id: string
  userName: string
  fullName: string
  email: string | null
  roles: string[]
  permissions: string[]
  employeeId: string | null
}

export type AuthStatus = 'loading' | 'authenticated' | 'anonymous'

export interface AuthContextValue {
  status: AuthStatus
  user: CurrentUser | null
  login: (userName: string, password: string) => Promise<void>
  logout: () => void
  can: (permission: Permission) => boolean
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>')
  return context
}
