import { useRef } from 'react';
import { SearchSelect } from './SearchSelect';
import { money } from '../lib/format';

export interface ProductOption {
  id: number;
  code: string;
  name: string;
  salePrice: number;
  purchasePrice: number;
}

export interface InvoiceLine {
  productId: number | null;
  quantity: string;
  unitPrice: string;
  discountPct: string;
  taxPct: string;
}

export function emptyLine(): InvoiceLine {
  return { productId: null, quantity: '1', unitPrice: '0', discountPct: '0', taxPct: '0' };
}

function lineTotal(line: InvoiceLine): number {
  const qty = Number(line.quantity) || 0;
  const price = Number(line.unitPrice) || 0;
  const disc = Number(line.discountPct) || 0;
  const tax = Number(line.taxPct) || 0;
  const sub = qty * price;
  const afterDisc = sub - (sub * disc) / 100;
  return afterDisc + (afterDisc * tax) / 100;
}

interface Props {
  products: ProductOption[];
  lines: InvoiceLine[];
  onChange: (lines: InvoiceLine[]) => void;
  priceField: 'salePrice' | 'purchasePrice';
  /** فرمِ مرجوعی تخفیفِ ردیف ندارد (Commandِ سرور فقط productId/quantity/unitPrice/taxPct می‌گیرد) —
   * ستونِ تخفیف پنهان می‌شود تا فیلدی که نادیده گرفته می‌شود به کاربر نشان داده نشود. */
  hideDiscount?: boolean;
}

export function InvoiceLineEditor({ products, lines, onChange, priceField, hideDiscount = false }: Props) {
  const tableRef = useRef<HTMLTableElement>(null);

  function updateLine(index: number, patch: Partial<InvoiceLine>) {
    const next = lines.slice();
    next[index] = { ...next[index], ...patch };
    onChange(next);
  }

  function addLine() {
    onChange([...lines, emptyLine()]);
  }

  function removeLine(index: number) {
    onChange(lines.filter((_, i) => i !== index));
  }

  /** هم‌الگو با DataGridQuickEntryHelperِ دسکتاپ — Enter در آخرین ستونِ آخرین ردیف
   * ردیفِ خالیِ نو می‌سازد و فوکوس را به کمبویِ کالایِ همان ردیف می‌برد. */
  function handleLastCellKeyDown(e: React.KeyboardEvent<HTMLInputElement>, rowIndex: number) {
    if (e.key !== 'Enter' || rowIndex !== lines.length - 1) return;
    e.preventDefault();
    addLine();
    requestAnimationFrame(() => {
      const rows = tableRef.current?.querySelectorAll('tbody tr');
      const lastRow = rows?.[rows.length - 1];
      lastRow?.querySelector<HTMLInputElement>('input')?.focus();
    });
  }

  const grandTotal = lines.reduce((sum, l) => sum + lineTotal(l), 0);

  return (
    <div>
      {/* پورتِ `.dgrid-wrap`/`table.dgrid`ِ design-system — همان کلاس‌هایِ گریدِ فشردهٔ فاکتورِ فروش/خرید. */}
      <div className="dgrid-wrap">
        <table ref={tableRef} className="dgrid">
          <thead>
            <tr>
              <th>کالا</th>
              <th className="num">تعداد</th>
              <th className="num">قیمتِ واحد</th>
              {!hideDiscount && <th className="num">تخفیف٪</th>}
              <th className="num">مالیات٪</th>
              <th className="num">جمع</th>
              <th style={{ width: 36 }} className="c" />
            </tr>
          </thead>
          <tbody>
            {lines.map((line, i) => (
              <tr key={i}>
                <td style={{ minWidth: 220 }}>
                  <SearchSelect
                    options={products.map((p) => ({ id: p.id, label: p.name, sublabel: p.code }))}
                    value={line.productId}
                    onChange={(id) => {
                      const p = products.find((x) => x.id === id);
                      updateLine(i, { productId: id, unitPrice: p ? String(p[priceField]) : line.unitPrice });
                    }}
                    placeholder="جست‌وجویِ کالا…"
                  />
                </td>
                <td className="num">
                  <input className="input input-sm" type="number" min="0" step="any" value={line.quantity} onChange={(e) => updateLine(i, { quantity: e.target.value })} />
                </td>
                <td className="num">
                  <input className="input input-sm" type="number" min="0" step="any" value={line.unitPrice} onChange={(e) => updateLine(i, { unitPrice: e.target.value })} />
                </td>
                {!hideDiscount && (
                  <td className="num">
                    <input className="input input-sm" type="number" min="0" max="100" step="any" value={line.discountPct} onChange={(e) => updateLine(i, { discountPct: e.target.value })} />
                  </td>
                )}
                <td className="num">
                  <input
                    className="input input-sm" type="number" min="0" max="100" step="any"
                    value={line.taxPct}
                    onChange={(e) => updateLine(i, { taxPct: e.target.value })}
                    onKeyDown={(e) => handleLastCellKeyDown(e, i)}
                  />
                </td>
                <td className="num strong">{money(lineTotal(line))}</td>
                <td className="c">
                  <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeLine(i)}>
                    ✕
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={hideDiscount ? 4 : 5}>جمع — {lines.length} ردیف</td>
              <td className="num">{money(grandTotal)}</td>
              <td />
            </tr>
          </tfoot>
        </table>
      </div>

      <div style={{ marginTop: 'var(--space-2)' }}>
        <button type="button" className="btn btn-secondary btn-sm" onClick={addLine}>
          + افزودنِ ردیف
        </button>
      </div>
    </div>
  );
}
