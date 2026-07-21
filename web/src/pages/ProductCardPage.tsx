import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { DataTable, type Column } from '../components/DataTable';
import { StatusMessage } from '../components/PageHeader';

interface ProductCardStockRow {
  warehouseName: string;
  quantity: number;
  isLow: boolean;
}

interface KardexRow {
  date: string;
  type: string;
  documentNumber: string | null;
  in: number;
  out: number;
  balance: number;
  unitCost: number;
  notes: string | null;
}

interface ProductCardDto {
  id: number;
  code: string;
  name: string;
  barcode: string | null;
  isActive: boolean;
  purchasePrice: number;
  salePrice: number;
  wholesalePrice: number;
  consumerPrice: number;
  taxRate: number;
  minStock: number;
  maxStock: number | null;
  reorderPoint: number | null;
  tracking: string;
  totalStock: number;
  warehouseStocks: ProductCardStockRow[];
}

export function ProductCardPage() {
  const { id } = useParams();
  const [card, setCard] = useState<ProductCardDto | null>(null);
  const [kardex, setKardex] = useState<KardexRow[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    apiGet<ProductCardDto>(`/api/products/${id}/card`)
      .then(setCard)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ کارتِ کالا.'));
    apiGet<KardexRow[]>(`/api/products/${id}/kardex`).then(setKardex).catch(() => {});
  }, [id]);

  if (error) return <StatusMessage kind="error">{error}</StatusMessage>;
  if (!card) return <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>;

  const stockColumns: Column<ProductCardStockRow>[] = [
    { key: 'w', header: 'انبار', render: (r) => r.warehouseName },
    {
      key: 'q', header: 'موجودی', numeric: true,
      render: (r) => <span style={{ fontWeight: 600, color: r.isLow ? 'var(--danger-700)' : 'var(--text-strong)' }}>{money(r.quantity)}</span>,
    },
  ];

  return (
    <div>
      <Link to="/products" style={{ fontSize: 'var(--text-sm)' }}>
        ← بازگشت به فهرستِ کالاها
      </Link>

      <div style={{ display: 'flex', gap: 'var(--space-4)', marginTop: 'var(--space-4)', alignItems: 'flex-start' }}>
        <div style={{ width: 300, flex: 'none', background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 14 }}>
          <div style={{ fontSize: 16, fontWeight: 700, color: 'var(--text-strong)' }}>{card.name}</div>
          <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 10 }}>
            کد: {card.code} {card.barcode ? `· بارکد: ${card.barcode}` : ''}
          </div>
          <hr style={{ margin: '10px 0', border: 'none', borderTop: '1px solid var(--gray-100)' }} />
          {[
            ['قیمتِ خرید', money(card.purchasePrice)],
            ['قیمتِ فروش', money(card.salePrice)],
            ['قیمتِ عمده', money(card.wholesalePrice)],
            ['قیمتِ مصرف‌کننده', money(card.consumerPrice)],
            ['نرخِ مالیات', `${card.taxRate}٪`],
            ['روشِ ردیابی', card.tracking],
            ['حداقلِ موجودی', money(card.minStock)],
          ].map(([k, v]) => (
            <div key={k} style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--text-sm)', padding: '5px 0' }}>
              <span style={{ color: 'var(--text-muted)' }}>{k}</span>
              <span style={{ fontWeight: 500 }}>{v}</span>
            </div>
          ))}
        </div>

        <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 14px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>مجموعِ موجودی (همهٔ انبارها)</div>
            <div className="num" style={{ fontSize: 20, fontWeight: 800, marginTop: 3 }}>{money(card.totalStock)}</div>
          </div>
          <DataTable columns={stockColumns} rows={card.warehouseStocks} rowKey={(r, i) => `${r.warehouseName}-${i}`} emptyText="موجودی‌ای ثبت نشده." />

          {/* پورتِ گریدِ «کاردکس (گردشِ کالا)»ِ product-card.html — رویِ GetKardexQueryِ
              ازقبل‌موجود که هیچ اندپوینتی صدایش نمی‌زد. */}
          <div>
            <div style={{ fontSize: 12, fontWeight: 600, color: 'var(--text-strong)', marginBottom: 6 }}>کاردکس (گردشِ کالا)</div>
            <div className="dgrid-wrap">
              <table className="dgrid">
                <thead>
                  <tr>
                    <th style={{ width: 80 }}>تاریخ</th>
                    <th style={{ width: 95 }}>سند</th>
                    <th>شرح</th>
                    <th style={{ width: 70 }} className="num">ورود</th>
                    <th style={{ width: 70 }} className="num">خروج</th>
                    <th style={{ width: 70 }} className="num">مانده</th>
                    <th style={{ width: 110 }} className="num">فی</th>
                  </tr>
                </thead>
                <tbody>
                  {kardex.map((r, i) => (
                    <tr key={i}>
                      <td className="num mut">{r.date}</td>
                      <td className="num">{r.documentNumber ?? '—'}</td>
                      <td>{r.type}{r.notes && <div className="mut" style={{ fontSize: 10.5 }}>{r.notes}</div>}</td>
                      <td className="num" style={{ color: r.in > 0 ? 'var(--success-700)' : undefined }}>{r.in > 0 ? money(r.in) : '·'}</td>
                      <td className="num" style={{ color: r.out > 0 ? 'var(--danger-500)' : undefined }}>{r.out > 0 ? money(r.out) : '·'}</td>
                      <td className="num strong">{money(r.balance)}</td>
                      <td className="num mut">{r.unitCost > 0 ? money(r.unitCost) : '—'}</td>
                    </tr>
                  ))}
                  {kardex.length === 0 && (
                    <tr>
                      <td colSpan={7} style={{ height: 'auto', padding: 'var(--space-4)', textAlign: 'center', color: 'var(--text-muted)', whiteSpace: 'normal' }}>
                        گردشی ثبت نشده.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
