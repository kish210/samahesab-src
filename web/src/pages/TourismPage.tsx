import { useEffect, useState } from 'react';
import { apiGet, apiPost, apiDelete, ApiError } from '../api/client';
import { DataTable, type Column } from '../components/DataTable';
import { PageHeader, StatusMessage } from '../components/PageHeader';
import { SearchSelect, type SearchSelectOption } from '../components/SearchSelect';
import { JalaliDateInput } from '../components/JalaliDateInput';
import { todayJalaliString } from '../lib/jalali';

interface AccountOption { id: number; code: string; name: string }
interface TourismSettingsDto {
  cashAccountId: number | null; receivableAccountId: number | null; revenueAccountId: number | null;
  cogsAccountId: number | null; supplierDepositAccountId: number | null; salesDiscountAccountId: number | null;
  depositDifferenceAccountId: number | null; commissionExpenseAccountId: number | null;
  salespersonPayableAccountId: number | null; bankAccountId: number | null;
  saleBaseAfterDiscountDefault: boolean; lowDepositThreshold: number; postPerSale: boolean; commissionThroughPayroll: boolean;
}
interface TourismProductRow {
  id: number; name: string; supplierPartyId: number; supplierName: string;
  purchasePrice: number; defaultSalePrice: number; requiresPassengerList: boolean; active: boolean;
}
interface SupplierOption { id: number; name: string; code: string }
interface TourismSaleRow {
  saleId: number; date: string; customerName: string; salespersonName: string;
  nearestTravelDate: string | null; passengerCount: number; netSale: number; profit: number;
  paymentMethod: string; isPosted: boolean;
}
interface TourismContext {
  branchId: number; fiscalYearId: number; salespersonPartyId: number | null; fullName: string; isSeller: boolean;
}
interface AvailabilityRow { productId: number; name: string; salePrice: number; remaining: number | null }

const numberFormat = new Intl.NumberFormat('fa-IR');
const REQUIRED_ACCOUNT_FIELDS: (keyof TourismSettingsDto)[] = ['cashAccountId', 'revenueAccountId', 'cogsAccountId', 'supplierDepositAccountId'];

/** ماژولِ اختیاریِ گردشگری — لایهٔ Application از پیش کامل بود (فروش/محصول/تنظیمات/گزارش)؛ کنترلرِ
 * APIِ پنلِ فروشنده (`TourismController`) هم از قبل بود اما فقط availability/sales/context را داشت —
 * تنظیماتِ نگاشتِ حساب‌ها و مدیریتِ محصول در وب هیچ راهی نداشتند (پیش‌نیازِ ثبتِ فروش). این صفحه
 * هر دو را اضافه می‌کند + فرمِ سادهٔ ثبتِ فروشِ تک‌ردیفی. */
