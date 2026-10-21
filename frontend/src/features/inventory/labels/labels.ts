import QRCode from 'qrcode'
import type { Material } from '../types'

/** One label design; printed <see cref="copies"/> times. */
export interface LabelItem {
  key: string
  code: string
  name: string
  specification: string | null
  unitName: string
  lotNumber?: string | null
  expiryDate?: string | null
  copies: number
}

export type LabelLayout = 'a4' | 'roll'

export const MAX_COPIES = 500

/**
 * Physical formats. A4 matches common 3 × 8 label sheets (70 × 36 mm, 4.5 mm top/bottom margin);
 * roll is one 50 × 30 mm label per page for thermal label printers.
 */
export const LAYOUTS: Record<LabelLayout, { perPage: number; page: string; pageWidth: string; pageHeight: string }> = {
  a4: { perPage: 24, page: 'size: A4; margin: 4.5mm 0', pageWidth: '210mm', pageHeight: '288mm' },
  roll: { perPage: 1, page: 'size: 50mm 30mm; margin: 0', pageWidth: '50mm', pageHeight: '30mm' },
}

export function labelItemFromMaterial(m: Pick<Material, 'id' | 'code' | 'name' | 'specification' | 'unitName'>): LabelItem {
  return { key: m.id, code: m.code, name: m.name, specification: m.specification, unitName: m.unitName, copies: 1 }
}

/** Repeats each item by its copies (clamped to 0..MAX_COPIES) and splits the result into pages. */
export function paginateLabels(items: LabelItem[], layout: LabelLayout): LabelItem[][] {
  const labels = items.flatMap((item) =>
    Array.from({ length: Math.max(0, Math.min(MAX_COPIES, Math.floor(item.copies))) }, () => item),
  )
  const perPage = LAYOUTS[layout].perPage
  const pages: LabelItem[][] = []
  for (let i = 0; i < labels.length; i += perPage) pages.push(labels.slice(i, i + perPage))
  return pages
}

/**
 * QR content is the plain material code: any scanner reads it, and scanning into a search box
 * finds the material.
 */
export function qrSvg(code: string): Promise<string> {
  return QRCode.toString(code, { type: 'svg', margin: 0, errorCorrectionLevel: 'M' })
}
