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
import { formatDate } from '@/lib/format'
import { useDebouncedValue } from '@/lib/hooks'
import { Permissions } from '@/lib/permissions'
import { useListParams } from '@/lib/useListParams'
import { useWarehouses } from '../api'
import { useDocuments } from './api'
import { DocumentStatusBadge } from './DocumentStatusBadge'
import { OpeningBalanceImportDialog } from './OpeningBalanceImportDialog'
import { DOCUMENT_PATHS, DOCUMENT_STATUSES, type DocumentStatus, type DocumentType, type StockDocumentSummary } from './types'

const DEFAULTS = { q: '', status: '', wh: '', from: '', to: '', page: 1, size: 20 }

export function DocumentListPage({ type }: { type: DocumentType }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const canEdit = useAuth().can(Permissions.InventoryEditDocuments)
  const route = DOCUMENT_PATHS[type].route

  const [filters, setFilters] = useListParams(DEFAULTS)
  const documents = useDocuments(type, {
    search: filters.q || undefined,
    status: (filters.status || undefined) as DocumentStatus | undefined,
    warehouseId: filters.wh || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
    page: filters.page,
    pageSize: filters.size,
  })
  const warehouses = useWarehouses()
  const [importOpen, setImportOpen] = useState(false)

  const [searchText, setSearchText] = useState(filters.q)
  const debouncedSearch = useDebouncedValue(searchText.trim())
  const appliedSearch = useRef(debouncedSearch)
  useEffect(() => {
    if (debouncedSearch === appliedSearch.current) return
    appliedSearch.current = debouncedSearch
    setFilters({ q: debouncedSearch })
  }, [debouncedSearch, setFilters])

  const rows = documents.data?.items ?? []
  const hasFilters = !!(filters.q || filters.status || filters.wh || filters.from || filters.to)

  const counterpart = (d: StockDocumentSummary) =>
    type === 'Receipt' ? d.partner : type === 'Issue' ? (d.employeeName ?? d.departmentName) : null
  const warehouseText = (d: StockDocumentSummary) => (type === 'Transfer' ? `${d.warehouseName} → ${d.toWarehouseName}` : d.warehouseName)

  return (
    <>
      <PageHeader
        title={t(`inventory.documents.title.${type}`)}
        meta={documents.data && t('inventory.documents.count', { count: documents.data.totalCount })}
        actions={
          canEdit && (
            <>
              {type === 'Receipt' && <Button onClick={() => setImportOpen(true)}>{t('inventory.documents.openingImport')}</Button>}
              <Button variant="primary" onClick={() => navigate(`${route}/new`)}>
                {t(`inventory.documents.add.${type}`)}
              </Button>
            </>
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
              placeholder={t('inventory.documents.searchPlaceholder')}
              aria-label={t('inventory.documents.searchPlaceholder')}
              className="pl-8"
            />
          </div>
          <Select
            value={filters.wh}
            onChange={(e) => setFilters({ wh: e.target.value })}
            aria-label={t('inventory.documents.warehouse')}
            className="w-[calc(50%-4px)] sm:w-44"
          >
            <option value="">{t('inventory.documents.allWarehouses')}</option>
            {warehouses.data?.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </Select>
          <Select
            value={filters.status}
            onChange={(e) => setFilters({ status: e.target.value })}
            aria-label={t('inventory.status')}
            className="w-[calc(50%-4px)] sm:w-36"
          >
            <option value="">{t('common.allStatuses')}</option>
            {DOCUMENT_STATUSES.map((s) => (
              <option key={s} value={s}>
                {t(`inventory.documents.status.${s}`)}
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
          {hasFilters && (
            <button
              type="button"
              onClick={() => {
                setSearchText('')
                setFilters({ q: '', status: '', wh: '', from: '', to: '' })
              }}
              className="px-1 text-[13px] text-brand-700 hover:underline"
            >
              {t('common.clearFilters')}
            </button>
          )}
        </div>

        <div className={clsx('transition-opacity', documents.isPlaceholderData && 'opacity-60')}>
          {documents.isPending ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
          ) : documents.isError ? (
            <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(documents.error, t)}</p>
          ) : rows.length === 0 ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">
              {hasFilters ? t('inventory.documents.emptyFiltered') : t('inventory.documents.empty')}
            </p>
          ) : (
            <>
              <div className="hidden overflow-x-auto md:block">
                <table className="w-full text-left">
                  <thead>
                    <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                      <th className="w-36 px-3 py-2 font-medium">{t('inventory.documents.number')}</th>
                      <th className="w-28 px-3 py-2 font-medium">{t('inventory.documents.date')}</th>
                      <th className="px-3 py-2 font-medium">{t('inventory.documents.warehouse')}</th>
                      {type !== 'Transfer' && <th className="px-3 py-2 font-medium">{t('inventory.documents.reason')}</th>}
                      {type !== 'Transfer' && (
                        <th className="px-3 py-2 font-medium">
                          {type === 'Receipt' ? t('inventory.documents.partner') : t('inventory.documents.employee')}
                        </th>
                      )}
                      <th className="w-20 px-3 py-2 text-right font-medium">{t('inventory.documents.lineCount')}</th>
                      <th className="w-28 px-3 py-2 font-medium">{t('inventory.status')}</th>
                      <th className="w-28 px-3 py-2 font-medium">{t('inventory.documents.createdBy')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((d) => (
                      <tr key={d.id} className="border-b border-zinc-100 last:border-b-0 hover:bg-zinc-50/70">
                        <td className="px-3 py-2">
                          <Link to={`${route}/${d.id}`} className="tabular text-[13px] font-medium text-brand-700 hover:underline">
                            {d.number}
                          </Link>
                        </td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{formatDate(d.documentDate)}</td>
                        <td className="px-3 py-2 text-zinc-800">{warehouseText(d)}</td>
                        {type !== 'Transfer' && (
                          <td className="px-3 py-2 text-[13px] text-zinc-700">
                            {d.reason && t(`inventory.documents.reasons.${d.reason}`)}
                          </td>
                        )}
                        {type !== 'Transfer' && <td className="px-3 py-2 text-[13px] text-zinc-700">{counterpart(d)}</td>}
                        <td className="tabular px-3 py-2 text-right text-[13px] text-zinc-700">{d.lineCount}</td>
                        <td className="px-3 py-2">
                          <DocumentStatusBadge status={d.status} />
                        </td>
                        <td className="px-3 py-2 text-[13px] text-zinc-600">{d.createdBy}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <ul className="divide-y divide-zinc-100 md:hidden">
                {rows.map((d) => (
                  <li key={d.id}>
                    <Link to={`${route}/${d.id}`} className="block px-3 py-2.5 active:bg-zinc-50">
                      <div className="flex items-baseline justify-between gap-2">
                        <span className="tabular text-sm font-medium text-brand-700">{d.number}</span>
                        <span className="tabular text-xs text-zinc-500">{formatDate(d.documentDate)}</span>
                      </div>
                      <div className="truncate text-[13px] text-zinc-700">
                        {[warehouseText(d), counterpart(d)].filter(Boolean).join(' · ')}
                      </div>
                      <div className="mt-0.5 flex items-center justify-between">
                        <DocumentStatusBadge status={d.status} />
                        <span className="text-xs text-zinc-500">{t('inventory.documents.lineCount')}: {d.lineCount}</span>
                      </div>
                    </Link>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>

        {documents.data && documents.data.totalCount > 0 && (
          <Pagination
            page={filters.page}
            pageSize={filters.size}
            total={documents.data.totalCount}
            onPageChange={(page) => setFilters({ page })}
            onPageSizeChange={(size) => setFilters({ size })}
          />
        )}
      </div>

      {importOpen && <OpeningBalanceImportDialog onClose={() => setImportOpen(false)} />}
    </>
  )
}
