import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { apiGet, apiPost, ApiError } from '../api/client';
import { money } from '../lib/format';
import { todayJalaliString } from '../lib/jalali';
import { StatusMessage } from '../components/PageHeader';

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
  loyaltyPoints: number;
  creditDays: number;
  isActive: boolean;
  balance: number;
  creditLimit: number;
  chequeInProgress: number;
  isCustomer: boolean;
  isSupplier: boolean;
}

const numberFormat = new Intl.NumberFormat('fa-IR');

interface LoyaltyTxnDto {
  points: number;
  type: string;
  description: string | null;
  date: string;
}
interface CustomerLoyaltyDto {
  customerId: number;
  balance: number;
  recent: LoyaltyTxnDto[];
}
interface PartyLedgerRow {
  date: string;
  docType: string;
  docNumber: string | null;
  description: string | null;
  amount: number;
  runningBalance: number;
}
interface SalesInvoiceRowDto {
  id: number;
  number: string;
  date: string;
  customerName: string;
  total: number;
  paid: number;
  remain: number;
  status: string;
}
interface ChequeRowDto {
  id: number;
  chequeNumber: string;
  bankName: string;
  amount: number;
  dueDate: string;
  status: number; // InProcess=0, Cleared=1, Returned=2, Transferred=3, Cancelled=4
}
const CHEQUE_STATUS_LABEL: Record<number, string> = { 0: 'در جریان', 1: 'وصول‌شده', 2: 'برگشتی', 3: 'واگذارشده', 4: 'ابطال‌شده' };

function Kv({ label, value }: { label: string; value: string | null | undefined }) {
  if (!value) return null;
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--text-sm)', padding: '6px 0', borderBottom: '1px solid var(--gray-100)' }}>
      <span style={{ color: 'var(--text-muted)' }}>{label}</span>
      <span style={{ color: 'var(--text-strong)', fontWeight: 500 }}>{value}</span>
    </div>
  );
}

