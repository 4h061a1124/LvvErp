const DIGITS = ['không', 'một', 'hai', 'ba', 'bốn', 'năm', 'sáu', 'bảy', 'tám', 'chín']
const SCALES = ['', 'nghìn', 'triệu', 'tỷ']

/** Reads 0–999. <paramref name="full"/>: a group after a higher one reads "không trăm", "linh". */
function readGroup(n: number, full: boolean): string {
  const hundreds = Math.floor(n / 100)
  const tens = Math.floor((n % 100) / 10)
  const units = n % 10
  const words: string[] = []

  if (hundreds > 0 || full) words.push(DIGITS[hundreds], 'trăm')
  if (tens === 0) {
    if (units > 0 && (hundreds > 0 || full)) words.push('linh')
  } else if (tens === 1) {
    words.push('mười')
  } else {
    words.push(DIGITS[tens], 'mươi')
  }
  if (units > 0) {
    if (units === 1 && tens >= 2) words.push('mốt')
    else if (units === 5 && tens >= 1) words.push('lăm')
    else words.push(DIGITS[units])
  }
  return words.join(' ')
}

/** Reads a whole number below one billion billion; "tỷ" repeats for larger amounts ("nghìn tỷ"). */
function readNumber(n: number): string {
  if (n === 0) return DIGITS[0]
  const parts: string[] = []
  const billions = Math.floor(n / 1e9)
  const rest = n % 1e9
  if (billions > 0) parts.push(readNumber(billions), 'tỷ')

  const groups = [Math.floor(rest / 1e6), Math.floor((rest % 1e6) / 1e3), rest % 1e3]
  groups.forEach((group, i) => {
    if (group === 0) return
    const full = parts.length > 0
    parts.push(readGroup(group, full))
    const scale = SCALES[2 - i]
    if (scale) parts.push(scale)
  })
  return parts.join(' ')
}

/** "1.050.000" -> "Một triệu không trăm năm mươi nghìn đồng" (amount in words on Vietnamese documents). */
export function amountInWords(amount: number): string {
  const n = Math.round(Math.abs(amount))
  const text = `${readNumber(n)} đồng`
  return text.charAt(0).toUpperCase() + text.slice(1)
}
