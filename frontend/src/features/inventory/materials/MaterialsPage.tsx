import { useQueryClient } from '@tanstack/react-query'
import clsx from 'clsx'
import type { TFunction } from 'i18next'
import { ArrowDown, ArrowUp, Search } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { ImportDialog } from '@/components/ImportDialog'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { Pagination } from '@/components/ui/Pagination'
import { useToast } from '@/components/ui/toast-context'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage } from '@/lib/errors'
import { formatQuantity } from '@/lib/format'
import { useDebouncedValue } from '@/lib/hooks'
import { Permissions } from '@/lib/permissions'
import { useListParams } from '@/lib/useListParams'
import type { Messages } from '@/locales/vi'
import { inventoryKeys, useCatalog, useDeleteMaterial, useMaterials } from '../api'
import { ActiveStatus } from '../components/ActiveStatus'
import { MaterialThumb } from '../components/MaterialThumb'
import { labelItemFromMaterial } from '../labels/labels'
import type { Material } from '../types'
import { MaterialFormDialog } from './MaterialFormDialog'

const DEFAULTS = { q: '', group: '', status: 'active', page: 1, size: 20, sort: 'code' }
type ColumnKey = keyof Messages['inventory']['materials']['columns']

export function MaterialsPage() {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const canManage = useAuth().can(Permissions.InventoryManageCatalog)

  const [filters, setFilters] = useListParams(DEFAULTS)
  const materials = useMaterials({
    search: filters.q || undefined,
    groupId: filters.group || undefined,
    isActive: filters.status === 'all' ? undefined : filters.status === 'active',
    page: filters.page,
    pageSize: filters.size,
    sort: filters.sort,
  })
  const groups = useCatalog('material-groups')
  const deleteMaterial = useDeleteMaterial()

  // Same debounce pattern as the employee directory.
  const [searchText, setSearchText] = useState(filters.q)
  const debouncedSearch = useDebouncedValue(searchText.trim())
  const appliedSearch = useRef(debouncedSearch)
  useEffect(() => {
    if (debouncedSearch === appliedSearch.current) return
    appliedSearch.current = debouncedSearch
    setFilters({ q: debouncedSearch })
  }, [debouncedSearch, setFilters])

  // Selection for label printing survives paging and filtering.
  const [selected, setSelected] = useState<Map<string, Material>>(new Map())
  const [editing, setEditing] = useState<Material | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [importOpen, setImportOpen] = useState(false)
  const [deleting, setDeleting] = useState<Material | null>(null)

  const rows = materials.data?.items ?? []
  const hasFilters = !!(filters.q || filters.group || filters.status !== DEFAULTS.status)
  const allOnPageSelected = rows.length > 0 && rows.every((m) => selected.has(m.id))

  function toggle(material: Material) {
    setSelected((current) => {
      const next = new Map(current)
      if (next.has(material.id)) next.delete(material.id)
      else next.set(material.id, material)
      return next
    })
  }

  function togglePage() {
    setSelected((current) => {
      const next = new Map(current)
      for (const m of rows) {
        if (allOnPageSelected) next.delete(m.id)
        else next.set(m.id, m)
      }
      return next
    })
  }

  function printLabels() {
    navigate('/inventory/labels', { state: { items: [...selected.values()].map(labelItemFromMaterial) } })
  }

  const openEdit = (material: Material) => {
    setEditing(material)
    setFormOpen(true)
  }

  async function confirmDelete() {
    if (!deleting) return
    try {
      await deleteMaterial.mutateAsync(deleting.id)
      toast.success(t('inventory.materials.delete.done', { code: deleting.code }))
      setSelected((current) => {
        const next = new Map(current)
        next.delete(deleting.id)
        return next
      })
      setFormOpen(false)
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setDeleting(null)
  }

  const header = (column: ColumnKey, sortKey?: string, className?: string) => {
    const label = t(`inventory.materials.columns.${column}`)
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
        title={t('inventory.materials.title')}
        meta={materials.data && t('inventory.materials.count', { count: materials.data.totalCount })}
        actions={
          canManage && (
            <>
              <Button onClick={() => setImportOpen(true)}>{t('inventory.materials.import')}</Button>
              <Button
                variant="primary"
                onClick={() => {
                  setEditing(null)
                  setFormOpen(true)
                }}
              >
                {t('inventory.materials.add')}
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
              placeholder={t('inventory.materials.searchPlaceholder')}
              aria-label={t('inventory.materials.searchPlaceholder')}
              className="pl-8"
            />
          </div>
          <Select
            value={filters.group}
            onChange={(e) => setFilters({ group: e.target.value })}
            aria-label={t('inventory.materials.columns.group')}
            className="w-[calc(50%-4px)] sm:w-48"
          >
            <option value="">{t('inventory.materials.allGroups')}</option>
            {groups.data?.map((g) => (
              <option key={g.id} value={g.id}>
                {g.name}
              </option>
            ))}
          </Select>
          <Select
            value={filters.status}
            onChange={(e) => setFilters({ status: e.target.value })}
            aria-label={t('inventory.materials.columns.status')}
            className="w-[calc(50%-4px)] sm:w-40"
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
                setFilters({ q: '', group: '', status: DEFAULTS.status })
              }}
              className="px-1 text-[13px] text-brand-700 hover:underline"
            >
              {t('common.clearFilters')}
            </button>
          )}
          {selected.size > 0 && (
            <div className="ml-auto flex items-center gap-2">
              <button type="button" onClick={() => setSelected(new Map())} className="px-1 text-[13px] text-zinc-600 hover:underline">
                {t('inventory.materials.clearSelection')}
              </button>
              <Button size="sm" onClick={printLabels}>
                {t('inventory.materials.printSelected', { count: selected.size })}
              </Button>
            </div>
          )}
        </div>

        <div className={clsx('transition-opacity', materials.isPlaceholderData && 'opacity-60')}>
          {materials.isPending ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
          ) : materials.isError ? (
            <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(materials.error, t)}</p>
          ) : rows.length === 0 ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">
              {hasFilters ? t('inventory.materials.emptyFiltered') : t('inventory.materials.empty')}
            </p>
          ) : (
            <>
              <div className="hidden overflow-x-auto md:block">
                <table className="w-full border-collapse text-left">
                  <thead>
                    <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                      <th className="w-9 py-2 pl-3">
                        <input
                          type="checkbox"
                          checked={allOnPageSelected}
                          onChange={togglePage}
                          aria-label={t('inventory.materials.selectAll')}
                          className="size-4 accent-brand-600"
                        />
                      </th>
                      <th className="w-11 py-2" />
                      {header('code', 'code', 'w-32')}
                      {header('name', 'name')}
                      {header('unit', undefined, 'w-20')}
                      {header('group', 'group')}
                      {header('minStock', undefined, 'w-28 text-right')}
                      {header('trackLots', undefined, 'w-24')}
                      {header('status', undefined, 'w-28')}
                      {canManage && <th className="w-24 px-3 py-2" />}
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((m) => (
                      <tr
                        key={m.id}
                        className={clsx(
                          'border-b border-zinc-100 last:border-b-0',
                          selected.has(m.id) ? 'bg-brand-50/60' : 'hover:bg-zinc-50/70',
                        )}
                      >
                        <td className="py-1.5 pl-3">
                          <input
                            type="checkbox"
                            checked={selected.has(m.id)}
                            onChange={() => toggle(m)}
                            aria-label={t('inventory.materials.select', { code: m.code })}
                            className="size-4 accent-brand-600"
                          />
                        </td>
                        <td className="py-1.5">
                          <MaterialThumb photoUrl={m.photoUrl} size={32} />
                        </td>
                        <td className="tabular px-3 py-1.5 text-[13px] text-zinc-600">{m.code}</td>
                        <td className="px-3 py-1.5">
                          <div className={clsx('font-medium', m.isActive ? 'text-zinc-900' : 'text-zinc-500')}>{m.name}</div>
                          {m.specification && <div className="text-xs text-zinc-500">{m.specification}</div>}
                        </td>
                        <td className="px-3 py-1.5 text-zinc-700">{m.unitName}</td>
                        <td className="px-3 py-1.5 text-zinc-700">{m.groupName}</td>
                        <td className="tabular px-3 py-1.5 text-right text-zinc-700">
                          {m.minStock > 0 ? formatQuantity(m.minStock, i18n.language) : <span className="text-zinc-400">—</span>}
                        </td>
                        <td className="px-3 py-1.5 text-[13px] text-zinc-700">{m.trackLots ? t('inventory.materials.lotTracked') : ''}</td>
                        <td className="px-3 py-1.5">
                          <ActiveStatus active={m.isActive} />
                        </td>
                        {canManage && (
                          <td className="px-3 py-1.5 text-right text-[13px] whitespace-nowrap">
                            <button type="button" onClick={() => openEdit(m)} className="px-1 text-brand-700 hover:underline">
                              {t('common.edit')}
                            </button>
                            <span className="mx-1 text-zinc-300">|</span>
                            <button type="button" onClick={() => setDeleting(m)} className="px-1 text-red-700 hover:underline">
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
                {rows.map((m) => (
                  <li key={m.id}>
                    <div
                      role={canManage ? 'button' : undefined}
                      tabIndex={canManage ? 0 : undefined}
                      onClick={canManage ? () => openEdit(m) : undefined}
                      onKeyDown={canManage ? (e) => e.key === 'Enter' && openEdit(m) : undefined}
                      className="flex gap-3 px-3 py-2.5 active:bg-zinc-50"
                    >
                      <MaterialThumb photoUrl={m.photoUrl} size={40} />
                      <div className="min-w-0 flex-1">
                        <div className="flex items-baseline justify-between gap-2">
                          <span className={clsx('truncate font-medium', m.isActive ? 'text-zinc-900' : 'text-zinc-500')}>{m.name}</span>
                          <span className="tabular shrink-0 text-xs text-zinc-500">{m.code}</span>
                        </div>
                        <div className="truncate text-[13px] text-zinc-600">
                          {[m.specification, m.unitName, m.groupName].filter(Boolean).join(' · ')}
                        </div>
                        <div className="mt-0.5 flex items-center gap-3 text-xs text-zinc-500">
                          {!m.isActive && <ActiveStatus active={false} />}
                          {m.trackLots && <span>{t('inventory.materials.lotTracked')}</span>}
                          {m.minStock > 0 && (
                            <span>
                              {t('inventory.materials.columns.minStock')}: {formatQuantity(m.minStock, i18n.language)}
                            </span>
                          )}
                        </div>
                      </div>
                    </div>
                  </li>
                ))}
              </ul>
            </>
          )}
        </div>

        {materials.data && materials.data.totalCount > 0 && (
          <Pagination
            page={filters.page}
            pageSize={filters.size}
            total={materials.data.totalCount}
            onPageChange={(page) => setFilters({ page })}
            onPageSizeChange={(size) => setFilters({ size })}
          />
        )}
      </div>

      {formOpen && <MaterialFormDialog material={editing} onClose={() => setFormOpen(false)} onDelete={setDeleting} />}
      {importOpen && (
        <ImportDialog
          title={t('inventory.materials.importDialog.title')}
          step1={t('inventory.materials.importDialog.step1')}
          templateUrl="/materials/import/template"
          templateFileName="mau-import-vat-tu.xlsx"
          importUrl="/materials/import"
          updateExistingLabel={t('inventory.materials.importDialog.updateExisting')}
          updateExistingHint={t('inventory.materials.importDialog.updateExistingHint')}
          columnLabel={(column) => materialColumnLabel(column, t)}
          onImported={() => queryClient.invalidateQueries({ queryKey: inventoryKeys.materials })}
          onClose={() => setImportOpen(false)}
        />
      )}
      <ConfirmDialog
        open={!!deleting}
        title={t('inventory.materials.delete.title')}
        confirmLabel={t('common.delete')}
        busy={deleteMaterial.isPending}
        onConfirm={confirmDelete}
        onClose={() => setDeleting(null)}
      >
        <p>{deleting && t('inventory.materials.delete.message', { code: deleting.code, name: deleting.name })}</p>
        <p className="text-[13px] text-zinc-500">{t('inventory.materials.delete.note')}</p>
      </ConfirmDialog>
    </>
  )
}

const importColumns: Record<string, ColumnKey> = {
  Code: 'code',
  Name: 'name',
  Specification: 'specification',
  Unit: 'unit',
  Group: 'group',
  MinStock: 'minStock',
  TrackLots: 'trackLots',
}

function materialColumnLabel(column: string, t: TFunction): string {
  const key = importColumns[column]
  return key ? t(`inventory.materials.columns.${key}`) : column
}