export function CustomerCardPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [card, setCard] = useState<CustomerCardDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loyalty, setLoyalty] = useState<CustomerLoyaltyDto | null>(null);
  const [loyaltyError, setLoyaltyError] = useState<string | null>(null);
  const [amount, setAmount] = useState('');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const [ledger, setLedger] = useState<PartyLedgerRow[] | null>(null);
  const [invoices, setInvoices] = useState<SalesInvoiceRowDto[] | null>(null);
  const [cheques, setCheques] = useState<ChequeRowDto[] | null>(null);
  const [activeMiniTab, setActiveMiniTab] = useState<'ledger' | 'invoices' | 'cheques' | 'loyalty'>('ledger');

  function loadLoyalty() {
    if (!id) return;
    apiGet<CustomerLoyaltyDto>(`/api/loyalty/${id}`).then(setLoyalty).catch(() => {});
  }

  useEffect(() => {
    if (!id) return;
    apiGet<CustomerCardDto>(`/api/customers/${id}/card`)
      .then(setCard)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ کارتِ مشتری.'));
    apiGet<PartyLedgerRow[]>(`/api/customers/${id}/ledger`).then(setLedger).catch(() => setLedger([]));
    apiGet<SalesInvoiceRowDto[]>(`/api/sales/invoices?customerId=${id}`).then(setInvoices).catch(() => setInvoices([]));
    apiGet<ChequeRowDto[]>(`/api/cheques?partyId=${id}`).then(setCheques).catch(() => setCheques([]));
    loadLoyalty();
  }, [id]);

  const currentJalaliYear = todayJalaliString().slice(0, 4);
  const invoiceCount = invoices?.length ?? 0;
  const ytdPurchases = (invoices ?? []).filter((i) => i.date.startsWith(currentJalaliYear)).reduce((s, i) => s + i.total, 0);

  async function award() {
    if (!id || !Number(amount)) return;
    setBusy(true);
    setLoyaltyError(null);
    try {
      await apiPost(`/api/loyalty/award`, { customerId: Number(id), purchaseAmount: Number(amount), reason: reason || 'دستی' });
      setAmount('');
      setReason('');
      loadLoyalty();
    } catch (e) {
      setLoyaltyError(e instanceof ApiError ? e.message : 'ثبتِ امتیاز ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  async function redeem() {
    if (!id || !Number(amount)) return;
    setBusy(true);
    setLoyaltyError(null);
    try {
      await apiPost(`/api/loyalty/redeem`, { customerId: Number(id), points: Number(amount), reason: reason || 'دستی' });
      setAmount('');
      setReason('');
      loadLoyalty();
    } catch (e) {
      setLoyaltyError(e instanceof ApiError ? e.message : 'استفاده از امتیاز ناموفق بود.');
    } finally {
      setBusy(false);
    }
  }

  if (error) return <div style={{ color: 'var(--danger-700)' }}>{error}</div>;
  if (!card) return <div style={{ color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>;

  const usedPct = card.creditLimit > 0 ? Math.min(100, Math.round((card.balance / card.creditLimit) * 100)) : 0;

  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <Link to="/customers" style={{ fontSize: 'var(--text-sm)' }}>
          ← بازگشت به فهرستِ مشتریان
        </Link>
        <div style={{ display: 'flex', gap: 6 }}>
          <button className="btn btn-secondary btn-sm" onClick={() => navigate(`/parties/${id}/edit`)}>ویرایش</button>
          <button className="btn btn-secondary btn-sm" onClick={() => navigate(`/sales/new?customerId=${id}`)}>فاکتورِ جدید</button>
          <button className="btn btn-secondary btn-sm" onClick={() => navigate('/treasury')}>دریافتِ وجه</button>
          <button className="btn btn-secondary btn-sm" onClick={() => window.print()}>صورت‌حساب</button>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 'var(--space-4)', marginTop: 'var(--space-4)', alignItems: 'flex-start' }}>
        <div style={{ width: 280, flex: 'none', display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 14 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 11 }}>
              <div
                style={{
                  width: 46, height: 46, borderRadius: 10, background: 'var(--blue-100)', color: 'var(--blue-700)',
                  display: 'flex', alignItems: 'center', justifyContent: 'center', fontWeight: 800, fontSize: 16, flex: 'none',
                }}
              >
                {card.name?.slice(0, 2) ?? '؟'}
              </div>
              <div>
                <div style={{ fontSize: 14.5, fontWeight: 700, color: 'var(--text-strong)', display: 'flex', alignItems: 'center', gap: 6 }}>
                  {card.name}
                  <span className={`st ${card.isActive ? 'g' : 'n'}`}><i />{card.isActive ? 'فعال' : 'غیرفعال'}</span>
                </div>
                <div style={{ fontSize: 11.5, color: 'var(--text-muted)' }}>کد: {card.code}</div>
              </div>
            </div>
            <hr style={{ margin: '11px 0', border: 'none', borderTop: '1px solid var(--gray-100)' }} />
            <Kv label="شخص رابط" value={card.contactPerson} />
            <Kv label="موبایل" value={card.mobile} />
            <Kv label="تلفن" value={card.phone} />
            <Kv label="شناسه ملی" value={card.nationalCode} />
            <Kv label="کد اقتصادی" value={card.economicCode} />
            <Kv label="آدرس" value={card.address} />
            <Kv label="ویزیتور" value={card.visitor} />
          </div>

          <div
            style={{
              borderRadius: 'var(--radius-md)',
              padding: '12px 14px',
              background: card.balance > 0 ? 'var(--danger-50)' : 'var(--success-50)',
              border: `1px solid ${card.balance > 0 ? '#E8C5C3' : 'var(--success-500)'}`,
            }}
          >
            <div style={{ fontSize: 11, color: card.balance > 0 ? 'var(--danger-700)' : 'var(--success-700)' }}>مانده حساب</div>
            <div className="num" style={{ fontSize: 20, fontWeight: 800, color: card.balance > 0 ? 'var(--danger-700)' : 'var(--success-700)', marginTop: 2 }}>
              {numberFormat.format(card.balance)} ریال
            </div>
            {card.creditLimit > 0 && (
              <>
                <div style={{ height: 5, borderRadius: 99, background: '#fff', marginTop: 7, overflow: 'hidden' }}>
                  <div style={{ height: '100%', width: `${usedPct}%`, background: 'var(--danger-500)', borderRadius: 99 }} />
                </div>
                <div style={{ fontSize: 11, color: 'var(--text-muted)', marginTop: 4, display: 'flex', justifyContent: 'space-between' }}>
                  <span>سقفِ اعتبار: {numberFormat.format(card.creditLimit)}</span>
                  <span>{numberFormat.format(usedPct)}٪</span>
                </div>
              </>
            )}
          </div>
        </div>

        <div style={{ flex: 1, minWidth: 0, display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: 'var(--space-3)' }}>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 12px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>جمعِ خریدِ امسال</div>
            <div className="num" style={{ fontSize: 16, fontWeight: 700, marginTop: 3 }}>{numberFormat.format(ytdPurchases)}</div>
          </div>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 12px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>تعدادِ فاکتور</div>
            <div className="num" style={{ fontSize: 16, fontWeight: 700, marginTop: 3 }}>{numberFormat.format(invoiceCount)}</div>
          </div>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 12px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>سطحِ قیمت</div>
            <div style={{ fontSize: 16, fontWeight: 700, marginTop: 3 }}>{card.priceLevel}</div>
          </div>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 12px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>امتیازِ باشگاه</div>
            <div className="num" style={{ fontSize: 16, fontWeight: 700, marginTop: 3 }}>{numberFormat.format(loyalty?.balance ?? card.loyaltyPoints)}</div>
          </div>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 12px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>چکِ در جریان</div>
            <div className="num" style={{ fontSize: 16, fontWeight: 700, marginTop: 3, color: card.chequeInProgress > 0 ? 'var(--warning-700)' : 'var(--text-strong)' }}>
              {numberFormat.format(card.chequeInProgress)} ریال
            </div>
          </div>

          <div style={{ gridColumn: '1 / -1', display: 'flex', flexDirection: 'column', minHeight: 0 }}>
            {/* پورتِ `.minitabs`ِ design-system (customer-card.html) — گردشِ حساب (U-PARTY-LEDGER،
                رویِ GetPartyLedgerQueryِ ازقبل‌موجودِ بدونِ UI) + فاکتورها/چک‌ها (U-WEB-CUSTCARD-1،
                رویِ endpointِ ازقبل‌موجودی که فقط کنترلر customerId/partyId را فراموش کرده بود
                پاس بدهد) + باشگاهِ مشتریان. */}
            <div className="minitabs">
              <button type="button" className={activeMiniTab === 'ledger' ? 'on' : ''} onClick={() => setActiveMiniTab('ledger')}>گردشِ حساب</button>
              <button type="button" className={activeMiniTab === 'invoices' ? 'on' : ''} onClick={() => setActiveMiniTab('invoices')}>فاکتورها</button>
              <button type="button" className={activeMiniTab === 'cheques' ? 'on' : ''} onClick={() => setActiveMiniTab('cheques')}>چک‌ها</button>
              <button type="button" className={activeMiniTab === 'loyalty' ? 'on' : ''} onClick={() => setActiveMiniTab('loyalty')}>باشگاهِ مشتریان</button>
            </div>

            {activeMiniTab === 'invoices' && (
              <div className="dgrid-wrap" style={{ borderRadius: '0 0 8px 8px', borderTop: 'none' }}>
                <table className="dgrid">
                  <thead>
                    <tr>
                      <th style={{ width: 90 }}>شماره</th>
                      <th style={{ width: 90 }}>تاریخ</th>
                      <th style={{ width: 90 }} className="c">وضعیت</th>
                      <th style={{ width: 115 }} className="num">مبلغِ کل</th>
                      <th style={{ width: 115 }} className="num">پرداختی</th>
                      <th style={{ width: 115 }} className="num">مانده</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(invoices ?? []).map((r) => (
                      <tr key={r.id}>
                        <td className="num">{r.number}</td>
                        <td className="num mut">{r.date}</td>
                        <td className="c">{r.status}</td>
                        <td className="num">{numberFormat.format(r.total)}</td>
                        <td className="num">{numberFormat.format(r.paid)}</td>
                        <td className="num strong">{numberFormat.format(r.remain)}</td>
                      </tr>
                    ))}
                    {invoices !== null && invoices.length === 0 && (
                      <tr>
                        <td colSpan={6} style={{ height: 'auto', padding: 'var(--space-4)', textAlign: 'center', color: 'var(--text-muted)', whiteSpace: 'normal' }}>
                          فاکتوری ثبت نشده.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            )}

            {activeMiniTab === 'cheques' && (
              <div className="dgrid-wrap" style={{ borderRadius: '0 0 8px 8px', borderTop: 'none' }}>
                <table className="dgrid">
                  <thead>
                    <tr>
                      <th style={{ width: 100 }}>شمارهٔ چک</th>
                      <th>بانک</th>
                      <th style={{ width: 90 }}>سررسید</th>
                      <th style={{ width: 115 }} className="num">مبلغ</th>
                      <th style={{ width: 100 }} className="c">وضعیت</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(cheques ?? []).map((c) => (
                      <tr key={c.id}>
                        <td className="num">{c.chequeNumber}</td>
                        <td>{c.bankName}</td>
                        <td className="num mut">{c.dueDate}</td>
                        <td className="num strong">{numberFormat.format(c.amount)}</td>
                        <td className="c">{CHEQUE_STATUS_LABEL[c.status] ?? c.status}</td>
                      </tr>
                    ))}
                    {cheques !== null && cheques.length === 0 && (
                      <tr>
                        <td colSpan={5} style={{ height: 'auto', padding: 'var(--space-4)', textAlign: 'center', color: 'var(--text-muted)', whiteSpace: 'normal' }}>
                          چکی ثبت نشده.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            )}

            {activeMiniTab === 'ledger' && (
              <div className="dgrid-wrap" style={{ borderRadius: '0 0 8px 8px', borderTop: 'none' }}>
                <table className="dgrid">
                  <thead>
                    <tr>
                      <th style={{ width: 80 }}>تاریخ</th>
                      <th style={{ width: 90 }}>نوع</th>
                      <th style={{ width: 90 }}>سند</th>
                      <th>شرح</th>
                      <th style={{ width: 115 }} className="num">مبلغ</th>
                      <th style={{ width: 125 }} className="num">مانده</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(ledger ?? []).map((r, i) => (
                      <tr key={i}>
                        <td className="num mut">{r.date}</td>
                        <td>{r.docType}</td>
                        <td className="num">{r.docNumber ?? '—'}</td>
                        <td>{r.description ?? ''}</td>
                        <td className="num" style={{ color: r.amount >= 0 ? undefined : 'var(--success-700)' }}>{numberFormat.format(r.amount)}</td>
                        <td className="num strong">{numberFormat.format(r.runningBalance)}</td>
                      </tr>
                    ))}
                    {ledger !== null && ledger.length === 0 && (
                      <tr>
                        <td colSpan={6} style={{ height: 'auto', padding: 'var(--space-4)', textAlign: 'center', color: 'var(--text-muted)', whiteSpace: 'normal' }}>
                          گردشی ثبت نشده.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            )}

            {activeMiniTab === 'loyalty' && (
              <div style={{
                background: 'var(--bg-surface)', border: '1px solid var(--border-strong)', borderTop: 'none',
                borderRadius: '0 0 8px 8px', padding: 14,
              }}>
                <div style={{ display: 'flex', gap: 8, alignItems: 'end', flexWrap: 'wrap', marginBottom: 10 }}>
                  <div className="field" style={{ maxWidth: 160 }}>
                    <label className="label">مبلغ/تعدادِ امتیاز</label>
                    <input className="input num" type="number" min="0" value={amount} onChange={(e) => setAmount(e.target.value)} />
                  </div>
                  <div className="field" style={{ maxWidth: 220 }}>
                    <label className="label">علت</label>
                    <input className="input" value={reason} onChange={(e) => setReason(e.target.value)} placeholder="مثلاً خریدِ حضوری" />
                  </div>
                  <button type="button" className="btn btn-primary btn-sm" disabled={busy || !Number(amount)} onClick={award}>
                    افزودنِ امتیاز (از رویِ مبلغِ خرید)
                  </button>
                  <button type="button" className="btn btn-secondary btn-sm" disabled={busy || !Number(amount)} onClick={redeem}>
                    استفاده از امتیاز
                  </button>
                </div>
                {loyaltyError && <StatusMessage kind="error">{loyaltyError}</StatusMessage>}
                {loyalty && loyalty.recent.length > 0 ? (
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
                    <thead>
                      <tr style={{ color: 'var(--text-muted)', textAlign: 'right' }}>
                        <th style={{ padding: '4px 6px', fontWeight: 500 }}>تاریخ</th>
                        <th style={{ padding: '4px 6px', fontWeight: 500 }}>نوع</th>
                        <th style={{ padding: '4px 6px', fontWeight: 500 }}>امتیاز</th>
                        <th style={{ padding: '4px 6px', fontWeight: 500 }}>شرح</th>
                      </tr>
                    </thead>
                    <tbody>
                      {loyalty.recent.map((t, i) => (
                        <tr key={i} style={{ borderTop: '1px solid var(--border)' }}>
                          <td className="num" style={{ padding: '5px 6px' }}>{new Date(t.date).toLocaleDateString('fa-IR')}</td>
                          <td style={{ padding: '5px 6px' }}>{t.type}</td>
                          <td className="num" style={{ padding: '5px 6px', color: t.points >= 0 ? 'var(--success-700)' : 'var(--danger-700)' }}>{money(t.points)}</td>
                          <td style={{ padding: '5px 6px' }}>{t.description ?? ''}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                ) : (
                  <div style={{ color: 'var(--text-muted)', fontSize: 12 }}>هنوز تراکنشِ امتیازی ثبت نشده است.</div>
                )}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
