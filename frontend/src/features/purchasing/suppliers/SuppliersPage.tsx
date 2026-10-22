import { useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import type { TFunction } from 'i18next'
import { ArrowDown, ArrowUp, Search } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ImportDialog } from '@/components/ImportDialog'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { Pagination } from '@/components/ui/Pagination'
import { useToast } from '@/components/ui/toast-context'
import { ActiveStatus } from '@/features/inventory/components/ActiveStatus'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage } from '@/lib/errors'
import { useDebouncedValue } from '@/lib/hooks'
import { Permissions } from '@/lib/permissions'
import { useListParams } from '@/lib/useListParams'
import type { Messages } from '@/locales/vi'
import { purchasingKeys, useDeleteSupplier, useSuppliers } from '../api'
import type { Supplier } from '../types'
import { SupplierFormDialog } from './SupplierFormDialog'

const DEFAULTS = { q: '', status: 'active', page: 1, size: 20, sort: 'code' }
type ColumnKey = keyof Messages['purchasing']['suppliers']['columns']

export function SuppliersPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const canManage = useAuth().can(Permissions.PurchasingManageSuppliers)

  const [filters, setFilters] = useListParams(DEFAULTS)
  const suppliers = useSuppliers({
    search: filters.q || undefined,
    isActive: filters.status === 'all' ? undefined : filters.status === 'active',
    page: filters.page,
    pageSize: filters.size,
    sort: filters.sort,
  })
  const deleteSupplier = useDeleteSupplier()

  const [searchText, setSearchText] = useState(filters.q)
  const debouncedSearch = useDebouncedValue(searchText.trim())
  const appliedSearch = useRef(debouncedSearch)
  useEffect(() => {
    if (debouncedSearch === appliedSearch.current) return
    appliedSearch.current = debouncedSearch
    setFilters({ q: debouncedSearch })
  }, [debouncedSearch, setFilters])

  const [editing, setEditing] = useState<Supplier | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [importOpen, setImportOpen] = useState(false)
  const [deleting, setDeleting] = useState<Supplier | null>(null)

  const rows = suppliers.data?.items ?? []
  const hasFilters = !!(filters.q || filters.status !== DEFAULTS.status)

  const openEdit = (supplier: Supplier) => {
    setEditing(supplier)
    setFormOpen(true)
  }

  async function confirmDelete() {
    if (!deleting) return
    try {
      await deleteSupplier.mutateAsync(deleting.id)
      toast.success(t('purchasing.suppliers.delete.done', { code: deleting.code }))
      setFormOpen(false)
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setDeleting(null)
  }

  const header = (column: ColumnKey, sortKey?: string, className?: string) => {
    const label = t(`purchasing.suppliers.columns.${column}`)
    if (!sortKey) return <th className={clsx('px-3 py-2 font-medium', className)}>{label}</th>
    const active = filters.sort === sortKey || filters.sort === `-${sortKey}`
    const descending = filters.sort === `-${sortKey}`
    return (
      <th className={clsx('px-3 py-2 font-medium', className)} aria-sort={active ? (descending ? 'descending' : 'ascending') : undefined}>
        <button
          type="button"
          onClick={() => setFilters({ sort: active && !descending ? `-${sortKey}` : sortKey })}
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
      <PageHeader
        title={t('purchasing.suppliers.title')}
        meta={suppliers.data && t('purchasing.suppliers.count', { count: suppliers.data.totalCount })}
        actions={
          canManage && (
            <>
              <Button onClick={() => setImportOpen(true)}>{t('purchasing.suppliers.import')}</Button>
              <Button
                variant="primary"
                onClick={() => {
                  setEditing(null)
                  setFormOpen(true)
                }}
              >
                {t('purchasing.suppliers.add')}
              </Button>
            </>
          )
        }
      />

      <div className="rounded-[4px] border border-zinc-200 bg-white">
        <div className="flex flex-wrap items-center gap-2 border-b border-zinc-200 p-2.5">
          <div className="relative w-full sm:w-72">
            <Search size={15} className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-zinc-400" />
            <Input
              type="search"
              value={searchText}
              onChange={(e) => setSearchText(e.target.value)}
              placeholder={t('purchasing.suppliers.searchPlaceholder')}
              aria-label={t('purchasing.suppliers.searchPlaceholder')}
              className="pl-8"
            />
          </div>
          <Select
            value={filters.status}
            onChange={(e) => setFilters({ status: e.target.value })}
            aria-label={t('purchasing.suppliers.columns.status')}
            className="w-full sm:w-40"
          >
            <option value="active">{t('common.active')}</option>
            <option value="inactive">{t('common.inactive')}</option>
            <option value="all">{t('common.allStatuses')}</option>
          </Select>
          {hasFilters && (
            <button
              type="button"
              onClick={() => {
                setSearchText('')
                setFilters({ q: '', status: DEFAULTS.status })
              }}
              className="px-1 text-[13px] text-brand-700 hover:underline"
            >
              {t('common.clearFilters')}
            </button>
          )}
        </div>

        <div className={clsx('transition-opacity', suppliers.isPlaceholderData && 'opacity-60')}>
          {suppliers.isPending ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
          ) : suppliers.isError ? (
            <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(suppliers.error, t)}</p>
          ) : rows.length === 0 ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">
              {hasFilters ? t('purchasing.suppliers.emptyFiltered') : t('purchasing.suppliers.empty')}
            </p>
          ) : (
            <>
              <div className="hidden overflow-x-auto md:block">
                <table className="w-full text-left">
                  <thead>
                    <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                      {header('code', 'code', 'w-28')}
                      {header('name', 'name')}
                      {header('taxCode', undefined, 'w-36')}
                      {header('contactName')}
                      {header('phone', undefined, 'w-32')}
                      {header('paymentTermDays', undefined, 'w-36 text-right')}
                      {header('status', undefined, 'w-28')}
                      {canManage && <th className="w-24 px-3 py-2" />}
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((s) => (
                      <tr key={s.id} className="border-b border-zinc-100 last:border-b-0 hover:bg-zinc-50/70">
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-600">{s.code}</td>
                        <td className="px-3 py-2">
                          <div className={clsx('font-medium', s.isActive ? 'text-zinc-900' : 'text-zinc-500')}>{s.name}</div>
                          {s.address && <div className="truncate text-xs text-zinc-500">{s.address}</div>}
                        </td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{s.taxCode}</td>
                        <td className="px-3 py-2 text-[13px] text-zinc-700">
                          {s.contactName}
                          {s.email && <div className="text-xs text-zinc-500">{s.email}</div>}
                        </td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{s.phone}</td>
                        <td className="tabular px-3 py-2 text-right text-[13px] text-zinc-700">
                          {t('purchasing.suppliers.days', { count: s.paymentTermDays })}
                        </td>
                        <td className="px-3 py-2">
                          <ActiveStatus active={s.isActive} />
                        </td>
                        {canManage && (
                          <td className="px-3 py-2 text-right text-[13px] whitespace-nowrap">
                            <button type="button" onClick={() => openEdit(s)} className="px-1 text-brand-700 hover:underline">
                              {t('common.edit')}
                            </button>
                            <span className="mx-1 text-zinc-300">|</span>
                            <button type="button" onClick={() => setDeleting(s)} className="px-1 text-red-700 hover:underline">
                              {t('common.delete')}
                            </button>
                          </td>
                        )}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <ul className="divide-y divide-zinc-100 md:hidden">
                {rows.map((s) => (
                  <li key={s.id}>
                    <div
                      role={canManage ? 'button' : undefined}
                      tabIndex={canManage ? 0 : undefined}
                      onClick={canManage ? () => openEdit(s) : undefined}
                      onKeyDown={canManage ? (e) => e.key === 'Enter' && openEdit(s) : undefined}
                      className="px-3 py-2.5 active:bg-zinc-50"
                    >
                      <div className="flex items-baseline justify-between gap-2">
                        <span className={clsx('truncate font-medium', s.isActive ? 'text-zinc-900' : 'text-zinc-500')}>{s.name}</span>
                        <span className="tabular shrink-0 text-xs text-zinc-500">{s.code}</span>
                      </div>
                      <div className="truncate text-[13px] text-zinc-600">
                        {[s.taxCode, s.contactName, s.phone].filter(Boolean).join(' · ')}
                      </div>
                      {!s.isActive && <ActiveStatus active={false} />}
                    </div>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>

        {suppliers.data && suppliers.data.totalCount > 0 && (
          <Pagination
            page={filters.page}
            pageSize={filters.size}
            total={suppliers.data.totalCount}
            onPageChange={(page) => setFilters({ page })}
            onPageSizeChange={(size) => setFilters({ size })}
          />
        )}
      </div>

      {formOpen && <SupplierFormDialog supplier={editing} onClose={() => setFormOpen(false)} onDelete={setDeleting} />}
      {importOpen && (
        <ImportDialog
          title={t('purchasing.suppliers.importDialog.title')}
          step1={t('purchasing.suppliers.importDialog.step1')}
          templateUrl="/suppliers/import/template"
          templateFileName="mau-import-nha-cung-cap.xlsx"
          importUrl="/suppliers/import"
          updateExistingLabel={t('purchasing.suppliers.importDialog.updateExisting')}
          updateExistingHint={t('purchasing.suppliers.importDialog.updateExistingHint')}
          columnLabel={(column) => supplierColumnLabel(column, t)}
          onImported={() => queryClient.invalidateQueries({ queryKey: purchasingKeys.suppliers })}
          onClose={() => setImportOpen(false)}
        />
      )}
      <ConfirmDialog
        open={!!deleting}
        title={t('purchasing.suppliers.delete.title')}
        confirmLabel={t('common.delete')}
        busy={deleteSupplier.isPending}
        onConfirm={confirmDelete}
        onClose={() => setDeleting(null)}
      >
        <p>{deleting && t('purchasing.suppliers.delete.message', { code: deleting.code, name: deleting.name })}</p>
        <p className="text-[13px] text-zinc-500">{t('purchasing.suppliers.delete.note')}</p>
      </ConfirmDialog>
    </>
  )
}

// Import error columns come back as backend keys ("TaxCode"); the column keys here are their camelCase.
function supplierColumnLabel(column: string, t: TFunction): string {
  const key = (column.charAt(0).toLowerCase() + column.slice(1)) as ColumnKey
  return t(`purchasing.suppliers.columns.${key}`, { defaultValue: column })
}
