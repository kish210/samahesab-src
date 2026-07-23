import { useEffect, useState } from 'react';
import { apiGet, apiPost, apiPut, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';

interface PermissionDef { code: string; module: string; label: string }
interface RoleRow { id: number; code: string; name: string; isSystem: boolean; isActive: boolean; permissions: string[] }
interface SecurityUserRow { id: number; username: string; fullName: string; isActive: boolean; roleIds: number[] }
interface AuditLogRow { id: number; when: string; user: string | null; action: string; tableName: string | null; details: string | null }

type Tab = 'roles' | 'users' | 'audit';

/** U-WEB-SECURITY — SecurityController/Application (کاتالوگِ مجوز، نقش‌ها، مجوزهایِ نقش،
 * کاربران، نقشِ کاربر، لاگِ حسابرسی) از قبل کامل بود؛ فقط صفحهٔ وب کم بود — حتی در دسکتاپ
 * طبقِ کامنتِ SettingsPage.tsx این بخش استاب بود. */
export function SecurityPage() {
  const [tab, setTab] = useState<Tab>('roles');
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  // ── نقش‌ها ──
  const [perms, setPerms] = useState<PermissionDef[]>([]);
  const [roles, setRoles] = useState<RoleRow[]>([]);
  const [selectedRole, setSelectedRole] = useState<RoleRow | null>(null);
  const [rolePerms, setRolePerms] = useState<Set<string>>(new Set());
  const [showNewRole, setShowNewRole] = useState(false);
  const [newRole, setNewRole] = useState({ code: '', name: '' });
  const [busy, setBusy] = useState(false);

  function loadRoles() {
    apiGet<RoleRow[]>('/api/security/roles').then(setRoles)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ نقش‌ها.'));
  }
  useEffect(() => {
    apiGet<PermissionDef[]>('/api/security/permissions').then(setPerms).catch(() => {});
    loadRoles();
  }, []);

  function selectRole(r: RoleRow) {
    setSelectedRole(r);
    setRolePerms(new Set(r.permissions));
    setNotice(null);
  }

  function togglePerm(code: string) {
    setRolePerms((prev) => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code); else next.add(code);
      return next;
    });
  }

  async function saveNewRole() {
    if (!newRole.code.trim() || !newRole.name.trim()) { setError('کد و نامِ نقش الزامی است.'); return; }
    setBusy(true);
    setError(null);
    try {
      await apiPost('/api/security/roles', { id: 0, code: newRole.code, name: newRole.name });
      setNotice('نقش ایجاد شد.');
      setShowNewRole(false);
      setNewRole({ code: '', name: '' });
      loadRoles();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ایجادِ نقش ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function saveRolePermissions() {
    if (!selectedRole) return;
    setBusy(true);
    setError(null);
    try {
      await apiPut(`/api/security/roles/${selectedRole.id}/permissions`, { codes: Array.from(rolePerms) });
      setNotice('مجوزهایِ نقش ذخیره شد.');
      loadRoles();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ذخیرهٔ مجوزها ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  const permsByModule = perms.reduce<Record<string, PermissionDef[]>>((acc, p) => {
    (acc[p.module] ??= []).push(p);
    return acc;
  }, {});

  const roleColumns: Column<RoleRow>[] = [
    { key: 'code', header: 'کد', render: (r) => r.code },
    { key: 'name', header: 'نام', render: (r) => <a onClick={() => selectRole(r)} style={{ cursor: 'pointer' }}>{r.name}</a> },
    { key: 'sys', header: '', render: (r) => r.isSystem ? <span className="badge badge-gray">سیستمی</span> : null },
    { key: 'status', header: 'وضعیت', render: (r) => <span className={`badge ${r.isActive ? 'badge-green' : 'badge-yellow'}`}>{r.isActive ? 'فعال' : 'غیرفعال'}</span> },
    { key: 'perms', header: 'تعدادِ مجوز', numeric: true, render: (r) => r.permissions.length },
  ];

  // ── کاربران ──
  const [users, setUsers] = useState<SecurityUserRow[]>([]);
  const [selectedUser, setSelectedUser] = useState<SecurityUserRow | null>(null);
  const [userRoles, setUserRoles] = useState<Set<number>>(new Set());

  function loadUsers() {
    apiGet<SecurityUserRow[]>('/api/security/users').then(setUsers)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ کاربران.'));
  }
  useEffect(() => { if (tab === 'users') loadUsers(); }, [tab]);

  function selectUser(u: SecurityUserRow) {
    setSelectedUser(u);
    setUserRoles(new Set(u.roleIds));
    setNotice(null);
  }

  function toggleUserRole(id: number) {
    setUserRoles((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  async function saveUserRoles() {
    if (!selectedUser) return;
    setBusy(true);
    setError(null);
    try {
      await apiPut(`/api/security/users/${selectedUser.id}/roles`, { roleIds: Array.from(userRoles) });
      setNotice('نقش‌هایِ کاربر ذخیره شد.');
      loadUsers();
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ذخیرهٔ نقش‌هایِ کاربر ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  const userColumns: Column<SecurityUserRow>[] = [
    { key: 'username', header: 'نامِ کاربری', render: (u) => <a onClick={() => selectUser(u)} style={{ cursor: 'pointer' }}>{u.username}</a> },
    { key: 'name', header: 'نام', render: (u) => u.fullName },
    { key: 'status', header: 'وضعیت', render: (u) => <span className={`badge ${u.isActive ? 'badge-green' : 'badge-yellow'}`}>{u.isActive ? 'فعال' : 'غیرفعال'}</span> },
    { key: 'roles', header: 'تعدادِ نقش', numeric: true, render: (u) => u.roleIds.length },
  ];

  // ── لاگِ حسابرسی ──
  const [auditRows, setAuditRows] = useState<AuditLogRow[]>([]);
  const [auditDays, setAuditDays] = useState(30);
  useEffect(() => {
    if (tab !== 'audit') return;
    apiGet<AuditLogRow[]>(`/api/security/auditlog?days=${auditDays}`).then(setAuditRows)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ لاگِ حسابرسی.'));
  }, [tab, auditDays]);

  const auditColumns: Column<AuditLogRow>[] = [
    { key: 'when', header: 'زمان', render: (r) => r.when },
    { key: 'user', header: 'کاربر', render: (r) => r.user ?? '—' },
    { key: 'action', header: 'عمل', render: (r) => r.action },
    { key: 'table', header: 'جدول', render: (r) => r.tableName ?? '—' },
    { key: 'details', header: 'جزئیات', render: (r) => r.details ?? '—' },
  ];

  return (
    <div>
      <PageHeader title="امنیت" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      <div className="minitabs" style={{ marginBottom: 'var(--space-4)' }}>
        <button type="button" className={tab === 'roles' ? 'on' : ''} onClick={() => setTab('roles')}>نقش‌ها</button>
        <button type="button" className={tab === 'users' ? 'on' : ''} onClick={() => setTab('users')}>کاربران</button>
        <button type="button" className={tab === 'audit' ? 'on' : ''} onClick={() => setTab('audit')}>لاگِ حسابرسی</button>
      </div>

      {tab === 'roles' && (
        <div style={{ display: 'flex', gap: 'var(--space-4)', alignItems: 'flex-start' }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div style={{ marginBottom: 'var(--space-3)' }}>
              <button type="button" className="btn btn-primary btn-sm" onClick={() => setShowNewRole((v) => !v)}>نقشِ نو</button>
            </div>
            {showNewRole && (
              <div className="gbox" style={{ padding: 'var(--space-3)', marginBottom: 'var(--space-3)' }}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)' }}>
                  <div className="field">
                    <label className="label">کدِ نقش</label>
                    <input className="input" value={newRole.code} onChange={(e) => setNewRole({ ...newRole, code: e.target.value })} style={{ direction: 'ltr' }} />
                  </div>
                  <div className="field">
                    <label className="label">نامِ نقش</label>
                    <input className="input" value={newRole.name} onChange={(e) => setNewRole({ ...newRole, name: e.target.value })} />
                  </div>
                </div>
                <div style={{ marginTop: 'var(--space-2)' }}>
                  <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={saveNewRole}>ذخیره</button>
                </div>
              </div>
            )}
            <DataTable columns={roleColumns} rows={roles} rowKey={(r) => r.id} emptyText="نقشی ثبت نشده." />
          </div>

          {selectedRole && (
            <div className="gbox" style={{ padding: 'var(--space-4)', width: 420, flex: 'none' }}>
              <div className="gh">مجوزهایِ نقش «{selectedRole.name}»</div>
              <div style={{ maxHeight: 420, overflowY: 'auto' }}>
                {Object.entries(permsByModule).map(([mod, list]) => (
                  <div key={mod} style={{ marginBottom: 10 }}>
                    <div style={{ fontWeight: 700, fontSize: 13, marginBottom: 4 }}>{mod}</div>
                    {list.map((p) => (
                      <label key={p.code} style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, padding: '3px 0' }}>
                        <input type="checkbox" checked={rolePerms.has(p.code)} onChange={() => togglePerm(p.code)} />
                        {p.label}
                      </label>
                    ))}
                  </div>
                ))}
              </div>
              <div style={{ marginTop: 'var(--space-3)', display: 'flex', gap: 'var(--space-2)' }}>
                <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={saveRolePermissions}>ذخیرهٔ مجوزها</button>
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => setSelectedRole(null)}>بستن</button>
              </div>
            </div>
          )}
        </div>
      )}

      {tab === 'users' && (
        <div style={{ display: 'flex', gap: 'var(--space-4)', alignItems: 'flex-start' }}>
          <div style={{ flex: 1, minWidth: 0 }}>
            <DataTable columns={userColumns} rows={users} rowKey={(u) => u.id} emptyText="کاربری ثبت نشده." />
          </div>
          {selectedUser && (
            <div className="gbox" style={{ padding: 'var(--space-4)', width: 320, flex: 'none' }}>
              <div className="gh">نقش‌هایِ کاربر «{selectedUser.username}»</div>
              {roles.map((r) => (
                <label key={r.id} style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: 13, padding: '4px 0' }}>
                  <input type="checkbox" checked={userRoles.has(r.id)} onChange={() => toggleUserRole(r.id)} />
                  {r.name}
                </label>
              ))}
              <div style={{ marginTop: 'var(--space-3)', display: 'flex', gap: 'var(--space-2)' }}>
                <button type="button" className="btn btn-primary btn-sm" disabled={busy} onClick={saveUserRoles}>ذخیره</button>
                <button type="button" className="btn btn-ghost btn-sm" onClick={() => setSelectedUser(null)}>بستن</button>
              </div>
            </div>
          )}
        </div>
      )}

      {tab === 'audit' && (
        <div>
          <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'end', marginBottom: 'var(--space-3)' }}>
            <div className="field">
              <label className="label">بازهٔ روز</label>
              <select className="select" value={auditDays} onChange={(e) => setAuditDays(Number(e.target.value))}>
                <option value={7}>۷ روز</option>
                <option value={30}>۳۰ روز</option>
                <option value={90}>۹۰ روز</option>
              </select>
            </div>
          </div>
          <DataTable columns={auditColumns} rows={auditRows} rowKey={(r) => r.id} emptyText="رخدادی ثبت نشده." />
        </div>
      )}
    </div>
  );
}
