import { useEffect, useReducer, useRef, useState } from 'react';
import { numberFormat } from '../lib/format';

type Op = '+' | '-' | '*' | '/';

function apply(a: number, b: number, op: Op): number {
  switch (op) {
    case '+': return a + b;
    case '-': return a - b;
    case '*': return a * b;
    case '/': return b === 0 ? NaN : a / b;
  }
}

interface CalcState {
  /** عددِ در حالِ تایپ به‌صورتِ رشته — تا «٫» و صفرِ ابتدایی موقعِ تایپ نپرد. */
  current: string;
  acc: number | null;
  op: Op | null;
  /** یعنی عددِ بعدی جایگزینِ نمایشگر شود، نه به آن چسبیده. */
  replaceNext: boolean;
}

type Action =
  | { t: 'digit'; d: string }
  | { t: 'op'; op: Op }
  | { t: 'eq' } | { t: 'clear' } | { t: 'back' } | { t: 'pct' };

const INITIAL: CalcState = { current: '0', acc: null, op: null, replaceNext: false };

/**
 * کلِ منطق در reducer است، نه در هندلرهایِ closure-محور. دلیلِ صریح: نسخهٔ اولِ این کامپوننت
 * `current`/`acc`/`op` را از closureِ رندر می‌خواند و وقتی چند کلید سریع‌تر از یک رندرِ React
 * می‌آمد، رویِ حالتِ کهنه حساب می‌کرد — در تستِ زنده «۱۲۵۰۰۰۰ + ۹٪» به‌جایِ ۱۱۲۵۰۰ عددِ
 * ۱۲۵۰۰ داد. در یک نرم‌افزارِ حسابداری، حسابِ غلط پذیرفتنی نیست؛ reducer همیشه آخرین حالت
 * را می‌بیند و این کلاسِ باگ را ریشه‌ای می‌بندد.
 */
function reducer(s: CalcState, a: Action): CalcState {
  switch (a.t) {
    case 'digit': {
      if (s.replaceNext) return { ...s, current: a.d === '.' ? '0.' : a.d, replaceNext: false };
      if (a.d === '.' && s.current.includes('.')) return s;
      if (s.current === '0' && a.d !== '.') return { ...s, current: a.d };
      return { ...s, current: s.current + a.d };
    }
    case 'op': {
      const cur = Number(s.current);
      // زنجیرهٔ «۲ + ۳ + …» باید ۵ را نشان بدهد، نه اینکه عملگر را بی‌صدا عوض کند.
      if (s.acc !== null && s.op && !s.replaceNext) {
        const r = apply(s.acc, cur, s.op);
        return { current: String(r), acc: r, op: a.op, replaceNext: true };
      }
      return { ...s, acc: cur, op: a.op, replaceNext: true };
    }
    case 'eq': {
      if (s.acc === null || !s.op) return s;
      const r = apply(s.acc, Number(s.current), s.op);
      return { current: Number.isFinite(r) ? String(r) : 'خطا', acc: null, op: null, replaceNext: true };
    }
    case 'clear':
      return INITIAL;
    case 'back': {
      if (s.replaceNext) return s;
      const c = s.current;
      const next = c.length <= 1 || (c.length === 2 && c.startsWith('-')) ? '0' : c.slice(0, -1);
      return { ...s, current: next };
    }
    case 'pct': {
      const cur = Number(s.current);
      // «۱۰۰ + ۹٪» = ۹٪ *از عملوندِ اول*؛ در ضرب/تقسیم و حالتِ تنها = تقسیم بر ۱۰۰.
      const r = s.acc !== null && (s.op === '+' || s.op === '-') ? (s.acc * cur) / 100 : cur / 100;
      return { ...s, current: String(r), replaceNext: false };
    }
  }
}

/**
 * ماشین‌حسابِ توپ‌بار — تاپ‌بارِ design-system این دکمه را داشت ولی در وب پیاده نشده بود.
 * چهار عمل + درصد + کیبورد + «کپیِ نتیجه» (کاربر معمولاً عدد را می‌خواهد در فرمِ
 * فاکتور/سند بچسباند، پس کپی مهم‌ترین اکشنِ خروجی است).
 */
