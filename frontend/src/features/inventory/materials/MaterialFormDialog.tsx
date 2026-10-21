import { zodResolver } from '@hookform/resolvers/zod'
import { useQueryClient } from '@tanstack/react-query'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { PhotoField } from '@/components/PhotoField'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Field, Input, Select } from '@/components/ui/Field'
import { useToast } from '@/components/ui/toast-context'
import { getErrorCode, getErrorMessage, getFieldErrors } from '@/lib/errors'
import { formatQuantityInput } from '@/lib/format'
import { usePhotoDraft } from '@/lib/photo'
import { deleteMaterialPhoto, inventoryKeys, uploadMaterialPhoto, useCatalog, useSaveMaterial } from '../api'
import { ActiveCheckbox } from '../components/ActiveStatus'
import { MaterialThumb } from '../components/MaterialThumb'
import { materialSchema, toMaterialInput, type MaterialFormValues } from '../schema'
import type { CatalogItem, Material } from '../types'

interface MaterialFormDialogProps {
  /** null = create */
  material: Material | null
  onClose: () => void
  onDelete?: (material: Material) => void
}

/** Mount only while open. Waits for units and groups so the current ones are pre-selected. */
export function MaterialFormDialog(props: MaterialFormDialogProps) {
  const { t } = useTranslation()
  const units = useCatalog('units')
  const groups = useCatalog('material-groups')
  const title = t(props.material ? 'inventory.materials.form.editTitle' : 'inventory.materials.form.createTitle')

  if (!units.data || !groups.data) {
    const failed = units.isError || groups.isError
    return (
      <Dialog open onClose={props.onClose} title={title}>
        <p className={failed ? 'py-6 text-center text-sm text-red-700' : 'py-6 text-center text-sm text-zinc-500'}>
          {failed ? t('errors.server') : t('common.loading')}
        </p>
      </Dialog>
    )
  }

  // Inactive entries can't be picked, except the one the material already uses.
  const pickable = (items: CatalogItem[], currentId?: string) => items.filter((i) => i.isActive || i.id === currentId)
  return (
    <MaterialForm
      {...props}
      title={title}
      units={pickable(units.data, props.material?.unitId)}
      groups={pickable(groups.data, props.material?.groupId)}
    />
  )
}

const EMPTY: MaterialFormValues = {
  code: '',
  name: '',
  specification: '',
  unitId: '',
  groupId: '',
  minStock: '0',
  trackLots: false,
  isActive: true,
}

