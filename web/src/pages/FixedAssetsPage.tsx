import { useEffect, useState } from 'react';
import { apiGet, apiPost, apiPut, ApiError } from '../api/client';
import { money } from '../lib/format';
import { todayJalaliString } from '../lib/jalali';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { JalaliDateInput } from '../components/JalaliDateInput';

interface FixedAssetRow {
  id: number;
  code: string;
  name: string;
  purchaseDate: string;
  purchaseCost: number;
  salvageValue: number;
  usefulLifeMonths: number;
  method: number;
  isActive: boolean;
  accumulatedDepreciation: number;
  bookValue: number;
  monthlyDepreciation: number;
  isFullyDepreciated: boolean;
  description: string | null;
}

interface Draft {
  id: number | null;
  code: string;
  name: string;
  purchaseDate: string;
  purchaseCost: string;
  salvageValue: string;
  usefulLifeMonths: string;
  method: number;
}

const EMPTY_DRAFT: Draft = {
  id: null, code: '', name: '', purchaseDate: todayJalaliString(),
  purchaseCost: '0', salvageValue: '0', usefulLifeMonths: '60', method: 0,
};

function methodLabel(m: number) { return m === 0 ? 'خطِ مستقیم' : 'نزولی'; }

/**
 * U-FIXED-ASSET — داراییِ ثابت و استهلاک (هم‌راستا با «نرم‌افزار دارایی ثابتِ» راهکاران):
 * ثبتِ بهایِ تمام‌شده/عمر/اسقاط، محاسبهٔ خودکارِ استهلاکِ ماهانه و ارزشِ دفتری، و صدورِ سندِ
 * تجمیعیِ استهلاک (بدهکارِ 8-03 / بستانکارِ 2-06) برایِ دورهٔ دلخواه.
 */
