/**
 * بارکدِ Code 128-B به‌صورتِ SVGِ خالص — بدونِ هیچ کتابخانه‌ای.
 * پیش‌تر برچسبِ چاپیِ کالا فقط کد را به‌صورتِ متن نشان می‌داد (نه نمادِ اسکن‌شدنی)؛ این کامپوننت
 * همان کد را به بارکدِ استانداردِ خرده‌فروشی (Code 128) رمز می‌کند که با هر اسکنرِ واقعی خوانده
 * می‌شود. Code 128-B انتخاب شد چون کلِ ASCIIِ چاپی (رقم/حرف/علامت) را پوشش می‌دهد — برخلافِ
 * EAN-13 که فقط ۱۳ رقمِ ثابت می‌پذیرد و برایِ کدهایِ دلخواهِ داخلی مناسب نیست.
 */

// جدولِ الگویِ Code 128 (۰..۱۰۶) — هر رشته عرضِ میله/فاصله‌هایِ متناوب است (میله، فاصله، …).
// الگویِ ۱۰۶ (Stop) هفت‌بخشی است و به میله ختم می‌شود.
const PATTERNS = [
  '212222', '222122', '222221', '121223', '121322', '131222', '122213', '122312', '132212', '221213',
  '221312', '231212', '112232', '122132', '122231', '113222', '123122', '123221', '223211', '221132',
  '221231', '213212', '223112', '312131', '311222', '321122', '321221', '312212', '322112', '322211',
  '212123', '212321', '232121', '111323', '131123', '131321', '112313', '132113', '132311', '211313',
  '231113', '231311', '112133', '112331', '132131', '113123', '113321', '133121', '313121', '211331',
  '231131', '213113', '213311', '213131', '311123', '311321', '331121', '312113', '312311', '332111',
  '314111', '221411', '431111', '111224', '111422', '121124', '121421', '141122', '141221', '112214',
  '112412', '122114', '122411', '142112', '142211', '241211', '221114', '413111', '241112', '134111',
  '111242', '121142', '121241', '114212', '124112', '124211', '411212', '421112', '421211', '212141',
  '214121', '412121', '111143', '111341', '131141', '114113', '114311', '411113', '411311', '113141',
  '114131', '311141', '411131', '211412', '211214', '211232', '2331112',
];

const START_B = 104;
const STOP = 106;

/** دنبالهٔ الگوهایِ Code 128-B برایِ یک متن، یا null اگر کاراکترِ خارج از بازهٔ ۳۲..۱۲۶ داشته باشد. */
function encode128B(text: string): string[] | null {
  const values: number[] = [START_B];
  for (const ch of text) {
    const code = ch.charCodeAt(0);
    if (code < 32 || code > 126) return null;   // خارج از Code 128-B
    values.push(code - 32);
  }
  // چک‌سام: (مقدارِ Start + Σ مقدارِ داده × موقعیت) mod 103
  let sum = START_B;
  for (let i = 1; i < values.length; i++) sum += values[i] * i;
  values.push(sum % 103);
  values.push(STOP);
  return values.map((v) => PATTERNS[v]);
}

interface BarcodeProps {
  value: string;
  /** عرضِ هر ماژول (px). پیش‌فرض ۲ برایِ چاپِ خوانا. */
  moduleWidth?: number;
  height?: number;
  /** نمایشِ متنِ کد زیرِ میله‌ها. */
  showText?: boolean;
}

export function Barcode({ value, moduleWidth = 2, height = 60, showText = true }: BarcodeProps) {
  const patterns = value ? encode128B(value) : null;
  if (!patterns) {
    // کاراکترِ غیرمجاز — به‌جایِ بارکدِ خراب، همان کد را خوانا نشان بده (خرابیِ صامت بدتر است).
    return <div style={{ fontFamily: 'monospace', fontSize: 16, letterSpacing: 2 }}>{value}</div>;
  }

  const bars: Array<{ x: number; w: number }> = [];
  let x = 0;
  for (const pattern of patterns) {
    for (let i = 0; i < pattern.length; i++) {
      const w = Number(pattern[i]) * moduleWidth;
      if (i % 2 === 0) bars.push({ x, w });   // شاخصِ زوج = میله (سیاه)
      x += w;
    }
  }
  const totalWidth = x;

  return (
    <svg width={totalWidth} height={height + (showText ? 18 : 0)} role="img" aria-label={`بارکد ${value}`}
      style={{ maxWidth: '100%' }}>
      <rect x={0} y={0} width={totalWidth} height={height} fill="#fff" />
      {bars.map((b, i) => (
        <rect key={i} x={b.x} y={0} width={b.w} height={height} fill="#000" />
      ))}
      {showText && (
        <text x={totalWidth / 2} y={height + 14} textAnchor="middle"
          fontFamily="monospace" fontSize={13} letterSpacing={2} fill="#000">{value}</text>
      )}
    </svg>
  );
}
