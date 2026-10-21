import clsx from 'clsx'
import { ArrowDown, ArrowUp, ChevronDown, ChevronRight, Search } from 'lucide-react'
import { Fragment, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { Pagination } from '@/components/ui/Pagination'
import { getErrorMessage } from '@/lib/errors'
import { formatDate, formatQuantity } from '@/lib/format'
import { useDebouncedValue } from '@/lib/hooks'
import { useListParams } from '@/lib/useListParams'
import { useCatalog, useWarehouses } from '../api'
import { MaterialThumb } from '../components/MaterialThumb'
import { useStockOnHand, useStockOnHandDetail, type StockOnHand } from './api'

const DEFAULTS = { q: '', group: '', wh: '', low: '', zero: '', page: 1, size: 50, sort: 'code' }

export function StockOnHandPage() {
  const { t, i18n } = useTranslation()
  const [filters, setFilters] = useListParams(DEFAULTS)
  const stock = useStockOnHand({
    search: filters.q || undefined,
    groupId: filters.group || undefined,
    warehouseId: filters.wh || undefined,
    belowMinOnly: filters.low === '1' || undefined,
    includeZero: filters.zero === '1' || undefined,
    page: filters.page,
    pageSize: filters.size,
    sort: filters.sort,
  })
  const warehouses = useWarehouses()
  const groups = useCatalog('material-groups')
  const [expanded, setExpanded] = useState<Set<string>>(new Set())

  const [searchText, setSearchText] = useState(filters.q)
  const debouncedSearch = useDebouncedValue(searchText.trim())
  const appliedSearch = useRef(debouncedSearch)
  useEffect(() => {
    if (debouncedSearch === appliedSearch.current) return
    appliedSearch.current = debouncedSearch
    setFilters({ q: debouncedSearch })
  }, [debouncedSearch, setFilters])

  const rows = stock.data?.items ?? []
  const hasFilters = !!(filters.q || filters.group || filters.wh || filters.low || filters.zero)
  const filteredByWarehouse = !!filters.wh
  const qty = (n: number) => formatQuantity(n, i18n.language)

  const toggle = (id: string) =>
    setExpanded((current) => {
      const next = new Set(current)
      if (next.has(id)) next.delete(id)
      else next.add(id)
      return next
    })

  const sortHeader = (label: string, key: string, className?: string) => {
    const active = filters.sort === key || filters.sort === `-${key}`
    const descending = filters.sort === `-${key}`
    return (
      <th className={clsx('px-3 py-2 font-medium', className)}>
        <button
          type="button"
          onClick={() => setFilters({ sort: active && !descending ? `-${key}` : key })}
          className={clsx('inline-flex items-center gap-1 hover:text-zinc-900', active && 'text-zinc-900')}
        >
          {label}
          {active && (descending ? <ArrowDown size={13} /> : <ArrowUp size={13} />)}
        </button>
      </th>
    )
  }

  return (
    <>
      <PageHeader title={t('inventory.stock.title')} meta={stock.data && t('inventory.materials.count', { count: stock.data.totalCount })} />

      <div className="rounded-[4px] border border-zinc-200 bg-white">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-zinc-200 p-2.5">
          <div className="relative w-full sm:w-64">
            <Search size={15} className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-zinc-400" />
            <Input
              type="search"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder={t('inventory.stock.searchPlaceholder')}
              aria-label={t('inventory.stock.searchPlaceholder')}
              className="pl-8"
            />
          </div>
          <Select value={filters.wh} onChange={(e) => setFilters({ wh: e.target.value })} aria-label={t('inventory.documents.warehouse')} className="w-[calc(50%-6px)] sm:w-44">
            <option value="">{t('inventory.documents.allWarehouses')}</option>
            {warehouses.data?.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </Select>
          <Select value={filters.group} onChange={(e) => setFilters({ group: e.target.value })} aria-label={t('inventory.materials.columns.group')} className="w-[calc(50%-6px)] sm:w-44">
            <option value="">{t('inventory.materials.allGroups')}</option>
            {groups.data?.map((g) => (
              <option key={g.id} value={g.id}>
                {g.name}
              </option>
            ))}
          </Select>
          <label className="flex items-center gap-1.5 text-[13px] text-zinc-700">
            <input type="checkbox" checked={filters.low === '1'} onChange={(e) => setFilters({ low: e.target.checked ? '1' : '' })} className="size-4 accent-brand-600" />
            {t('inventory.stock.belowMinOnly')}
          </label>
          <label className="flex items-center gap-1.5 text-[13px] text-zinc-700">
            <input type="checkbox" checked={filters.zero === '1'} onChange={(e) => setFilters({ zero: e.target.checked ? '1' : '' })} className="size-4 accent-brand-600" />
            {t('inventory.stock.includeZero')}
          </label>
          {hasFilters && (
            <button
              type="button"
              onClick={() => {
                setSearchText('')
                setFilters({ q: '', group: '', wh: '', low: '', zero: '' })
              }}
              className="px-1 text-[13px] text-brand-700 hover:underline"
            >
              {t('common.clearFilters')}
            </button>
          )}
        </div>

        <div className={clsx('overflow-x-auto transition-opacity', stock.isPlaceholderData && 'opacity-60')}>
          {stock.isPending ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
          ) : stock.isError ? (
            <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(stock.error, t)}</p>
          ) : rows.length === 0 ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">{hasFilters ? t('inventory.stock.emptyFiltered') : t('inventory.stock.empty')}</p>
          ) : (
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                  <th className="w-8" />
                  <th className="hidden w-11 sm:table-cell" />
                  {sortHeader(t('inventory.materials.columns.code'), 'code', 'w-32')}
                  {sortHeader(t('inventory.materials.columns.name'), 'name')}
                  <th className="hidden px-3 py-2 font-medium lg:table-cell">{t('inventory.materials.columns.group')}</th>
                  {sortHeader(t('inventory.stock.quantity'), 'quantity', 'w-32 text-right')}
                  {filteredByWarehouse && <th className="hidden w-32 px-3 py-2 text-right font-medium md:table-cell">{t('inventory.stock.total')}</th>}
                  <th className="hidden w-28 px-3 py-2 text-right font-medium md:table-cell">{t('inventory.stock.minStock')}</th>
                  <th className="w-20 px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <Fragment key={row.materialId}>
                    <tr className="border-b border-zinc-100 hover:bg-zinc-50/70">
                      <td className="pl-2">
                        <button
                          type="button"
                          onClick={() => toggle(row.materialId)}
                          aria-expanded={expanded.has(row.materialId)}
                          aria-label={t('inventory.stock.showDetail', { code: row.code })}
                          className="rounded-[3px] p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
                        >
                          {expanded.has(row.materialId) ? <ChevronDown size={15} /> : <ChevronRight size={15} />}
                        </button>
                      </td>
                      <td className="hidden py-1.5 sm:table-cell">
                        <MaterialThumb photoUrl={row.photoUrl} size={30} />
                      </td>
                      <td className="tabular px-3 py-1.5 text-[13px] text-zinc-600">{row.code}</td>
                      <td className="px-3 py-1.5">
                        <div className="text-zinc-900">{row.name}</div>
                        {row.specification && <div className="text-xs text-zinc-500">{row.specification}</div>}
                      </td>
                      <td className="hidden px-3 py-1.5 text-[13px] text-zinc-700 lg:table-cell">{row.groupName}</td>
                      <td className={clsx('tabular px-3 py-1.5 text-right font-medium', row.isBelowMin ? 'text-red-700' : 'text-zinc-900')}>
                        {qty(row.quantity)} <span className="text-xs font-normal text-zinc-500">{row.unitName}</span>
                      </td>
                      {filteredByWarehouse && <td className="tabular hidden px-3 py-1.5 text-right text-zinc-700 md:table-cell">{qty(row.totalQuantity)}</td>}
                      <td className="tabular hidden px-3 py-1.5 text-right text-[13px] md:table-cell">
                        {row.minStock > 0 ? (
                          <span className={row.isBelowMin ? 'text-red-700' : 'text-zinc-600'} title={row.isBelowMin ? t('inventory.stock.belowMin') : undefined}>
                            {qty(row.minStock)}
                          </span>
                        ) : (
                          <span className="text-zinc-400">—</span>
                        )}
                      </td>
                      <td className="px-3 py-1.5 text-right">
                        <Link to={`/inventory/stock-card?material=${row.materialId}${filters.wh ? `&wh=${filters.wh}` : ''}`} className="text-[13px] whitespace-nowrap text-brand-700 hover:underline">
                          {t('inventory.stock.openCard')}
                        </Link>
                      </td>
                    </tr>
                    {expanded.has(row.materialId) && <DetailRow row={row} warehouseId={filters.wh || undefined} colSpan={filteredByWarehouse ? 9 : 8} />}
                  </Fragment>
                ))}
              </tbody>
            </table>
          )}
        </div>

        {stock.data && stock.data.totalCount > 0 && (
          <Pagination
            page={filters.page}
            pageSize={filters.size}
            total={stock.data.totalCount}
            onPageChange={(page) => setFilters({ page })}
            onPageSizeChange={(size) => setFilters({ size })}
          />
        )}
      </div>
    </>
  )
}

