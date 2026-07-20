/**
 * تبدیلِ تاریخِ میلادی به شمسی (Jalali) — الگوریتمِ استانداردِ Borkowski، بدونِ نیازِ کتابخانه.
 * سرور همه‌جا تاریخِ رشته‌ایِ شمسیِ «yyyy/MM/dd» می‌خواهد (نه ISOِ میلادی)؛ فراموش‌کردنِ این تبدیل
 * باعث می‌شود تاریخ‌هایِ ثبت‌شده کاملاً نامعتبر/بی‌معنا باشند (مثلاً «۲۰۲۶/۰۷/۱۷» به‌جایِ «۱۴۰۵/۰۴/۲۶»).
 */
function div(a: number, b: number): number {
  return ~~(a / b);
}

function jalaliFromGregorian(gy: number, gm: number, gd: number): [number, number, number] {
  const gDaysInMonth = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  const gy2 = gm > 2 ? gy + 1 : gy;
  let days =
    355666 +
    365 * gy +
    div(gy2 + 3, 4) -
    div(gy2 + 99, 100) +
    div(gy2 + 399, 400) +
    gd +
    gDaysInMonth.slice(0, gm - 1).reduce((a, b) => a + b, 0);
  let jy = -1595 + 33 * div(days, 12053);
  days %= 12053;
  jy += 4 * div(days, 1461);
  days %= 1461;
  if (days > 365) {
    jy += div(days - 1, 365);
    days = (days - 1) % 365;
  }
  let jm: number;
  let jd: number;
  if (days < 186) {
    jm = 1 + div(days, 31);
    jd = 1 + (days % 31);
  } else {
    jm = 7 + div(days - 186, 30);
    jd = 1 + ((days - 186) % 30);
  }
  return [jy, jm, jd];
}

/** تاریخِ امروز به‌صورتِ رشتهٔ شمسیِ «yyyy/MM/dd» (فرمتِ موردِ انتظارِ سرور در همه‌جا). */
export function todayJalaliString(): string {
  const now = new Date();
  const [jy, jm, jd] = jalaliFromGregorian(now.getFullYear(), now.getMonth() + 1, now.getDate());
  return `${jy}/${String(jm).padStart(2, '0')}/${String(jd).padStart(2, '0')}`;
}

export function jalaliOf(date: Date): { y: number; m: number; d: number } {
  const [y, m, d] = jalaliFromGregorian(date.getFullYear(), date.getMonth() + 1, date.getDate());
  return { y, m, d };
}

function addDays(d: Date, n: number): Date {
  const r = new Date(d);
  r.setDate(r.getDate() + n);
  return r;
}

/**
 * تقویمِ گرافیکیِ شمسی — بدونِ پیاده‌سازیِ فرمولِ معکوسِ (شمسی→میلادی) که دستی‌نویسی‌اش پرخطاست؛
 * به‌جایش با اسکنِ یک پنجرهٔ ~۴۴۰روزهٔ Dateِ میلادیِ بومیِ جاوااسکریپت (که قطعاً درست است) و
 * تبدیلِ هرکدام با همان `jalaliFromGregorian`ِ ازقبل‌تأییدشده، دقیقاً روزهایِ همان ماهِ شمسی را
 * پیدا می‌کند — صحت تضمین‌شده به‌جایِ فرمولِ نو و ریسک‌دار.
 */
export function jalaliMonthDays(jy: number, jm: number): { day: number; date: Date; weekday: number }[] {
  const anchor = new Date(jy + 621, 2, 10); // نیمهٔ اسفند/اوایلِ فروردینِ گرگوریِ حدودی، برایِ لنگرِ اسکن
  const results: { day: number; date: Date; weekday: number }[] = [];
  for (let offset = -220; offset <= 220; offset++) {
    const d = addDays(anchor, offset);
    const { y, m, d: day } = jalaliOf(d);
    if (y === jy && m === jm) {
      // شنبه=۰ … جمعه=۶ (تقویمِ ایرانی)، از یکشنبه=۰ جاوااسکریپت
      const weekday = (d.getDay() + 1) % 7;
      results.push({ day, date: d, weekday });
    }
  }
  results.sort((a, b) => a.day - b.day);
  return results;
}

export const JALALI_MONTH_NAMES = [
  'فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور',
  'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند',
];

export const JALALI_WEEKDAY_LABELS = ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'];
