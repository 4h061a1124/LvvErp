import clsx from 'clsx'
import { Search } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { getErrorMessage } from '@/lib/errors'
import { formatDateTime } from '@/lib/format'
import { ROLES } from '@/lib/permissions'
import { normalizeSearch } from '@/lib/text'
import { useListParams } from '@/lib/useListParams'
import { useUsers } from './api'
import type { User } from './types'
import { UserFormDialog } from './UserFormDialog'

const DEFAULTS = { q: '', role: '', status: '' }

function AccountStatus({ active }: { active: boolean }) {
  const { t } = useTranslation()
  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1.5 text-[13px] whitespace-nowrap before:size-1.5 before:rounded-full',
        active ? 'text-emerald-700 before:bg-emerald-600' : 'text-red-700 before:bg-red-600',
      )}
    >
      {active ? t('users.active') : t('users.locked')}
    </span>
  )
}

export function UsersPage() {
  const { t, i18n } = useTranslation()
  const users = useUsers()
  const [filters, setFilters] = useListParams(DEFAULTS)
  const [editing, setEditing] = useState<User | null>(null)
  const [formOpen, setFormOpen] = useState(false)

  const rows = useMemo(() => {
    const term = normalizeSearch(filters.q)
    return (users.data ?? []).filter(
      (u) =>
        (!term || [u.userName, u.fullName, u.employeeCode, u.employeeName].some((v) => v && normalizeSearch(v).includes(term))) &&
        (!filters.role || u.roles.includes(filters.role as User['roles'][number])) &&
        (!filters.status || (filters.status === 'active') === u.isActive),
    )
  }, [users.data, filters])
  const hasFilters = !!(filters.q || filters.role || filters.status)

  const openEdit = (user: User | null) => {
    setEditing(user)
    setFormOpen(true)
  }

  const roleList = (u: User) => u.roles.map((r) => t(`roles.${r}`)).join(', ')
  const employee = (u: User) => (u.employeeId ? `${u.employeeCode} – ${u.employeeName}` : null)

  return (
    <>
      <PageHeader
        title={t('users.title')}
        meta={users.data && t('users.count', { count: users.data.length })}
        actions={
          <Button variant="primary" onClick={() => openEdit(null)}>
            {t('users.add')}
          </Button>
        }
      />

      <div className="rounded-[4px] border border-zinc-200 bg-white">
        <div className="flex flex-wrap items-center gap-2 border-b border-zinc-200 p-2.5">
          <div className="relative w-full sm:w-72">
            <Search size={15} className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-zinc-400" />
            <Input
              type="search"
              value={filters.q}
              onChange={(e) => setFilters({ q: e.target.value })}
              placeholder={t('users.searchPlaceholder')}
              aria-label={t('users.searchPlaceholder')}
              className="pl-8"
            />
          </div>
          <Select
            value={filters.role}
            onChange={(e) => setFilters({ role: e.target.value })}
            aria-label={t('users.columns.roles')}
            className="w-[calc(50%-4px)] sm:w-48"
          >
            <option value="">{t('users.allRoles')}</option>
            {ROLES.map((r) => (
              <option key={r} value={r}>
                {t(`roles.${r}`)}
              </option>
            ))}
          </Select>
          <Select
            value={filters.status}
            onChange={(e) => setFilters({ status: e.target.value })}
            aria-label={t('users.columns.status')}
            className="w-[calc(50%-4px)] sm:w-44"
          >
            <option value="">{t('common.allStatuses')}</option>
            <option value="active">{t('users.active')}</option>
            <option value="locked">{t('users.locked')}</option>
          </Select>
          {hasFilters && (
            <button type="button" onClick={() => setFilters({ q: '', role: '', status: '' })} className="px-1 text-[13px] text-brand-700 hover:underline">
              {t('common.clearFilters')}
            </button>
          )}
        </div>

        {users.isPending ? (
          <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
        ) : users.isError ? (
          <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(users.error, t)}</p>
        ) : rows.length === 0 ? (
          <p className="px-3 py-10 text-center text-sm text-zinc-500">{hasFilters ? t('users.emptyFiltered') : t('users.empty')}</p>
        ) : (
          <>
            <div className="hidden overflow-x-auto md:block">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                    <th className="w-36 px-3 py-2 font-medium">{t('users.columns.userName')}</th>
                    <th className="px-3 py-2 font-medium">{t('users.columns.fullName')}</th>
                    <th className="px-3 py-2 font-medium">{t('users.columns.roles')}</th>
                    <th className="px-3 py-2 font-medium">{t('users.columns.employee')}</th>
                    <th className="px-3 py-2 font-medium">{t('users.columns.department')}</th>
                    <th className="w-40 px-3 py-2 font-medium">{t('users.columns.lastLogin')}</th>
                    <th className="w-32 px-3 py-2 font-medium">{t('users.columns.status')}</th>
                    <th className="w-16 px-3 py-2" />
                  </tr>
                </thead>
                <tbody>
                  {rows.map((u) => (
                    <tr key={u.id} className="border-b border-zinc-100 last:border-b-0 hover:bg-zinc-50/70">
                      <td className="px-3 py-2 text-[13px] text-zinc-700">{u.userName}</td>
                      <td className={clsx('px-3 py-2 font-medium', u.isActive ? 'text-zinc-900' : 'text-zinc-500')}>{u.fullName}</td>
                      <td className="px-3 py-2 text-[13px] text-zinc-700">{roleList(u)}</td>
                      <td className="px-3 py-2 text-[13px] text-zinc-700">
                        {employee(u) ?? <span className="text-amber-700">{t('users.noEmployee')}</span>}
                      </td>
                      <td className="px-3 py-2 text-[13px] text-zinc-700">{u.departmentName}</td>
                      <td className="tabular px-3 py-2 text-[13px] text-zinc-600">
                        {u.lastLoginAt ? formatDateTime(u.lastLoginAt, i18n.language) : <span className="text-zinc-400">{t('users.never')}</span>}
                      </td>
                      <td className="px-3 py-2">
                        <AccountStatus active={u.isActive} />
                      </td>
                      <td className="px-3 py-2 text-right text-[13px]">
                        <button type="button" onClick={() => openEdit(u)} className="px-1 text-brand-700 hover:underline">
                          {t('common.edit')}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <ul className="divide-y divide-zinc-100 md:hidden">
              {rows.map((u) => (
                <li key={u.id}>
                  <button type="button" onClick={() => openEdit(u)} className="block w-full px-3 py-2.5 text-left active:bg-zinc-50">
                    <div className="flex items-baseline justify-between gap-2">
                      <span className={clsx('truncate font-medium', u.isActive ? 'text-zinc-900' : 'text-zinc-500')}>{u.fullName}</span>
                      <span className="shrink-0 text-xs text-zinc-500">{u.userName}</span>
                    </div>
                    <div className="truncate text-[13px] text-zinc-600">{[roleList(u), employee(u), u.departmentName].filter(Boolean).join(' · ')}</div>
                    {!u.isActive && <AccountStatus active={false} />}
                  </button>
                </li>
              ))}
            </ul>
          </>
        )}
      </div>

      {formOpen && <UserFormDialog user={editing} onClose={() => setFormOpen(false)} />}
    </>
  )
}
