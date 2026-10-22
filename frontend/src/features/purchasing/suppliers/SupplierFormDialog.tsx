import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Field, Input } from '@/components/ui/Field'
import { useToast } from '@/components/ui/toast-context'
import { ActiveCheckbox } from '@/features/inventory/components/ActiveStatus'
import { getErrorCode, getErrorMessage, getFieldErrors } from '@/lib/errors'
import { useSaveSupplier } from '../api'
import { supplierSchema, toSupplierInput, type SupplierFormValues } from '../schema'
import type { Supplier } from '../types'

const EMPTY: SupplierFormValues = {
  code: '',
  name: '',
  taxCode: '',
  address: '',
  contactName: '',
  phone: '',
  email: '',
  paymentTermDays: '0',
  paymentTerms: '',
  isActive: true,
}

interface SupplierFormDialogProps {
  /** null = create */
  supplier: Supplier | null
  onClose: () => void
  onDelete?: (supplier: Supplier) => void
}

/** Mount only while open. */
export function SupplierFormDialog({ supplier, onClose, onDelete }: SupplierFormDialogProps) {
  const { t } = useTranslation()
  const toast = useToast()
  const save = useSaveSupplier()

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<SupplierFormValues>({
    resolver: zodResolver(supplierSchema),
    defaultValues: supplier
      ? {
          code: supplier.code,
          name: supplier.name,
          taxCode: supplier.taxCode ?? '',
          address: supplier.address ?? '',
          contactName: supplier.contactName ?? '',
          phone: supplier.phone ?? '',
          email: supplier.email ?? '',
          paymentTermDays: String(supplier.paymentTermDays),
          paymentTerms: supplier.paymentTerms ?? '',
          isActive: supplier.isActive,
        }
      : EMPTY,
  })

  const onSubmit = handleSubmit(async (values) => {
    try {
      const saved = await save.mutateAsync({ id: supplier?.id, input: toSupplierInput(values) })
      toast.success(t(supplier ? 'purchasing.suppliers.form.updated' : 'purchasing.suppliers.form.created', { code: saved.code }))
      onClose()
    } catch (err) {
      const code = getErrorCode(err)
      if (code === 'DuplicateCode') return setError('code', { message: 'errors.codes.DuplicateCode' })
      if (code === 'DuplicateTaxCode') return setError('taxCode', { message: 'errors.codes.DuplicateTaxCode' })
      const known = Object.entries(getFieldErrors(err)).filter(([key]) => key in EMPTY)
      if (known.length) for (const [key, msg] of known) setError(key as keyof SupplierFormValues, { message: msg })
      else toast.error(getErrorMessage(err, t))
    }
  })

  const message = (key?: string) => (key ? t(key as never) : undefined)
  const column = (key: keyof SupplierFormValues) => t(`purchasing.suppliers.columns.${key as Exclude<keyof SupplierFormValues, 'isActive'>}`)
  const text = (key: Exclude<keyof SupplierFormValues, 'isActive'>, options: { required?: boolean; hint?: string; wide?: boolean; className?: string } = {}) => (
    <Field
      label={column(key)}
      htmlFor={key}
      required={options.required}
      hint={options.hint}
      className={options.wide ? 'sm:col-span-2' : undefined}
      error={message(errors[key]?.message)}
    >
      <Input id={key} autoComplete="off" invalid={!!errors[key]} className={options.className} {...register(key)} />
    </Field>
  )

  return (
    <Dialog
      open
      onClose={onClose}
      title={t(supplier ? 'purchasing.suppliers.form.editTitle' : 'purchasing.suppliers.form.createTitle')}
      width="lg"
      footer={
        <>
          {supplier && onDelete && (
            <Button variant="dangerGhost" className="mr-auto" onClick={() => onDelete(supplier)}>
              {t('common.delete')}
            </Button>
          )}
          <Button onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" type="submit" form="supplier-form" disabled={isSubmitting}>
            {isSubmitting ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id="supplier-form" onSubmit={onSubmit} noValidate className="grid gap-x-4 gap-y-3 sm:grid-cols-2">
        {text('code', { required: true, className: 'uppercase' })}
        {text('taxCode', { hint: t('purchasing.suppliers.form.taxCodeHint') })}
        {text('name', { required: true, wide: true })}
        {text('address', { wide: true })}
        {text('contactName')}
        {text('phone')}
        {text('email')}
        {text('paymentTermDays', { hint: t('purchasing.suppliers.form.paymentTermDaysHint') })}
        {text('paymentTerms', { wide: true })}
        <div className="sm:col-span-2">
          <Controller control={control} name="isActive" render={({ field }) => <ActiveCheckbox checked={field.value} onChange={field.onChange} />} />
        </div>
      </form>
    </Dialog>
  )
}