function MaterialForm({
  material,
  title,
  units,
  groups,
  onClose,
  onDelete,
}: MaterialFormDialogProps & { title: string; units: CatalogItem[]; groups: CatalogItem[] }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const save = useSaveMaterial()
  const photo = usePhotoDraft()

  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<MaterialFormValues>({
    resolver: zodResolver(materialSchema),
    defaultValues: material
      ? {
          code: material.code,
          name: material.name,
          specification: material.specification ?? '',
          unitId: material.unitId,
          groupId: material.groupId,
          minStock: formatQuantityInput(material.minStock, i18n.language),
          trackLots: material.trackLots,
          isActive: material.isActive,
        }
      : EMPTY,
  })

  const onSubmit = handleSubmit(async (values) => {
    try {
      const saved = await save.mutateAsync({ id: material?.id, input: toMaterialInput(values) })
      try {
        if (photo.draft.file) await uploadMaterialPhoto(saved.id, photo.draft.file)
        else if (photo.draft.remove && material?.photoUrl) await deleteMaterialPhoto(saved.id)
      } catch {
        toast.error(t('photo.failed'))
      }
      if (photo.changed) await queryClient.invalidateQueries({ queryKey: inventoryKeys.materials })

      toast.success(t(material ? 'inventory.materials.form.updated' : 'inventory.materials.form.created', { code: saved.code }))
      onClose()
    } catch (err) {
      const code = getErrorCode(err)
      if (code === 'DuplicateCode') return setError('code', { message: 'errors.codes.DuplicateCode' })
      if (code === 'TrackLotsLocked') return setError('trackLots', { message: 'errors.codes.TrackLotsLocked' })
      const known = Object.entries(getFieldErrors(err)).filter(([key]) => key in EMPTY)
      if (known.length) for (const [key, msg] of known) setError(key as keyof MaterialFormValues, { message: msg })
      else toast.error(getErrorMessage(err, t))
    }
  })

  const message = (key?: string) => (key ? t(key as never) : undefined)

  return (
    <Dialog
      open
      onClose={onClose}
      title={title}
      width="lg"
      footer={
        <>
          {material && onDelete && (
            <Button variant="dangerGhost" className="mr-auto" onClick={() => onDelete(material)}>
              {t('common.delete')}
            </Button>
          )}
          <Button onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" type="submit" form="material-form" disabled={isSubmitting}>
            {isSubmitting ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id="material-form" onSubmit={onSubmit} noValidate>
        <div className="mb-4 border-b border-zinc-100 pb-4">
          <PhotoField
            currentUrl={material?.photoUrl ?? null}
            draft={photo.draft}
            onChange={photo.setDraft}
            placeholder={(size) => <MaterialThumb photoUrl={null} size={size} />}
          />
        </div>

        <div className="grid gap-x-4 gap-y-3 sm:grid-cols-2">
          <Field label={t('inventory.materials.columns.code')} htmlFor="code" required error={message(errors.code?.message)}>
            <Input id="code" autoComplete="off" className="uppercase" invalid={!!errors.code} {...register('code')} />
          </Field>
          <Field label={t('inventory.materials.columns.name')} htmlFor="name" required error={message(errors.name?.message)}>
            <Input id="name" autoComplete="off" invalid={!!errors.name} {...register('name')} />
          </Field>
          <Field
            label={t('inventory.materials.columns.specification')}
            htmlFor="specification"
            className="sm:col-span-2"
            error={message(errors.specification?.message)}
          >
            <Input id="specification" autoComplete="off" invalid={!!errors.specification} {...register('specification')} />
          </Field>
          <Field label={t('inventory.materials.columns.unit')} htmlFor="unitId" required error={message(errors.unitId?.message)}>
            <Select id="unitId" invalid={!!errors.unitId} {...register('unitId')}>
              <option value="">{t('common.choose')}</option>
              {units.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label={t('inventory.materials.columns.group')} htmlFor="groupId" required error={message(errors.groupId?.message)}>
            <Select id="groupId" invalid={!!errors.groupId} {...register('groupId')}>
              <option value="">{t('common.choose')}</option>
              {groups.map((g) => (
                <option key={g.id} value={g.id}>
                  {g.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field
            label={t('inventory.materials.columns.minStock')}
            htmlFor="minStock"
            error={message(errors.minStock?.message)}
            hint={t('inventory.materials.form.minStockHint')}
          >
            <Input id="minStock" inputMode="decimal" autoComplete="off" invalid={!!errors.minStock} {...register('minStock')} />
          </Field>
          <div className="space-y-3 sm:pt-6">
            <Controller
              control={control}
              name="trackLots"
              render={({ field }) => (
                <label className="flex cursor-pointer items-start gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={field.value}
                    onChange={(e) => field.onChange(e.target.checked)}
                    className="mt-0.5 size-4 accent-brand-600"
                  />
                  <span>
                    <span className="text-zinc-800">{t('inventory.materials.form.trackLotsLabel')}</span>
                    <span className={errors.trackLots ? 'block text-xs text-red-600' : 'block text-xs text-zinc-500'}>
                      {message(errors.trackLots?.message) ?? t('inventory.materials.form.trackLotsHint')}
                    </span>
                  </span>
                </label>
              )}
            />
            <Controller
              control={control}
              name="isActive"
              render={({ field }) => <ActiveCheckbox checked={field.value} onChange={field.onChange} />}
            />
          </div>
        </div>
      </form>
    </Dialog>
  )
}
