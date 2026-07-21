import { money } from '../lib/format';

interface Props {
  itemsSubtotal: number;
  lineDiscount: number;
  tax: number;
  invoiceDiscount?: number;
  onInvoiceDiscountChange?: (v: string) => void;
  shipping: string;
  onShippingChange: (v: string) => void;
  otherCosts: string;
  onOtherCostsChange: (v: string) => void;
  grandTotal: number;
  notes: string;
  onNotesChange: (v: string) => void;
}

/** پورتِ `.inv-side`/`.pay-sum`ِ design-system (sales-invoice.html/purchase-invoice.html)
 * — پنلِ کناریِ جمع‌بندیِ مبلغِ فاکتور که در پیاده‌سازیِ وب جا افتاده بود. */
export function InvoiceSidePanel({
  itemsSubtotal,
  lineDiscount,
  tax,
  invoiceDiscount,
  onInvoiceDiscountChange,
  shipping,
  onShippingChange,
  otherCosts,
  onOtherCostsChange,
  grandTotal,
  notes,
  onNotesChange,
}: Props) {
  return (
    <div className="inv-side">
      <div className="pay-sum">
        <div className="row">
          <span>جمعِ اقلام</span>
          <span className="v">{money(itemsSubtotal)}</span>
        </div>
        <div className="row">
          <span>تخفیفِ سطری</span>
          <span className="v" style={{ color: 'var(--danger-500)' }}>
            −{money(lineDiscount)}
          </span>
        </div>
        {onInvoiceDiscountChange && (
          <div className="row">
            <span>تخفیفِ کلی</span>
            <span className="v">
              <input
                className="input-c num"
                type="number"
                min="0"
                style={{ width: 90, height: 26, fontSize: 12 }}
                value={invoiceDiscount ?? 0}
                onChange={(e) => onInvoiceDiscountChange(e.target.value)}
              />
            </span>
          </div>
        )}
        <div className="row">
          <span>مالیات</span>
          <span className="v">{money(tax)}</span>
        </div>
        <div className="row">
          <span>هزینهٔ حمل</span>
          <span className="v">
            <input
              className="input-c num"
              type="number"
              min="0"
              style={{ width: 90, height: 26, fontSize: 12 }}
              value={shipping}
              onChange={(e) => onShippingChange(e.target.value)}
            />
          </span>
        </div>
        <div className="row">
          <span>سایرِ هزینه‌ها</span>
          <span className="v">
            <input
              className="input-c num"
              type="number"
              min="0"
              style={{ width: 90, height: 26, fontSize: 12 }}
              value={otherCosts}
              onChange={(e) => onOtherCostsChange(e.target.value)}
            />
          </span>
        </div>
        <div className="total">
          <span className="l">مبلغِ قابلِ پرداخت</span>
          <span className="v">{money(grandTotal)}</span>
        </div>
      </div>
      <div className="gbox">
        <div className="gh">توضیحاتِ فاکتور</div>
        <div className="gb">
          <textarea
            className="input-c"
            style={{ height: 60, padding: '7px 9px', resize: 'none' }}
            placeholder="یادداشتِ داخلی…"
            value={notes}
            onChange={(e) => onNotesChange(e.target.value)}
          />
        </div>
      </div>
    </div>
  );
}
