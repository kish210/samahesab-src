import { useEffect, useRef, useState } from 'react';
import { apiGet, apiUpload, apiDelete, ApiError } from '../api/client';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface ModuleRow {
  key: string;
  displayName: string;
  version: string;
  source: string;
}

interface InstallResult {
  installed: boolean;
  key: string;
  restartRequired: boolean;
  message: string;
}

export function ModulesPage() {
  const [rows, setRows] = useState<ModuleRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [msg, setMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  async function load() {
    setLoading(true);
    try {
      setRows(await apiGet<ModuleRow[]>('/api/modules'));
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فهرستِ ماژول‌ها.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    load();
  }, []);

  async function onFileChosen(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setMsg(null);
    if (!file.name.toLowerCase().endsWith('.mspkg')) {
      setMsg({ kind: 'error', text: 'فقط فایلِ .mspkg پذیرفته می‌شود.' });
      if (fileRef.current) fileRef.current.value = '';
      return;
    }
    setUploading(true);
    try {
      const res = await apiUpload<InstallResult>('/api/modules/install', file);
      setMsg({ kind: 'success', text: res.message });
      await load();
    } catch (err) {
      setMsg({ kind: 'error', text: err instanceof ApiError ? err.message : 'نصبِ ماژول ناموفق بود.' });
    } finally {
      setUploading(false);
      if (fileRef.current) fileRef.current.value = '';
    }
  }

  async function remove(key: string) {
    setMsg(null);
    try {
      const res = await apiDelete<{ message: string }>(`/api/modules/${encodeURIComponent(key)}`);
      setMsg({ kind: 'success', text: res.message });
      await load();
    } catch (err) {
      setMsg({ kind: 'error', text: err instanceof ApiError ? err.message : 'حذفِ ماژول ناموفق بود.' });
    }
  }

  const columns: Column<ModuleRow>[] = [
    { key: 'name', header: 'ماژول', render: (r) => r.displayName },
    { key: 'key', header: 'کلید', render: (r) => <span style={{ direction: 'ltr', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>{r.key}</span> },
    { key: 'version', header: 'نسخه', render: (r) => <span style={{ direction: 'ltr' }}>{r.version}</span> },
    {
      key: 'source', header: 'وضعیت',
      render: (r) => (
        <span className={`badge ${r.source.includes('ری‌استارت') ? 'badge-amber' : 'badge-green'}`}>{r.source}</span>
      ),
    },
    {
      key: 'action', header: '',
      render: (r) =>
        r.source.includes('ری‌استارت') ? (
          <button className="btn btn-ghost btn-sm" onClick={() => remove(r.key)}>
            حذف
          </button>
        ) : null,
    },
  ];

  return (
    <div>
      <PageHeader
        title="مدیریتِ ماژول‌ها"
        actions={
          <>
            <input ref={fileRef} type="file" accept=".mspkg" style={{ display: 'none' }} onChange={onFileChosen} />
            <button className="btn btn-primary btn-sm" disabled={uploading} onClick={() => fileRef.current?.click()}>
              {uploading ? 'در حالِ نصب…' : 'نصبِ ماژول از فایل'}
            </button>
          </>
        }
      />

      <p style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)', marginBottom: 'var(--space-4)' }}>
        فایلِ <span style={{ direction: 'ltr' }}>.mspkg</span> ماژول را انتخاب کنید تا رویِ سرور نصب شود.
        ماژولِ تازه‌نصب‌شده با <b>یک‌بار ری‌استارتِ سرور</b> فعال می‌شود.
      </p>

      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {msg && <StatusMessage kind={msg.kind}>{msg.text}</StatusMessage>}
      {loading && !error && <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>}
      {!loading && !error && <DataTable columns={columns} rows={rows} rowKey={(r) => r.key} emptyText="ماژولی یافت نشد." />}
    </div>
  );
}
