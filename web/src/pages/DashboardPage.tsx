import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiGet, ApiError } from '../api/client';
import { money } from '../lib/format';
import { todayJalaliString } from '../lib/jalali';
import { useAuth } from '../auth/AuthContext';

interface DashRecentInvoice { number: string; date: string; party: string; total: number; status: string }
interface DashChequeDue { chequeNumber: string; bankName: string; amount: number; dueDate: string; kind: string }
interface DashAlert { icon: string; text: string; level: string; nav: string }

interface DashboardDto {
  todaySales: number;
  receivable: number;
  overdueCheques: number;
  lowStockCount: number;
  recentSales: DashRecentInvoice[];
  chequesDue: DashChequeDue[];
  alerts: DashAlert[];
}

/** نگاشتِ کلیدِ ناوبریِ سرور (هم‌الگو با دسکتاپ: `NavigateToCommand`) به مسیرِ وب. */
const NAV_MAP: Record<string, string> = {
  ChequeBoard: '/cheques',
  Products: '/products',
  Receivables: '/treasury',
  InventoryReport: '/products',
};

function MiniKpi({ label, value, danger, onClick }: { label: string; value: string; danger?: boolean; onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      style={{
        textAlign: 'inherit', cursor: 'pointer', background: 'var(--bg-surface)', border: '1px solid var(--border)',
        borderRadius: 'var(--radius-md)', padding: '9px 12px', display: 'flex', flexDirection: 'column', gap: 3,
      }}
    >
      <span style={{ fontSize: 11, color: 'var(--text-muted)' }}>{label}</span>
      <span className="num" style={{ fontSize: 16, fontWeight: 700, color: danger ? 'var(--danger-700)' : 'var(--text-strong)' }}>{value}</span>
    </button>
  );
}

const QUICK_ACCESS = [
  { icon: '📄', label: 'ثبتِ سند', key: 'Ctrl+1', to: '/vouchers' },
  { icon: '🛒', label: 'فاکتورِ فروش', key: 'Ctrl+2', to: '/sales/new' },
  { icon: '🚚', label: 'فاکتورِ خرید', key: 'Ctrl+3', to: '/purchase/new' },
  { icon: '🖥', label: 'صندوقِ فروش', key: 'F12', to: '/pos' },
  { icon: '↹', label: 'انتقالِ انبار', key: 'Ctrl+4', to: '/warehouse' },
  { icon: '🧾', label: 'تابلویِ چک', key: 'Ctrl+5', to: '/cheques' },
  { icon: '👤', label: 'کارتِ مشتری', key: 'Ctrl+6', to: '/customers' },
  { icon: '📊', label: 'گزارش‌ها', key: 'Ctrl+R', to: '/trial-balance' },
];

