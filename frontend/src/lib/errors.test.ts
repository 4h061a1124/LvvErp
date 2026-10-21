import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import i18n from 'i18next'
import { describe, expect, it } from 'vitest'
import { getErrorMessage, getFieldErrors } from './errors'

function httpError(status: number, data: unknown = {}) {
  const response = { status, data, statusText: '', headers: {}, config: { headers: new AxiosHeaders() } } as AxiosResponse
  return new AxiosError('failed', 'ERR_BAD_RESPONSE', undefined, undefined, response)
}

const t = i18n.getFixedT('vi')

describe('getFieldErrors', () => {
  it('camel-cases server field names and keeps the first message', () => {
    const error = httpError(400, { errors: { FullName: ['Required', 'Too long'], Code: ['Bad'] } })
    expect(getFieldErrors(error)).toEqual({ fullName: 'Required', code: 'Bad' })
  })

  it('returns an empty object for non-validation errors', () => {
    expect(getFieldErrors(new Error('boom'))).toEqual({})
    expect(getFieldErrors(httpError(500))).toEqual({})
  })
})

describe('getErrorMessage', () => {
  it('translates by status code', () => {
    expect(getErrorMessage(httpError(403), t)).toBe('Bạn không có quyền thực hiện thao tác này.')
    expect(getErrorMessage(httpError(500), t)).toBe('Có lỗi xảy ra. Vui lòng thử lại.')
  })

  it('reports network failures separately', () => {
    expect(getErrorMessage(new AxiosError('Network Error', 'ERR_NETWORK'), t)).toBe('Không kết nối được máy chủ.')
  })

  it('uses the server detail for conflicts', () => {
    expect(getErrorMessage(httpError(409, { detail: 'Employee code already exists.' }), t)).toBe(
      'Employee code already exists.',
    )
  })
})
