import { useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ErpIcon, type IconName } from './ErpIcons';

export interface PaletteItem {
  label: string;
  sub: string;
  icon: IconName;
  to: string;
}

/**
 * پالتِ فرمان (Ctrl+K / F3) — پورت‌شده از design-system/screens/erp-shell.js، ولی روی
 * NAV واقعیِ اپ (نه دادهٔ نمایشیِ مکاپ) — Enter مستقیم به مسیرِ واقعی می‌رود.
 */
export function CommandPalette({ items }: { items: PaletteItem[] }) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [sel, setSel] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

  const filtered = items.filter((i) => !query.trim() || (i.label + ' ' + i.sub).includes(query.trim()));

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setOpen((o) => !o);
        return;
      }
      if (e.key === 'F3') {
        e.preventDefault();
        setOpen(true);
        return;
      }
    }
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, []);

  useEffect(() => {
    if (open) {
      setQuery('');
      setSel(0);
      setTimeout(() => inputRef.current?.focus(), 30);
    }
  }, [open]);

  function onListKeyDown(e: React.KeyboardEvent) {
    if (e.key === 'Escape') { e.preventDefault(); setOpen(false); }
    else if (e.key === 'ArrowDown') { e.preventDefault(); setSel((s) => Math.min(s + 1, filtered.length - 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setSel((s) => Math.max(s - 1, 0)); }
    else if (e.key === 'Enter') {
      e.preventDefault();
      const item = filtered[sel];
      if (item) { navigate(item.to); setOpen(false); }
    }
  }

  if (!open) return null;

  return (
    <div className="cmdk-ov on" onClick={(e) => { if (e.target === e.currentTarget) setOpen(false); }}>
      <div className="cmdk">
        <div className="cmdk-in">
          <ErpIcon name="search" />
          <input
            ref={inputRef}
            placeholder="جستجویِ صفحه، مشتری، کالا…"
            autoComplete="off"
            value={query}
            onChange={(e) => { setQuery(e.target.value); setSel(0); }}
            onKeyDown={onListKeyDown}
          />
          <span className="cmdk-esc">Esc</span>
        </div>
        <div className="cmdk-list">
          {filtered.length === 0 && <div className="cmdk-empty">نتیجه‌ای یافت نشد</div>}
          {filtered.map((item, i) => (
            <a
              key={item.to}
              className={`cmdk-it${i === sel ? ' hl' : ''}`}
              href={item.to}
              onMouseMove={() => setSel(i)}
              onClick={(e) => { e.preventDefault(); navigate(item.to); setOpen(false); }}
            >
              <span className="cmdk-ic" style={{ background: 'var(--blue-50)', color: 'var(--blue-700)' }}>
                <ErpIcon name={item.icon} />
              </span>
              <span className="cmdk-tx">
                <span className="t">{item.label}</span>
                <span className="s">{item.sub}</span>
              </span>
            </a>
          ))}
        </div>
        <div className="cmdk-foot">
          <span><b>↑↓</b> حرکت</span>
          <span><b>Enter</b> انتخاب</span>
          <span><b>Esc</b> بستن</span>
          <span style={{ marginInlineStart: 'auto' }}>جستجویِ سراسریِ سما حساب</span>
        </div>
      </div>
    </div>
  );
}
