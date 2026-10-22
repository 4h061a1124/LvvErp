import clsx from 'clsx'
import { Search } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { Button } from '@/components/ui/Button'
import { Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { Pagination } from '@/components/ui/Pagination'
import { useDepartments } from '@/features/employees/api'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage } from '@/lib/errors'
import { formatDate } from '@/lib/format'
import { useDebouncedValue } from '@/lib/hooks'
import { Permissions } from '@/lib/permissions'
import { useListParams } from '@/lib/useListParams'
import { useRequests } from '../api'
import { RequestStatusBadge } from '../components/StatusBadges'
import { REQUEST_STATUSES, type RequestStatus } from '../types'

const DEFAULTS = { q: '', status: '', dept: '', mine: '', from: '', to: '', page: 1, size: 20 }

export function RequestListPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { user, can } = useAuth()
  const seesAll = can(Permissions.PurchasingView)

  const [filters, setFilters] = useListParams(DEFAULTS)
  const requests = useRequests({
    search: filters.q || undefined,
    status: (filters.status || undefined) as RequestStatus | undefined,
    departmentId: filters.dept || undefined,
    mine: filters.mine === '1' || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
    page: filters.page,
    pageSize: filters.size,
  })
  const departments = useDepartments()

  const [searchText, setSearchText] = useState(filters.q)
  const debouncedSearch = useDebouncedValue(searchText.trim())
  const appliedSearch = useRef(debouncedSearch)
  useEffect(() => {
    if (debouncedSearch === appliedSearch.current) return
    appliedSearch.current = debouncedSearch
    setFilters({ q: debouncedSearch })
  }, [debouncedSearch, setFilters])

  const rows = requests.data?.items ?? []
  const hasFilters = !!(filters.q || filters.status || filters.dept || filters.mine || filters.from || filters.to)

  return (
    <>
      <PageHeader
        title={t('purchasing.requests.title')}
        meta={requests.data && t('purchasing.requests.count', { count: requests.data.totalCount })}
        actions={
          <Button variant="primary" onClick={() => navigate('/purchasing/requests/new')}>
            {t('purchasing.requests.add')}
          </Button>
        }
      />

      {!user?.departmentId && (
        <div className="mb-4 border-l-[3px] border-amber-500 bg-amber-50 px-3 py-2 text-[13px] text-amber-900">
          {t('purchasing.requests.noDepartment')}
        </div>
      )}

      <div className="rounded-[4px] border border-zinc-200 bg-white">
        <div className="flex flex-wrap items-center gap-2 border-b border-zinc-200 p-2.5">
          <div className="relative w-full sm:w-64">
            <Search size={15} className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-zinc-400" />
            <Input
              type="search"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder={t('purchasing.requests.searchPlaceholder')}
              aria-label={t('purchasing.requests.searchPlaceholder')}
              className="pl-8"
            />
          </div>
          {seesAll && (
            <Select
              value={filters.dept}
              onChange={(e) => setFilters({ dept: e.target.value })}
              aria-label={t('purchasing.requests.department')}
              className="w-[calc(50%-4px)] sm:w-44"
            >
              <option value="">{t('purchasing.requests.allDepartments')}</option>
              {departments.data?.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </Select>
          )}
          <Select
            value={filters.status}
            onChange={(e) => setFilters({ status: e.target.value })}
            aria-label={t('inventory.status')}
            className="w-[calc(50%-4px)] sm:w-40"
          >
            <option value="">{t('common.allStatuses')}</option>
            {REQUEST_STATUSES.map((s) => (
              <option key={s} value={s}>
                {t(`purchasing.requests.status.${s}`)}
              </option>
            ))}
          </Select>
          <label className="flex items-center gap-1.5 text-[13px] text-zinc-600">
            {t('inventory.documents.fromDate')}
            <Input type="date" value={filters.from} onChange={(e) => setFilters({ from: e.target.value })} className="w-36" />
          </label>
          <label className="flex items-center gap-1.5 text-[13px] text-zinc-600">
            {t('inventory.documents.toDate')}
            <Input type="date" value={filters.to} onChange={(e) => setFilters({ to: e.target.value })} className="w-36" />
          </label>
          <label className="flex cursor-pointer items-center gap-1.5 text-[13px] text-zinc-700">
            <input
              type="checkbox"
              checked={filters.mine === '1'}
              onChange={(e) => setFilters({ mine: e.target.checked ? '1' : '' })}
              className="size-4 accent-brand-600"
            />
            {t('purchasing.requests.mine')}
          </label>
          {hasFilters && (
            <button
              type="button"
              onClick={() => {
                setSearchText('')
                setFilters({ q: '', status: '', dept: '', mine: '', from: '', to: '' })
              }}
              className="px-1 text-[13px] text-brand-700 hover:underline"
            >
              {t('common.clearFilters')}
            </button>
          )}
        </div>

        <div className={clsx('transition-opacity', requests.isPlaceholderData && 'opacity-60')}>
          {requests.isPending ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
          ) : requests.isError ? (
            <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(requests.error, t)}</p>
          ) : rows.length === 0 ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">
              {hasFilters ? t('purchasing.requests.emptyFiltered') : t('purchasing.requests.empty')}
            </p>
          ) : (
            <>
              <div className="hidden overflow-x-auto md:block">
                <table className="w-full text-left">
                  <thead>
                    <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                      <th className="w-36 px-3 py-2 font-medium">{t('purchasing.requests.number')}</th>
                      <th className="w-28 px-3 py-2 font-medium">{t('purchasing.requests.requestDate')}</th>
                      <th className="w-28 px-3 py-2 font-medium">{t('purchasing.requests.neededDate')}</th>
                      <th className="px-3 py-2 font-medium">{t('purchasing.requests.department')}</th>
                      <th className="px-3 py-2 font-medium">{t('purchasing.requests.requester')}</th>
                      <th className="px-3 py-2 font-medium">{t('purchasing.requests.purpose')}</th>
                      <th className="w-20 px-3 py-2 text-right font-medium">{t('purchasing.requests.lineCount')}</th>
                      <th className="w-36 px-3 py-2 font-medium">{t('inventory.status')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((r) => (
                      <tr key={r.id} className="border-b border-zinc-100 last:border-b-0 hover:bg-zinc-50/70">
                        <td className="px-3 py-2">
                          <Link to={`/purchasing/requests/${r.id}`} className="tabular text-[13px] font-medium text-brand-700 hover:underline">
                            {r.number}
                          </Link>
                        </td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{formatDate(r.requestDate)}</td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{formatDate(r.neededDate)}</td>
                        <td className="px-3 py-2 text-[13px] text-zinc-700">{r.departmentName}</td>
                        <td className="px-3 py-2 text-[13px] text-zinc-800">{r.requesterName}</td>
                        <td className="max-w-72 truncate px-3 py-2 text-[13px] text-zinc-600">{r.note}</td>
                        <td className="tabular px-3 py-2 text-right text-[13px] text-zinc-700">{r.lineCount}</td>
                        <td className="px-3 py-2">
                          <RequestStatusBadge status={r.status} />
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <ul className="divide-y divide-zinc-100 md:hidden">
                {rows.map((r) => (
                  <li key={r.id}>
                    <Link to={`/purchasing/requests/${r.id}`} className="block px-3 py-2.5 active:bg-zinc-50">
                      <div className="flex items-baseline justify-between gap-2">
                        <span className="tabular text-sm font-medium text-brand-700">{r.number}</span>
                        <span className="tabular text-xs text-zinc-500">{formatDate(r.requestDate)}</span>
                      </div>
                      <div className="truncate text-[13px] text-zinc-700">{[r.departmentName, r.requesterName, r.note].filter(Boolean).join(' · ')}</div>
                      <div className="mt-0.5">
                        <RequestStatusBadge status={r.status} />
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>

        {requests.data && requests.data.totalCount > 0 && (
          <Pagination
            page={filters.page}
            pageSize={filters.size}
            total={requests.data.totalCount}
            onPageChange={(page) => setFilters({ page })}
            onPageSizeChange={(size) => setFilters({ size })}
          />
        )}
      </div>
    </>
  )
}
