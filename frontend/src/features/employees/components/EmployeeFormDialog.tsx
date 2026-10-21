import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Field, Input, Select } from '@/components/ui/Field'
import { useToast } from '@/components/ui/toast-context'
import { getErrorMessage, getFieldErrors, getStatus } from '@/lib/errors'
import { deletePhoto, employeeKeys, uploadPhoto, useDepartments, usePositions, useSaveEmployee } from '../api'
import { checkPhoto, employeeSchema, PHOTO_TYPES, toEmployeeInput, type EmployeeFormValues } from '../schema'
import { EMPLOYEE_STATUSES, type Employee, type Lookup } from '../types'
import { EmployeeAvatar } from './EmployeeAvatar'

interface EmployeeFormDialogProps {
  /** null = create */
  employee: Employee | null
  onClose: () => void
  onDelete?: (employee: Employee) => void
}

const EMPTY: EmployeeFormValues = {
  code: '',
  fullName: '',
  departmentId: '',
  positionId: '',
  email: '',
  phone: '',
  status: 'Active',
}

function toFormValues(employee: Employee | null): EmployeeFormValues {
  if (!employee) return EMPTY
  return {
    code: employee.code,
    fullName: employee.fullName,
    departmentId: employee.departmentId,
    positionId: employee.positionId,
    email: employee.email ?? '',
    phone: employee.phone ?? '',
    status: employee.status,
  }
}

/** Mount only while open (the parent renders it conditionally), so every opening starts clean. */
export function EmployeeFormDialog(props: EmployeeFormDialogProps) {
  const { t } = useTranslation()
  const departments = useDepartments()
  const positions = usePositions()

  // The selects need their options before the form mounts, or the employee's current
  // department/position can't be pre-selected.
  if (!departments.data || !positions.data) {
    const failed = departments.isError || positions.isError
    return (
      <Dialog open onClose={props.onClose} title={t(props.employee ? 'employees.form.editTitle' : 'employees.form.createTitle')}>
        <p className={failed ? 'py-6 text-center text-sm text-red-700' : 'py-6 text-center text-sm text-zinc-500'}>
          {failed ? t('errors.server') : t('common.loading')}
        </p>
      </Dialog>
    )
  }

  return <EmployeeForm {...props} departments={departments.data} positions={positions.data} />
}

