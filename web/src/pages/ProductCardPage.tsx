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
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    apiGet<ProductCardDto>(`/api/products/${id}/card`)
      .then(setCard)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ کارتِ کالا.'));
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

        <div style={{ flex: 1, minWidth: 0 }}>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 14px', marginBottom: 'var(--space-3)' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>مجموعِ موجودی (همهٔ انبارها)</div>
            <div className="num" style={{ fontSize: 20, fontWeight: 800, marginTop: 3 }}>{money(card.totalStock)}</div>
          </div>
          <DataTable columns={stockColumns} rows={card.warehouseStocks} rowKey={(r, i) => `${r.warehouseName}-${i}`} emptyText="موجودی‌ای ثبت نشده." />
        </div>
      </div>
    </div>
  );
}
