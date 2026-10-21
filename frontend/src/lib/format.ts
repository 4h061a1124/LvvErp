const formatters = new Map<string, Intl.NumberFormat>()

/** Quantities have up to 3 decimals: "1.250,5" in Vietnamese, "1,250.5" in English. */
export function formatQuantity(value: number, language: string): string {
  let formatter = formatters.get(language)
  if (!formatter) {
    formatter = new Intl.NumberFormat(language === 'vi' ? 'vi-VN' : 'en-US', { maximumFractionDigits: 3 })
    formatters.set(language, formatter)
  }
  return formatter.format(value)
}

/** Accepts "12.5" or "12,5" (Vietnamese decimal comma); rejects "1.234,5" style ambiguity. Null when invalid. */
export function parseQuantity(text: string): number | null {
  const s = text.trim().replace(/\s/g, '')
  if (!s || (s.includes('.') && s.includes(','))) return null
  if (!/^\d+([.,]\d+)?$/.test(s)) return null
  return Number(s.replace(',', '.'))
}

export function hasAtMostThreeDecimals(value: number): boolean {
  return Math.abs(Math.round(value * 1000) - value * 1000) < 1e-6
}

/** Value for an editable text box: local decimal separator, no thousands grouping ("1250,5"). */
export function formatQuantityInput(value: number, language: string): string {
  return new Intl.NumberFormat(language === 'vi' ? 'vi-VN' : 'en-US', { maximumFractionDigits: 3, useGrouping: false }).format(value)
}

/** "2027-12-31" -> "31/12/2027" (day-first, as used in Vietnam, in both languages). */
export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return ''
  const [year, month, day] = isoDate.slice(0, 10).split('-')
  return `${day}/${month}/${year}`
}

/** Today's date as yyyy-MM-dd in the browser's time zone (the factory's, for its users). */
export function todayIso(): string {
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

/** Date and time in the user's language, e.g. "21/10/2026 14:05". */
export function formatDateTime(iso: string | null | undefined, language: string): string {
  if (!iso) return ''
  return new Date(iso).toLocaleString(language === 'vi' ? 'vi-VN' : 'en-GB', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}