export function CalculatorPopover({ onClose }: { onClose: () => void }) {
  const [s, dispatch] = useReducer(reducer, INITIAL);
  const [copied, setCopied] = useState(false);
  const boxRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onDocClick(e: MouseEvent) {
      if (boxRef.current && !boxRef.current.contains(e.target as Node)) onClose();
    }
    document.addEventListener('mousedown', onDocClick);
    return () => document.removeEventListener('mousedown', onDocClick);
  }, [onClose]);

  // کیبورد — ماشین‌حساب بدونِ کیبورد برایِ ورودِ سریعِ عدد بی‌فایده است. چون همه‌چیز از
  // dispatch می‌گذرد، این هندلر هیچ حالتی را از closure نمی‌خواند و نیازی به وابستگی ندارد.
  useEffect(() => {
    function onKey(e: KeyboardEvent) {
      const k = e.key;
      let act: Action | null = null;
      if (k >= '0' && k <= '9') act = { t: 'digit', d: k };
      else if (k === '.' || k === ',') act = { t: 'digit', d: '.' };
      else if (k === '+' || k === '-' || k === '*' || k === '/') act = { t: 'op', op: k as Op };
      else if (k === 'Enter' || k === '=') act = { t: 'eq' };
      else if (k === 'Backspace') act = { t: 'back' };
      else if (k === '%') act = { t: 'pct' };
      else if (k.toLowerCase() === 'c') act = { t: 'clear' };
      else if (k === 'Escape') { onClose(); e.preventDefault(); return; }
      if (!act) return;
      setCopied(false);
      dispatch(act);
      e.preventDefault();
    }
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, [onClose]);

  async function copyResult() {
    try {
      await navigator.clipboard.writeText(s.current);
      setCopied(true);
    } catch { /* دسترسیِ کلیپ‌بورد رد شد — بی‌صدا؛ کاربر می‌تواند دستی انتخاب کند */ }
  }

  const shown = (() => {
    if (s.current === 'خطا') return 'خطا';
    const n = Number(s.current);
    if (!Number.isFinite(n)) return 'خطا';
    const [i, f] = s.current.split('.');
    const head = numberFormat.format(Number(i || 0));
    return f !== undefined ? `${head}٫${f}` : head;
  })();

  function press(a: Action) { setCopied(false); dispatch(a); }

  const KEYS: Array<[string, Action, string?]> = [
    ['C', { t: 'clear' }, 'op'], ['⌫', { t: 'back' }, 'op'], ['%', { t: 'pct' }, 'op'], ['÷', { t: 'op', op: '/' }, 'op'],
    ['۷', { t: 'digit', d: '7' }], ['۸', { t: 'digit', d: '8' }], ['۹', { t: 'digit', d: '9' }], ['×', { t: 'op', op: '*' }, 'op'],
    ['۴', { t: 'digit', d: '4' }], ['۵', { t: 'digit', d: '5' }], ['۶', { t: 'digit', d: '6' }], ['−', { t: 'op', op: '-' }, 'op'],
    ['۱', { t: 'digit', d: '1' }], ['۲', { t: 'digit', d: '2' }], ['۳', { t: 'digit', d: '3' }], ['+', { t: 'op', op: '+' }, 'op'],
    ['۰', { t: 'digit', d: '0' }], ['٫', { t: 'digit', d: '.' }], ['=', { t: 'eq' }, 'eq'],
  ];

  return (
    <div ref={boxRef} role="dialog" aria-label="ماشین‌حساب" style={{
      position: 'absolute', top: '100%', insetInlineEnd: 8, marginTop: 6, zIndex: 200,
      background: 'var(--bg-surface, #fff)', border: '1px solid var(--border)',
      borderRadius: 'var(--radius-md)', boxShadow: '0 8px 24px rgba(0,0,0,.18)', padding: 10, width: 240,
    }}>
      <div className="num" style={{
        direction: 'ltr', textAlign: 'left', fontSize: 20, fontWeight: 700, padding: '8px 10px',
        background: 'var(--bg-sunken)', border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)',
        overflowX: 'auto', whiteSpace: 'nowrap',
      }}>{shown}</div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 5, marginTop: 8 }}>
        {KEYS.map(([label, act, kind]) => (
          <button key={label} type="button" onClick={() => press(act)}
            style={{
              gridColumn: label === '=' ? 'span 2' : undefined,
              padding: '9px 0', fontSize: 15, cursor: 'pointer',
              border: '1px solid var(--border)', borderRadius: 'var(--radius-sm)',
              background: kind === 'eq' ? 'var(--primary-600, #1b4d8f)' : kind === 'op' ? 'var(--bg-sunken)' : 'var(--bg-surface, #fff)',
              color: kind === 'eq' ? '#fff' : 'inherit',
              fontWeight: kind ? 600 : 500,
            }}>{label}</button>
        ))}
      </div>

      <button type="button" className="btn btn-secondary btn-sm" style={{ width: '100%', marginTop: 8 }}
        onClick={copyResult}>{copied ? '✓ کپی شد' : 'کپیِ نتیجه'}</button>
      <div style={{ color: 'var(--text-muted)', fontSize: 11, marginTop: 6, textAlign: 'center' }}>
        کیبورد: اعداد · + − × ÷ · Enter=مساوی · Esc=بستن
      </div>
    </div>
  );
}