export function DashboardPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const [data, setData] = useState<DashboardDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);

  useEffect(() => {
    apiGet<DashboardDto>(`/api/dashboard/full?today=${encodeURIComponent(todayJalaliString())}`)
      .then(setData)
      .catch((e) => setError(e instanceof ApiError ? e.message : 'خطا در بارگیریِ داشبورد.'));
  }, [refreshKey]);

  if (error) return <div style={{ color: 'var(--danger-700)' }}>{error}</div>;
  if (!data) return <div style={{ color: 'var(--text-muted)' }}>در حالِ بارگیری…</div>;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
      {/* ── تولبار: میز کار + خوش‌آمد + اکشن — هم‌الگو با DashboardView.xaml دسکتاپ ── */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: 8 }}>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: 12 }}>
          <span style={{ fontSize: 14, fontWeight: 700 }}>میز کار</span>
          <span style={{ fontSize: 13, color: 'var(--text-muted)' }}>خوش آمدید، {user?.fullName}</span>
          <span className="num" style={{ fontSize: 12, color: 'var(--text-muted)' }}>امروز — {todayJalaliString()}</span>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button type="button" className="btn btn-secondary btn-sm" onClick={() => setRefreshKey((k) => k + 1)}>🔄 بازخوانی</button>
          <button type="button" className="btn btn-primary btn-sm" onClick={() => navigate('/vouchers')}>+ سندِ جدید</button>
        </div>
      </div>

      {/* ── mini-kpi ── */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 8 }}>
        <MiniKpi label="فروشِ امروز" value={`${money(data.todaySales)} ریال`} onClick={() => navigate('/sales')} />
        <MiniKpi label="دریافتنیِ کل" value={money(data.receivable)} danger onClick={() => navigate('/treasury')} />
        <MiniKpi label="چکِ سررسید" value={`${money(data.overdueCheques)} فقره`} onClick={() => navigate('/cheques')} />
        <MiniKpi label="کالایِ کم‌موجود" value={`${money(data.lowStockCount)} قلم`} danger onClick={() => navigate('/products')} />
      </div>

      {/* ── ستونِ اصلی + ستونِ کنار ── */}
      <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) 320px', gap: 12, alignItems: 'start' }}>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12, minWidth: 0 }}>
          {/* دسترسیِ سریع */}
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 12 }}>
            <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>دسترسیِ سریع — عملیاتِ روزانه</div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: 8 }}>
              {QUICK_ACCESS.map((q) => (
                <button
                  key={q.label}
                  type="button"
                  onClick={() => navigate(q.to)}
                  style={{
                    display: 'flex', alignItems: 'center', gap: 10, cursor: 'pointer', textAlign: 'inherit',
                    background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 9, padding: '9px 12px',
                  }}
                >
                  <span style={{
                    width: 34, height: 34, borderRadius: 8, background: 'var(--blue-50)', display: 'flex',
                    alignItems: 'center', justifyContent: 'center', fontSize: 16, flex: 'none',
                  }}>{q.icon}</span>
                  <span>
                    <div style={{ fontSize: 12.5, fontWeight: 600 }}>{q.label}</div>
                    <div className="num" style={{ fontSize: 10.5, color: 'var(--text-muted)' }}>{q.key}</div>
                  </span>
                </button>
              ))}
            </div>
          </div>

          {/* اسنادِ اخیرِ من */}
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 12 }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
              <span style={{ fontSize: 13, fontWeight: 600 }}>اسنادِ اخیرِ من</span>
              <button type="button" onClick={() => navigate('/sales')} style={{ background: 'transparent', border: 'none', color: 'var(--blue-600)', cursor: 'pointer', fontSize: 11.5 }}>همهٔ اسناد ↗</button>
            </div>
            {data.recentSales.length === 0 ? (
              <div style={{ textAlign: 'center', color: 'var(--text-muted)', padding: '24px 0', fontSize: 12 }}>هنوز سند/فاکتوری ثبت نشده است.</div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 12.5 }}>
                <thead>
                  <tr style={{ color: 'var(--text-muted)', textAlign: 'right' }}>
                    <th style={{ padding: '4px 6px', fontWeight: 500 }}>شماره</th>
                    <th style={{ padding: '4px 6px', fontWeight: 500 }}>تاریخ</th>
                    <th style={{ padding: '4px 6px', fontWeight: 500 }}>شرح/مشتری</th>
                    <th style={{ padding: '4px 6px', fontWeight: 500 }}>مبلغ</th>
                    <th style={{ padding: '4px 6px', fontWeight: 500 }}>وضعیت</th>
                  </tr>
                </thead>
                <tbody>
                  {data.recentSales.map((r) => (
                    <tr key={r.number} style={{ borderTop: '1px solid var(--border)' }}>
                      <td className="num" style={{ padding: '5px 6px' }}>{r.number}</td>
                      <td className="num" style={{ padding: '5px 6px' }}>{r.date}</td>
                      <td style={{ padding: '5px 6px' }}>{r.party}</td>
                      <td className="num" style={{ padding: '5px 6px' }}>{money(r.total)}</td>
                      <td style={{ padding: '5px 6px' }}>{r.status}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>

        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {/* کارهایِ امروز */}
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 12 }}>
            <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>کارهایِ امروز</div>
            {data.alerts.length === 0 ? (
              <div style={{ color: 'var(--success-700, #1a7f37)', fontSize: 12 }}>✓ کارِ معوقی نیست — همه‌چیز مرتب است.</div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
                {data.alerts.map((a, i) => (
                  <div key={i} style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                    <span>{a.icon}</span>
                    <span style={{ fontSize: 12.5, flex: 1 }}>{a.text}</span>
                    <button type="button" onClick={() => navigate(NAV_MAP[a.nav] ?? '/')} style={{ background: 'transparent', border: 'none', color: 'var(--blue-600)', cursor: 'pointer', fontSize: 11 }}>برو ↗</button>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* سررسیدِ چک‌هایِ نزدیک */}
          <div style={{ background: 'var(--bg-surface)', border: '1px solid var(--border)', borderRadius: 'var(--radius-md)', padding: 12 }}>
            <div style={{ fontSize: 13, fontWeight: 600, marginBottom: 8 }}>سررسیدِ چک‌هایِ نزدیک</div>
            {data.chequesDue.length === 0 ? (
              <div style={{ color: 'var(--text-muted)', fontSize: 12 }}>چکی در سررسیدِ نزدیک نیست.</div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                {data.chequesDue.map((c) => (
                  <div key={c.chequeNumber} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <div>
                      <div style={{ fontSize: 12.5, fontWeight: 500 }}>{c.bankName}</div>
                      <div className="num" style={{ fontSize: 11, color: 'var(--text-muted)' }}>{c.chequeNumber}</div>
                    </div>
                    <div style={{ textAlign: 'left' }}>
                      <div className="num" style={{ fontSize: 12, fontWeight: 700 }}>{money(c.amount)}</div>
                      <div className="num" style={{ fontSize: 11, color: 'var(--warning-700, #9a6700)' }}>{c.dueDate}</div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
