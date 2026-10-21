import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { PageHeader } from '@/components/ui/PageHeader'
import { useEmployees } from '@/features/employees/api'
import { EmployeeAvatar } from '@/features/employees/components/EmployeeAvatar'
import { StatusBadge } from '@/features/employees/components/StatusBadge'
import { EMPLOYEE_STATUSES, type EmployeeStatus } from '@/features/employees/types'
import { useAuth } from '@/lib/auth-context'

function useEmployeeCount(status?: EmployeeStatus) {
  return useEmployees({ status, page: 1, pageSize: 1 }).data?.totalCount
}

function Stat({ label, value, to }: { label: string; value: number | undefined; to: string }) {
  return (
    <Link to={to} className="block bg-white px-4 py-3 hover:bg-zinc-50">
      <div className="text-[13px] text-zinc-500">{label}</div>
      <div className="tabular mt-1 text-2xl font-semibold text-zinc-900">{value ?? '–'}</div>
    </Link>
  )
}

export function DashboardPage() {
  const { t } = useTranslation()
  const { user } = useAuth()

  const total = useEmployeeCount()
  const active = useEmployeeCount('Active')
  const onLeave = useEmployeeCount('OnLeave')
  const resigned = useEmployeeCount('Resigned')
  const counts: Record<EmployeeStatus, number | undefined> = { Active: active, OnLeave: onLeave, Resigned: resigned }

  const recent = useEmployees({ page: 1, pageSize: 6, sort: '-createdAt' })

  return (
    <>
      <PageHeader title={t('dashboard.title')} meta={t('dashboard.hello', { name: user?.fullName })} />

      <div className="mb-5 grid grid-cols-2 gap-px overflow-hidden rounded-[4px] border border-zinc-200 bg-zinc-200 lg:grid-cols-4">
        <Stat label={t('dashboard.total')} value={total} to="/employees" />
        {EMPLOYEE_STATUSES.map((s) => (
          <Stat key={s} label={t(`employees.status.${s}`)} value={counts[s]} to={`/employees?status=${s}`} />
        ))}
      </div>

      <section className="rounded-[4px] border border-zinc-200 bg-white">
        <div className="flex items-center justify-between border-b border-zinc-200 px-3 py-2">
          <h2 className="text-sm font-semibold text-zinc-800">{t('dashboard.recent')}</h2>
          <Link to="/employees?sort=-createdAt" className="text-[13px] text-brand-700 hover:underline">
            {t('common.viewAll')}
          </Link>
        </div>
        {recent.data?.items.length ? (
          <ul className="divide-y divide-zinc-100">
            {recent.data.items.map((e) => (
              <li key={e.id} className="flex items-center gap-3 px-3 py-2">
                <EmployeeAvatar name={e.fullName} photoUrl={e.photoUrl} size={30} />
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm font-medium text-zinc-900">{e.fullName}</div>
                  <div className="truncate text-xs text-zinc-500">
                    {e.code} · {e.positionName} · {e.departmentName}
                  </div>
                </div>
                <StatusBadge status={e.status} />
              </li>
            ))}
          </ul>
        ) : (
          <p className="px-3 py-6 text-center text-sm text-zinc-500">
            {recent.isPending ? t('common.loading') : t('employees.empty')}
          </p>
        )}
      </section>
    </>
  )
}