function DetailRow({ row, warehouseId, colSpan }: { row: StockOnHand; warehouseId: string | undefined; colSpan: number }) {
  const { t, i18n } = useTranslation()
  const detail = useStockOnHandDetail(row.materialId, warehouseId)
  return (
    <tr className="border-b border-zinc-100 bg-zinc-50/60">
      <td colSpan={colSpan} className="py-2 pr-3 pl-10">
        {detail.isPending ? (
          <span className="text-[13px] text-zinc-500">{t('common.loading')}</span>
        ) : !detail.data?.length ? (
          <span className="text-[13px] text-zinc-500">{t('inventory.stock.detailEmpty')}</span>
        ) : (
          <table className="w-full max-w-2xl text-[13px]">
            <tbody>
              {detail.data.map((d, i) => (
                <tr key={i}>
                  <td className="py-0.5 pr-4 text-zinc-800">{d.warehouseName}</td>
                  <td className="tabular py-0.5 pr-4 text-zinc-600">{d.locationCode ?? '—'}</td>
                  <td className="py-0.5 pr-4 text-zinc-600">
                    {d.lotNumber && `${t('inventory.documents.lot')} ${d.lotNumber}`}
                    {d.expiryDate && <span className="ml-2 text-zinc-500">{t('inventory.documents.expiryDate')} {formatDate(d.expiryDate)}</span>}
                  </td>
                  <td className="tabular py-0.5 text-right text-zinc-900">
                    {formatQuantity(d.quantity, i18n.language)} <span className="text-zinc-500">{row.unitName}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </td>
    </tr>
  )
}
