import { useEffect, useState } from 'react';
import { apiGet, apiPost, apiPut, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';

interface BranchRow {
  id: number;
  code: string;
  name: string;
  address: string | null;
  phone: string | null;
  managerName: string | null;
  isHQ: boolean;
  isActive: boolean;
}

const emptyForm = { id: 0, code: '', name: '', address: '', phone: '', managerName: '' };

/** U-WEB-BRANCHES — Application (GetBranchesQuery/SaveBranchCommand/ToggleBranchCommand) و
 * BranchesController از قبل کامل بودند؛ فقط صفحهٔ وب کم بود («خلاصهٔ شعب» فقط گزارشِ
 * فقط‌خواندنی است، نه مدیریتِ CRUDِ شعبه). */
export function BranchesPage() {
  const [branches, setBranches] = useState<BranchRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [showForm, setShowForm] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [saving, setSaving] = useState(false);

  function load() {
    apiGet<BranchRow[]>('/api/branches').then(setBranches)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ شعب.'));
  }
  useEffect(load, []);

  function startNew() {
    setForm(emptyForm);
    setShowForm(true);
    setNotice(null);
    setError(null);
  }

  function startEdit(b: BranchRow) {
    setForm({ id: b.id, code: b.code, name: b.name, address: b.address ?? '', phone: b.phone ?? '', managerName: b.managerName ?? '' });
    setShowForm(true);
    setNotice(null);
    setError(null);
  }

  async function save() {
    if (!form.code.trim() || !form.name.trim()) { setError('کد و نامِ شعبه الزامی است.'); return; }
    setSaving(true);
    setError(null);
    try {
      await apiPost('/api/branches', {
        id: form.id, code: form.code, name: form.name,
        address: form.address || null, phone: form.phone || null, managerName: form.managerName || null,
        isHQ: branches.find((b) => b.id === form.id)?.isHQ ?? false,
      });
      setNotice(form.id ? 'شعبه ویرایش شد.' : 'شعبه ایجاد شد.');
      setShowForm(false);
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.');
    } finally {
      setSaving(false);
    }
  }

  async function toggle(b: BranchRow) {
    try {
      await apiPut(`/api/branches/${b.id}/active/${!b.isActive}`, {});
      load();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'تغییرِ وضعیت ناموفق بود.');
    }
  }

  const columns: Column<BranchRow>[] = [
    { key: 'code', header: 'کد', render: (b) => b.code },
    { key: 'name', header: 'نام', render: (b) => <>{b.name} {b.isHQ && <span className="badge badge-gray">مرکزی</span>}</> },
    { key: 'address', header: 'آدرس', render: (b) => b.address ?? '—' },
    { key: 'phone', header: 'تلفن', render: (b) => b.phone ?? '—' },
    { key: 'manager', header: 'مدیر', render: (b) => b.managerName ?? '—' },
    { key: 'status', header: 'وضعیت', render: (b) => <span className={`badge ${b.isActive ? 'badge-green' : 'badge-yellow'}`}>{b.isActive ? 'فعال' : 'غیرفعال'}</span> },
    {
      key: 'actions', header: '', render: (b) => (
        <div style={{ display: 'flex', gap: 6 }}>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => startEdit(b)}>ویرایش</button>
          {!b.isHQ && (
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => toggle(b)}>
              {b.isActive ? 'غیرفعال کن' : 'فعال کن'}
            </button>
          )}
        </div>
      ),
    },
  ];

  return (
    <div>
      <PageHeader title="شعب" actions={<button className="btn btn-primary btn-sm" onClick={startNew}>شعبهٔ نو</button>} />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      {showForm && (
        <div className="gbox" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-4)', maxWidth: 640 }}>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)' }}>
            <div className="field">
              <label className="label">کدِ شعبه</label>
              <input className="input" value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} disabled={!!form.id} />
            </div>
            <div className="field">
              <label className="label">نامِ شعبه</label>
              <input className="input" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>
            <div className="field">
              <label className="label">آدرس</label>
              <input className="input" value={form.address} onChange={(e) => setForm({ ...form, address: e.target.value })} />
            </div>
            <div className="field">
              <label className="label">تلفن</label>
              <input className="input" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
            </div>
            <div className="field">
              <label className="label">مدیرِ شعبه</label>
              <input className="input" value={form.managerName} onChange={(e) => setForm({ ...form, managerName: e.target.value })} />
            </div>
          </div>
          <div style={{ marginTop: 'var(--space-3)', display: 'flex', gap: 'var(--space-2)' }}>
            <button type="button" className="btn btn-primary btn-sm" disabled={saving} onClick={save}>
              {saving ? 'در حالِ ذخیره…' : 'ذخیره'}
            </button>
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => setShowForm(false)}>انصراف</button>
          </div>
        </div>
      )}

      <DataTable columns={columns} rows={branches} rowKey={(b) => b.id} emptyText="شعبه‌ای ثبت نشده." />
    </div>
  );
}
