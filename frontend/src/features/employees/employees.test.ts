import i18n from 'i18next'
import { describe, expect, it } from 'vitest'
import { importErrorMessage } from '@/lib/import'
import { checkPhoto } from '@/lib/photo'
import { initialOf } from './format'
import { importColumnLabel } from './importErrors'
import { employeeSchema, toEmployeeInput } from './schema'

const valid = {
  code: 'nv001',
  fullName: 'Nguyễn Văn A',
  departmentId: 'd1',
  positionId: 'p1',
  email: '',
  phone: '',
  status: 'Active' as const,
}

describe('employeeSchema', () => {
  it('accepts a minimal valid employee', () => {
    expect(employeeSchema.safeParse(valid).success).toBe(true)
  })

  it.each([
    ['code', ''],
    ['code', 'nv 001'],
    ['code', 'mã01'],
    ['code', 'X'.repeat(21)],
    ['fullName', '   '],
    ['departmentId', ''],
    ['email', 'not-an-email'],
    ['phone', 'abc'],
  ])('rejects invalid %s %j', (field, value) => {
    expect(employeeSchema.safeParse({ ...valid, [field]: value }).success).toBe(false)
  })

  it('returns i18n keys as messages', () => {
    const result = employeeSchema.safeParse({ ...valid, code: '' })
    expect(result.error?.issues[0].message).toBe('validation.required')
  })

  it('maps blanks to null and upper-cases the code', () => {
    const input = toEmployeeInput(employeeSchema.parse({ ...valid, code: ' nv001 ', email: ' ' }))
    expect(input).toMatchObject({ code: 'NV001', email: null, phone: null })
  })
})

describe('checkPhoto', () => {
  it('accepts small images and rejects other types or large files', () => {
    expect(checkPhoto(new File(['x'], 'a.png', { type: 'image/png' }))).toBeNull()
    expect(checkPhoto(new File(['x'], 'a.gif', { type: 'image/gif' }))).toBe('photo.invalid')
    const big = new File([new Uint8Array(2 * 1024 * 1024 + 1)], 'big.jpg', { type: 'image/jpeg' })
    expect(checkPhoto(big)).toBe('photo.tooLarge')
  })
})

describe('initialOf', () => {
  it('uses the given name, which comes last in Vietnamese', () => {
    expect(initialOf('Nguyễn Văn Đức')).toBe('Đ')
    expect(initialOf('  trần   thị hằng ')).toBe('H')
    expect(initialOf('')).toBe('')
  })
})

describe('import error messages', () => {
  const t = i18n.getFixedT('vi')

  it('translates known codes with the offending value', () => {
    const message = importErrorMessage(
      { row: 3, column: 'Department', errorCode: 'UnknownDepartment', message: 'Department not found', value: 'Marketing' },
      t,
    )
    expect(message).toBe('Không có bộ phận "Marketing"')
  })

  it('falls back to the server text for unknown codes', () => {
    const message = importErrorMessage({ row: 2, column: 'Code', errorCode: 'SomethingNew', message: 'Server says no', value: null }, t)
    expect(message).toBe('Server says no')
  })

  it('labels columns in the current language', () => {
    expect(importColumnLabel('FullName', t)).toBe('Họ tên')
    expect(importColumnLabel('FullName', i18n.getFixedT('en'))).toBe('Full name')
  })
})
