import { useTranslation } from 'react-i18next'
import { Navigate, Outlet, useLocation } from 'react-router'
import { useAuth } from '@/lib/auth-context'

export function RequireAuth() {
  const { status } = useAuth()
  const location = useLocation()
  const { t } = useTranslation()

  if (status === 'loading') {
    return <div className="p-6 text-sm text-zinc-500">{t('common.loading')}</div>
  }
  if (status === 'anonymous') {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }
  return <Outlet />
}
