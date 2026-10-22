import { useTranslation } from 'react-i18next'
import { AsyncCombobox } from '@/components/ui/AsyncCombobox'
import { api } from '@/lib/api'
import type { PagedResult } from '@/lib/types'
import type { SupplierRef } from '../orders/orderModel'
import type { Supplier } from '../types'

async function searchSuppliers(term: string): Promise<SupplierRef[]> {
  const { data } = await api.get<PagedResult<Supplier>>('/suppliers', {
    params: { search: term || undefined, isActive: true, pageSize: 10, sort: 'name' },
  })
  return data.items.map((s) => ({ id: s.id, code: s.code, name: s.name, paymentTermDays: s.paymentTermDays }))
}

/** Active suppliers by code, name or tax code. */
export function SupplierPicker({
  id,
  value,
  onChange,
  invalid,
}: {
  id?: string
  value: SupplierRef | null
  onChange: (supplier: SupplierRef | null) => void
  invalid?: boolean
}) {
  const { t } = useTranslation()
  return (
    <AsyncCombobox<SupplierRef>
      id={id}
      value={value}
      onChange={onChange}
      queryKey="supplier-picker"
      search={searchSuppliers}
      getKey={(s) => s.id}
      getLabel={(s) => `${s.code} – ${s.name}`}
      renderOption={(s) => (
        <span className="flex items-baseline gap-2">
          <span className="tabular shrink-0 text-[13px] text-zinc-500">{s.code}</span>
          <span className="truncate">{s.name}</span>
        </span>
      )}
      exactMatch={(items, term) => items.find((s) => s.code === term.trim().toUpperCase())}
      placeholder={t('purchasing.orders.supplierSearch')}
      noMatchesText={t('inventory.documents.noMatches')}
      invalid={invalid}
    />
  )
}
