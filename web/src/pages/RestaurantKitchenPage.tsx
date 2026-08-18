import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { numberFormat } from '../lib/format';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { printThermal } from '../lib/thermalPrint';

interface KitchenItemDto {
  id: number;
  productName: string;
  quantity: number;
  status: string;
  notes: string | null;
}
interface KitchenTicketDto {
  id: number;
  ticketNumber: string;
  orderId: number;
  tableName: string | null;
  status: string;
  statusCode: number;
  createdAt: string;
  items: KitchenItemDto[];
}

const NEXT_STATUS: Record<number, { code: number; label: string } | null> = {
  0: { code: 1, label: 'شروعِ آماده‌سازی' },
  1: { code: 2, label: 'آماده شد' },
  2: { code: 3, label: 'تحویل داده شد' },
  3: null,
};
const STATUS_COLOR: Record<number, string> = {
  0: 'var(--gray-100)', 1: 'var(--warning-50, #fffbeb)', 2: 'var(--success-50)', 3: 'var(--gray-50)',
};

/** تابلویِ آشپزخانه (KDS) — رسیدهایِ فعال + پیشرفتِ وضعیت. */
export function RestaurantKitchenPage() {
  const [tickets, setTickets] = useState<KitchenTicketDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  function load() {
    apiGet<KitchenTicketDto[]>('/api/restaurant/kitchen').then(setTickets).catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ تابلویِ آشپزخانه.'));
  }

  useEffect(() => {
    load();
    const t = setInterval(load, 15000);
    return () => clearInterval(t);
  }, []);

  /** چاپِ حرارتیِ رسیدِ آشپزخانه (KOT) — فقط همان رسید در پنجرهٔ ۸۰mm. */
  function printTicket(t: KitchenTicketDto) {
    printThermal({
      title: `رسیدِ آشپزخانه ${t.ticketNumber}`,
      header: [
        { label: 'میز', value: t.tableName ?? 'بیرون‌بر/پیک' },
        { label: 'وضعیت', value: t.status },
        { label: 'زمان', value: new Date(t.createdAt).toLocaleTimeString('fa-IR') },
      ],
      items: t.items.map((it) => ({
        name: it.productName,
        qty: `تعداد: ${numberFormat.format(it.quantity)}`,
        amount: '',
        note: it.notes ?? undefined,
      })),
      footer: ['— KOT —'],
    });
  }

  async function advance(ticket: KitchenTicketDto) {
    const next = NEXT_STATUS[ticket.statusCode];
    if (!next) return;
    setBusyId(ticket.id);
    setError(null);
    try {
      await apiPost(`/api/restaurant/kitchen/${ticket.id}/status/${next.code}`, undefined);
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ وضعیت ناموفق بود.');
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div>
      <PageHeader title="تابلویِ آشپزخانه" actions={
        <button className="btn btn-secondary btn-sm" onClick={load}>🔄 بازخوانی</button>
      } />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {tickets === null ? (
        <div style={{ color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>
      ) : tickets.length === 0 ? (
        <div style={{ color: 'var(--text-muted)' }}>رسیدِ فعالی نیست.</div>
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(240px, 1fr))', gap: 12 }}>
          {tickets.map((t) => {
            const next = NEXT_STATUS[t.statusCode];
            return (
              <div key={t.id} style={{ background: STATUS_COLOR[t.statusCode], border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 12 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
                  <span style={{ fontWeight: 700 }}>{t.ticketNumber}</span>
                  <span className="badge badge-blue">{t.status}</span>
                </div>
                <div style={{ fontSize: 12, color: 'var(--text-muted)', marginBottom: 8 }}>
                  {t.tableName ?? 'بیرون‌بر/پیک'}
                </div>
                <ul style={{ margin: 0, padding: 0, listStyle: 'none', marginBottom: 10 }}>
                  {t.items.map((it) => (
                    <li key={it.id} style={{ fontSize: 13, padding: '3px 0', borderBottom: '1px solid rgba(0,0,0,.06)' }}>
                      <span className="num">{numberFormat.format(it.quantity)}×</span> {it.productName}
                      {it.notes && <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>{it.notes}</div>}
                    </li>
                  ))}
                </ul>
                <div style={{ display: 'flex', gap: 6 }}>
                  <button type="button" className="btn btn-secondary btn-sm no-print" onClick={() => printTicket(t)}>🖨 چاپ</button>
                  {next && (
                    <button type="button" className="btn btn-primary btn-sm" disabled={busyId === t.id} style={{ flex: 1 }}
                      onClick={() => advance(t)}>
                      {busyId === t.id ? '...' : next.label}
                    </button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
