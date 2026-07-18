import { todayJalaliString } from '../lib/jalali';

/** الگویِ تاریخِ شمسیِ موردِ انتظارِ سرور: yyyy/MM/dd */
const JALALI_RE = /^\d{4}\/\d{2}\/\d{2}$/;

export function isValidJalali(value: string): boolean {
  if (!JALALI_RE.test(value)) return false;
  const [y, m, d] = value.split('/').map(Number);
  if (y < 1300 || y > 1500) return false;
  if (m < 1 || m > 12) return false;
  // ماه‌های ۱–۶ سی‌ویک‌روزه، ۷–۱۱ سی‌روزه، ۱۲ حداکثر ۳۰ (سالِ کبیسه)
  const maxDay = m <= 6 ? 31 : m <= 11 ? 30 : 30;
  return d >= 1 && d <= maxDay;
}

interface Props {
  value: string;
  onChange: (value: string) => void;
  label?: string;
}

/**
 * ورودیِ تاریخِ شمسی — پیش‌تر فرم‌ها همیشه «امروز» را هاردکد می‌فرستادند و کاربر نمی‌توانست
 * تاریخِ دیگری ثبت کند. ورودیِ متنیِ ساده با اعتبارسنجیِ الگو + دکمهٔ «امروز»
 * (تقویمِ گرافیکیِ شمسی عمداً اضافه نشده تا وابستگیِ نو به پروژه تحمیل نشود).
 */
export function JalaliDateInput({ value, onChange, label }: Props) {
  const invalid = value.length > 0 && !isValidJalali(value);
  return (
    <div className="field">
      {label && <label className="label">{label}</label>}
      <div style={{ display: 'flex', gap: 6 }}>
        <input
          className={`input${invalid ? ' is-error' : ''}`}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder="1405/04/26"
          style={{ direction: 'ltr', textAlign: 'center' }}
        />
        <button type="button" className="btn btn-secondary btn-sm" onClick={() => onChange(todayJalaliString())}>
          امروز
        </button>
      </div>
      {invalid && <div className="hint" style={{ color: 'var(--danger-700)' }}>قالبِ تاریخ باید «۱۴۰۵/۰۴/۲۶» باشد.</div>}
    </div>
  );
}
