export const numberFormat = new Intl.NumberFormat('fa-IR');

export function money(n: number): string {
  return numberFormat.format(Math.round(n));
}

// ── عدد به حروفِ فارسی — معادلِ `NumberToWords`ِ دسکتاپ (PrintService.cs) برایِ «مبلغ به حروف»
//    در چاپِ فاکتور. تا هزار میلیارد (تریلیون) پشتیبانی می‌شود؛ کافی برایِ مبالغِ ریالی.
const YEKAN = ['', 'یک', 'دو', 'سه', 'چهار', 'پنج', 'شش', 'هفت', 'هشت', 'نه'];
const DAH = ['ده', 'یازده', 'دوازده', 'سیزده', 'چهارده', 'پانزده', 'شانزده', 'هفده', 'هجده', 'نوزده'];
const DAHGAN = ['', '', 'بیست', 'سی', 'چهل', 'پنجاه', 'شصت', 'هفتاد', 'هشتاد', 'نود'];
const SADGAN = ['', 'صد', 'دویست', 'سیصد', 'چهارصد', 'پانصد', 'ششصد', 'هفتصد', 'هشتصد', 'نهصد'];
const SCALE = ['', ' هزار', ' میلیون', ' میلیارد', ' هزار میلیارد'];

/** یک گروهِ سه‌رقمی (۱..۹۹۹) را به حروف تبدیل می‌کند. */
function threeDigitsToWords(n: number): string {
  const parts: string[] = [];
  const h = Math.floor(n / 100);
  const rest = n % 100;
  if (h > 0) parts.push(SADGAN[h]);
  if (rest >= 10 && rest <= 19) {
    parts.push(DAH[rest - 10]);
  } else {
    const t = Math.floor(rest / 10);
    const u = rest % 10;
    if (t > 0) parts.push(DAHGAN[t]);
    if (u > 0) parts.push(YEKAN[u]);
  }
  return parts.join(' و ');
}

/** عددِ صحیحِ نامنفی را به حروفِ فارسی برمی‌گرداند (مثلاً ۱۲۰۰ → «یک هزار و دویست»). */
export function numberToPersianWords(value: number): string {
  let n = Math.floor(Math.abs(value));
  if (n === 0) return 'صفر';
  const groups: number[] = [];
  while (n > 0) { groups.push(n % 1000); n = Math.floor(n / 1000); }
  const words: string[] = [];
  for (let i = groups.length - 1; i >= 0; i--) {
    if (groups[i] === 0) continue;
    words.push(threeDigitsToWords(groups[i]) + SCALE[i]);
  }
  return words.join(' و ');
}
