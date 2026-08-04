import { useState } from 'react';
import { apiPost, ApiError } from '../api/client';
import { StatusMessage } from './PageHeader';

interface QuickCreatedParty { id: number; name: string; code: string }
interface QuickCreatedProduct { id: number; name: string; code: string; salePrice: number; purchasePrice: number }

type Kind = 'customer' | 'supplier' | 'product';

interface Props {
  kind: Kind;
  initialName: string;
  onClose: () => void;
  onCreated: (item: QuickCreatedParty | QuickCreatedProduct) => void;
}

/**
 * فرمِ فوریِ ساختِ رکوردِ کمکی — درخواستِ کاربر: «توی باکس‌هایِ اجباری مثلِ مشتری/کالا، اگر
 * موردِ موردنظر توی لیست نبود، همان‌جا فرمِ افزودن باز شود» تا کاربر مجبور نباشد فرمِ فاکتور را
 * رها کند و به صفحهٔ جداگانهٔ ساختِ مشتری/کالا برود. فقط فیلدهایِ الزامیِ حداقلی می‌گیرد؛ ادامهٔ
 * تکمیلِ اطلاعات (آدرس/سقفِ اعتبار/…) بعداً از صفحهٔ ویرایشِ کاملِ همان رکورد قابلِ انجام است.
 */
export function QuickCreateModal({ kind, initialName, onClose, onCreated }: Props) {
  const [name, setName] = useState(initialName);
  const [code, setCode] = useState(kind === 'product' ? 'K' + Date.now().toString().slice(-8) : 'P' + Date.now().toString().slice(-8));
  const [phone, setPhone] = useState('');
  const [salePrice, setSalePrice] = useState('0');
  const [purchasePrice, setPurchasePrice] = useState('0');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const title = kind === 'product' ? 'کالایِ نو' : kind === 'supplier' ? 'تأمین‌کنندهٔ نو' : 'مشتریِ نو';

  async function save() {
    if (name.trim().length < 2) { setError('نام باید دستِ‌کم ۲ نویسه باشد.'); return; }
    if (!code.trim()) { setError('کد الزامی است.'); return; }
    setSaving(true);
    setError(null);
    try {
      if (kind === 'product') {
        const res = await apiPost<{ id: number }>('/api/products', {
          code, barcode: null, name, nameEn: null, groupId: null, unitId: 1,
          purchasePrice: Number(purchasePrice) || 0, salePrice: Number(salePrice) || 0,
          wholesalePrice: Number(salePrice) || 0, consumerPrice: Number(salePrice) || 0,
          minStock: 0, maxStock: null, hasSerial: false, hasBatch: false, hasExpiry: false,
          valuationMethod: 0, taxRate: 0, description: null, image: null, brandId: null, productType: 0,
        });
        onCreated({ id: res.id, name, code, salePrice: Number(salePrice) || 0, purchasePrice: Number(purchasePrice) || 0 });
      } else {
        const isSupplier = kind === 'supplier';
        const res = await apiPost<{ id: number }>('/api/customers', {
          customerType: 'حقیقی', firstName: name, lastName: '', companyName: null,
          phone: phone || null, mobile: phone || null, email: null,
          province: null, city: null, address: null, postalCode: null,
          creditLimit: 0, creditDays: 0, priceLevel: 'خرده', discount: 0,
          nationalCode: null, economicCode: null, notes: null, contactPerson: null, visitor: null,
          isCustomerRole: !isSupplier, isSupplierRole: isSupplier,
          isEmployeeRole: false, isSalespersonRole: false, code, groupId: null, birthDate: null,
        });
        onCreated({ id: res.id, name, code });
      }
    } catch (e) {
      setError(e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div style={{
      position: 'fixed', inset: 0, background: 'rgba(0,0,0,.35)', zIndex: 100,
      display: 'flex', alignItems: 'center', justifyContent: 'center',
    }} onMouseDown={(e) => { if (e.target === e.currentTarget) onClose(); }}>
      <div className="gbox" style={{ width: 380, maxWidth: '92vw' }}>
        <div className="gh">{title}</div>
        <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
          <div className="field">
            <label className="label">نام</label>
            <input className="input" autoFocus value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">کد</label>
            <input className="input" value={code} onChange={(e) => setCode(e.target.value)} />
          </div>
          {kind === 'product' ? (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-3)' }}>
              <div className="field">
                <label className="label">قیمتِ فروش</label>
                <input className="input" type="number" min="0" value={salePrice} onChange={(e) => setSalePrice(e.target.value)} />
              </div>
              <div className="field">
                <label className="label">قیمتِ خرید</label>
                <input className="input" type="number" min="0" value={purchasePrice} onChange={(e) => setPurchasePrice(e.target.value)} />
              </div>
            </div>
          ) : (
            <div className="field">
              <label className="label">موبایل/تلفن</label>
              <input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} />
            </div>
          )}
          {error && <StatusMessage kind="error">{error}</StatusMessage>}
          <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
            <button type="button" className="btn btn-primary btn-sm" disabled={saving} onClick={save}>
              {saving ? 'در حالِ ذخیره…' : 'ذخیره و انتخاب'}
            </button>
            <button type="button" className="btn btn-ghost btn-sm" onClick={onClose}>انصراف</button>
          </div>
        </div>
      </div>
    </div>
  );
}
