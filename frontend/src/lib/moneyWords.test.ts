import { describe, expect, it } from 'vitest'
import { amountInWords } from './moneyWords'

describe('amountInWords', () => {
  it.each([
    [0, 'Không đồng'],
    [5, 'Năm đồng'],
    [15, 'Mười lăm đồng'],
    [21, 'Hai mươi mốt đồng'],
    [105, 'Một trăm linh năm đồng'],
    [115, 'Một trăm mười lăm đồng'],
    [16_297, 'Mười sáu nghìn hai trăm chín mươi bảy đồng'],
    [1_050_000, 'Một triệu không trăm năm mươi nghìn đồng'],
    [1_000_005, 'Một triệu không trăm linh năm đồng'],
    [50_000_000, 'Năm mươi triệu đồng'],
    [1_000_000_000, 'Một tỷ đồng'],
    [2_500_300_000, 'Hai tỷ năm trăm triệu ba trăm nghìn đồng'],
    [1_200_000_000_000, 'Một nghìn hai trăm tỷ đồng'],
  ])('%d', (amount, words) => {
    expect(amountInWords(amount)).toBe(words)
  })
})
