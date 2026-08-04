import { useMemo, useState } from 'react';

export interface SearchSelectOption {
  id: number;
  label: string;
  sublabel?: string;
}

interface SearchSelectProps {
  options: SearchSelectOption[];
  value: number | null;
  onChange: (id: number | null) => void;
  placeholder?: string;
  /** برچسبِ ردیفِ «+ افزودنِ …» در پایینِ فهرست — اگر داده شود، با کلیک `onCreateNew` صدا می‌شود
   * (مثلاً وقتی مشتری/کالایِ موردنظر در فهرست نیست، همان‌جا فرمِ ساختِ سریع باز شود). */
  createNewLabel?: string;
  onCreateNew?: (query: string) => void;
}

const VISIBLE_LIMIT = 50;

/** انتخابِ جست‌وجوپذیر — فیلترِ سمتِ کلاینت رویِ فهرستی که از قبل گرفته شده (مشتری/تأمین‌کننده/کالا).
 * پیش‌تر فهرستِ اولیه (بدونِ تایپ) به ۳۰ موردِ اول محدود می‌شد بدونِ هیچ نشانه‌ای — با کاتالوگِ
 * بزرگ (>۳۰ کالا/مشتری) کاربر گمان می‌کرد فهرست «کامل بارگیری نشده» (باگِ گزارش‌شده). حالا حدِ
 * نمایش بالاتر است + وقتی هنوز مواردی فراتر از حد هست، راهنمای «برای دیدنِ بقیه تایپ کنید» نشان
 * داده می‌شود؛ تایپ‌کردن همیشه رویِ کلِ `options` فیلتر می‌کند، نه فقط ۳۰ تایِ اول. */
export function SearchSelect({ options, value, onChange, placeholder, createNewLabel, onCreateNew }: SearchSelectProps) {
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);

  const selected = options.find((o) => o.id === value);
  const term = query.trim();
  const matched = useMemo(() => {
    if (!term) return options;
    return options.filter((o) => o.label.includes(term) || o.sublabel?.includes(term));
  }, [options, term]);
  const filtered = matched.slice(0, VISIBLE_LIMIT);
  const hiddenCount = matched.length - filtered.length;

  return (
    <div style={{ position: 'relative' }}>
      <input
        className="input"
        placeholder={placeholder}
        value={open ? query : (selected?.label ?? '')}
        onFocus={() => {
          setOpen(true);
          setQuery('');
        }}
        onChange={(e) => setQuery(e.target.value)}
        onBlur={() => setTimeout(() => setOpen(false), 150)}
      />
      {open && (
        <div
          style={{
            position: 'absolute', zIndex: 10, top: '100%', insetInlineStart: 0, insetInlineEnd: 0,
            background: 'var(--bg-surface)', border: '1px solid var(--border-strong)', borderRadius: 'var(--radius-sm)',
            marginTop: 4, maxHeight: 280, overflowY: 'auto', boxShadow: 'var(--shadow-md)',
          }}
        >
          {filtered.map((o) => (
            <div
              key={o.id}
              onMouseDown={() => {
                onChange(o.id);
                setOpen(false);
              }}
              style={{ padding: '8px 12px', cursor: 'pointer', fontSize: 'var(--text-sm)' }}
              onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--bg-hover)')}
              onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
            >
              <div>{o.label}</div>
              {o.sublabel && <div style={{ color: 'var(--text-muted)', fontSize: 'var(--text-xs)' }}>{o.sublabel}</div>}
            </div>
          ))}
          {filtered.length === 0 && !onCreateNew && (
            <div style={{ padding: '8px 12px', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>یافت نشد.</div>
          )}
          {hiddenCount > 0 && (
            <div style={{ padding: '6px 12px', fontSize: 'var(--text-xs)', color: 'var(--text-muted)', borderTop: '1px solid var(--gray-100)' }}>
              {hiddenCount} موردِ دیگر — برایِ دیدن، تایپ کنید…
            </div>
          )}
          {onCreateNew && (
            <div
              onMouseDown={() => {
                onCreateNew(term);
                setOpen(false);
              }}
              style={{
                padding: '8px 12px', cursor: 'pointer', fontSize: 'var(--text-sm)', color: 'var(--blue-600)', fontWeight: 600,
                borderTop: filtered.length > 0 || hiddenCount > 0 ? '1px solid var(--gray-100)' : undefined,
              }}
              onMouseEnter={(e) => (e.currentTarget.style.background = 'var(--bg-hover)')}
              onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
            >
              + {createNewLabel ?? 'موردِ جدید'}{term ? `: «${term}»` : ''}
            </div>
          )}
        </div>
      )}
    </div>
  );
}
