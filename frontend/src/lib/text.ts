/** Lower-case without Vietnamese diacritics, for client-side filtering ("Đá cắt" -> "da cat"). */
export function normalizeSearch(text: string): string {
  return text
    .trim()
    .toLowerCase()
    .replace(/đ/g, 'd')
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
}
