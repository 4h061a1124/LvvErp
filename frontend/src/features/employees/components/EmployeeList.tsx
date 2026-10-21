import { Phone } from 'lucide-react'
import type { Employee } from '../types'
import { EmployeeAvatar } from './EmployeeAvatar'
import { StatusBadge } from './StatusBadge'

/** Compact list used instead of the table on narrow screens. */
export function EmployeeList({ employees, onSelect }: { employees: Employee[]; onSelect?: (employee: Employee) => void }) {
  return (
    <ul className="divide-y divide-zinc-100">
      {employees.map((e) => {
        const body = (
          <>
            <EmployeeAvatar name={e.fullName} photoUrl={e.photoUrl} size={40} />
            <div className="min-w-0 flex-1">
              <div className="flex items-baseline justify-between gap-2">
                <span className="truncate font-medium text-zinc-900">{e.fullName}</span>
                <span className="tabular shrink-0 text-xs text-zinc-500">{e.code}</span>
              </div>
              <div className="truncate text-[13px] text-zinc-600">
                {e.positionName} · {e.departmentName}
              </div>
              <div className="mt-0.5 flex items-center justify-between gap-2">
                <StatusBadge status={e.status} />
                {e.phone && (
                  <a
                    href={`tel:${e.phone.replace(/[^\d+]/g, '')}`}
                    onClick={(ev) => ev.stopPropagation()}
                    className="tabular inline-flex items-center gap-1 text-[13px] text-brand-700"
                  >
                    <Phone size={12} />
                    {e.phone}
                  </a>
                )}
              </div>
            </div>
          </>
        )

        return (
          <li key={e.id}>
            {onSelect ? (
              <div
                role="button"
                tabIndex={0}
                onClick={() => onSelect(e)}
                onKeyDown={(ev) => ev.key === 'Enter' && onSelect(e)}
                className="flex gap-3 px-3 py-2.5 active:bg-zinc-50"
              >
                {body}
              </div>
            ) : (
              <div className="flex gap-3 px-3 py-2.5">{body}</div>
            )}
          </li>
        )
      })}
    </ul>
  )
}
