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
      <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', overflow: 'auto' }}>
        <table ref={tableRef} style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ background: 'var(--gray-50)', borderBottom: '1px solid var(--border)' }}>
              {['کالا', 'تعداد', 'قیمتِ واحد', ...(hideDiscount ? [] : ['تخفیف٪']), 'مالیات٪', 'جمع', ''].map((h) => (
                <th key={h} style={{ padding: '8px 10px', fontSize: 'var(--text-sm)', color: 'var(--text-muted)', textAlign: 'start' }}>
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {lines.map((line, i) => (
              <tr key={i} style={{ borderBottom: '1px solid var(--gray-100)' }}>
                <td style={{ padding: '6px 10px', minWidth: 220 }}>
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
                <td style={{ padding: '6px 10px', width: 90 }}>
                  <input className="input input-sm" type="number" min="0" step="any" value={line.quantity} onChange={(e) => updateLine(i, { quantity: e.target.value })} />
                </td>
                <td style={{ padding: '6px 10px', width: 130 }}>
                  <input className="input input-sm" type="number" min="0" step="any" value={line.unitPrice} onChange={(e) => updateLine(i, { unitPrice: e.target.value })} />
                </td>
                {!hideDiscount && (
                  <td style={{ padding: '6px 10px', width: 80 }}>
                    <input className="input input-sm" type="number" min="0" max="100" step="any" value={line.discountPct} onChange={(e) => updateLine(i, { discountPct: e.target.value })} />
                  </td>
                )}
                <td style={{ padding: '6px 10px', width: 80 }}>
                  <input
                    className="input input-sm" type="number" min="0" max="100" step="any"
                    value={line.taxPct}
                    onChange={(e) => updateLine(i, { taxPct: e.target.value })}
                    onKeyDown={(e) => handleLastCellKeyDown(e, i)}
                  />
                </td>
                <td className="num" style={{ padding: '6px 10px', fontWeight: 600, whiteSpace: 'nowrap' }}>{money(lineTotal(line))}</td>
                <td style={{ padding: '6px 10px' }}>
                  <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeLine(i)}>
                    حذف
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: 'var(--space-2)' }}>
        <button type="button" className="btn btn-secondary btn-sm" onClick={addLine}>
          + افزودنِ ردیف
        </button>
        <div style={{ fontSize: 'var(--text-md)', fontWeight: 700 }}>جمعِ کل: {money(grandTotal)} ریال</div>
      </div>
    </div>
  );
}
