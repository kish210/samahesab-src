import { useEffect, useMemo, useState } from 'react';
import { apiGet, apiPost, apiDelete, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface AccountDto {
  id: number;
  code: string;
  name: string;
  level: number;
  nature: string;
  accountType: string | null;
  parentId: number | null;
  isLeaf: boolean;
  isActive: boolean;
}

interface AccountNode extends AccountDto {
  children: AccountNode[];
}

const LEVEL_LABELS = ['', 'گروه', 'کل', 'معین', 'تفصیلی'];
const NATURE_OPTIONS = ['بدهکار', 'بستانکار'];
const TYPE_OPTIONS = ['دارایی', 'بدهی', 'سرمایه', 'درآمد', 'هزینه'];

/** سرور Natureِ اِنامِ خام («Debit»/«Credit») برمی‌گرداند، نه برچسبِ فارسی؛ اینجا فقط برایِ نمایش ترجمه می‌شود. */
function natureLabel(nature: string): string {
  return nature === 'Credit' || nature === 'بستانکار' ? 'بستانکار' : 'بدهکار';
}
function isCredit(nature: string): boolean {
  return nature === 'Credit' || nature === 'بستانکار';
}

function buildTree(flat: AccountDto[]): AccountNode[] {
  const nodes = new Map<number, AccountNode>(flat.map((a) => [a.id, { ...a, children: [] }]));
  const roots: AccountNode[] = [];
  for (const node of nodes.values()) {
    if (node.parentId && nodes.has(node.parentId)) {
      nodes.get(node.parentId)!.children.push(node);
    } else {
      roots.push(node);
    }
  }
  return roots;
}

interface FormState {
  id: number | null;
  code: string;
  name: string;
  nature: string;
  accountType: string;
  parentId: number | null;
  description: string;
}

function emptyForm(parentId: number | null, nature: string): FormState {
  return { id: null, code: '', name: '', nature, accountType: TYPE_OPTIONS[0], parentId, description: '' };
}

/** دفترِ حساب‌ها — درختِ سرفصلِ حسابداری + ساخت/ویرایش/حذف، متصل به CRUDِ ازقبل‌موجودِ AccountsController
 * که تا این نشست هیچ صفحه‌ای در وب نداشت (فقط جست‌وجویِ داخلِ فرمِ سند از آن استفاده می‌کرد). */
export function AccountsPage() {
  const [accounts, setAccounts] = useState<AccountDto[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<FormState | null>(null);
  const [expanded, setExpanded] = useState<Set<number>>(new Set());
  const [submitting, setSubmitting] = useState(false);

  async function load() {
    try {
      const data = await apiGet<AccountDto[]>('/api/accounts');
      setAccounts(data);
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ دفترِ حساب‌ها.');
    }
  }

  useEffect(() => {
    load();
  }, []);

  const tree = useMemo(() => buildTree(accounts ?? []), [accounts]);
  const byId = useMemo(() => new Map((accounts ?? []).map((a) => [a.id, a])), [accounts]);

  function toggle(id: number) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function startAddChild(parent: AccountDto | null) {
    setForm(emptyForm(parent?.id ?? null, parent ? natureLabel(parent.nature) : 'بدهکار'));
  }

  function startEdit(a: AccountDto) {
    setForm({ id: a.id, code: a.code, name: a.name, nature: natureLabel(a.nature), accountType: a.accountType ?? TYPE_OPTIONS[0], parentId: a.parentId, description: '' });
  }

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    if (!form) return;
    setError(null);
    if (!form.code.trim() || !form.name.trim()) {
      setError('کد و نامِ حساب الزامی است.');
      return;
    }
    setSubmitting(true);
    try {
      await apiPost<{ accountId: number }>('/api/accounts', {
        id: form.id,
        code: form.code,
        name: form.name,
        nature: form.nature,
        accountType: form.accountType,
        parentId: form.parentId,
        description: form.description || null,
      });
      setForm(null);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'ذخیرهٔ حساب ناموفق بود.');
    } finally {
      setSubmitting(false);
    }
  }

  async function remove(a: AccountDto) {
    if (!confirm(`حذفِ حسابِ «${a.name}» (${a.code})؟`)) return;
    setError(null);
    try {
      await apiDelete(`/api/accounts/${a.id}`);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'حذفِ حساب ناموفق بود — ممکن است تراکنش یا زیرحساب داشته باشد.');
    }
  }

  function Row({ node, depth }: { node: AccountNode; depth: number }) {
    const hasChildren = node.children.length > 0;
    const isOpen = expanded.has(node.id);
    return (
      <>
        <div style={{
          display: 'flex', alignItems: 'center', gap: 8, padding: '6px 8px', borderBottom: '1px solid var(--border)',
          paddingInlineStart: depth * 20 + 8,
        }}>
          <button type="button" onClick={() => hasChildren && toggle(node.id)}
            style={{ width: 18, background: 'transparent', border: 'none', cursor: hasChildren ? 'pointer' : 'default', color: 'var(--text-muted)' }}>
            {hasChildren ? (isOpen ? '▾' : '▸') : ''}
          </button>
          <span className="num" style={{ width: 90, color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>{node.code}</span>
          <span style={{ flex: 1, fontWeight: node.isLeaf ? 400 : 600 }}>{node.name}</span>
          <span style={{ fontSize: 'var(--text-xs)', color: 'var(--text-muted)' }}>{LEVEL_LABELS[node.level] ?? node.level}</span>
          <span className={`badge ${isCredit(node.nature) ? 'badge-gray' : 'badge-blue'}`} style={{ fontSize: 'var(--text-xs)' }}>{natureLabel(node.nature)}</span>
          {!node.isActive && <span className="badge badge-gray">غیرفعال</span>}
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => startEdit(node)}>ویرایش</button>
          {!node.isLeaf && <button type="button" className="btn btn-ghost btn-sm" onClick={() => startAddChild(node)}>+ زیرحساب</button>}
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => remove(node)}>حذف</button>
        </div>
        {isOpen && node.children.map((c) => <Row key={c.id} node={c} depth={depth + 1} />)}
      </>
    );
  }

  const parentAccount = form?.parentId ? byId.get(form.parentId) : null;

  return (
    <div>
      <PageHeader title="دفترِ حساب‌ها" actions={
        <button className="btn btn-primary" onClick={() => startAddChild(null)}>+ حسابِ ریشه</button>
      } />

      {error && <div style={{ marginBottom: 'var(--space-3)' }}><StatusMessage kind="error">{error}</StatusMessage></div>}

      <div style={{ display: 'grid', gridTemplateColumns: form ? 'minmax(0,1fr) 320px' : '1fr', gap: 'var(--space-4)', alignItems: 'start' }}>
        <div style={{ border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', background: 'var(--bg-surface)' }}>
          {accounts === null ? (
            <div style={{ padding: 'var(--space-4)', color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>
          ) : tree.length === 0 ? (
            <div style={{ padding: 'var(--space-4)', color: 'var(--text-muted)' }}>حسابی یافت نشد.</div>
          ) : (
            tree.map((n) => <Row key={n.id} node={n} depth={0} />)
          )}
        </div>

        {form && (
          <form onSubmit={submit} style={{
            border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', background: 'var(--bg-surface)',
            padding: 'var(--space-4)', display: 'flex', flexDirection: 'column', gap: 'var(--space-3)',
          }}>
            <h3 style={{ margin: 0 }}>{form.id ? 'ویرایشِ حساب' : 'حسابِ نو'}</h3>
            {parentAccount && (
              <div className="field">
                <label className="label">حسابِ پدر</label>
                <input className="input" value={`${parentAccount.name} (${parentAccount.code})`} disabled />
              </div>
            )}
            <div className="field">
              <label className="label">کد</label>
              <input className="input num" value={form.code} disabled={!!form.id}
                onChange={(e) => setForm({ ...form, code: e.target.value })} placeholder="مثلاً 1-01-002" />
            </div>
            <div className="field">
              <label className="label">نام</label>
              <input className="input" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>
            <div className="field">
              <label className="label">ماهیت</label>
              <select className="select" value={form.nature} onChange={(e) => setForm({ ...form, nature: e.target.value })}>
                {NATURE_OPTIONS.map((n) => <option key={n} value={n}>{n}</option>)}
              </select>
            </div>
            <div className="field">
              <label className="label">نوعِ حساب</label>
              <select className="select" value={form.accountType} disabled={!!form.id}
                onChange={(e) => setForm({ ...form, accountType: e.target.value })}>
                {TYPE_OPTIONS.map((t) => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
            <div className="field">
              <label className="label">توضیحات</label>
              <input className="input" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            </div>
            <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
              <button type="submit" className="btn btn-primary" disabled={submitting}>
                {submitting ? 'در حالِ ذخیره…' : 'ذخیره'}
              </button>
              <button type="button" className="btn btn-secondary" onClick={() => setForm(null)}>انصراف</button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