export function TourismPage() {
  const [tab, setTab] = useState<'settings' | 'products' | 'sales'>('settings');

  const [accounts, setAccounts] = useState<AccountOption[]>([]);
  const [settings, setSettings] = useState<TourismSettingsDto | null>(null);
  const [settingsMsg, setSettingsMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);
  const [savingSettings, setSavingSettings] = useState(false);

  const [products, setProducts] = useState<TourismProductRow[]>([]);
  const [productsError, setProductsError] = useState<string | null>(null);
  const [suppliers, setSuppliers] = useState<SupplierOption[]>([]);
  const [newProduct, setNewProduct] = useState({ name: '', supplierPartyId: null as number | null, purchasePrice: '', defaultSalePrice: '', requiresPassengerList: false });
  const [savingProduct, setSavingProduct] = useState(false);

  const [sales, setSales] = useState<TourismSaleRow[]>([]);
  const [salesError, setSalesError] = useState<string | null>(null);
  const [ctx, setCtx] = useState<TourismContext | null>(null);
  const [availability, setAvailability] = useState<AvailabilityRow[]>([]);
  const [saleForm, setSaleForm] = useState({ productId: null as number | null, quantity: '1', unitSalePrice: '', discountAmount: '0', paymentMethod: 'نقدی', date: todayJalaliString() });
  const [savingSale, setSavingSale] = useState(false);
  const [saleMsg, setSaleMsg] = useState<{ kind: 'error' | 'success'; text: string } | null>(null);

  useEffect(() => {
    apiGet<AccountOption[]>('/api/accounts?leafOnly=true').then(setAccounts).catch(() => {});
    apiGet<TourismSettingsDto>('/api/tourism/settings').then(setSettings).catch(() => {});
  }, []);

  useEffect(() => {
    if (tab === 'products') {
      apiGet<TourismProductRow[]>('/api/tourism/products?activeOnly=false').then(setProducts)
        .catch((e) => setProductsError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ محصولات.'));
      apiGet<SupplierOption[]>('/api/suppliers').then(setSuppliers).catch(() => {});
    }
    if (tab === 'sales') {
      apiGet<TourismSaleRow[]>('/api/tourism/sales').then(setSales)
        .catch((e) => setSalesError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ فروش‌ها.'));
      apiGet<TourismContext>('/api/tourism/context').then(setCtx).catch(() => {});
      apiGet<AvailabilityRow[]>('/api/tourism/availability').then(setAvailability).catch(() => {});
    }
  }, [tab]);

  const accountOptions: SearchSelectOption[] = accounts.map((a) => ({ id: a.id, label: a.name, sublabel: a.code }));

  function accountField(key: keyof TourismSettingsDto, label: string) {
    if (!settings) return null;
    const value = settings[key] as number | null;
    return (
      <div className="frow">
        <label>{label}</label>
        <SearchSelect options={accountOptions} value={value} placeholder="انتخابِ حساب…"
          onChange={(id) => setSettings({ ...settings, [key]: id })} />
      </div>
    );
  }

  async function saveSettings() {
    if (!settings) return;
    setSavingSettings(true);
    setSettingsMsg(null);
    try {
      await apiPost('/api/tourism/settings', settings);
      setSettingsMsg({ kind: 'success', text: 'تنظیمات ذخیره شد.' });
    } catch (e) {
      setSettingsMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ذخیره ناموفق بود.' });
    } finally {
      setSavingSettings(false);
    }
  }

  const settingsIncomplete = settings != null && REQUIRED_ACCOUNT_FIELDS.some((f) => settings[f] == null);

  async function saveProduct() {
    if (!newProduct.name.trim() || !newProduct.supplierPartyId) return;
    setSavingProduct(true);
    try {
      await apiPost('/api/tourism/products', {
        id: 0, name: newProduct.name.trim(), supplierPartyId: newProduct.supplierPartyId,
        purchasePrice: Number(newProduct.purchasePrice) || 0, defaultSalePrice: Number(newProduct.defaultSalePrice) || 0,
        productGroupId: null, requiresPassengerList: newProduct.requiresPassengerList, active: true,
      });
      setNewProduct({ name: '', supplierPartyId: null, purchasePrice: '', defaultSalePrice: '', requiresPassengerList: false });
      setProducts(await apiGet<TourismProductRow[]>('/api/tourism/products?activeOnly=false'));
    } catch (e) {
      setProductsError(e instanceof ApiError ? e.message : 'ذخیرهٔ محصول ناموفق بود.');
    } finally {
      setSavingProduct(false);
    }
  }

  async function removeProduct(id: number) {
    try {
      await apiDelete(`/api/tourism/products/${id}`);
      setProducts(await apiGet<TourismProductRow[]>('/api/tourism/products?activeOnly=false'));
    } catch (e) {
      setProductsError(e instanceof ApiError ? e.message : 'حذفِ محصول ناموفق بود.');
    }
  }

  async function submitSale() {
    if (!ctx || !saleForm.productId) return;
    setSavingSale(true);
    setSaleMsg(null);
    try {
      const r = await apiPost<{ saleId: number }>('/api/tourism/sales', {
        branchId: ctx.branchId, fiscalYearId: ctx.fiscalYearId, date: saleForm.date,
        salespersonPartyId: ctx.salespersonPartyId ?? 0, customerPartyId: null,
        paymentMethod: saleForm.paymentMethod,
        lines: [{
          productId: saleForm.productId, quantity: Number(saleForm.quantity) || 1,
          unitSalePrice: Number(saleForm.unitSalePrice) || 0, discountAmount: Number(saleForm.discountAmount) || 0,
        }],
      });
      setSaleMsg({ kind: 'success', text: `فروش با شمارهٔ ${r.saleId} ثبت شد.` });
      setSaleForm({ productId: null, quantity: '1', unitSalePrice: '', discountAmount: '0', paymentMethod: 'نقدی', date: todayJalaliString() });
      setSales(await apiGet<TourismSaleRow[]>('/api/tourism/sales'));
    } catch (e) {
      setSaleMsg({ kind: 'error', text: e instanceof ApiError ? e.message : 'ثبتِ فروش ناموفق بود.' });
    } finally {
      setSavingSale(false);
    }
  }

  const productColumns: Column<TourismProductRow>[] = [
    { key: 'name', header: 'محصول', render: (r) => r.name },
    { key: 'supplier', header: 'تأمین‌کننده', render: (r) => r.supplierName },
    { key: 'purchase', header: 'بهایِ خرید', numeric: true, render: (r) => <span className="num">{numberFormat.format(r.purchasePrice)}</span> },
    { key: 'sale', header: 'قیمتِ فروش', numeric: true, render: (r) => <span className="num">{numberFormat.format(r.defaultSalePrice)}</span> },
    { key: 'pax', header: 'لیستِ مسافر', render: (r) => (r.requiresPassengerList ? 'الزامی' : '—') },
    { key: 'status', header: 'وضعیت', render: (r) => <span className={`badge ${r.active ? 'badge-green' : 'badge-gray'}`}>{r.active ? 'فعال' : 'غیرفعال'}</span> },
    { key: 'action', header: '', render: (r) => r.active ? <button className="btn btn-ghost btn-sm" onClick={() => removeProduct(r.id)}>غیرفعال‌سازی</button> : null },
  ];

  const saleColumns: Column<TourismSaleRow>[] = [
    { key: 'date', header: 'تاریخ', render: (r) => <span className="num mut">{r.date}</span> },
    { key: 'customer', header: 'مشتری', render: (r) => r.customerName },
    { key: 'sp', header: 'فروشنده', render: (r) => r.salespersonName },
    { key: 'travel', header: 'نزدیک‌ترین سفر', render: (r) => r.nearestTravelDate ? <span className="num">{r.nearestTravelDate}</span> : '—' },
    { key: 'pax', header: 'مسافر', numeric: true, render: (r) => <span className="num">{r.passengerCount}</span> },
    { key: 'net', header: 'فروشِ خالص', numeric: true, render: (r) => <span className="num">{numberFormat.format(r.netSale)}</span> },
    { key: 'pay', header: 'روشِ پرداخت', render: (r) => r.paymentMethod },
    { key: 'posted', header: 'سند', render: (r) => <span className={`badge ${r.isPosted ? 'badge-green' : 'badge-amber'}`}>{r.isPosted ? 'صادرشده' : 'بدونِ سند'}</span> },
  ];

  return (
    <div>
      <PageHeader title="گردشگری" />

      <div className="minitabs">
        <button type="button" className={tab === 'settings' ? 'on' : ''} onClick={() => setTab('settings')}>تنظیمات</button>
        <button type="button" className={tab === 'products' ? 'on' : ''} onClick={() => setTab('products')}>محصولات</button>
        <button type="button" className={tab === 'sales' ? 'on' : ''} onClick={() => setTab('sales')}>فروش</button>
      </div>

      {tab === 'settings' && (
        <div className="gbox" style={{ marginTop: 'var(--space-4)', maxWidth: 480 }}>
          <div className="gh">نگاشتِ حساب‌هایِ کنترلی</div>
          <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
            {settings == null ? (
              <StatusMessage kind="muted">در حالِ بارگیری…</StatusMessage>
            ) : (
              <>
                {accountField('cashAccountId', 'حسابِ نقد')}
                {accountField('receivableAccountId', 'حسابِ دریافتنی')}
                {accountField('revenueAccountId', 'حسابِ درآمد')}
                {accountField('cogsAccountId', 'حسابِ بهایِ تمام‌شده')}
                {accountField('supplierDepositAccountId', 'حسابِ ودیعهٔ تأمین‌کننده')}
                {accountField('salesDiscountAccountId', 'حسابِ تخفیفِ فروش')}
                {accountField('commissionExpenseAccountId', 'حسابِ هزینهٔ پورسانت')}
                {accountField('salespersonPayableAccountId', 'حسابِ پرداختنیِ فروشنده')}
                {accountField('bankAccountId', 'حسابِ بانک')}
                <div>
                  <button className="btn btn-primary btn-sm" disabled={savingSettings} onClick={saveSettings}>
                    {savingSettings ? 'در حالِ ذخیره…' : 'ذخیره'}
                  </button>
                </div>
                {settingsMsg && <StatusMessage kind={settingsMsg.kind}>{settingsMsg.text}</StatusMessage>}
                {settingsIncomplete && !settingsMsg && (
                  <StatusMessage kind="muted">حساب‌هایِ نقد/درآمد/بهایِ‌تمام‌شده/ودیعه تا تکمیل نشوند، «ثبتِ فروش» رد می‌شود.</StatusMessage>
                )}
              </>
            )}
          </div>
        </div>
      )}

      {tab === 'products' && (
        <div style={{ marginTop: 'var(--space-4)' }}>
          <div className="gbox" style={{ marginBottom: 'var(--space-4)', maxWidth: 560 }}>
            <div className="gh">محصولِ نو</div>
            <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
              <div className="frow">
                <label>نام</label>
                <input className="input-c" value={newProduct.name} onChange={(e) => setNewProduct({ ...newProduct, name: e.target.value })} />
              </div>
              <div className="frow">
                <label>تأمین‌کننده</label>
                <SearchSelect options={suppliers.map((s) => ({ id: s.id, label: s.name, sublabel: s.code }))}
                  value={newProduct.supplierPartyId} placeholder="انتخابِ تأمین‌کننده…"
                  onChange={(id) => setNewProduct({ ...newProduct, supplierPartyId: id })} />
              </div>
              <div className="frow">
                <label>بهایِ خرید</label>
                <input className="input-c num" style={{ direction: 'ltr' }} value={newProduct.purchasePrice}
                  onChange={(e) => setNewProduct({ ...newProduct, purchasePrice: e.target.value })} />
              </div>
              <div className="frow">
                <label>قیمتِ فروش</label>
                <input className="input-c num" style={{ direction: 'ltr' }} value={newProduct.defaultSalePrice}
                  onChange={(e) => setNewProduct({ ...newProduct, defaultSalePrice: e.target.value })} />
              </div>
              <div className="frow">
                <label>لیستِ مسافر</label>
                <input type="checkbox" checked={newProduct.requiresPassengerList}
                  onChange={(e) => setNewProduct({ ...newProduct, requiresPassengerList: e.target.checked })} />
              </div>
              <div>
                <button className="btn btn-primary btn-sm" disabled={savingProduct} onClick={saveProduct}>
                  {savingProduct ? 'در حالِ ذخیره…' : 'افزودنِ محصول'}
                </button>
              </div>
            </div>
          </div>
          {productsError && <StatusMessage kind="error">{productsError}</StatusMessage>}
          {!productsError && <DataTable columns={productColumns} rows={products} rowKey={(r) => r.id} emptyText="محصولی ثبت نشده است." />}
        </div>
      )}

      {tab === 'sales' && (
        <div style={{ marginTop: 'var(--space-4)' }}>
          <div className="gbox" style={{ marginBottom: 'var(--space-4)', maxWidth: 560 }}>
            <div className="gh">ثبتِ فروش</div>
            <div className="gb" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
              <div className="frow">
                <label>تاریخ</label>
                <JalaliDateInput value={saleForm.date} onChange={(v) => setSaleForm({ ...saleForm, date: v })} />
              </div>
              <div className="frow">
                <label>محصول</label>
                <SearchSelect options={availability.map((a) => ({ id: a.productId, label: a.name, sublabel: numberFormat.format(a.salePrice) }))}
                  value={saleForm.productId} placeholder="انتخابِ محصول…"
                  onChange={(id) => {
                    const p = availability.find((a) => a.productId === id);
                    setSaleForm({ ...saleForm, productId: id, unitSalePrice: p ? String(p.salePrice) : saleForm.unitSalePrice });
                  }} />
              </div>
              <div className="frow">
                <label>تعداد</label>
                <input className="input-c num" style={{ direction: 'ltr' }} value={saleForm.quantity}
                  onChange={(e) => setSaleForm({ ...saleForm, quantity: e.target.value })} />
              </div>
              <div className="frow">
                <label>قیمتِ واحد</label>
                <input className="input-c num" style={{ direction: 'ltr' }} value={saleForm.unitSalePrice}
                  onChange={(e) => setSaleForm({ ...saleForm, unitSalePrice: e.target.value })} />
              </div>
              <div className="frow">
                <label>تخفیف</label>
                <input className="input-c num" style={{ direction: 'ltr' }} value={saleForm.discountAmount}
                  onChange={(e) => setSaleForm({ ...saleForm, discountAmount: e.target.value })} />
              </div>
              <div className="frow">
                <label>روشِ پرداخت</label>
                <select className="select-c" value={saleForm.paymentMethod}
                  onChange={(e) => setSaleForm({ ...saleForm, paymentMethod: e.target.value })}>
                  <option value="نقدی">نقدی</option>
                  <option value="کارت‌خوان">کارت‌خوان</option>
                  <option value="نسیه">نسیه</option>
                </select>
              </div>
              <div>
                <button className="btn btn-primary btn-sm" disabled={savingSale || !ctx} onClick={submitSale}>
                  {savingSale ? 'در حالِ ثبت…' : 'ثبتِ فروش'}
                </button>
              </div>
              {saleMsg && <StatusMessage kind={saleMsg.kind}>{saleMsg.text}</StatusMessage>}
              {ctx && ctx.fiscalYearId === 0 && (
                <StatusMessage kind="error">سالِ مالیِ فعال یافت نشد — ثبتِ فروش ممکن نیست.</StatusMessage>
              )}
            </div>
          </div>
          {salesError && <StatusMessage kind="error">{salesError}</StatusMessage>}
          {!salesError && <DataTable columns={saleColumns} rows={sales} rowKey={(r) => r.saleId} emptyText="فروشی ثبت نشده است." />}
        </div>
      )}
    </div>
  );
}
