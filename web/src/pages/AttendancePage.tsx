import { useEffect, useState } from 'react';
import { apiGet, apiPost, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { DataTable, type Column } from '../components/DataTable';
import { SearchSelect, type SearchSelectOption } from '../components/SearchSelect';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';

interface AttendanceRow {
  employeeId: number; employeeName: string; checkIn: string; checkOut: string;
  workHours: number; overtimeHours: number; status: string;
}
interface LeaveRequestRow {
  id: number; employeeId: number; employeeName: string; leaveType: string;
  startDate: string; endDate: string; days: number; hours: number; status: string; reason: string | null;
}
interface MonthlySummary {
  presentDays: number; absentDays: number; leaveDays: number; paidLeaveDays: number; unpaidLeaveDays: number;
  holidayWorkDays: number; workedHours: number; overtimeHours: number; nightHours: number; holidayHours: number;
  totalTardyMinutes: number; totalEarlyLeaveMinutes: number;
}
interface EmployeeRow { id: number; fullName: string }
interface DeviceRow {
  id: number; name: string; code: string | null; location: string | null; isActive: boolean;
  ipAddress: string | null; port: number; commKey: string | null;
}

const STATUS_OPTIONS = ['حاضر', 'غایب', 'مرخصی'];
const LEAVE_TYPES = ['استحقاقی', 'استعلاجی', 'بدونِ حقوق', 'اداری'];

/** U-WEB-ATTENDANCE — حضوروغیاب. لایهٔ Application از قبل کامل بود، فقط endpoint/صفحه نداشت.
 * U-ATT-ZK — تبِ «دستگاه‌ها»: اتصالِ مستقیم به دستگاهِ زدکتکو (TCP/IP پورت ۴۳۷۰) + همگام‌سازیِ دستی.
 * ⚠️ محدودیتِ صادقانه: شیفت/تقویمِ تعطیلات و کاردکسِ مرخصی هنوز UI ندارند؛ همگام‌سازیِ دستگاه
 * بدونِ سخت‌افزارِ واقعی تست نشده (رجوع کن به ZkTecoDeviceClient.cs). */
export function AttendancePage() {
  const [tab, setTab] = useState<'day' | 'leaves' | 'monthly' | 'devices'>('day');
  const [workDate, setWorkDate] = useState(todayJalaliString());
  const [rows, setRows] = useState<AttendanceRow[]>([]);
  const [leaves, setLeaves] = useState<LeaveRequestRow[]>([]);
  const [employees, setEmployees] = useState<EmployeeRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  function loadDay() {
    apiGet<AttendanceRow[]>(`/api/attendance/day?workDate=${encodeURIComponent(workDate)}`)
      .then(setRows).catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ برگهٔ حضور.'));
  }
  function loadLeaves() {
    apiGet<LeaveRequestRow[]>('/api/attendance/leaves').then(setLeaves).catch(() => {});
  }
  useEffect(loadDay, [workDate]);
  useEffect(loadLeaves, []);
  useEffect(() => {
    apiGet<EmployeeRow[]>('/api/employees').then((list) => setEmployees(list.map((e: any) => ({ id: e.id, fullName: e.fullName ?? e.name })))).catch(() => {});
  }, []);

  async function setStatus(employeeId: number, status: string) {
    try {
      await apiPost('/api/attendance/upsert', { employeeId, workDate, status });
      loadDay();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ وضعیت ناموفق بود.'); }
  }

  async function decide(id: number, approve: boolean) {
    try {
      await apiPost(`/api/attendance/leaves/${id}/decide`, { approve, decisionDate: todayJalaliString() });
      setNotice(approve ? 'مرخصی تأیید شد.' : 'مرخصی رد شد.');
      loadLeaves();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ تصمیم ناموفق بود.'); }
  }

  // ── فرمِ درخواستِ مرخصیِ نو ──
  const [showNewLeave, setShowNewLeave] = useState(false);
  const [leaveEmployeeId, setLeaveEmployeeId] = useState<number | null>(null);
  const [leaveType, setLeaveType] = useState(LEAVE_TYPES[0]);
  const [leaveStart, setLeaveStart] = useState(todayJalaliString());
  const [leaveEnd, setLeaveEnd] = useState(todayJalaliString());
  const [leaveDays, setLeaveDays] = useState('1');

  async function submitLeave() {
    if (!leaveEmployeeId) { setError('انتخابِ کارمند الزامی است.'); return; }
    try {
      await apiPost('/api/attendance/leaves', {
        employeeId: leaveEmployeeId, leaveType, startDate: leaveStart, endDate: leaveEnd,
        days: Number(leaveDays) || 1,
      });
      setNotice('درخواستِ مرخصی ثبت شد.');
      setShowNewLeave(false);
      loadLeaves();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ درخواست ناموفق بود.'); }
  }

  // ── تجمیعِ ماهانه ──
  const [monthlyEmployeeId, setMonthlyEmployeeId] = useState<number | null>(null);
  const [monthlyYear, setMonthlyYear] = useState('1405');
  const [monthlyMonth, setMonthlyMonth] = useState(1);
  const [summary, setSummary] = useState<MonthlySummary | null>(null);

  function loadMonthly() {
    if (!monthlyEmployeeId) return;
    apiGet<{ summary: MonthlySummary }>(`/api/attendance/monthly?employeeId=${monthlyEmployeeId}&year=${monthlyYear}&month=${monthlyMonth}`)
      .then((d) => setSummary(d.summary))
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ تجمیعِ ماهانه.'));
  }

  // ── دستگاه‌هایِ زدکتکو ──
  const [devices, setDevices] = useState<DeviceRow[]>([]);
  const [showDeviceForm, setShowDeviceForm] = useState(false);
  const [devName, setDevName] = useState('');
  const [devCode, setDevCode] = useState('');
  const [devLocation, setDevLocation] = useState('');
  const [devIp, setDevIp] = useState('');
  const [devPort, setDevPort] = useState('4370');
  const [devCommKey, setDevCommKey] = useState('0');
  const [syncingId, setSyncingId] = useState<number | null>(null);

  function loadDevices() {
    apiGet<DeviceRow[]>('/api/attendance/devices').then(setDevices).catch(() => {});
  }
  useEffect(loadDevices, []);

  async function saveDevice() {
    if (!devName.trim()) { setError('نامِ دستگاه الزامی است.'); return; }
    try {
      await apiPost('/api/attendance/devices', {
        id: 0, name: devName, code: devCode || null, location: devLocation || null,
        isActive: true, ipAddress: devIp || null, port: Number(devPort) || 4370, commKey: devCommKey || null,
      });
      setNotice('دستگاه ثبت شد.');
      setShowDeviceForm(false);
      setDevName(''); setDevCode(''); setDevLocation(''); setDevIp(''); setDevPort('4370'); setDevCommKey('0');
      loadDevices();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'ثبتِ دستگاه ناموفق بود.'); }
  }

  async function syncDevice(d: DeviceRow) {
    if (!d.ipAddress) { setError('ابتدا آدرسِ IPِ دستگاه را تنظیم کنید.'); return; }
    setSyncingId(d.id);
    setError(null);
    try {
      const r = await apiPost<{ punchesRead: number; punchesInserted: number; daysProcessed: number }>(
        `/api/attendance/devices/${d.id}/sync`, {});
      setNotice(`${d.name}: ${r.punchesRead} ضربه خوانده شد، ${r.punchesInserted} موردِ نو ثبت شد (${r.daysProcessed} روز).`);
      if (tab === 'day') loadDay();
    } catch (e) { setError(e instanceof ApiError ? e.message : 'همگام‌سازیِ دستگاه ناموفق بود.'); }
    finally { setSyncingId(null); }
  }

  const deviceColumns: Column<DeviceRow>[] = [
    { key: 'name', header: 'نام', render: (r) => r.name },
    { key: 'code', header: 'کد', render: (r) => r.code || '—' },
    { key: 'location', header: 'محل', render: (r) => r.location || '—' },
    { key: 'ip', header: 'آدرسِ IP', render: (r) => <span style={{ direction: 'ltr' }}>{r.ipAddress ? `${r.ipAddress}:${r.port}` : '—'}</span> },
    {
      key: 'active', header: 'وضعیت',
      render: (r) => <span className={`badge ${r.isActive ? 'badge-green' : 'badge-gray'}`}>{r.isActive ? 'فعال' : 'غیرفعال'}</span>,
    },
    {
      key: 'action', header: '',
      render: (r) => (
        <button type="button" className="btn btn-ghost btn-sm" disabled={syncingId === r.id} onClick={() => syncDevice(r)}>
          {syncingId === r.id ? 'در حالِ همگام‌سازی…' : 'همگام‌سازی'}
        </button>
      ),
    },
  ];

  const employeeOptions: SearchSelectOption[] = employees.map((e) => ({ id: e.id, label: e.fullName }));

  const dayColumns: Column<AttendanceRow>[] = [
    { key: 'name', header: 'کارمند', render: (r) => r.employeeName },
    { key: 'in', header: 'ورود', render: (r) => <span style={{ direction: 'ltr' }}>{r.checkIn || '—'}</span> },
    { key: 'out', header: 'خروج', render: (r) => <span style={{ direction: 'ltr' }}>{r.checkOut || '—'}</span> },
    { key: 'work', header: 'ساعتِ کار', numeric: true, render: (r) => r.workHours },
    { key: 'ot', header: 'اضافه‌کاری', numeric: true, render: (r) => r.overtimeHours },
    {
      key: 'status', header: 'وضعیت',
      render: (r) => <span className={`badge ${r.status === 'حاضر' ? 'badge-green' : r.status === 'غایب' ? 'badge-red' : r.status === 'مرخصی' ? 'badge-yellow' : 'badge-gray'}`}>{r.status}</span>,
    },
    {
      key: 'action', header: '',
      render: (r) => (
        <div style={{ display: 'flex', gap: 6 }}>
          {STATUS_OPTIONS.map((s) => (
            <button key={s} type="button" className="btn btn-ghost btn-sm" onClick={() => setStatus(r.employeeId, s)}>{s}</button>
          ))}
        </div>
      ),
    },
  ];

  const leaveColumns: Column<LeaveRequestRow>[] = [
    { key: 'name', header: 'کارمند', render: (r) => r.employeeName },
    { key: 'type', header: 'نوع', render: (r) => r.leaveType },
    { key: 'from', header: 'از', render: (r) => r.startDate },
    { key: 'to', header: 'تا', render: (r) => r.endDate },
    { key: 'days', header: 'روز', numeric: true, render: (r) => r.days },
    {
      key: 'status', header: 'وضعیت',
      render: (r) => <span className={`badge ${r.status === 'تأییدشده' ? 'badge-green' : r.status === 'ردشده' ? 'badge-red' : 'badge-yellow'}`}>{r.status}</span>,
    },
    {
      key: 'action', header: '',
      render: (r) => r.status === 'درخواست' ? (
        <div style={{ display: 'flex', gap: 6 }}>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => decide(r.id, true)}>تأیید</button>
          <button type="button" className="btn btn-ghost btn-sm" onClick={() => decide(r.id, false)}>رد</button>
        </div>
      ) : null,
    },
  ];

  return (
    <div>
      <PageHeader title="حضور و غیاب" />
      {error && <StatusMessage kind="error">{error}</StatusMessage>}
      {notice && <StatusMessage kind="success">{notice}</StatusMessage>}

      <div className="minitabs" style={{ marginBottom: 'var(--space-4)' }}>
        <button type="button" className={tab === 'day' ? 'on' : ''} onClick={() => setTab('day')}>برگهٔ روزانه</button>
        <button type="button" className={tab === 'leaves' ? 'on' : ''} onClick={() => setTab('leaves')}>مرخصی‌ها</button>
        <button type="button" className={tab === 'monthly' ? 'on' : ''} onClick={() => setTab('monthly')}>تجمیعِ ماهانه</button>
        <button type="button" className={tab === 'devices' ? 'on' : ''} onClick={() => setTab('devices')}>دستگاه‌ها</button>
      </div>

      {tab === 'day' && (
        <div>
          <div style={{ maxWidth: 240, marginBottom: 'var(--space-3)' }}>
            <JalaliDateInput value={workDate} onChange={setWorkDate} label="تاریخ" />
          </div>
          <DataTable columns={dayColumns} rows={rows} rowKey={(r) => r.employeeId} emptyText="کارمندِ فعالی نیست." />
        </div>
      )}

      {tab === 'leaves' && (
        <div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setShowNewLeave((v) => !v)}>درخواستِ مرخصیِ نو</button>
          </div>
          {showNewLeave && (
            <div className="gbox" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
              <div className="gh">درخواستِ مرخصی</div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 'var(--space-3)', marginTop: 'var(--space-2)' }}>
                <div className="field" style={{ gridColumn: 'span 2' }}>
                  <label className="label">کارمند</label>
                  <SearchSelect options={employeeOptions} value={leaveEmployeeId} onChange={setLeaveEmployeeId} placeholder="جست‌وجویِ کارمند…" />
                </div>
                <div className="field">
                  <label className="label">نوعِ مرخصی</label>
                  <select className="select" value={leaveType} onChange={(e) => setLeaveType(e.target.value)}>
                    {LEAVE_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                  </select>
                </div>
                <div className="field">
                  <label className="label">تعدادِ روز</label>
                  <input className="input" type="number" min="0.5" step="0.5" value={leaveDays} onChange={(e) => setLeaveDays(e.target.value)} />
                </div>
                <JalaliDateInput value={leaveStart} onChange={setLeaveStart} label="از تاریخ" />
                <JalaliDateInput value={leaveEnd} onChange={setLeaveEnd} label="تا تاریخ" />
              </div>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <button type="button" className="btn btn-primary btn-sm" onClick={submitLeave}>ثبتِ درخواست</button>
              </div>
            </div>
          )}
          <DataTable columns={leaveColumns} rows={leaves} rowKey={(r) => r.id} emptyText="درخواستِ مرخصی ثبت نشده." />
        </div>
      )}

      {tab === 'monthly' && (
        <div>
          <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'flex-end', marginBottom: 'var(--space-4)' }}>
            <div className="field" style={{ minWidth: 260 }}>
              <label className="label">کارمند</label>
              <SearchSelect options={employeeOptions} value={monthlyEmployeeId} onChange={setMonthlyEmployeeId} placeholder="جست‌وجویِ کارمند…" />
            </div>
            <div className="field" style={{ maxWidth: 120 }}>
              <label className="label">سال</label>
              <input className="input" value={monthlyYear} onChange={(e) => setMonthlyYear(e.target.value)} style={{ direction: 'ltr' }} />
            </div>
            <div className="field" style={{ maxWidth: 100 }}>
              <label className="label">ماه</label>
              <input className="input" type="number" min="1" max="12" value={monthlyMonth} onChange={(e) => setMonthlyMonth(Number(e.target.value))} />
            </div>
            <button type="button" className="btn btn-secondary btn-sm" onClick={loadMonthly}>نمایش</button>
          </div>
          {summary && (
            <div className="sumbar">
              <span>حاضر: {summary.presentDays}</span>
              <span>غایب: {summary.absentDays}</span>
              <span>مرخصی: {summary.leaveDays}</span>
              <span>کارکردِ تعطیل: {summary.holidayWorkDays}</span>
              <span>ساعتِ کار: {summary.workedHours}</span>
              <span>اضافه‌کاری: {summary.overtimeHours}</span>
              <span>تأخیر (دقیقه): {summary.totalTardyMinutes}</span>
            </div>
          )}
        </div>
      )}

      {tab === 'devices' && (
        <div>
          <div style={{ marginBottom: 'var(--space-3)' }}>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setShowDeviceForm((v) => !v)}>دستگاهِ نو</button>
          </div>
          {showDeviceForm && (
            <div className="gbox" style={{ padding: 'var(--space-4)', marginBottom: 'var(--space-4)' }}>
              <div className="gh">دستگاهِ زدکتکو (TCP/IP)</div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)', marginTop: 'var(--space-2)' }}>
                <div className="field">
                  <label className="label">نام</label>
                  <input className="input" value={devName} onChange={(e) => setDevName(e.target.value)} />
                </div>
                <div className="field">
                  <label className="label">کد/سریال</label>
                  <input className="input" value={devCode} onChange={(e) => setDevCode(e.target.value)} />
                </div>
                <div className="field">
                  <label className="label">محل</label>
                  <input className="input" value={devLocation} onChange={(e) => setDevLocation(e.target.value)} />
                </div>
                <div className="field">
                  <label className="label">آدرسِ IP</label>
                  <input className="input" value={devIp} onChange={(e) => setDevIp(e.target.value)} style={{ direction: 'ltr' }} placeholder="192.168.1.201" />
                </div>
                <div className="field">
                  <label className="label">پورت</label>
                  <input className="input" type="number" value={devPort} onChange={(e) => setDevPort(e.target.value)} style={{ direction: 'ltr' }} />
                </div>
                <div className="field">
                  <label className="label">رمزِ ارتباطی (CommKey)</label>
                  <input className="input" value={devCommKey} onChange={(e) => setDevCommKey(e.target.value)} style={{ direction: 'ltr' }} />
                </div>
              </div>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <button type="button" className="btn btn-primary btn-sm" onClick={saveDevice}>ثبتِ دستگاه</button>
              </div>
            </div>
          )}
          <DataTable columns={deviceColumns} rows={devices} rowKey={(r) => r.id} emptyText="دستگاهی ثبت نشده." />
        </div>
      )}
    </div>
  );
}
