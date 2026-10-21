import clsx from 'clsx'
import { useQueryClient } from '@tanstack/react-query'
import { Search } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ImportDialog } from '@/components/ImportDialog'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { Pagination } from '@/components/ui/Pagination'
import { useToast } from '@/components/ui/toast-context'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage } from '@/lib/errors'
import { useDebouncedValue } from '@/lib/hooks'
import { Permissions } from '@/lib/permissions'
import { employeeKeys, useDeleteEmployee, useDepartments, useEmployees } from './api'
import { EmployeeFormDialog } from './components/EmployeeFormDialog'
import { EmployeeList } from './components/EmployeeList'
import { EmployeeTable } from './components/EmployeeTable'
import { importColumnLabel } from './importErrors'
import { EMPLOYEE_STATUSES, type Employee, type EmployeeStatus } from './types'
import { useEmployeeFilters } from './useEmployeeFilters'

export function EmployeesPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const { can } = useAuth()
  const canManage = can(Permissions.EmployeesManage)

  const { filters, setFilters, queryParams, hasFilters } = useEmployeeFilters()
  const employees = useEmployees(queryParams)
  const departments = useDepartments()
  const deleteEmployee = useDeleteEmployee()
  const queryClient = useQueryClient()

  // Typing updates the URL after a pause; only react when the debounced text itself changes,
  // otherwise a stale value would undo "Clear" before the debounce catches up.
  const [searchText, setSearchText] = useState(filters.search)
  const debouncedSearch = useDebouncedValue(searchText.trim())
  const appliedSearch = useRef(debouncedSearch)
  useEffect(() => {
    if (debouncedSearch === appliedSearch.current) return
    appliedSearch.current = debouncedSearch
    setFilters({ search: debouncedSearch })
  }, [debouncedSearch, setFilters])

  const [editing, setEditing] = useState<Employee | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [importOpen, setImportOpen] = useState(false)
  const [deleting, setDeleting] = useState<Employee | null>(null)

  const openCreate = () => {
    setEditing(null)
    setFormOpen(true)
  }
  const openEdit = (employee: Employee) => {
    setEditing(employee)
    setFormOpen(true)
  }

  async function confirmDelete() {
    if (!deleting) return
    try {
      await deleteEmployee.mutateAsync(deleting.id)
      toast.success(t('employees.delete.done', { code: deleting.code }))
      setDeleting(null)
      setFormOpen(false)
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
  }

  const data = employees.data
  const rows = data?.items ?? []

  return (
    <>
      <PageHeader
        title={t('employees.title')}
        meta={data && t('employees.count', { count: data.totalCount })}
        actions={
          canManage && (
            <>
              <Button onClick={() => setImportOpen(true)}>{t('employees.import')}</Button>
              <Button variant="primary" onClick={openCreate}>
                {t('employees.add')}
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
              placeholder={t('employees.searchPlaceholder')}
              aria-label={t('employees.searchPlaceholder')}
              className="pl-8"
            />
          </div>
          <Select
            value={filters.departmentId}
            onChange={(e) => setFilters({ departmentId: e.target.value })}
            aria-label={t('employees.columns.department')}
            className="w-[calc(50%-4px)] sm:w-48"
          >
            <option value="">{t('employees.allDepartments')}</option>
            {departments.data?.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
          <Select
            value={filters.status}
            onChange={(e) => setFilters({ status: e.target.value as EmployeeStatus | '' })}
            aria-label={t('employees.columns.status')}
            className="w-[calc(50%-4px)] sm:w-44"
          >
            <option value="">{t('employees.allStatuses')}</option>
            {EMPLOYEE_STATUSES.map((s) => (
              <option key={s} value={s}>
                {t(`employees.status.${s}`)}
              </option>
            ))}
          </Select>
          {hasFilters && (
            <button
              type="button"
              onClick={() => {
                setSearchText('')
                setFilters({ search: '', departmentId: '', status: '' })
              }}
              className="px-1 text-[13px] text-brand-700 hover:underline"
            >
              {t('common.clearFilters')}
            </button>
          )}
        </div>

        <div className={clsx('transition-opacity', employees.isPlaceholderData && 'opacity-60')}>
          {employees.isPending ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
          ) : employees.isError ? (
            <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(employees.error, t)}</p>
          ) : rows.length === 0 ? (
            <p className="px-3 py-10 text-center text-sm text-zinc-500">
              {hasFilters ? t('employees.emptyFiltered') : t('employees.empty')}
            </p>
          ) : (
            <>
              <div className="hidden overflow-x-auto md:block">
                <EmployeeTable
                  employees={rows}
                  sort={filters.sort}
                  onSort={(sort) => setFilters({ sort })}
                  canManage={canManage}
                  onEdit={openEdit}
                  onDelete={setDeleting}
                />
              </div>
              <div className="md:hidden">
                <EmployeeList employees={rows} onSelect={canManage ? openEdit : undefined} />
              </div>
            </>
          )}
        </div>

        {data && data.totalCount > 0 && (
          <Pagination
            page={filters.page}
            pageSize={filters.pageSize}
            total={data.totalCount}
            onPageChange={(page) => setFilters({ page })}
            onPageSizeChange={(pageSize) => setFilters({ pageSize })}
          />
        )}
      </div>

      {canManage && (
        <>
          {formOpen && (
            <EmployeeFormDialog employee={editing} onClose={() => setFormOpen(false)} onDelete={setDeleting} />
          )}
          {importOpen && (
            <ImportDialog
              title={t('employees.importDialog.title')}
              step1={t('employees.importDialog.step1')}
              templateUrl="/employees/import/template"
              templateFileName="mau-import-nhan-vien.xlsx"
              importUrl="/employees/import"
              updateExistingLabel={t('employees.importDialog.updateExisting')}
              updateExistingHint={t('employees.importDialog.updateExistingHint')}
              columnLabel={(column) => importColumnLabel(column, t)}
              onImported={() => queryClient.invalidateQueries({ queryKey: employeeKeys.all })}
              onClose={() => setImportOpen(false)}
            />
          )}
          <ConfirmDialog
            open={!!deleting}
            title={t('employees.delete.title')}
            confirmLabel={t('common.delete')}
            busy={deleteEmployee.isPending}
            onConfirm={confirmDelete}
            onClose={() => setDeleting(null)}
          >
            <p>{deleting && t('employees.delete.message', { code: deleting.code, name: deleting.fullName })}</p>
            <p className="text-[13px] text-zinc-500">{t('employees.delete.note')}</p>
          </ConfirmDialog>
        </>
      )}
    </>
  )
}
