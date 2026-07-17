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
