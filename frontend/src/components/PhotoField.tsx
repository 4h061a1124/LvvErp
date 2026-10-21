import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { checkPhoto, PHOTO_TYPES, type PhotoDraft } from '@/lib/photo'

interface PhotoFieldProps {
  /** Photo currently saved, if any. */
  currentUrl: string | null
  draft: PhotoDraft
  onChange: (draft: PhotoDraft) => void
  /** Rendered when there is no photo to show (e.g. initials). */
  placeholder: (size: number) => ReactNode
  size?: number
}

export function PhotoField({ currentUrl, draft, onChange, placeholder, size = 72 }: PhotoFieldProps) {
  const { t } = useTranslation()
  const input = useRef<HTMLInputElement>(null)
  const [error, setError] = useState<string | null>(null)

  const previewUrl = useMemo(() => (draft.file ? URL.createObjectURL(draft.file) : null), [draft.file])
  useEffect(() => () => void (previewUrl && URL.revokeObjectURL(previewUrl)), [previewUrl])

  const shown = previewUrl ?? (draft.remove ? null : currentUrl)

  function choose(file: File | undefined) {
    if (!file) return
    const problem = checkPhoto(file)
    setError(problem ? t(problem) : null)
    if (!problem) onChange({ file, remove: false })
  }

  return (
    <div className="flex items-center gap-3">
      {shown ? (
        <img
          src={shown}
          alt=""
          style={{ width: size, height: size }}
          className="shrink-0 rounded-[3px] border border-zinc-200 bg-zinc-100 object-cover"
        />
      ) : (
        placeholder(size)
      )}
      <div>
        <div className="flex flex-wrap gap-2">
          <Button size="sm" onClick={() => input.current?.click()}>
            {shown ? t('photo.change') : t('photo.choose')}
          </Button>
          {shown && (
            <Button size="sm" variant="ghost" onClick={() => onChange({ file: null, remove: true })}>
              {t('photo.remove')}
            </Button>
          )}
        </div>
        <p className={error ? 'mt-1.5 text-xs text-red-600' : 'mt-1.5 text-xs text-zinc-500'}>{error ?? t('photo.hint')}</p>
        <input
          ref={input}
          type="file"
          accept={PHOTO_TYPES.join(',')}
          className="hidden"
          onChange={(e) => {
            choose(e.target.files?.[0])
            e.target.value = ''
          }}
        />
      </div>
    </div>
  )
}
