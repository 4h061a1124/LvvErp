import { useTranslation } from 'react-i18next'
import { AsyncCombobox } from '@/components/ui/AsyncCombobox'
import type { Employee } from '@/features/employees/types'
import { api } from '@/lib/api'
import type { PagedResult } from '@/lib/types'
import type { Material } from '../types'
import type { MaterialRef } from './documentModel'

async function searchMaterials(term: string): Promise<MaterialRef[]> {
  const { data } = await api.get<PagedResult<Material>>('/materials', {
    params: { search: term || undefined, isActive: true, pageSize: 10, sort: 'code' },
  })
  return data.items.map((m) => ({ id: m.id, code: m.code, name: m.name, unitName: m.unitName, trackLots: m.trackLots }))
}

interface MaterialPickerProps {
  id?: string
  value: MaterialRef | null
  onChange: (material: MaterialRef) => void
  invalid?: boolean
  clearOnSelect?: boolean
  placeholder?: string
  autoFocus?: boolean
}

/** Active materials by code or name. Scanning a label (code + Enter) picks the exact code. */
export function MaterialPicker({ id, value, onChange, invalid, clearOnSelect, placeholder, autoFocus }: MaterialPickerProps) {
  const { t } = useTranslation()
  return (
    <AsyncCombobox<MaterialRef>
      id={id}
      value={value}
      onChange={(m) => m && onChange(m)}
      queryKey="material-picker"
      search={searchMaterials}
      getKey={(m) => m.id}
      getLabel={(m) => `${m.code} – ${m.name}`}
      renderOption={(m) => (
        <span className="flex items-baseline gap-2">
          <span className="tabular shrink-0 text-[13px] text-zinc-500">{m.code}</span>
          <span className="truncate">{m.name}</span>
          <span className="ml-auto shrink-0 text-xs text-zinc-400">{m.unitName}</span>
        </span>
      )}
      exactMatch={(items, term) => items.find((m) => m.code === term.trim().toUpperCase())}
      clearOnSelect={clearOnSelect}
      placeholder={placeholder ?? t('inventory.documents.materialSearch')}
      noMatchesText={t('inventory.documents.noMatches')}
      invalid={invalid}
      ariaLabel={id ? undefined : t('inventory.documents.material')}
      autoFocus={autoFocus}
    />
  )
}

export interface EmployeeRef {
  id: string
  code: string
  fullName: string
  departmentId: string
}

async function searchEmployees(term: string): Promise<EmployeeRef[]> {
  const { data } = await api.get<PagedResult<Employee>>('/employees', {
    params: { search: term || undefined, pageSize: 10, sort: 'fullName' },
  })
  return data.items.map((e) => ({ id: e.id, code: e.code, fullName: e.fullName, departmentId: e.departmentId }))
}

export function EmployeePicker({
  id,
  value,
  onChange,
}: {
  id?: string
  value: EmployeeRef | null
  onChange: (employee: EmployeeRef | null) => void
}) {
  const { t } = useTranslation()
  return (
    <AsyncCombobox<EmployeeRef>
      id={id}
      value={value}
      onChange={onChange}
      queryKey="employee-picker"
      search={searchEmployees}
      getKey={(e) => e.id}
      // A saved document only carries the recipient's name, not the code.
      getLabel={(e) => (e.code ? `${e.fullName} (${e.code})` : e.fullName)}
      renderOption={(e) => (
        <span className="flex items-baseline gap-2">
          <span className="truncate">{e.fullName}</span>
          <span className="tabular ml-auto shrink-0 text-xs text-zinc-500">{e.code}</span>
        </span>
      )}
      exactMatch={(items, term) => items.find((e) => e.code === term.trim().toUpperCase())}
      placeholder={t('inventory.documents.employeeSearch')}
      noMatchesText={t('inventory.documents.noMatches')}
    />
  )
}
