import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';

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
  const [card, setCard] = useState<CustomerCardDto | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    apiGet<CustomerCardDto>(`/api/customers/${id}/card`)
      .then(setCard)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ کارتِ مشتری.'));
  }, [id]);

  if (error) return <div style={{ color: 'var(--danger-700)' }}>{error}</div>;
  if (!card) return <div style={{ color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>;

  const usedPct = card.creditLimit > 0 ? Math.min(100, Math.round((card.balance / card.creditLimit) * 100)) : 0;

  return (
    <div>
      <Link to="/customers" style={{ fontSize: 'var(--text-sm)' }}>
        ← بازگشت به فهرستِ مشتریان
      </Link>

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
                <div style={{ fontSize: 14.5, fontWeight: 700, color: 'var(--text-strong)' }}>{card.name}</div>
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
                  <span>{usedPct}٪</span>
                </div>
              </>
            )}
          </div>
        </div>

        <div style={{ flex: 1, minWidth: 0, display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-3)' }}>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 12px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>سطحِ قیمت</div>
            <div style={{ fontSize: 16, fontWeight: 700, marginTop: 3 }}>{card.priceLevel}</div>
          </div>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 12px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>امتیازِ باشگاه</div>
            <div className="num" style={{ fontSize: 16, fontWeight: 700, marginTop: 3 }}>{numberFormat.format(card.loyaltyPoints)}</div>
          </div>
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: '10px 12px' }}>
            <div style={{ fontSize: 11, color: 'var(--text-muted)' }}>چکِ در جریان</div>
            <div className="num" style={{ fontSize: 16, fontWeight: 700, marginTop: 3, color: card.chequeInProgress > 0 ? 'var(--warning-700)' : 'var(--text-strong)' }}>
              {numberFormat.format(card.chequeInProgress)} ریال
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
