import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { money } from '../lib/format';

interface ShiftSummary {
  id: number;
  openingFloat: number;
  cashSales: number;
  cardSales: number;
  salesCount: number;
  expectedCash: number;
  countedCash: number;
  variance: number;
  varianceVoucherId: number | null;
}

/** U-WEB-SHIFTS — Application (Open/RecordSale/CloseShiftCommand + GetOpenShiftQuery) و
 * ShiftsController از قبل کامل بودند؛ فقط صفحهٔ وب کم بود (هم‌الگو با ShiftViewModel/
 * ShiftView.xamlِ دسکتاپ — گزارشِ X زنده + بستنِ شیفت با گزارشِ Z). */
export function ShiftPage() {
  const [shift, setShift] = useState<ShiftSummary | null>(null);
  const [loaded, setLoaded] = useState(false);
  const [newFloat, setNewFloat] = useState(0);
  const [countedCash, setCountedCash] = useState(0);
  const [closedSummary, setClosedSummary] = useState<ShiftSummary | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function refresh() {
    try {
      const s = await apiGet<ShiftSummary | null>('/api/shifts/current');
      setShift(s);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ وضعیتِ شیفت.');
    } finally {
      setLoaded(true);
    }
  }
  useEffect(() => { refresh(); }, []);

  async function openShift() {
    setBusy(true);
    setError(null);
    try {
      await apiPost('/api/shifts/open', { openingFloat: newFloat });
      setClosedSummary(null);
      setNotice('صندوق باز شد.');
      setNewFloat(0);
      await refresh();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'باز کردنِ صندوق ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function closeShift() {
    setBusy(true);
    setError(null);
    try {
      const summary = await apiPost<ShiftSummary>('/api/shifts/close', { countedCash });
      setClosedSummary(summary);
      setShift(null);
      setCountedCash(0);
      setNotice(null);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'بستنِ صندوق ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  if (!loaded) return <div><PageHeader title="شیفتِ صندوق" /></div>;

  return (
    <div>
      <PageHeader title="شیفتِ صندوق" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      {!shift && !closedSummary && (
        <div className="gbox" style={{ padding: 'var(--space-4)', maxWidth: 420 }}>
          <div className="gh">بازکردنِ صندوق</div>
          <div className="field">
            <label className="label">تنخواهِ ابتدایِ شیفت</label>
            <input className="input" type="number" value={newFloat}
              onChange={(e) => setNewFloat(Number(e.target.value))} style={{ direction: 'ltr' }} />
          </div>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={openShift}>
              {busy ? 'در حالِ بازکردن…' : 'بازکردنِ صندوق'}
            </button>
          </div>
        </div>
      )}

      {shift && (
        <div style={{ display: 'flex', gap: 'var(--space-4)', flexWrap: 'wrap' }}>
          <div className="gbox" style={{ padding: 'var(--space-4)', minWidth: 280 }}>
            <div className="gh">گزارشِ X (زنده) — شیفتِ #{shift.id}</div>
            <div className="sumbar" style={{ flexDirection: 'column', alignItems: 'stretch', gap: 6 }}>
              <div className="s"><span className="l">تنخواهِ ابتدا</span><span className="v">{money(shift.openingFloat)}</span></div>
              <div className="s"><span className="l">فروشِ نقدی</span><span className="v">{money(shift.cashSales)}</span></div>
              <div className="s"><span className="l">فروشِ کارتی</span><span className="v">{money(shift.cardSales)}</span></div>
              <div className="s"><span className="l">تعدادِ فروش</span><span className="v">{shift.salesCount}</span></div>
              <div className="s"><span className="l">نقدِ موردانتظار</span><span className="v strong">{money(shift.expectedCash)}</span></div>
            </div>
          </div>

          <div className="gbox" style={{ padding: 'var(--space-4)', minWidth: 280 }}>
            <div className="gh">بستنِ صندوق (Z)</div>
            <div className="field">
              <label className="label">نقدِ شمرده‌شده</label>
              <input className="input" type="number" value={countedCash}
                onChange={(e) => setCountedCash(Number(e.target.value))} style={{ direction: 'ltr' }} />
            </div>
            <div style={{ marginTop: 'var(--space-3)' }}>
              <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={closeShift}>
                {busy ? 'در حالِ بستن…' : 'بستنِ صندوق'}
              </button>
            </div>
          </div>
        </div>
      )}

      {closedSummary && (
        <div className="gbox" style={{ padding: 'var(--space-4)', maxWidth: 480 }}>
          <div className="gh">گزارشِ Z — شیفتِ #{closedSummary.id} بسته شد</div>
          <div className="sumbar" style={{ flexDirection: 'column', alignItems: 'stretch', gap: 6 }}>
            <div className="s"><span className="l">نقدِ موردانتظار</span><span className="v">{money(closedSummary.expectedCash)}</span></div>
            <div className="s"><span className="l">نقدِ شمرده‌شده</span><span className="v">{money(closedSummary.countedCash)}</span></div>
            <div className="s">
              <span className="l">مغایرت</span>
              <span className="v strong" style={{ color: closedSummary.variance === 0 ? undefined : 'var(--danger-700)' }}>
                {closedSummary.variance === 0 ? 'بدون مغایرت'
                  : closedSummary.variance > 0 ? `اضافه: ${money(closedSummary.variance)}` : `کسری: ${money(-closedSummary.variance)}`}
              </span>
            </div>
            {closedSummary.varianceVoucherId != null && (
              <div className="s"><span className="l">سندِ مغایرت</span><span className="v">#{closedSummary.varianceVoucherId}</span></div>
            )}
          </div>
          <div style={{ marginTop: 'var(--space-3)' }}>
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => setClosedSummary(null)}>بازکردنِ شیفتِ نو</button>
          </div>
        </div>
      )}
    </div>
  );
}