export function FixedAssetsPage() {
  const [rows, setRows] = useState<FixedAssetRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [showForm, setShowForm] = useState(false);
  const [draft, setDraft] = useState<Draft>(EMPTY_DRAFT);
  const [saving, setSaving] = useState(false);
  const [period, setPeriod] = useState(todayJalaliString().slice(0, 7));
  const [running, setRunning] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  function load() {
    setLoading(true);
    apiGet<FixedAssetRow[]>('/api/fixed-assets')
      .then(setRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ دارایی‌ها.'))
      .finally(() => setLoading(false));
  }

  useEffect(load, []);

  function openCreate() { setDraft(EMPTY_DRAFT); setError(null); setNotice(null); setShowForm(true); }

  function openEdit(r: FixedAssetRow) {
    setDraft({
      id: r.id, code: r.code, name: r.name, purchaseDate: r.purchaseDate,
      purchaseCost: String(r.purchaseCost), salvageValue: String(r.salvageValue),
      usefulLifeMonths: String(r.usefulLifeMonths), method: r.method,
    });
    setError(null); setNotice(null); setShowForm(true);
  }

  async function save() {
    setError(null);
    if (!draft.code.trim() || !draft.name.trim()) { setError('کد و نامِ دارایی الزامی است.'); return; }
    if (Number(draft.purchaseCost) < 0 || Number(draft.salvageValue) < 0) { setError('مبالغ نمی‌توانند منفی باشند.'); return; }
    if (Number(draft.usefulLifeMonths) <= 0) { setError('عمرِ مفید باید بزرگ‌تر از صفر باشد.'); return; }
    setSaving(true);
    try {
      const body = {
        code: draft.code.trim(), name: draft.name.trim(), purchaseDate: draft.purchaseDate,
        purchaseCost: Number(draft.purchaseCost) || 0, salvageValue: Number(draft.salvageValue) || 0,
        usefulLifeMonths: Number(draft.usefulLifeMonths), method: draft.method,
        description: null,
      };
      if (draft.id == null) await apiPost('/api/fixed-assets', body);
      else await apiPut(`/api/fixed-assets/${draft.id}`, body);
      setShowForm(false);
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.');
    } finally {
      setSaving(false);
    }
  }

  async function toggleActive(r: FixedAssetRow) {
    try {
      await apiPost(`/api/fixed-assets/${r.id}/${r.isActive ? 'deactivate' : 'activate'}`);
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ وضعیت ناموفق بود.');
    }
  }

  async function runDepreciation() {
    setError(null); setNotice(null);
    if (!/^\d{4}\/\d{2}$/.test(period)) { setError('دوره باید به شکلِ yyyy/MM باشد (مثلاً 1405/05).'); return; }
    setRunning(true);
    try {
      const res = await apiPost<{ voucherId: number }>('/api/fixed-assets/depreciate', { periodMonth: period });
      setNotice(res.voucherId > 0
        ? `✅ سندِ استهلاکِ دورهٔ ${period} صادر شد (شناسهٔ سند: ${res.voucherId}).`
        : `برایِ دورهٔ ${period} داراییِ قابلِ استهلاکی نبود.`);
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'اجرایِ استهلاک ناموفق بود.');
    } finally {
      setRunning(false);
    }
  }

  const columns: Column<FixedAssetRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'نام', render: (r) => r.name },
    { key: 'purchaseCost', header: 'بهایِ تمام‌شده', numeric: true, render: (r) => money(r.purchaseCost) },
    { key: 'accumulated', header: 'استهلاکِ انباشته', numeric: true, render: (r) => money(r.accumulatedDepreciation) },
    { key: 'bookValue', header: 'ارزشِ دفتری', numeric: true, render: (r) => money(r.bookValue) },
    { key: 'monthly', header: 'استهلاکِ ماهانه', numeric: true, render: (r) => money(r.monthlyDepreciation) },
    { key: 'method', header: 'روش', render: (r) => methodLabel(r.method) },
    {
      key: 'status', header: 'وضعیت',
      render: (r) => (
        <span className={`badge ${!r.isActive ? 'badge-gray' : r.isFullyDepreciated ? 'badge-yellow' : 'badge-green'}`}>
          {!r.isActive ? 'غیرفعال' : r.isFullyDepreciated ? 'مستهلک‌شده' : 'فعال'}
        </span>
      ),
    },
    {
      key: 'action', header: '',
      render: (r) => (
        <div style={{ display: 'flex', gap: 6 }}>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => openEdit(r)}>ویرایش</button>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggleActive(r)}>
            {r.isActive ? 'غیرفعال‌سازی' : 'فعال‌سازی'}
          </button>
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader
        title="دارایی‌های ثابت"
        actions={<button type="button" className="btn btn-primary btn-sm" onClick={openCreate}>داراییِ نو</button>}
      />

      <div className="gbox" style={{ marginBottom: 'var(--space-4)' }}>
        <div className="gb" style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <div className="field" style={{ margin: 0 }}>
            <label className="label">دورهٔ استهلاک (yyyy/MM)</label>
            <input className="input" style={{ width: 130 }} value={period} onChange={(e) => setPeriod(e.target.value)} />
          </div>
          <button type="button" className="btn btn-secondary btn-sm" disabled={running} onClick={runDepreciation}>
            {running ? 'در حالِ اجرا…' : 'اجرایِ استهلاک و صدورِ سند'}
          </button>
        </div>
      </div>

      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}
      {error && <StatusMessage kind="error">{error}</StatusMessage>}

      {showForm && (
        <div className="gbox" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="gh">{draft.id == null ? 'داراییِ نو' : 'ویرایشِ دارایی'}</div>
          <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(200px, 1fr))', gap: 'var(--space-3)' }}>
              <div className="field">
                <label className="label">کد</label>
                <input className="input" value={draft.code} onChange={(e) => setDraft((p) => ({ ...p, code: e.target.value }))} />
              </div>
              <div className="field">
                <label className="label">نام</label>
                <input className="input" value={draft.name} onChange={(e) => setDraft((p) => ({ ...p, name: e.target.value }))} />
              </div>
              <JalaliDateInput label="تاریخِ خرید/بهره‌برداری" value={draft.purchaseDate} onChange={(v) => setDraft((p) => ({ ...p, purchaseDate: v }))} />
              <div className="field">
                <label className="label">عمرِ مفید (ماه)</label>
                <input className="input" type="number" min="1" value={draft.usefulLifeMonths} onChange={(e) => setDraft((p) => ({ ...p, usefulLifeMonths: e.target.value }))} />
              </div>
              <div className="field">
                <label className="label">بهایِ تمام‌شده (ریال)</label>
                <input className="input" type="number" min="0" value={draft.purchaseCost} onChange={(e) => setDraft((p) => ({ ...p, purchaseCost: e.target.value }))} />
              </div>
              <div className="field">
                <label className="label">ارزشِ اسقاط (ریال)</label>
                <input className="input" type="number" min="0" value={draft.salvageValue} onChange={(e) => setDraft((p) => ({ ...p, salvageValue: e.target.value }))} />
              </div>
              <div className="field">
                <label className="label">روشِ استهلاک</label>
                <select className="input" value={draft.method} onChange={(e) => setDraft((p) => ({ ...p, method: Number(e.target.value) }))}>
                  <option value={0}>خطِ مستقیم</option>
                  <option value={1}>نزولی (ماندهٔ کاهنده)</option>
                </select>
              </div>
            </div>
            <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
              <button type="button" className="btn btn-primary btn-sm" disabled={saving} onClick={save}>
                {saving ? 'در حالِ ذخیره…' : 'ذخیره'}
              </button>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setShowForm(false)}>انصراف</button>
            </div>
          </div>
        </div>
      )}

      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.id} emptyText="داراییِ ثابتی ثبت نشده است." />}
    </div>
  );
}
