import clsx from 'clsx'
import { useCallback, useState, type ReactNode } from 'react'
import { ToastContext, type ToastApi } from './toast-context'

type ToastKind = 'success' | 'error'

interface ToastItem {
  id: number
  kind: ToastKind
  message: string
}

let nextId = 1

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<ToastItem[]>([])

  const push = useCallback((kind: ToastKind, message: string) => {
    const id = nextId++
    setItems((current) => [...current.slice(-2), { id, kind, message }])
    setTimeout(() => setItems((current) => current.filter((t) => t.id !== id)), kind === 'error' ? 6000 : 3500)
  }, [])

  const [api] = useState<ToastApi>(() => ({
    success: (message) => push('success', message),
    error: (message) => push('error', message),
  }))

  return (
    <ToastContext.Provider value={api}>
      {children}
      <div
        className="pointer-events-none fixed inset-x-0 bottom-0 z-50 flex flex-col items-center gap-2 p-3 sm:items-end"
        aria-live="polite"
      >
        {items.map((item) => (
          <div
            key={item.id}
            role={item.kind === 'error' ? 'alert' : 'status'}
            className={clsx(
              'pointer-events-auto w-full max-w-sm rounded-[3px] border border-l-4 bg-white px-3 py-2.5 text-sm shadow-md',
              item.kind === 'success' ? 'border-zinc-200 border-l-emerald-600' : 'border-zinc-200 border-l-red-600',
            )}
          >
            {item.message}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}
