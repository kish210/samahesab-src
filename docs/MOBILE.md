# سما حساب — اپِ موبایل (نسخهٔ بتا)

اپِ موبایلِ سما حساب یک **PWA** (Progressive Web App) است که مثلِ کلاینتِ دسکتاپ، کلاینتِ سرورِ مرکزیِ `SamaHesab.API` است. خودِ سرورِ API آن را سرو می‌کند (`wwwroot/app/`)، پس نیازی به استقرارِ جداگانه نیست.

> **چرا PWA؟** بدونِ حسابِ Apple Developer ($۹۹/سال) امکانِ توزیعِ TestFlight نیست و build کردنِ iOS هم به مک نیاز دارد. PWA روی **هر دو** iOS و Android بدونِ حساب/هزینه نصب می‌شود و یک کدِ واحد دارد.

## آدرس
پس از روشن بودنِ سرورِ API، اپ در این آدرس در دسترس است:
```
http://<آی‌پیِ-سرور>:5080/app/
```
(همان سروری که کلاینتِ دسکتاپ به آن وصل می‌شود.)

## نصب روی iOS (بدونِ حساب developer)
1. در **Safari** آدرسِ بالا را باز کن.
2. دکمهٔ **اشتراک‌گذاری** (Share) → **«افزودن به صفحهٔ اصلی» (Add to Home Screen)**.
3. آیکونِ سما حساب روی صفحهٔ اصلی می‌نشیند و **به‌صورتِ تمام‌صفحه (standalone)** اجرا می‌شود — مثلِ یک اپِ بومی.

## نصب روی Android (بدونِ حساب/APK)
1. در **Chrome** آدرسِ بالا را باز کن.
2. منو → **«نصبِ برنامه» (Install app)** یا بنرِ خودکارِ نصب.
3. اپ نصب و در drawer/صفحهٔ اصلی ظاهر می‌شود.

## ساختِ APKِ واقعی برای Android (اختیاری — برای توزیعِ فایل)
PWA برای نصبِ مستقیم کافی است؛ اگر فایلِ `.apk` برای توزیع (مثلاً Firebase App Distribution یا sideload) خواستی، PWA را در یک **TWA (Trusted Web Activity)** بپیچ:

```bash
# نیازمندِ Node.js + JDK + Android SDK (یک‌بار نصب)
npm i -g @bubblewrap/cli
bubblewrap init --manifest http://<سرور>:5080/app/manifest.webmanifest
bubblewrap build      # → app-release-signed.apk
```
خروجی یک APKِ امضاشده است که بدونِ حسابِ پولیِ Play قابلِ sideload/توزیعِ بتاست (Play Store به ثبتِ یک‌بارهٔ $۲۵ نیاز دارد که برای بتا لازم نیست).

> این محیطِ توسعه Android SDK/JDK ندارد، پس APK اینجا build نشده؛ مراحلِ بالا روی ماشینی با SDK اجرا می‌شود.

## امکاناتِ نسخهٔ بتا
- ورود به سرور (JWT) با آدرسِ سرورِ قابلِ‌تنظیم.
- داشبورد: سلامِ کاربر + KPIهای داشبوردِ مدیر + فهرستِ هشدارها (چکِ سررسید/کسریِ موجودی).
- کشِ پوستهٔ اپ (Service Worker) برای بازشدنِ سریع/آفلاینِ پوسته.
- RTL کامل، پالتِ رنگِ برند، طراحیِ موبایل‌محور (تارگتِ لمسِ ≥۴۴px).

## فایل‌ها
`src/SamaHesab.API/wwwroot/app/` → `index.html` · `styles.css` · `app.js` · `manifest.webmanifest` · `sw.js` · `icon.svg`.
سرو شدن: `app.UseDefaultFiles()` + `app.UseStaticFiles()` در `Program.cs`.
