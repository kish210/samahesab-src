import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { apiGet, apiPost, apiFetch, ApiError } from '../api/client';
import { PageHeader, StatusMessage } from '../components/PageHeader';

interface ProductCardDto {
  id: number;
  code: string;
  name: string;
  barcode: string | null;
  purchasePrice: number;
  salePrice: number;
  wholesalePrice: number;
  consumerPrice: number;
  taxRate: number;
  minStock: number;
  maxStock: number | null;
  tracking: string;
}

/**
 * U-WEB-CRUD — فرمِ ساخت/ویرایشِ کالا. ساخت → POST /api/products · ویرایش → PUT /api/products/{id}
 * (هر دو اندپوینت همین دور به API اضافه شدند؛ Commandها از قبل در Application بودند).
 * مقادیرِ enum مطابقِ Domain: ProductType 0=کالا · ValuationMethod 0=میانگینِ موزون.
 */
export function ProductEditPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const isEdit = !!id;

  const [loading, setLoading] = useState(isEdit);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [barcode, setBarcode] = useState('');
  const [purchasePrice, setPurchasePrice] = useState('0');
  const [salePrice, setSalePrice] = useState('0');
  const [wholesalePrice, setWholesalePrice] = useState('0');
  const [consumerPrice, setConsumerPrice] = useState('0');
  const [taxRate, setTaxRate] = useState('0');
  const [minStock, setMinStock] = useState('0');

  useEffect(() => {
    if (!isEdit) {
      setCode('K' + Date.now().toString().slice(-8));
      return;
    }
    apiGet<ProductCardDto>(`/api/products/${id}/card`)
      .then((p) => {
        setCode(p.code);
        setName(p.name);
        setBarcode(p.barcode ?? '');
        setPurchasePrice(String(p.purchasePrice ?? 0));
        setSalePrice(String(p.salePrice ?? 0));
        setWholesalePrice(String(p.wholesalePrice ?? 0));
        setConsumerPrice(String(p.consumerPrice ?? 0));
        setTaxRate(String(p.taxRate ?? 0));
        setMinStock(String(p.minStock ?? 0));
      })
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ کالا.'))
      .finally(() => setLoading(false));
  }, [id, isEdit]);

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    if (name.trim().length < 2) {
      setError('نامِ کالا الزامی است (دستِ‌کم ۲ نویسه).');
      return;
    }
    if (!isEdit && !code.trim()) {
      setError('کدِ کالا الزامی است.');
      return;
    }

    const body = {
      code,
      barcode: barcode || null,
      name,
      nameEn: null,
      groupId: null,
      unitId: 1,
      purchasePrice: Number(purchasePrice) || 0,
      salePrice: Number(salePrice) || 0,
      wholesalePrice: Number(wholesalePrice) || 0,
      consumerPrice: Number(consumerPrice) || 0,
      minStock: Number(minStock) || 0,
      maxStock: null,
      hasSerial: false,
      hasBatch: false,
      hasExpiry: false,
      valuationMethod: 0,   // میانگینِ موزون
      taxRate: Number(taxRate) || 0,
      description: null,
      image: null,
    };

    setSaving(true);
    try {
      if (isEdit) {
        await apiFetch(`/api/products/${id}`, { method: 'PUT', body: JSON.stringify({ ...body, productId: Number(id) }) });
      } else {
        await apiPost('/api/products', { ...body, brandId: null, productType: 0 });
      }
      navigate('/products', { replace: true });
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'ذخیرهٔ کالا ناموفق بود.');
    } finally {
      setSaving(false);
    }
  }

  if (loading) return <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>;

  return (
    <div>
      <PageHeader title={isEdit ? 'ویرایشِ کالا' : 'کالایِ نو'} />
      <form onSubmit={submit} style={{ maxWidth: 780 }}>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)' }}>
          <div className="field">
            <label className="label">کد</label>
            <input className="input" value={code} onChange={(e) => setCode(e.target.value)} disabled={isEdit} />
          </div>
          <div className="field" style={{ gridColumn: 'span 2' }}>
            <label className="label">نامِ کالا<span className="req">*</span></label>
            <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">بارکد</label>
            <input className="input" value={barcode} onChange={(e) => setBarcode(e.target.value)} style={{ direction: 'ltr' }} />
          </div>
          <div className="field">
            <label className="label">قیمتِ خرید</label>
            <input className="input" type="number" min="0" value={purchasePrice} onChange={(e) => setPurchasePrice(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">قیمتِ فروش</label>
            <input className="input" type="number" min="0" value={salePrice} onChange={(e) => setSalePrice(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">قیمتِ عمده</label>
            <input className="input" type="number" min="0" value={wholesalePrice} onChange={(e) => setWholesalePrice(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">قیمتِ مصرف‌کننده</label>
            <input className="input" type="number" min="0" value={consumerPrice} onChange={(e) => setConsumerPrice(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">نرخِ مالیات (٪)</label>
            <input className="input" type="number" min="0" max="100" value={taxRate} onChange={(e) => setTaxRate(e.target.value)} />
          </div>
          <div className="field">
            <label className="label">حداقلِ موجودی</label>
            <input className="input" type="number" min="0" value={minStock} onChange={(e) => setMinStock(e.target.value)} />
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
          <button type="button" className="btn btn-secondary" onClick={() => navigate(-1)}>
            انصراف
          </button>
        </div>
      </form>
    </div>
  );
}
