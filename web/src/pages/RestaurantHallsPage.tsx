import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface TableDto {
  id: number;
  hallId: number;
  name: string;
  capacity: number;
  status: string;
  statusCode: number;
  currentOrderId: number | null;
  positionX: number;
  positionY: number;
  occupiedSince: string | null;
}
interface HallDto {
  id: number;
  name: string;
  displayOrder: number;
  tables: TableDto[];
}

const STATUS_COLOR: Record<number, string> = {
  0: 'var(--success-50)',
  1: 'var(--danger-50)',
  2: 'var(--warning-50, #fffbeb)',
  3: 'var(--blue-50)',
};
const STATUS_BORDER: Record<number, string> = {
  0: 'var(--success-500)',
  1: 'var(--danger-500)',
  2: 'var(--warning-700, #b58a3c)',
  3: 'var(--blue-500)',
};

/** میزهایِ رستوران — نقشهٔ سالن‌ها، رنگ‌بندی بر اساسِ وضعیت. معادلِ وب برایِ جریانِ گارسونِ دسکتاپ. */
export function RestaurantHallsPage() {
  const navigate = useNavigate();
  const [halls, setHalls] = useState<HallDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  function load() {
    apiGet<HallDto[]>('/api/restaurant/halls').then(setHalls).catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ سالن‌ها.'));
  }

  useEffect(load, []);

  async function openTable(t: TableDto) {
    if (t.currentOrderId) {
      navigate(`/restaurant/orders/${t.currentOrderId}`);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const res = await apiPost<{ orderId: number }>('/api/restaurant/orders/open', {
        orderType: 0, tableId: t.id, guestCount: 1,
      });
      navigate(`/restaurant/orders/${res.orderId}`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'بازکردنِ میز ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function openTakeaway(orderType: 1 | 2) {
    setBusy(true);
    setError(null);
    try {
      const res = await apiPost<{ orderId: number }>('/api/restaurant/orders/open', {
        orderType, guestCount: 1,
      });
      navigate(`/restaurant/orders/${res.orderId}`);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ثبتِ سفارش ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <PageHeader title="سالن‌ها و میزها" actions={
        <>
          <button className="btn btn-secondary" disabled={busy} onClick={() => openTakeaway(1)}>+ سفارشِ بیرون‌بر</button>
          <button className="btn btn-secondary" disabled={busy} onClick={() => openTakeaway(2)}>+ سفارشِ پیک</button>
        </>
      } />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {halls === null ? (
        <div style={{ color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>
      ) : halls.length === 0 ? (
        <div style={{ color: 'var(--text-muted)' }}>سالنی تعریف نشده است.</div>
      ) : (
        halls.map((hall) => (
          <div key={hall.id} style={{ marginBottom: 'var(--space-5)' }}>
            <h3 style={{ marginBottom: 'var(--space-3)' }}>{hall.name}</h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(120px, 1fr))', gap: 10 }}>
              {hall.tables.map((t) => (
                <button
                  key={t.id}
                  type="button"
                  disabled={busy}
                  onClick={() => openTable(t)}
                  style={{
                    background: STATUS_COLOR[t.statusCode] ?? 'var(--bg-surface)',
                    border: `2px solid ${STATUS_BORDER[t.statusCode] ?? 'var(--border)'}`,
                    borderRadius: 'var(--radius-md)', padding: 12, cursor: 'pointer', textAlign: 'center',
                  }}
                >
                  <div style={{ fontWeight: 700, fontSize: 14 }}>{t.name}</div>
                  <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 2 }}>{t.capacity} نفره</div>
                  <div style={{ fontSize: 11.5, fontWeight: 600, marginTop: 4 }}>{t.status}</div>
                </button>
              ))}
            </div>
          </div>
        ))
      )}
    </div>
  );
}
