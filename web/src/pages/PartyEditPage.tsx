import { useEffect, useState } from 'react';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { apiGet, apiPost, apiFetch, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';

/** کارتِ شخص (همان DTOِ صفحهٔ کارت) — برایِ پیش‌پرکردنِ فرم در حالتِ ویرایش. */
interface CustomerCardDto {
  id: number;
  name: string;
  code: string;
  customerType: string;
  priceLevel: string;
  mobile: string | null;
  phone: string | null;
  nationalCode: string | null;
  economicCode: string | null;
  contactPerson: string | null;
  visitor: string | null;
  province: string | null;
  city: string | null;
  address: string | null;
  creditDays: number;
  balance: number;
  creditLimit: number;
  isCustomer: boolean;
  isSupplier: boolean;
}

/**
 * U-WEB-CRUD — فرمِ مشترکِ ساخت/ویرایشِ شخص. در سما حساب «مشتری» و «تأمین‌کننده» هر دو یک
 * موجودیتِ Party با تیکِ نقش‌اند، پس یک فرم هر دو را می‌سازد (`?role=supplier` نقشِ پیش‌فرض را
 * تأمین‌کننده می‌گذارد). ساخت → POST /api/customers · ویرایش → PUT /api/customers/{id}.
 */
export function PartyEditPage() {
  const { id } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const isEdit = !!id;
  const defaultSupplier = searchParams.get('role') === 'supplier';

  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [code, setCode] = useState('');
  const [customerType, setCustomerType] = useState('حقیقی');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [companyName, setCompanyName] = useState('');
  const [mobile, setMobile] = useState('');
  const [phone, setPhone] = useState('');
  const [nationalCode, setNationalCode] = useState('');
  const [economicCode, setEconomicCode] = useState('');
  const [city, setCity] = useState('');
  const [address, setAddress] = useState('');
  const [creditLimit, setCreditLimit] = useState('0');
  const [creditDays, setCreditDays] = useState('0');
  const [priceLevel, setPriceLevel] = useState('خرده');
  const [isCustomerRole, setIsCustomerRole] = useState(!defaultSupplier);
  const [isSupplierRole, setIsSupplierRole] = useState(defaultSupplier);

  useEffect(() => {
    if (!isEdit) {
      // کدِ پیشنهادیِ ساده برایِ رکوردِ نو (کاربر می‌تواند عوضش کند).
      setCode('P' + Date.now().toString().slice(-8));
      return;
    }
    apiGet<CustomerCardDto>(`/api/customers/${id}/card`)
      .then((c) => {
        setCode(c.code);
        setCustomerType(c.customerType || 'حقیقی');
        // نامِ نمایشی در کارت یکجاست؛ برایِ «حقیقی» به نام/نام‌خانوادگی تقسیمش می‌کنیم.
        if (c.customerType?.trim() === 'حقوقی') setCompanyName(c.name);
        else {
          const parts = (c.name ?? '').trim().split(' ');
          setFirstName(parts.slice(0, -1).join(' ') || parts[0] || '');
          setLastName(parts.length > 1 ? parts[parts.length - 1] : '');
        }
        setMobile(c.mobile ?? '');
        setPhone(c.phone ?? '');
        setNationalCode(c.nationalCode ?? '');
        setEconomicCode(c.economicCode ?? '');
        setCity(c.city ?? '');
        setAddress(c.address ?? '');
        setCreditLimit(String(c.creditLimit ?? 0));
        setCreditDays(String(c.creditDays ?? 0));
        setPriceLevel(c.priceLevel || 'خرده');
        setIsCustomerRole(c.isCustomer);
        setIsSupplierRole(c.isSupplier);
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ اطلاعاتِ شخص.'))
      .finally(() => setLoading(false));
  }, [id, isEdit]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    const isLegal = customerType.trim() === 'حقوقی';
    const displayName = isLegal ? companyName.trim() : `${firstName} ${lastName}`.trim();
    if (displayName.length < 2) {
      setError('نامِ معتبر وارد کنید (برایِ حقوقی «نامِ شرکت»، برایِ حقیقی «نام» — دستِ‌کم ۲ نویسه).');
      return;
    }
    if (!isCustomerRole && !isSupplierRole) {
      setError('دستِ‌کم یک نقش (مشتری یا تأمین‌کننده) باید تیک بخورد.');
      return;
    }
    if (!isEdit && !code.trim()) {
      setError('کدِ شخص الزامی است.');
      return;
    }

    const body = {
      customerType,
      firstName: isLegal ? null : firstName,
      lastName: isLegal ? null : lastName,
      companyName: isLegal ? companyName : null,
      phone, mobile, email: null,
      province: null, city, address, postalCode: null,
      creditLimit: Number(creditLimit) || 0,
      creditDays: Number(creditDays) || 0,
      priceLevel,
      discount: 0,
      nationalCode, economicCode,
      notes: null, contactPerson: null, visitor: null,
      isCustomerRole, isSupplierRole,
      isEmployeeRole: false, isSalespersonRole: false,
    };

    setSaving(true);
    try {
      if (isEdit) {
        await apiFetch(`/api/customers/${id}`, { method: 'PUT', body: JSON.stringify({ ...body, id: Number(id) }) });
      } else {
        await apiPost('/api/customers', { ...body, code, groupId: null, birthDate: null });
      }
      navigate(isSupplierRole && !isCustomerRole ? '/suppliers' : '/customers', { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'ذخیرهٔ شخص ناموفق بود.');
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>;

  const isLegal = customerType.trim() === 'حقوقی';

  return (
    <div>
      <PageHeader title={isEdit ? 'ویرایشِ شخص' : 'شخصِ نو (مشتری / تأمین‌کننده)'} />
      <form onSubmit={submit} style={{ maxWidth: 780 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)' }}>
          <div className="field">
            <label className="label">کد</label>
            <input className="input" value={code} onChange={(e) => setCode(e.target.value)} disabled={isEdit} />
          </div>
          <div className="field">
            <label className="label">نوعِ شخص</label>
            <select className="select" value={customerType} onChange={(e) => setCustomerType(e.target.value)}>
              <option value="حقیقی">حقیقی</option>
              <option value="حقوقی">حقوقی</option>
            </select>
          </div>
          <div className="field">
            <label className="label">سطحِ قیمت</label>
            <select className="select" value={priceLevel} onChange={(e) => setPriceLevel(e.target.value)}>
              <option value="خرده">خرده</option>
              <option value="عمده">عمده</option>
            </select>
          </div>

          {isLegal ? (
            <div className="field" style={{ gridColumn: 'span 2' }}>
              <label className="label">نامِ شرکت<span className="req">*</span></label>
              <input className="input" value={companyName} onChange={(e) => setCompanyName(e.target.value)} />
            </div>
          ) : (
            <>
              <div className="field">
                <label className="label">نام<span className="req">*</span></label>
                <input className="input" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
              </div>
              <div className="field">
                <label className="label">نامِ خانوادگی</label>
                <input className="input" value={lastName} onChange={(e) => setLastName(e.target.value)} />
              </div>
            </>
          )}

          <div className="field">
            <label className="label">موبایل</label>
            <input className="input" value={mobile} onChange={(e) => setMobile(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">تلفن</label>
            <input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">شهر</label>
            <input className="input" value={city} onChange={(e) => setCity(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">کدِ ملی</label>
            <input className="input" value={nationalCode} onChange={(e) => setNationalCode(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">کدِ اقتصادی</label>
            <input className="input" value={economicCode} onChange={(e) => setEconomicCode(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">سقفِ اعتبار (ریال)</label>
            <input className="input" type="number" min="0" value={creditLimit} onChange={(e) => setCreditLimit(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">مهلتِ اعتبار (روز)</label>
            <input className="input" type="number" min="0" value={creditDays} onChange={(e) => setCreditDays(e.target.value)} />
          </div>
          <div className="field" style={{ gridColumn: 'span 2' }}>
            <label className="label">آدرس</label>
            <input className="input" value={address} onChange={(e) => setAddress(e.target.value)} />
          </div>
        </div>

        <div style={{ display: 'flex', gap: 'var(--space-5)', marginTop: 'var(--space-4)' }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
            <input type="checkbox" checked={isCustomerRole} onChange={(e) => setIsCustomerRole(e.target.checked)} />
            مشتری (خریدار)
          </label>
          <label style={{ display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer' }}>
            <input type="checkbox" checked={isSupplierRole} onChange={(e) => setIsSupplierRole(e.target.checked)} />
            تأمین‌کننده
          </label>
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
          <button type="button" className="btn btn-secondary" onClick={() => navigate(-1)}>
            انصراف
          </button>
        </div>
      </form>
    </div>
  );
}
