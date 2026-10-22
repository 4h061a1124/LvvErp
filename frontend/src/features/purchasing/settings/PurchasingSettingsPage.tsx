import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { Field, Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { useToast } from '@/components/ui/toast-context'
import { useDepartments } from '@/features/employees/api'
import type { Lookup } from '@/features/employees/types'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage, getFieldErrors } from '@/lib/errors'
import { formatDateTime, formatMoney } from '@/lib/format'
import { Permissions } from '@/lib/permissions'
import { usePurchasingSettings, useSavePurchasingSettings } from '../api'
import { parseMoney } from '../schema'
import type { PurchasingSettings } from '../types'

export function PurchasingSettingsPage() {
  const { t } = useTranslation()
  const settings = usePurchasingSettings()
  const departments = useDepartments()

  return (
    <>
      <PageHeader title={t('purchasing.settings.title')} />
      {settings.isError ? (
        <p className="py-10 text-center text-sm text-red-700">{getErrorMessage(settings.error, t)}</p>
      ) : !settings.data || !departments.data ? (
        <p className="py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
      ) : (
        <SettingsForm settings={settings.data} departments={departments.data} />
      )}
    </>
  )
}

type CompanyKey = 'companyName' | 'companyAddress' | 'companyTaxCode' | 'companyPhone'
const COMPANY_FIELDS: { key: CompanyKey; max: number; wide?: boolean }[] = [
  { key: 'companyName', max: 200, wide: true },
  { key: 'companyAddress', max: 500, wide: true },
  { key: 'companyTaxCode', max: 14 },
  { key: 'companyPhone', max: 20 },
]

function SettingsForm({ settings, departments }: { settings: PurchasingSettings; departments: Lookup[] }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const canEdit = useAuth().can(Permissions.PurchasingManageSettings)
  const save = useSavePurchasingSettings()

  const [threshold, setThreshold] = useState(formatMoney(settings.directorApprovalThreshold, i18n.language))
  const [departmentId, setDepartmentId] = useState(settings.purchasingDepartmentId ?? '')
  const [company, setCompany] = useState<Record<CompanyKey, string>>({
    companyName: settings.companyName ?? '',
    companyAddress: settings.companyAddress ?? '',
    companyTaxCode: settings.companyTaxCode ?? '',
    companyPhone: settings.companyPhone ?? '',
  })
  const [errors, setErrors] = useState<Partial<Record<'threshold' | 'department' | CompanyKey, string>>>({})

  async function submit(e: FormEvent) {
    e.preventDefault()
    const amount = parseMoney(threshold)
    const taxCode = company.companyTaxCode.trim()
    const next = {
      threshold: amount === null || amount <= 0 ? t('purchasing.settings.invalidAmount') : undefined,
      department: departmentId ? undefined : t('validation.required'),
      companyTaxCode: taxCode && !/^\d{10}(-\d{3})?$/.test(taxCode) ? t('validation.taxCode') : undefined,
    }
    setErrors(next)
    if (next.threshold || next.department || next.companyTaxCode) return
    try {
      const orNull = (v: string) => v.trim() || null
      const saved = await save.mutateAsync({
        directorApprovalThreshold: amount!,
        purchasingDepartmentId: departmentId,
        companyName: orNull(company.companyName),
        companyAddress: orNull(company.companyAddress),
        companyTaxCode: orNull(company.companyTaxCode),
        companyPhone: orNull(company.companyPhone),
      })
      setThreshold(formatMoney(saved.directorApprovalThreshold, i18n.language))
      toast.success(t('purchasing.settings.saved'))
    } catch (err) {
      const fields = getFieldErrors(err)
      const mapped = {
        threshold: fields.directorApprovalThreshold,
        department: fields.purchasingDepartmentId,
        companyName: fields.companyName,
        companyAddress: fields.companyAddress,
        companyTaxCode: fields.companyTaxCode,
        companyPhone: fields.companyPhone,
      }
      if (Object.values(mapped).some(Boolean)) setErrors(mapped)
      else toast.error(getErrorMessage(err, t))
    }
  }

  return (
    <form onSubmit={submit} noValidate className="max-w-3xl rounded-[4px] border border-zinc-200 bg-white">
      <div className="space-y-4 p-4">
        <Field
          label={t('purchasing.settings.threshold')}
          htmlFor="threshold"
          required
          error={errors.threshold}
          hint={t('purchasing.settings.thresholdHint')}
        >
          <Input
            id="threshold"
            inputMode="numeric"
            autoComplete="off"
            value={threshold}
            disabled={!canEdit}
            invalid={!!errors.threshold}
            onChange={(e) => setThreshold(e.target.value)}
            onBlur={() => {
              const amount = parseMoney(threshold)
              if (amount !== null) setThreshold(formatMoney(amount, i18n.language))
            }}
            className="tabular w-56 text-right"
          />
        </Field>
        <Field
          label={t('purchasing.settings.department')}
          htmlFor="department"
          required
          error={errors.department}
          hint={t('purchasing.settings.departmentHint')}
        >
          <Select
            id="department"
            value={departmentId}
            disabled={!canEdit}
            invalid={!!errors.department}
            onChange={(e) => setDepartmentId(e.target.value)}
            className="w-72"
          >
            <option value="">{t('common.choose')}</option>
            {departments.map((d) => (
              <option key={d.id} value={d.id}>
                {d.name}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      <div className="border-t border-zinc-200 p-4">
        <h2 className="text-sm font-semibold text-zinc-800">{t('purchasing.settings.company')}</h2>
        <p className="mb-3 text-xs text-zinc-500">{t('purchasing.settings.companyHint')}</p>
        <div className="grid gap-x-4 gap-y-3 sm:grid-cols-2">
          {COMPANY_FIELDS.map(({ key, max, wide }) => (
            <Field key={key} label={t(`purchasing.settings.${key}`)} htmlFor={key} error={errors[key]} className={wide ? 'sm:col-span-2' : undefined}>
              <Input
                id={key}
                autoComplete="off"
                maxLength={max}
                value={company[key]}
                disabled={!canEdit}
                invalid={!!errors[key]}
                onChange={(e) => setCompany((c) => ({ ...c, [key]: e.target.value }))}
              />
            </Field>
          ))}
        </div>
      </div>

      <div className="flex items-center justify-between gap-3 border-t border-zinc-200 px-4 py-2.5">
        <span className="text-xs text-zinc-500">
          {settings.updatedAt &&
            t('purchasing.settings.updatedBy', { user: settings.updatedBy ?? '', time: formatDateTime(settings.updatedAt, i18n.language) })}
        </span>
        {canEdit && (
          <Button variant="primary" type="submit" disabled={save.isPending}>
            {save.isPending ? t('common.saving') : t('common.save')}
          </Button>
        )}
      </div>
    </form>
  )
}
