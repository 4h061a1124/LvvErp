import clsx from 'clsx'
import { X } from 'lucide-react'
import { useEffect, useRef, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'

interface DialogProps {
  open: boolean
  onClose: () => void
  title: string
  children: ReactNode
  footer?: ReactNode
  width?: 'sm' | 'md' | 'lg'
}

const widths = { sm: 'sm:max-w-sm', md: 'sm:max-w-lg', lg: 'sm:max-w-2xl' }

// Native <dialog>: focus trap, Esc and top-layer stacking come for free.
export function Dialog({ open, onClose, title, children, footer, width = 'md' }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null)
  const { t } = useTranslation()

  useEffect(() => {
    const dialog = ref.current
    if (!dialog) return
    if (open && !dialog.open) dialog.showModal()
    if (!open && dialog.open) dialog.close()
  }, [open])

  return (
    <dialog
      ref={ref}
      onCancel={(e) => {
        e.preventDefault()
        onClose()
      }}
      className={clsx(
        'm-0 mt-auto w-full max-w-none rounded-t-md bg-white p-0 text-zinc-900 shadow-xl',
        'sm:m-auto sm:rounded-md',
        widths[width],
      )}
    >
      {open && (
        <div className="flex max-h-[90dvh] flex-col">
          <header className="flex h-11 shrink-0 items-center justify-between border-b border-zinc-200 pr-2 pl-4">
            <h2 className="text-[15px] font-semibold">{title}</h2>
            <button
              type="button"
              onClick={onClose}
              className="rounded-[3px] p-1.5 text-zinc-500 hover:bg-zinc-100 hover:text-zinc-800"
              aria-label={t('common.close')}
            >
              <X size={16} />
            </button>
          </header>
          <div className="overflow-y-auto px-4 py-4">{children}</div>
          {footer && (
            <footer className="flex shrink-0 justify-end gap-2 border-t border-zinc-200 bg-zinc-50 px-4 py-2.5">
              {footer}
            </footer>
          )}
        </div>
      )}
    </dialog>
  )
}
