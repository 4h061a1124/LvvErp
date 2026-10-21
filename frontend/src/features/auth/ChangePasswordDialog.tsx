import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Field, Input } from '@/components/ui/Field'
import { useToast } from '@/components/ui/toast-context'
import { api } from '@/lib/api'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage, getFieldErrors } from '@/lib/errors'

const schema = z
  .object({
    currentPassword: z.string().min(1, 'validation.required'),
    newPassword: z
      .string()
      .min(8, 'auth.passwordRule')
      .regex(/[A-Za-z]/, 'auth.passwordRule')
      .regex(/[0-9]/, 'auth.passwordRule'),
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, { path: ['confirmPassword'], message: 'auth.passwordMismatch' })
  .refine((v) => v.newPassword !== v.currentPassword, { path: ['newPassword'], message: 'auth.passwordSame' })

type FormValues = z.infer<typeof schema>

/** Mount only while open, so every opening starts clean. */
export function ChangePasswordDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const { logout } = useAuth()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  })

  const onSubmit = handleSubmit(async ({ currentPassword, newPassword }) => {
    try {
      await api.post('/auth/change-password', { currentPassword, newPassword })
      toast.success(t('auth.passwordChanged'))
      onClose()
      // All existing tokens are revoked server-side, including this one.
      logout()
    } catch (err) {
      if (getFieldErrors(err).currentPassword) {
        setError('currentPassword', { message: 'auth.wrongCurrent' })
      } else {
        toast.error(getErrorMessage(err, t))
      }
    }
  })

  // Messages in the schema are i18n keys.
  const message = (key?: string) => (key ? t(key as never) : undefined)

  return (
    <Dialog
      open
      onClose={onClose}
      title={t('auth.changePassword')}
      width="sm"
      footer={
        <>
          <Button onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" type="submit" form="change-password-form" disabled={isSubmitting}>
            {isSubmitting ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id="change-password-form" onSubmit={onSubmit} className="space-y-3" noValidate>
        <Field label={t('auth.currentPassword')} htmlFor="currentPassword" error={message(errors.currentPassword?.message)}>
          <Input
            id="currentPassword"
            type="password"
            autoComplete="current-password"
            invalid={!!errors.currentPassword}
            {...register('currentPassword')}
          />
        </Field>
        <Field
          label={t('auth.newPassword')}
          htmlFor="newPassword"
          error={message(errors.newPassword?.message)}
          hint={t('auth.passwordRule')}
        >
          <Input
            id="newPassword"
            type="password"
            autoComplete="new-password"
            invalid={!!errors.newPassword}
            {...register('newPassword')}
          />
        </Field>
        <Field label={t('auth.confirmPassword')} htmlFor="confirmPassword" error={message(errors.confirmPassword?.message)}>
          <Input
            id="confirmPassword"
            type="password"
            autoComplete="new-password"
            invalid={!!errors.confirmPassword}
            {...register('confirmPassword')}
          />
        </Field>
      </form>
    </Dialog>
  )
}
