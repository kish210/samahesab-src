import { useEffect, useState } from 'react';
import { apiGet } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { PageHeader } from '../components/PageHeader';

interface LicenseStatus { isExpired: boolean; daysRemaining: number | null; expiresUtc: string | null }

function Kv({ label, value }: { label: string; value: string }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: 'var(--text-sm)', padding: '7px 0', borderBottom: '1px solid var(--gray-100)' }}>
      <span style={{ color: 'var(--text-muted)' }}>{label}</span>
      <span style={{ fontWeight: 500 }}>{value}</span>
    </div>
  );
}

/** «تنظیمات → دربارهٔ سیستم» — وب برخلافِ دسکتاپ هیچ صفحهٔ تنظیماتی نداشت. این صفحه اطلاعاتِ
 * پایه (نسخه/مجوز/کاربر) را نشان می‌دهد؛ تنظیماتِ شرکت/کاربران (`CompanySettingsView`/
 * `UserManagementView` در دسکتاپ هم فقط استابِ «در حالِ توسعه»اند) خارج از حدودِ این افزودن است. */
export function SettingsPage() {
  const { user } = useAuth();
  const [license, setLicense] = useState<LicenseStatus | null>(null);

  useEffect(() => {
    apiGet<LicenseStatus>('/api/license/status').then(setLicense).catch(() => {});
  }, []);

  return (
    <div>
      <PageHeader title="دربارهٔ سیستم" />
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(260px, 1fr))', gap: 'var(--space-4)', alignItems: 'start' }}>
        <div className="gbox">
          <div className="gh">برنامه</div>
          <div className="gb">
            <Kv label="نامِ برنامه" value="سما حساب" />
            <Kv label="نسخه" value="۲٫۹" />
          </div>
        </div>

        <div className="gbox">
          <div className="gh">مجوزِ نصب</div>
          <div className="gb">
            {license == null ? (
              <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-sm)' }}>در حالِ بارگیری…</div>
            ) : (
              <>
                <Kv label="وضعیت" value={license.isExpired ? 'دورهٔ رایگان تمام شده' : 'فعال (دورهٔ رایگانِ یک‌ساله)'} />
                <Kv label="روزهایِ باقی‌مانده" value={license.daysRemaining != null ? `${license.daysRemaining} روز` : '—'} />
              </>
            )}
          </div>
        </div>

        <div className="gbox">
          <div className="gh">کاربرِ جاری</div>
          <div className="gb">
            <Kv label="نام" value={user?.fullName ?? '—'} />
            <Kv label="نامِ کاربری" value={user?.username ?? '—'} />
          </div>
        </div>
      </div>
    </div>
  );
}
