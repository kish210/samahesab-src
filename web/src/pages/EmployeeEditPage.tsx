import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { apiGet, apiPost, apiFetch, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';

interface EmployeeDetailDto {
  id: number; code: string; nationalCode: string; firstName: string; lastName: string;
  hireDate: string; contractType: string; baseSalary: number;
  mobile: string | null; phone: string | null; email: string | null; address: string | null;
  bankName: string | null; bankAccount: string | null; shebaNumber: string | null;
  insuranceNumber: string | null; childrenCount: number; notes: string | null;
}

/** U-WEB-HR — ساخت/ویرایشِ کارمند. `SaveEmployeeCommand` از قبل در Application/HRM بود ولی
 * هیچ endpoint/فرمِ وبی نداشت. */
export function EmployeeEditPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const isEdit = !!id;

  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [code, setCode] = useState('');
  const [nationalCode, setNationalCode] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [hireDate, setHireDate] = useState(todayJalaliString());
  const [contractType, setContractType] = useState('دائم');
  const [baseSalary, setBaseSalary] = useState('0');
  const [mobile, setMobile] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [address, setAddress] = useState('');
  const [bankName, setBankName] = useState('');
  const [bankAccount, setBankAccount] = useState('');
  const [shebaNumber, setShebaNumber] = useState('');
  const [insuranceNumber, setInsuranceNumber] = useState('');
  const [childrenCount, setChildrenCount] = useState('0');
  const [notes, setNotes] = useState('');

  useEffect(() => {
    if (!isEdit) {
      setCode('E' + Date.now().toString().slice(-8));
      return;
    }
    apiGet<EmployeeDetailDto>(`/api/employees/${id}`)
      .then((e) => {
        setCode(e.code);
        setNationalCode(e.nationalCode);
        setFirstName(e.firstName);
        setLastName(e.lastName);
        setHireDate(e.hireDate);
        setContractType(e.contractType);
        setBaseSalary(String(e.baseSalary));
        setMobile(e.mobile ?? '');
        setPhone(e.phone ?? '');
        setEmail(e.email ?? '');
        setAddress(e.address ?? '');
        setBankName(e.bankName ?? '');
        setBankAccount(e.bankAccount ?? '');
        setShebaNumber(e.shebaNumber ?? '');
        setInsuranceNumber(e.insuranceNumber ?? '');
        setChildrenCount(String(e.childrenCount ?? 0));
        setNotes(e.notes ?? '');
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ اطلاعاتِ کارمند.'))
      .finally(() => setLoading(false));
  }, [id, isEdit]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (!nationalCode.trim() || !firstName.trim() || !lastName.trim()) {
      setError('کدِ ملی/نام/نامِ خانوادگی الزامی است.');
      return;
    }

    const body = {
      id: isEdit ? Number(id) : 0,
      code, nationalCode, firstName, lastName, hireDate,
      baseSalary: Number(baseSalary) || 0, contractType,
      mobile: mobile || null, phone: phone || null, email: email || null, address: address || null,
      bankName: bankName || null, bankAccount: bankAccount || null, shebaNumber: shebaNumber || null,
      insuranceNumber: insuranceNumber || null, notes: notes || null,
    };

    setSaving(true);
    try {
      if (isEdit) await apiFetch(`/api/employees/${id}`, { method: 'PUT', body: JSON.stringify(body) });
      else await apiPost('/api/employees', body);
      navigate('/hr/employees', { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'ذخیرهٔ کارمند ناموفق بود.');
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>;

  return (
    <div>
      <PageHeader title={isEdit ? 'ویرایشِ کارمند' : 'کارمندِ نو'} />
      <form onSubmit={submit} style={{ maxWidth: 780 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)' }}>
          <div className="field">
            <label className="label">کد</label>
            <input className="input" value={code} onChange={(e) => setCode(e.target.value)} disabled={isEdit} />
          </div>
          <div className="field">
            <label className="label">نام<span className="req">*</span></label>
            <input className="input" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">نامِ خانوادگی<span className="req">*</span></label>
            <input className="input" value={lastName} onChange={(e) => setLastName(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">کدِ ملی<span className="req">*</span></label>
            <input className="input" value={nationalCode} onChange={(e) => setNationalCode(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">موبایل</label>
            <input className="input" value={mobile} onChange={(e) => setMobile(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">تلفن</label>
            <input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">ایمیل</label>
            <input className="input" value={email} onChange={(e) => setEmail(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <JalaliDateInput value={hireDate} onChange={setHireDate} label="تاریخِ استخدام" />
          <div className="field">
            <label className="label">نوعِ قرارداد</label>
            <select className="select" value={contractType} onChange={(e) => setContractType(e.target.value)}>
              <option value="دائم">دائم</option>
              <option value="موقت">موقت</option>
              <option value="پروژه‌ای">پروژه‌ای</option>
            </select>
          </div>
          <div className="field">
            <label className="label">حقوقِ پایه (ریال)</label>
            <input className="input" type="number" min="0" value={baseSalary} onChange={(e) => setBaseSalary(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">تعدادِ فرزندِ مشمولِ حق‌اولاد</label>
            <input className="input" type="number" min="0" max="2" value={childrenCount} onChange={(e) => setChildrenCount(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">بانک</label>
            <input className="input" value={bankName} onChange={(e) => setBankName(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">شمارهٔ حساب</label>
            <input className="input" value={bankAccount} onChange={(e) => setBankAccount(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">شماره‌شبا</label>
            <input className="input" value={shebaNumber} onChange={(e) => setShebaNumber(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">شمارهٔ بیمه</label>
            <input className="input" value={insuranceNumber} onChange={(e) => setInsuranceNumber(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field" style={{ gridColumn: 'span 3' }}>
            <label className="label">آدرس</label>
            <input className="input" value={address} onChange={(e) => setAddress(e.target.value)} />
          </div>
          <div className="field" style={{ gridColumn: 'span 3' }}>
            <label className="label">یادداشت</label>
            <input className="input" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </div>
        </div>

        {error && (
          <div style={{ marginTop: 'var(--space-3)' }}>
            <StatusMessage kind="error">{error}</StatusMessage>
          </div>
        )}

        <div style={{ display: 'flex', gap: 'var(--space-2)', marginTop: 'var(--space-4)' }}>
          <button type="submit" className="btn btn-primary" disabled={saving}>
            {saving ? 'در حالِ ذخیره…' : 'ذخیره'}
          </button>
          <button type="button" className="btn btn-secondary" onClick={() => navigate(-1)}>انصراف</button>
        </div>
      </form>
    </div>
  );
}
