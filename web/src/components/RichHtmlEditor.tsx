import { useEffect, useRef, useState } from 'react';

interface Props {
  value: string;
  onChange: (html: string) => void;
  tokens: string[];
  minHeight?: number;
  placeholder?: string;
}

const TOOLBAR_BTNS: { cmd: string; label: string; title: string }[] = [
  { cmd: 'bold', label: 'B', title: 'ضخیم' },
  { cmd: 'italic', label: 'I', title: 'مورب' },
  { cmd: 'underline', label: 'U', title: 'زیرخط' },
  { cmd: 'justifyRight', label: '⇥', title: 'راست‌چین' },
  { cmd: 'justifyCenter', label: '↔', title: 'وسط‌چین' },
  { cmd: 'justifyLeft', label: '⇤', title: 'چپ‌چین' },
  { cmd: 'insertUnorderedList', label: '•', title: 'فهرست' },
];

/**
 * ویرایشگرِ WYSIWYGِ سبک برایِ قالب‌هایِ چاپ — پیش‌تر کاربرِ نهایی کدِ HTMLِ خام می‌دید که
 * برایِ کاربرِ غیرِفنی مناسب نبود («کد نشون میده و این مناسبِ end user نیست»). این کامپوننت
 * رویِ `contentEditable` + `document.execCommand` (پشتیبانیِ گستردهٔ Chrome/Edge، بدونِ
 * وابستگیِ نو) یک تجربهٔ Wordمانند می‌دهد؛ منبعِ حقیقت هنوز HTMLِ خام است — دکمهٔ «نمایشِ
 * کدِ HTML» برایِ کاربرانِ فنی/عیب‌یابی به‌جا مانده.
 */
export function RichHtmlEditor({ value, onChange, tokens, minHeight = 180, placeholder }: Props) {
  const editorRef = useRef<HTMLDivElement>(null);
  const [sourceMode, setSourceMode] = useState(false);
  const [tokenPick, setTokenPick] = useState('');

  // فقط وقتی منبعِ بیرونی عوض شد (نه هر ضربه‌کلید) contentEditable را بازنویسی کن — وگرنه
  // موقعیتِ نشانگر (caret) در هر تایپ به ابتدایِ متن می‌پرد.
  useEffect(() => {
    if (!sourceMode && editorRef.current && editorRef.current.innerHTML !== value) {
      editorRef.current.innerHTML = value;
    }
  }, [value, sourceMode]);

  function exec(cmd: string) {
    editorRef.current?.focus();
    document.execCommand(cmd);
    if (editorRef.current) onChange(editorRef.current.innerHTML);
  }

  function insertToken(token: string) {
    if (!token) return;
    editorRef.current?.focus();
    document.execCommand('insertText', false, `{${token}}`);
    if (editorRef.current) onChange(editorRef.current.innerHTML);
    setTokenPick('');
  }

  function insertRowsBlock() {
    editorRef.current?.focus();
    document.execCommand('insertHTML', false,
      '<div>[[ROWS]]&nbsp;{#}. {ProductName} × {Quantity} = {LineTotal}&nbsp;[[/ROWS]]</div>');
    if (editorRef.current) onChange(editorRef.current.innerHTML);
  }

  return (
    <div style={{ border: '1px solid var(--border-strong)', borderRadius: 'var(--radius-sm)', overflow: 'hidden' }}>
      <div style={{
        display: 'flex', flexWrap: 'wrap', gap: 2, padding: 6, background: 'var(--bg-sunken)',
        borderBottom: '1px solid var(--border)',
      }}>
        {!sourceMode && TOOLBAR_BTNS.map((b) => (
          <button key={b.cmd} type="button" title={b.title} className="btn btn-ghost btn-sm"
            style={{ minWidth: 30, padding: '2px 8px' }}
            onMouseDown={(e) => { e.preventDefault(); exec(b.cmd); }}>
            {b.label}
          </button>
        ))}
        {!sourceMode && (
          <button type="button" title="افزودنِ ردیفِ اقلامِ تکرارشونده" className="btn btn-ghost btn-sm"
            onMouseDown={(e) => { e.preventDefault(); insertRowsBlock(); }}>
            + جدولِ اقلام
          </button>
        )}
        {!sourceMode && (
          <select
            className="select" style={{ height: 26, fontSize: 12, maxWidth: 160 }}
            value={tokenPick}
            onChange={(e) => insertToken(e.target.value)}
          >
            <option value="">+ افزودنِ توکن…</option>
            {tokens.map((t) => <option key={t} value={t}>{t}</option>)}
          </select>
        )}
        <span style={{ flex: 1 }} />
        <button type="button" className="btn btn-ghost btn-sm" style={{ fontSize: 11 }}
          onClick={() => setSourceMode((s) => !s)}>
          {sourceMode ? 'بازگشت به ویرایشگرِ گرافیکی' : 'نمایشِ کدِ HTML'}
        </button>
      </div>

      {sourceMode ? (
        <textarea
          className="input" style={{ direction: 'ltr', fontFamily: 'monospace', minHeight, border: 'none', borderRadius: 0 }}
          value={value} onChange={(e) => onChange(e.target.value)}
        />
      ) : (
        <div
          ref={editorRef}
          contentEditable
          suppressContentEditableWarning
          data-placeholder={placeholder}
          onInput={(e) => onChange((e.target as HTMLDivElement).innerHTML)}
          style={{ minHeight, padding: 12, outline: 'none', fontFamily: 'Tahoma, sans-serif', fontSize: 13 }}
        />
      )}
    </div>
  );
}
