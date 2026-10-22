import { describe, expect, it } from 'vitest'
import { formatDate, formatQuantity, formatQuantityInput, hasAtMostThreeDecimals, parseQuantity } from '@/lib/format'
import { normalizeSearch } from '@/lib/text'
import { labelItemFromMaterial, paginateLabels, qrSvg, type LabelItem } from './labels/labels'
import { materialSchema, toMaterialInput } from './schema'

const item = (code: string, copies: number): LabelItem => ({
  key: code,
  code,
  name: 'Bu lông',
  specification: null,
  unitName: 'Cái',
  copies,
})

describe('paginateLabels', () => {
  it('repeats items by copies and fills A4 sheets of 24', () => {
    const pages = paginateLabels([item('A', 20), item('B', 10)], 'a4')

    expect(pages.map((p) => p.length)).toEqual([24, 6])
    expect(pages[0].filter((l) => l.code === 'B')).toHaveLength(4)
  })

  it('puts one label per page on a roll', () => {
    expect(paginateLabels([item('A', 3)], 'roll')).toHaveLength(3)
  })

  it('ignores zero, negative and fractional copies and caps huge numbers', () => {
    expect(paginateLabels([item('A', 0), item('B', -2)], 'a4')).toEqual([])
    expect(paginateLabels([item('A', 2.7)], 'roll')).toHaveLength(2)
    expect(paginateLabels([item('A', 10_000)], 'roll')).toHaveLength(500)
  })

  it('starts each material at one copy', () => {
    expect(labelItemFromMaterial({ id: '1', code: 'X', name: 'X', specification: null, unitName: 'Kg' }).copies).toBe(1)
  })
})

describe('qrSvg', () => {
  it('encodes the material code as an SVG', async () => {
    const svg = await qrSvg('BL-M8X30')
    expect(svg.startsWith('<svg')).toBe(true)
    expect(svg).toContain('viewBox')
  })
})

describe('quantities', () => {
  it('formats with the language decimal separator', () => {
    expect(formatQuantity(1250.5, 'vi')).toBe('1.250,5')
    expect(formatQuantity(1250.5, 'en')).toBe('1,250.5')
    expect(formatQuantity(0.125, 'vi')).toBe('0,125')
    expect(formatQuantityInput(1250.5, 'vi')).toBe('1250,5')
  })

  it.each([
    ['12,5', 12.5],
    ['12.5', 12.5],
    [' 1000 ', 1000],
    ['0', 0],
  ])('parses %j', (text, value) => expect(parseQuantity(text)).toBe(value))

  it.each(['1.234,5', '-1', 'abc', '', '1e3'])('rejects %j', (text) => expect(parseQuantity(text)).toBeNull())

  it('checks three decimals', () => {
    expect(hasAtMostThreeDecimals(0.125)).toBe(true)
    expect(hasAtMostThreeDecimals(0.1255)).toBe(false)
  })

  it('formats dates day-first', () => {
    expect(formatDate('2027-12-31')).toBe('31/12/2027')
    expect(formatDate(null)).toBe('')
  })
})

describe('materialSchema', () => {
  const valid = {
    code: 'bl-m8',
    name: 'Bu lông',
    specification: '',
    unitId: 'u',
    groupId: 'g',
    minStock: '12,5',
    trackLots: false,
    receiptTolerancePercent: '2,5',
    isActive: true,
  }

  it('accepts a Vietnamese decimal and converts it', () => {
    const input = toMaterialInput(materialSchema.parse(valid))
    expect(input).toMatchObject({ code: 'BL-M8', minStock: 12.5, receiptTolerancePercent: 2.5, specification: null })
  })

  it.each([
    ['code', 'BL M8'],
    ['code', 'X'.repeat(31)],
    ['name', ' '],
    ['unitId', ''],
    ['minStock', '-1'],
    ['minStock', '0,1255'],
    ['receiptTolerancePercent', '101'],
    ['receiptTolerancePercent', '1,255'],
    ['receiptTolerancePercent', ''],
  ])('rejects invalid %s %j', (field, value) => {
    expect(materialSchema.safeParse({ ...valid, [field]: value }).success).toBe(false)
  })
})

describe('normalizeSearch', () => {
  it('drops case and Vietnamese diacritics', () => {
    expect(normalizeSearch('  Đá CẮT ')).toBe('da cat')
    expect(normalizeSearch('Phụ tùng')).toBe('phu tung')
  })
})
