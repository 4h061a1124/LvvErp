import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Field } from '@/components/ui/Field'

interface ReasonDialogProps {
  title: string
  children?: ReactNode
  confirmLabel: string
  busy?: boolean
  /** Reject and cancel are destructive; close is not. */
  danger?: boolean
  onConfirm: (reason: string) => void
  onClose: () => void
}

/** Asks for the reason that reject, cancel and close require. Mount only while open. */
export function ReasonDialog({ title, children, confirmLabel, busy, danger = true, onConfirm, onClose }: ReasonDialogProps) {
  const { t } = useTranslation()
  const [reason, setReason] = useState('')
  const [error, setError] = useState(false)

  function confirm() {
    if (!reason.trim()) return setError(true)
    onConfirm(reason.trim())
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title={title}
      width="sm"
      footer={
        <>
          <Button onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </Button>
          <Button variant={danger ? 'danger' : 'primary'} onClick={confirm} disabled={busy}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <div className="space-y-3 text-sm text-zinc-700">
        {children}
        <Field label={t('purchasing.requests.reason')} htmlFor="reason" required error={error ? t('purchasing.requests.reasonRequired') : undefined}>
          <textarea
            id="reason"
            rows={3}
            maxLength={500}
            value={reason}
            autoFocus
            onChange={(e) => {
              setReason(e.target.value)
              setError(false)
            }}
            className="block w-full rounded-[3px] border border-zinc-300 px-2.5 py-1.5 text-sm focus:border-brand-500 focus:ring-2 focus:ring-brand-500/15 focus:outline-none"
          />
        </Field>
      </div>
    </Dialog>
  )
}
