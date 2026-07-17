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
}

/** انتخابِ جست‌وجوپذیرِ ساده — فیلترِ سمتِ کلاینت رویِ فهرستی که از قبل گرفته شده (مشتری/تأمین‌کننده/کالا). */
export function SearchSelect({ options, value, onChange, placeholder }: SearchSelectProps) {
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);

  const selected = options.find((o) => o.id === value);
  const filtered = useMemo(() => {
    const term = query.trim();
    if (!term) return options.slice(0, 30);
    return options.filter((o) => o.label.includes(term) || o.sublabel?.includes(term)).slice(0, 30);
  }, [options, query]);

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
            marginTop: 4, maxHeight: 240, overflowY: 'auto', boxShadow: 'var(--shadow-md)',
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
          {filtered.length === 0 && (
            <div style={{ padding: '8px 12px', fontSize: 'var(--text-sm)', color: 'var(--text-muted)' }}>یافت نشد.</div>
          )}
        </div>
      )}
    </div>
  );
}
