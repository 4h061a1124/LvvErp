import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Field, Input } from '@/components/ui/Field'
import { useToast } from '@/components/ui/toast-context'
import { EmployeePicker } from '@/features/inventory/documents/pickers'
import { getErrorCode, getErrorMessage, getFieldErrors } from '@/lib/errors'
import { ROLES } from '@/lib/permissions'
import { useSaveUser } from './api'
import { newUserValues, toCreateInput, toUpdateInput, userSchema, userToValues, type UserFormValues } from './schema'
import type { User } from './types'

/** Mount only while open. <paramref name="user"/> null = create. */
export function UserFormDialog({ user, onClose }: { user: User | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const save = useSaveUser()

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<UserFormValues>({
    resolver: zodResolver(userSchema),
    defaultValues: user ? userToValues(user) : newUserValues(),
  })

  const onSubmit = handleSubmit(async (values) => {
    try {
      const saved = user
        ? await save.mutateAsync({ id: user.id, input: toUpdateInput(values) })
        : await save.mutateAsync({ input: toCreateInput(values) })
      toast.success(t(user ? 'users.form.updated' : 'users.form.created', { userName: saved.userName }))
      onClose()
    } catch (err) {
      const code = getErrorCode(err)
      if (code === 'DuplicateUserName') return setError('userName', { message: 'errors.codes.DuplicateUserName' })
      if (code === 'EmployeeAlreadyLinked') return setError('employee', { message: 'errors.codes.EmployeeAlreadyLinked' })
      const fields = getFieldErrors(err)
      const map: Record<string, keyof UserFormValues> = {
        userName: 'userName',
        fullName: 'fullName',
        email: 'email',
        password: 'password',
        newPassword: 'password',
        roles: 'roles',
        employeeId: 'employee',
      }
      const known = Object.entries(fields).filter(([key]) => key in map)
      if (known.length) for (const [key, msg] of known) setError(map[key], { message: msg })
      else toast.error(getErrorMessage(err, t))
    }
  })

  const message = (key?: string) => (key ? t(key as never) : undefined)

  return (
    <Dialog
      open
      onClose={onClose}
      title={user ? t('users.form.editTitle', { userName: user.userName }) : t('users.form.createTitle')}
      width="lg"
      footer={
        <>
          <Button onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" type="submit" form="user-form" disabled={isSubmitting}>
            {isSubmitting ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id="user-form" onSubmit={onSubmit} noValidate className="grid gap-x-4 gap-y-3 sm:grid-cols-2">
        <Field
          label={t('users.columns.userName')}
          htmlFor="userName"
          required={!user}
          hint={user ? undefined : t('users.form.userNameHint')}
          error={message(errors.userName?.message)}
        >
          <Input id="userName" autoComplete="off" disabled={!!user} invalid={!!errors.userName} className="lowercase" {...register('userName')} />
        </Field>
        <Field label={t('users.columns.fullName')} htmlFor="fullName" required error={message(errors.fullName?.message)}>
          <Input id="fullName" autoComplete="off" invalid={!!errors.fullName} {...register('fullName')} />
        </Field>
        <Field label={t('users.form.email')} htmlFor="email" error={message(errors.email?.message)}>
          <Input id="email" type="email" autoComplete="off" invalid={!!errors.email} {...register('email')} />
        </Field>
        <Field
          label={user ? t('users.form.newPassword') : t('users.form.password')}
          htmlFor="password"
          required={!user}
          hint={user ? t('users.form.newPasswordHint') : t('auth.passwordRule')}
          error={message(errors.password?.message)}
        >
          <Input id="password" type="password" autoComplete="new-password" invalid={!!errors.password} {...register('password')} />
        </Field>
        <Field
          label={t('users.form.employee')}
          htmlFor="employee"
          className="sm:col-span-2"
          hint={t('users.form.employeeHint')}
          error={message(errors.employee?.message)}
        >
          <Controller
            control={control}
            name="employee"
            render={({ field }) => (
              <div className="flex items-center gap-2">
                <div className="flex-1">
                  <EmployeePicker id="employee" value={field.value} onChange={field.onChange} />
                </div>
                {field.value && (
                  <button type="button" onClick={() => field.onChange(null)} className="px-1 text-[13px] text-zinc-600 hover:underline">
                    {t('inventory.materials.clearSelection')}
                  </button>
                )}
              </div>
            )}
          />
        </Field>

        <fieldset className="sm:col-span-2">
          <legend className="mb-1 text-[13px] font-medium text-zinc-700">
            {t('users.form.roles')}
            <span className="ml-0.5 text-red-600">*</span>
          </legend>
          <Controller
            control={control}
            name="roles"
            render={({ field }) => (
              <div className="grid gap-x-4 gap-y-1.5 rounded-[3px] border border-zinc-200 p-2.5 sm:grid-cols-2">
                {ROLES.map((role) => (
                  <label key={role} className="flex cursor-pointer items-start gap-2 text-sm">
                    <input
                      type="checkbox"
                      checked={field.value.includes(role)}
                      onChange={(e) => field.onChange(e.target.checked ? [...field.value, role] : field.value.filter((r) => r !== role))}
                      className="mt-0.5 size-4 accent-brand-600"
                    />
                    <span>
                      <span className="text-zinc-800">{t(`roles.${role}`)}</span>
                      <span className="block text-xs text-zinc-500">{t(`users.roleHints.${role}`)}</span>
                    </span>
                  </label>
                ))}
              </div>
            )}
          />
          {errors.roles && <p className="mt-1 text-xs text-red-600">{message(errors.roles.message)}</p>}
        </fieldset>

        {user && (
          <div className="sm:col-span-2">
            <Controller
              control={control}
              name="isActive"
              render={({ field }) => (
                <label className="flex cursor-pointer items-start gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={field.value}
                    onChange={(e) => field.onChange(e.target.checked)}
                    className="mt-0.5 size-4 accent-brand-600"
                  />
                  <span>
                    <span className="text-zinc-800">{t('users.form.isActive')}</span>
                    <span className="block text-xs text-zinc-500">{t('users.form.isActiveHint')}</span>
                  </span>
                </label>
              )}
            />
          </div>
        )}
      </form>
    </Dialog>
  )
}
