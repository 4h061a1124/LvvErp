import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { Button } from '@/components/ui/Button'
import { getErrorMessage } from '@/lib/errors'
import { formatDate, formatMoney, formatQuantity } from '@/lib/format'
import { amountInWords } from '@/lib/moneyWords'
import { useOrder, usePurchasingSettings } from '../api'
import type { PurchaseOrder, PurchasingSettings } from '../types'
import { VAT_PERCENT } from './orderModel'

/**
 * /purchasing/orders/{id}/print – the order as sent to the supplier, on A4 portrait.
 * Always printed in Vietnamese: it is a document for the supplier, not part of the UI.
 */
export function OrderPrintPage() {
  const { t } = useTranslation()
  const { id } = useParams()
  const order = useOrder(id)
  const settings = usePurchasingSettings()

  if (order.isError) return <p className="py-10 text-center text-sm text-red-700">{getErrorMessage(order.error, t)}</p>
  if (!order.data || !settings.data) return <p className="py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>

  const approved = !['Draft', 'Submitted', 'Rejected', 'Cancelled'].includes(order.data.status)
  return (
    <>
      <style>{'@page { size: A4 portrait; margin: 12mm 14mm; }'}</style>
      <div className="mb-4 flex flex-wrap items-center justify-between gap-3 print:hidden">
        <div>
          <h1 className="text-lg font-semibold text-zinc-900">{t('purchasing.print.title', { number: order.data.number })}</h1>
          <Link to={`/purchasing/orders/${order.data.id}`} className="text-[13px] text-brand-700 hover:underline">
            ← {order.data.number}
          </Link>
        </div>
        <div className="flex items-center gap-3">
          {!approved && <span className="text-[13px] text-amber-700">{t('purchasing.print.notApproved')}</span>}
          <Button variant="primary" onClick={() => window.print()}>
            {t('purchasing.print.print')}
          </Button>
        </div>
      </div>
      <p className="mb-3 text-xs text-zinc-500 print:hidden">{t('purchasing.print.hint')}</p>
      <div className="overflow-x-auto pb-4 print:overflow-visible print:pb-0">
        <OrderSheet order={order.data} company={settings.data} draft={!approved} />
      </div>
    </>
  )
}

