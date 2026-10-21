import type { ReactNode } from 'react'

export function PageHeader({ title, meta, actions }: { title: string; meta?: ReactNode; actions?: ReactNode }) {
  return (
    <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h1 className="text-lg font-semibold text-zinc-900">{title}</h1>
        {meta && <div className="mt-0.5 text-[13px] text-zinc-500">{meta}</div>}
      </div>
      {actions && <div className="flex gap-2">{actions}</div>}
    </div>
  )
}
