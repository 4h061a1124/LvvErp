import { useState } from 'react'

export const PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp']
export const PHOTO_MAX_BYTES = 2 * 1024 * 1024

/** Returns an i18n key for a problem with the chosen photo, or null when it is fine. */
export function checkPhoto(file: File): 'photo.invalid' | 'photo.tooLarge' | null {
  if (!PHOTO_TYPES.includes(file.type)) return 'photo.invalid'
  if (file.size > PHOTO_MAX_BYTES) return 'photo.tooLarge'
  return null
}

/** Pending photo change in a form: a new file, a removal, or nothing. */
export interface PhotoDraft {
  file: File | null
  remove: boolean
}

export function usePhotoDraft() {
  const [draft, setDraft] = useState<PhotoDraft>({ file: null, remove: false })
  return { draft, setDraft, changed: draft.file !== null || draft.remove }
}