function OrderSheet({ order, company, draft }: { order: PurchaseOrder; company: PurchasingSettings; draft: boolean }) {
  const qty = (v: number) => formatQuantity(v, 'vi')
  const money = (v: number, digits = 0) => formatMoney(v, 'vi', digits)
  const vatLabel = order.vatRate === 'Exempt' ? 'Không chịu thuế' : `${VAT_PERCENT[order.vatRate]}%`
  const [year, month, day] = order.orderDate.split('-')
  const cell = 'border border-zinc-800 px-1.5 py-1'

  return (
    <article
      className="relative mx-auto w-[210mm] min-h-[297mm] bg-white px-[14mm] py-[12mm] text-[12.5px] leading-snug text-black shadow-[0_0_0_1px_rgb(228_228_231)] print:min-h-0 print:w-auto print:p-0 print:shadow-none"
      style={{ fontFamily: '"Be Vietnam Pro", Arial, sans-serif' }}
    >
      {draft && (
        <div className="pointer-events-none absolute inset-0 flex items-center justify-center text-[64px] font-bold tracking-widest text-zinc-200/70 select-none [transform:rotate(-30deg)]">
          BẢN NHÁP
        </div>
      )}

      <header className="flex items-start justify-between gap-6">
        <div className="max-w-[60%]">
          <div className="text-[13.5px] font-semibold uppercase">{company.companyName ?? ''}</div>
          {company.companyAddress && <div>Địa chỉ: {company.companyAddress}</div>}
          {company.companyTaxCode && <div>Mã số thuế: {company.companyTaxCode}</div>}
          {company.companyPhone && <div>Điện thoại: {company.companyPhone}</div>}
        </div>
        <div className="text-right">
          <div>
            Số: <span className="font-semibold">{order.number}</span>
          </div>
          <div>
            Ngày {day} tháng {month} năm {year}
          </div>
        </div>
      </header>

      <h2 className="mt-5 mb-4 text-center text-[18px] font-bold uppercase">Đơn đặt hàng</h2>

      <section className="mb-3 grid grid-cols-[9.5rem_1fr] gap-y-0.5">
        <span>Kính gửi:</span>
        <span className="font-semibold">{order.supplier.name}</span>
        {order.supplier.address && (
          <>
            <span>Địa chỉ:</span>
            <span>{order.supplier.address}</span>
          </>
        )}
        {order.supplier.taxCode && (
          <>
            <span>Mã số thuế:</span>
            <span>{order.supplier.taxCode}</span>
          </>
        )}
        {(order.supplier.contactName || order.supplier.phone) && (
          <>
            <span>Người liên hệ:</span>
            <span>{[order.supplier.contactName, order.supplier.phone, order.supplier.email].filter(Boolean).join(' · ')}</span>
          </>
        )}
      </section>
      <p className="mb-2">Chúng tôi đề nghị Quý công ty cung cấp các mặt hàng sau:</p>

      <table className="w-full border-collapse">
        <thead>
          <tr className="text-center font-semibold">
            <th className={`${cell} w-8`}>STT</th>
            <th className={`${cell} w-24`}>Mã hàng</th>
            <th className={cell}>Tên hàng, quy cách</th>
            <th className={`${cell} w-14`}>ĐVT</th>
            <th className={`${cell} w-20`}>Số lượng</th>
            <th className={`${cell} w-24`}>Đơn giá</th>
            <th className={`${cell} w-28`}>Thành tiền</th>
          </tr>
        </thead>
        <tbody>
          {order.lines.map((l) => (
            <tr key={l.id} className="break-inside-avoid align-top">
              <td className={`${cell} text-center`}>{l.lineNo}</td>
              <td className={cell}>{l.materialCode}</td>
              <td className={cell}>
                {l.materialName}
                {l.specification && <div className="text-[11.5px]">{l.specification}</div>}
                {l.note && <div className="text-[11.5px] italic">{l.note}</div>}
              </td>
              <td className={`${cell} text-center`}>{l.unitName}</td>
              <td className={`${cell} text-right tabular-nums`}>{qty(l.quantity)}</td>
              <td className={`${cell} text-right tabular-nums`}>{money(l.unitPrice, 2)}</td>
              <td className={`${cell} text-right tabular-nums`}>{money(l.amount)}</td>
            </tr>
          ))}
          <tr>
            <td className={`${cell} text-right`} colSpan={6}>
              Cộng tiền hàng
            </td>
            <td className={`${cell} text-right tabular-nums`}>{money(order.subtotal)}</td>
          </tr>
          <tr>
            <td className={`${cell} text-right`} colSpan={6}>
              Thuế GTGT ({vatLabel})
            </td>
            <td className={`${cell} text-right tabular-nums`}>{money(order.vatAmount)}</td>
          </tr>
          <tr className="font-semibold">
            <td className={`${cell} text-right`} colSpan={6}>
              Tổng cộng
            </td>
            <td className={`${cell} text-right tabular-nums`}>{money(order.total)}</td>
          </tr>
        </tbody>
      </table>
      <p className="mt-1.5">
        Số tiền bằng chữ: <span className="italic">{amountInWords(order.total)}.</span>
      </p>

      <section className="mt-3 space-y-0.5">
        <div>
          Ngày giao hàng: {order.expectedDate ? formatDate(order.expectedDate) : 'theo thỏa thuận'}. Địa điểm giao hàng: {order.warehouseName}
          {company.companyAddress ? `, ${company.companyAddress}` : ''}.
        </div>
        <div>Thanh toán: {order.paymentTermDays > 0 ? `trong ${order.paymentTermDays} ngày kể từ ngày nhận hàng và hóa đơn.` : 'khi nhận hàng và hóa đơn.'}</div>
        {order.note && <div>Ghi chú: {order.note}</div>}
        <div>Hàng giao kèm hóa đơn GTGT và phiếu giao hàng ghi rõ số đơn đặt hàng {order.number}.</div>
      </section>

      <footer className="mt-8 grid grid-cols-3 gap-4 text-center break-inside-avoid">
        {['Người lập', 'Người duyệt', 'Xác nhận của nhà cung cấp'].map((title) => (
          <div key={title}>
            <div className="font-semibold">{title}</div>
            <div className="text-[11.5px] italic">(Ký, ghi rõ họ tên)</div>
            <div className="h-20" />
          </div>
        ))}
      </footer>
    </article>
  )
}
