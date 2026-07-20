import { useEffect, useRef, useState } from 'react';
import { todayJalaliString, jalaliOf, jalaliMonthDays, JALALI_MONTH_NAMES, JALALI_WEEKDAY_LABELS } from '../lib/jalali';

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

const numberFormat = new Intl.NumberFormat('fa-IR');
// سالِ شمسی (مثلِ ۱۴۰۵) با numberFormatِ معمولی جداکنندهٔ هزارگان می‌گیرد («۱٬۴۰۵») — برایِ سال
// جداکننده نمی‌خواهیم، فقط رقم‌هایِ لاتین→فارسی.
const faDigits = new Intl.NumberFormat('fa-IR', { useGrouping: false });

/**
 * ورودیِ تاریخِ شمسی — پیش‌تر فرم‌ها همیشه «امروز» را هاردکد می‌فرستادند و کاربر نمی‌توانست
 * تاریخِ دیگری ثبت کند. ورودیِ متنی + اعتبارسنجیِ الگو + دکمهٔ «امروز» + تقویمِ گرافیکیِ شمسی
 * (پاپ‌آوِر با ناوبریِ ماه، بدونِ کتابخانهٔ نو — روی `jalaliMonthDays`ِ خودمان).
 */
export function JalaliDateInput({ value, onChange, label }: Props) {
  const invalid = value.length > 0 && !isValidJalali(value);
  const [open, setOpen] = useState(false);
  const wrapRef = useRef<HTMLDivElement>(null);

  const base = isValidJalali(value)
    ? { y: Number(value.split('/')[0]), m: Number(value.split('/')[1]) }
    : jalaliOf(new Date());
  const [viewYear, setViewYear] = useState(base.y);
  const [viewMonth, setViewMonth] = useState(base.m);

  useEffect(() => {
    if (open) {
      const b = isValidJalali(value) ? { y: Number(value.split('/')[0]), m: Number(value.split('/')[1]) } : jalaliOf(new Date());
      setViewYear(b.y);
      setViewMonth(b.m);
    }
  }, [open]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    if (!open) return;
    function onDocClick(e: MouseEvent) {
      if (wrapRef.current && !wrapRef.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, [open]);

  function goMonth(delta: number) {
    let m = viewMonth + delta;
    let y = viewYear;
    if (m < 1) { m = 12; y -= 1; }
    if (m > 12) { m = 1; y += 1; }
    setViewMonth(m);
    setViewYear(y);
  }

  function pick(day: number) {
    onChange(`${viewYear}/${String(viewMonth).padStart(2, '0')}/${String(day).padStart(2, '0')}`);
    setOpen(false);
  }

  const days = jalaliMonthDays(viewYear, viewMonth);
  const selectedDay = isValidJalali(value) && Number(value.split('/')[0]) === viewYear && Number(value.split('/')[1]) === viewMonth
    ? Number(value.split('/')[2]) : null;
  const todayStr = todayJalaliString();

  return (
    <div className="field" ref={wrapRef} style={{ position: 'relative' }}>
      {label && <label className="label">{label}</label>}
      <div style={{ display: 'flex', gap: 6 }}>
        <input
          className={`input${invalid ? ' is-error' : ''}`}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onFocus={() => setOpen(true)}
          placeholder="1405/04/26"
          style={{ direction: 'ltr', textAlign: 'center' }}
        />
        <button type="button" className="btn btn-secondary btn-sm" onClick={() => onChange(todayJalaliString())}>
          امروز
        </button>
        <button type="button" className="btn btn-secondary btn-sm" onClick={() => setOpen((o) => !o)} title="تقویم">
          📅
        </button>
      </div>
      {invalid && <div className="hint" style={{ color: 'var(--danger-700)' }}>قالبِ تاریخ باید «۱۴۰۵/۰۴/۲۶» باشد.</div>}

      {open && (
        <div
          style={{
            position: 'absolute', zIndex: 20, top: '100%', insetInlineStart: 0, marginTop: 4,
            background: 'var(--bg-surface)', border: '1px solid var(--border-strong)', borderRadius: 'var(--radius-md)',
            boxShadow: 'var(--shadow-md)', padding: 'var(--space-3)', width: 260,
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 'var(--space-2)' }}>
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => goMonth(-1)}>‹</button>
            <div style={{ fontWeight: 600, fontSize: 'var(--text-sm)' }}>
              {JALALI_MONTH_NAMES[viewMonth - 1]} {faDigits.format(viewYear)}
            </div>
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => goMonth(1)}>›</button>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 2, marginBottom: 4 }}>
            {JALALI_WEEKDAY_LABELS.map((w) => (
              <div key={w} style={{ textAlign: 'center', fontSize: 'var(--text-xs)', color: 'var(--text-muted)' }}>{w}</div>
            ))}
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(7, 1fr)', gap: 2 }}>
            {days.length > 0 && Array.from({ length: days[0].weekday }).map((_, i) => <div key={`pad-${i}`} />)}
            {days.map(({ day, weekday: _w }) => {
              const dateStr = `${viewYear}/${String(viewMonth).padStart(2, '0')}/${String(day).padStart(2, '0')}`;
              const isSelected = selectedDay === day;
              const isToday = dateStr === todayStr;
              return (
                <button
                  key={day}
                  type="button"
                  onClick={() => pick(day)}
                  className="num"
                  style={{
                    height: 28, borderRadius: 'var(--radius-sm)', border: isToday ? '1px solid var(--blue-600)' : '1px solid transparent',
                    background: isSelected ? 'var(--blue-600)' : 'transparent',
                    color: isSelected ? '#fff' : 'var(--text-strong)',
                    fontSize: 'var(--text-sm)', cursor: 'pointer',
                  }}
                >
                  {numberFormat.format(day)}
                </button>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
