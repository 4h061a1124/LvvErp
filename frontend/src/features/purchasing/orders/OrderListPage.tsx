import clsx from 'clsx'
import { Search } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router'
import { Button } from '@/components/ui/Button'
import { Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { Pagination } from '@/components/ui/Pagination'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage } from '@/lib/errors'
import { formatDate, formatMoney } from '@/lib/format'
import { useDebouncedValue } from '@/lib/hooks'
import { Permissions } from '@/lib/permissions'
import { useListParams } from '@/lib/useListParams'
import { useOrders } from '../api'
import { OrderStatusBadge } from '../components/StatusBadges'
import { ORDER_STATUSES, type OrderStatus, type PurchaseOrderSummary } from '../types'

const DEFAULTS = { q: '', status: '', overdue: '', from: '', to: '', page: 1, size: 20 }

export function OrderListPage() {
  const { t, i18n } = useTranslation()
  const navigate = useNavigate()
  const canManage = useAuth().can(Permissions.PurchasingManageOrders)

  const [filters, setFilters] = useListParams(DEFAULTS)
  const orders = useOrders({
    search: filters.q || undefined,
    status: (filters.status || undefined) as OrderStatus | undefined,
    overdueOnly: filters.overdue === '1' || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
    page: filters.page,
    pageSize: filters.size,
  })

  const [searchText, setSearchText] = useState(filters.q)
  const debouncedSearch = useDebouncedValue(searchText.trim())
  const appliedSearch = useRef(debouncedSearch)
  useEffect(() => {
    if (debouncedSearch === appliedSearch.current) return
    appliedSearch.current = debouncedSearch
    setFilters({ q: debouncedSearch })
  }, [debouncedSearch, setFilters])

  const rows = orders.data?.items ?? []
  const hasFilters = !!(filters.q || filters.status || filters.overdue || filters.from || filters.to)
  const expected = (o: PurchaseOrderSummary) =>
    o.expectedDate && <span className={o.isOverdue ? 'font-medium text-red-700' : undefined}>{formatDate(o.expectedDate)}</span>

  return (
    <>
      <PageHeader
        title={t('purchasing.orders.title')}
        meta={orders.data && t('purchasing.orders.count', { count: orders.data.totalCount })}
        actions={
          canManage && (
            <Button variant="primary" onClick={() => navigate('/purchasing/orders/new')}>
              {t('purchasing.orders.add')}
            </Button>
          )
        }
      />

      <div className="rounded-[4px] border border-zinc-200 bg-white">
        <div className="flex flex-wrap items-center gap-2 border-b border-zinc-200 p-2.5">
          <div className="relative w-full sm:w-64">
            <Search size={15} className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-zinc-400" />
            <Input
              type="search"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder={t('purchasing.orders.searchPlaceholder')}
              aria-label={t('purchasing.orders.searchPlaceholder')}
              className="pl-8"
            />
          </div>
          <Select
            value={filters.status}
            onChange={(e) => setFilters({ status: e.target.value })}
            aria-label={t('inventory.status')}
            className="w-full sm:w-40"
          >
            <option value="">{t('common.allStatuses')}</option>
            {ORDER_STATUSES.map((s) => (
              <option key={s} value={s}>
                {t(`purchasing.orders.status.${s}`)}
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
              checked={filters.overdue === '1'}
              onChange={(e) => setFilters({ overdue: e.target.checked ? '1' : '' })}
              className="size-4 accent-brand-600"
            />
            {t('purchasing.orders.overdueOnly')}
          </label>
          {hasFilters && (
            <button
              type="button"
              onClick={() => {
                setSearchText('')
                setFilters({ q: '', status: '', overdue: '', from: '', to: '' })
              }}
              className="px-1 text-[13px] text-brand-700 hover:underline"
            >
              {t('common.clearFilters')}
            </button>
          )}
        </div>

        <div className={clsx('transition-opacity', orders.isPlaceholderData && 'opacity-60')}>
          {orders.isPending ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
          ) : orders.isError ? (
            <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(orders.error, t)}</p>
          ) : rows.length === 0 ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">
              {hasFilters ? t('purchasing.orders.emptyFiltered') : t('purchasing.orders.empty')}
            </p>
          ) : (
            <>
              <div className="hidden overflow-x-auto md:block">
                <table className="w-full text-left">
                  <thead>
                    <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                      <th className="w-36 px-3 py-2 font-medium">{t('purchasing.orders.number')}</th>
                      <th className="w-28 px-3 py-2 font-medium">{t('purchasing.orders.orderDate')}</th>
                      <th className="w-32 px-3 py-2 font-medium">{t('purchasing.orders.expectedDate')}</th>
                      <th className="px-3 py-2 font-medium">{t('purchasing.orders.supplier')}</th>
                      <th className="px-3 py-2 font-medium">{t('purchasing.orders.warehouse')}</th>
                      <th className="w-36 px-3 py-2 text-right font-medium">{t('purchasing.orders.total')}</th>
                      <th className="w-32 px-3 py-2 font-medium">{t('inventory.status')}</th>
                      <th className="w-28 px-3 py-2 font-medium">{t('inventory.documents.createdBy')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((o) => (
                      <tr key={o.id} className="border-b border-zinc-100 last:border-b-0 hover:bg-zinc-50/70">
                        <td className="px-3 py-2">
                          <Link to={`/purchasing/orders/${o.id}`} className="tabular text-[13px] font-medium text-brand-700 hover:underline">
                            {o.number}
                          </Link>
                        </td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{formatDate(o.orderDate)}</td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{expected(o)}</td>
                        <td className="px-3 py-2 text-[13px] text-zinc-800">{o.supplierName}</td>
                        <td className="px-3 py-2 text-[13px] text-zinc-700">{o.warehouseName}</td>
                        <td className="tabular px-3 py-2 text-right text-[13px] text-zinc-900">{formatMoney(o.total, i18n.language)}</td>
                        <td className="px-3 py-2">
                          <OrderStatusBadge status={o.status} />
                        </td>
                        <td className="px-3 py-2 text-[13px] text-zinc-600">{o.createdBy}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <ul className="divide-y divide-zinc-100 md:hidden">
                {rows.map((o) => (
                  <li key={o.id}>
                    <Link to={`/purchasing/orders/${o.id}`} className="block px-3 py-2.5 active:bg-zinc-50">
                      <div className="flex items-baseline justify-between gap-2">
                        <span className="tabular text-sm font-medium text-brand-700">{o.number}</span>
                        <span className="tabular text-sm text-zinc-900">{formatMoney(o.total, i18n.language)}</span>
                      </div>
                      <div className="truncate text-[13px] text-zinc-700">{o.supplierName}</div>
                      <div className="mt-0.5 flex items-center justify-between text-xs text-zinc-500">
                        <OrderStatusBadge status={o.status} />
                        <span>{expected(o)}</span>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>

        {orders.data && orders.data.totalCount > 0 && (
          <Pagination
            page={filters.page}
            pageSize={filters.size}
            total={orders.data.totalCount}
            onPageChange={(page) => setFilters({ page })}
            onPageSizeChange={(size) => setFilters({ size })}
          />
        )}
      </div>
    </>
  )
}
