import { zodResolver } from '@hookform/resolvers/zod'
import clsx from 'clsx'
import { Search } from 'lucide-react'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Dialog } from '@/components/ui/Dialog'
import { Field, Input } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { useToast } from '@/components/ui/toast-context'
import { useAuth } from '@/lib/auth-context'
import { getErrorCode, getErrorMessage } from '@/lib/errors'
import { Permissions } from '@/lib/permissions'
import { normalizeSearch } from '@/lib/text'
import { useCatalog, useDeleteCatalogItem, useSaveCatalogItem } from '../api'
import { ActiveCheckbox, ActiveStatus } from '../components/ActiveStatus'
import { catalogItemSchema, type CatalogItemFormValues } from '../schema'
import type { CatalogItem, CatalogResource } from '../types'

type Section = 'units' | 'groups'

/** Units and material groups: same fields, same page. */
export function CatalogPage({ resource, section }: { resource: CatalogResource; section: Section }) {
  const { t } = useTranslation()
  const toast = useToast()
  const canManage = useAuth().can(Permissions.InventoryManageCatalog)
  const items = useCatalog(resource)
  const remove = useDeleteCatalogItem(resource)

  const [search, setSearch] = useState('')
  const [editing, setEditing] = useState<CatalogItem | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [deleting, setDeleting] = useState<CatalogItem | null>(null)

  const term = normalizeSearch(search)
  const rows = (items.data ?? []).filter(
    (i) => !term || normalizeSearch(i.code).includes(term) || normalizeSearch(i.name).includes(term),
  )

  async function confirmDelete() {
    if (!deleting) return
    try {
      await remove.mutateAsync(deleting.id)
      toast.success(t('inventory.deleted', { name: deleting.name }))
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setDeleting(null)
  }

  return (
    <>
      <PageHeader
        title={t(`inventory.${section}.title`)}
        actions={
          canManage && (
            <Button
              variant="primary"
              onClick={() => {
                setEditing(null)
                setFormOpen(true)
              }}
            >
              {t(`inventory.${section}.add`)}
            </Button>
          )
        }
      />

      <div className="max-w-3xl rounded-[4px] border border-zinc-200 bg-white">
        <div className="border-b border-zinc-200 p-2.5">
          <div className="relative w-full sm:w-64">
            <Search size={15} className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-zinc-400" />
            <Input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={`${t('inventory.code')}, ${t('inventory.name').toLowerCase()}`}
              aria-label={t('inventory.name')}
              className="pl-8"
            />
          </div>
        </div>

        {items.isPending ? (
          <p className="px-3 py-8 text-center text-sm text-zinc-500">{t('common.loading')}</p>
        ) : rows.length === 0 ? (
          <p className="px-3 py-8 text-center text-sm text-zinc-500">{t(`inventory.${section}.empty`)}</p>
        ) : (
          <table className="w-full text-left">
            <thead>
              <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                <th className="w-32 px-3 py-2 font-medium">{t('inventory.code')}</th>
                <th className="px-3 py-2 font-medium">{t('inventory.name')}</th>
                <th className="w-32 px-3 py-2 font-medium">{t('inventory.status')}</th>
                {canManage && (
                  <th className="w-28 px-3 py-2">
                    <span className="sr-only">{t('common.actions')}</span>
                  </th>
                )}
              </tr>
            </thead>
            <tbody>
              {rows.map((item) => (
                <tr key={item.id} className="border-b border-zinc-100 last:border-b-0 hover:bg-zinc-50/70">
                  <td className="tabular px-3 py-2 text-[13px] text-zinc-600">{item.code}</td>
                  <td className={clsx('px-3 py-2', item.isActive ? 'text-zinc-900' : 'text-zinc-500')}>{item.name}</td>
                  <td className="px-3 py-2">
                    <ActiveStatus active={item.isActive} />
                  </td>
                  {canManage && (
                    <td className="px-3 py-2 text-right text-[13px] whitespace-nowrap">
                      <button
                        type="button"
                        onClick={() => {
                          setEditing(item)
                          setFormOpen(true)
                        }}
                        className="px-1 text-brand-700 hover:underline"
                      >
                        {t('common.edit')}
                      </button>
                      <span className="mx-1 text-zinc-300">|</span>
                      <button type="button" onClick={() => setDeleting(item)} className="px-1 text-red-700 hover:underline">
                        {t('common.delete')}
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {formOpen && (
        <CatalogItemDialog resource={resource} section={section} item={editing} onClose={() => setFormOpen(false)} />
      )}
      <ConfirmDialog
        open={!!deleting}
        title={t(`inventory.${section}.deleteTitle`)}
        confirmLabel={t('common.delete')}
        busy={remove.isPending}
        onConfirm={confirmDelete}
        onClose={() => setDeleting(null)}
      >
        <p>{deleting && t(`inventory.${section}.deleteMessage`, { name: deleting.name })}</p>
      </ConfirmDialog>
    </>
  )
}

function CatalogItemDialog({
  resource,
  section,
  item,
  onClose,
}: {
  resource: CatalogResource
  section: Section
  item: CatalogItem | null
  onClose: () => void
}) {
  const { t } = useTranslation()
  const toast = useToast()
  const save = useSaveCatalogItem(resource)
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<CatalogItemFormValues>({
    resolver: zodResolver(catalogItemSchema),
    defaultValues: item ? { code: item.code, name: item.name, isActive: item.isActive } : { code: '', name: '', isActive: true },
  })

  const onSubmit = handleSubmit(async (values) => {
    try {
      const saved = await save.mutateAsync({ id: item?.id, input: { ...values, code: values.code.toUpperCase() } })
      toast.success(t('inventory.saved', { name: saved.name }))
      onClose()
    } catch (err) {
      const code = getErrorCode(err)
      if (code === 'DuplicateCode') setError('code', { message: 'errors.codes.DuplicateCode' })
      else if (code === 'DuplicateName') setError('name', { message: 'errors.codes.DuplicateName' })
      else toast.error(getErrorMessage(err, t))
    }
  })

  const message = (key?: string) => (key ? t(key as never) : undefined)

  return (
    <Dialog
      open
      onClose={onClose}
      title={t(item ? `inventory.${section}.editTitle` : `inventory.${section}.createTitle`)}
      width="sm"
      footer={
        <>
          <Button onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" type="submit" form="catalog-item-form" disabled={isSubmitting}>
            {isSubmitting ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id="catalog-item-form" onSubmit={onSubmit} className="space-y-3" noValidate>
        <Field label={t('inventory.code')} htmlFor="code" required error={message(errors.code?.message)}>
          <Input id="code" autoComplete="off" className="uppercase" invalid={!!errors.code} {...register('code')} />
        </Field>
        <Field label={t('inventory.name')} htmlFor="name" required error={message(errors.name?.message)}>
          <Input id="name" autoComplete="off" invalid={!!errors.name} {...register('name')} />
        </Field>
        <Controller
          control={control}
          name="isActive"
          render={({ field }) => <ActiveCheckbox checked={field.value} onChange={field.onChange} />}
        />
      </form>
    </Dialog>
  )
}