function EmployeeForm({
  employee,
  onClose,
  onDelete,
  departments,
  positions,
}: EmployeeFormDialogProps & { departments: Lookup[]; positions: Lookup[] }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const save = useSaveEmployee()

  const fileInput = useRef<HTMLInputElement>(null)
  const [photoFile, setPhotoFile] = useState<File | null>(null)
  const [removePhoto, setRemovePhoto] = useState(false)
  const [photoError, setPhotoError] = useState<string | null>(null)

  const photoPreview = useMemo(() => (photoFile ? URL.createObjectURL(photoFile) : null), [photoFile])
  useEffect(() => () => void (photoPreview && URL.revokeObjectURL(photoPreview)), [photoPreview])

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<EmployeeFormValues>({ resolver: zodResolver(employeeSchema), defaultValues: toFormValues(employee) })

  function choosePhoto(file: File | undefined) {
    if (!file) return
    const problem = checkPhoto(file)
    setPhotoError(problem ? t(problem) : null)
    if (problem) return
    setPhotoFile(file)
    setRemovePhoto(false)
  }

  const onSubmit = handleSubmit(async (values) => {
    try {
      const saved = await save.mutateAsync({ id: employee?.id, input: toEmployeeInput(values) })

      try {
        if (photoFile) await uploadPhoto(saved.id, photoFile)
        else if (removePhoto && employee?.photoUrl) await deletePhoto(saved.id)
      } catch {
        toast.error(t('employees.form.photoFailed'))
      }
      if (photoFile || removePhoto) await queryClient.invalidateQueries({ queryKey: employeeKeys.all })

      toast.success(t(employee ? 'employees.form.updated' : 'employees.form.created', { code: saved.code }))
      onClose()
    } catch (err) {
      if (getStatus(err) === 409) {
        setError('code', { message: 'employees.form.codeExists' })
        return
      }
      const known = Object.entries(getFieldErrors(err)).filter(([key]) => key in EMPTY)
      if (known.length) {
        for (const [key, msg] of known) setError(key as keyof EmployeeFormValues, { message: msg })
      } else {
        toast.error(getErrorMessage(err, t))
      }
    }
  })

  // Client messages are i18n keys; server messages are shown as-is (t returns unknown keys unchanged).
  const message = (key?: string) => (key ? t(key as never) : undefined)

  const currentPhoto = photoPreview ?? (removePhoto ? null : (employee?.photoUrl ?? null))

  return (
    <Dialog
      open
      onClose={onClose}
      title={employee ? t('employees.form.editTitle') : t('employees.form.createTitle')}
      width="lg"
      footer={
        <>
          {employee && onDelete && (
            <Button variant="dangerGhost" className="mr-auto" onClick={() => onDelete(employee)}>
              {t('common.delete')}
            </Button>
          )}
          <Button onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" type="submit" form="employee-form" disabled={isSubmitting}>
            {isSubmitting ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id="employee-form" onSubmit={onSubmit} noValidate>
        <div className="mb-4 flex items-center gap-3 border-b border-zinc-100 pb-4">
          <EmployeeAvatar name={employee?.fullName ?? ''} photoUrl={currentPhoto} size={72} />
          <div>
            <div className="flex flex-wrap gap-2">
              <Button size="sm" onClick={() => fileInput.current?.click()}>
                {currentPhoto ? t('employees.form.changePhoto') : t('employees.form.choosePhoto')}
              </Button>
              {currentPhoto && (
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={() => {
                    setPhotoFile(null)
                    setRemovePhoto(true)
                  }}
                >
                  {t('employees.form.removePhoto')}
                </Button>
              )}
            </div>
            <p className={photoError ? 'mt-1.5 text-xs text-red-600' : 'mt-1.5 text-xs text-zinc-500'}>
              {photoError ?? t('employees.form.photoHint')}
            </p>
            <input
              ref={fileInput}
              type="file"
              accept={PHOTO_TYPES.join(',')}
              className="hidden"
              onChange={(e) => {
                choosePhoto(e.target.files?.[0])
                e.target.value = ''
              }}
            />
          </div>
        </div>

        <div className="grid gap-x-4 gap-y-3 sm:grid-cols-2">
          <Field label={t('employees.columns.code')} htmlFor="code" required error={message(errors.code?.message)}>
            <Input id="code" autoComplete="off" className="uppercase" invalid={!!errors.code} {...register('code')} />
          </Field>
          <Field label={t('employees.columns.fullName')} htmlFor="fullName" required error={message(errors.fullName?.message)}>
            <Input id="fullName" autoComplete="off" invalid={!!errors.fullName} {...register('fullName')} />
          </Field>
          <Field
            label={t('employees.columns.department')}
            htmlFor="departmentId"
            required
            error={message(errors.departmentId?.message)}
          >
            <Select id="departmentId" invalid={!!errors.departmentId} {...register('departmentId')}>
              <option value="">{t('employees.form.choose')}</option>
              {departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field
            label={t('employees.columns.position')}
            htmlFor="positionId"
            required
            error={message(errors.positionId?.message)}
          >
            <Select id="positionId" invalid={!!errors.positionId} {...register('positionId')}>
              <option value="">{t('employees.form.choose')}</option>
              {positions.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label={t('employees.columns.email')} htmlFor="email" error={message(errors.email?.message)}>
            <Input id="email" type="email" autoComplete="off" invalid={!!errors.email} {...register('email')} />
          </Field>
          <Field label={t('employees.columns.phone')} htmlFor="phone" error={message(errors.phone?.message)}>
            <Input id="phone" type="tel" autoComplete="off" invalid={!!errors.phone} {...register('phone')} />
          </Field>
          <Field label={t('employees.columns.status')} htmlFor="status" required>
            <Select id="status" {...register('status')}>
              {EMPLOYEE_STATUSES.map((s) => (
                <option key={s} value={s}>
                  {t(`employees.status.${s}`)}
                </option>
              ))}
            </Select>
          </Field>
        </div>
      </form>
    </Dialog>
  )
}
