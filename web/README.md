# SamaHesab — کلاینتِ وب (فازِ ۶)

کلاینتِ وبِ سما حساب — Vite + React + TypeScript، مصرف‌کنندهٔ همان `SamaHesab.API` که کلاینتِ
دسکتاپ/PWA ازش استفاده می‌کنند (بدونِ بک‌اندِ جدا).

## اجرا (توسعه)

1. مطمئن شو `SamaHesab.API` روی `http://localhost:5080` در حالِ اجراست (`dotnet run --project ../src/SamaHesab.API`).
2. `npm install`
3. `npm run dev` → **`http://localhost:5173/web/`** (مسیرِ `/web/` — همان مسیرِ نسخهٔ نصب‌شده)

آدرسِ API از `.env` خوانده می‌شود (`VITE_API_BASE_URL=http://localhost:5080` — مبدأِ متفاوت، از CORSِ سرور رد می‌شود).

## نسخهٔ نصب‌شده (داخلِ نصابِ سرور)

نیازی به وب‌سرور/نصبِ جداگانه نیست — خودِ `SamaHesab.API` کلاینت را سرو می‌کند:

```
http://<آی‌پیِ-سرور>:5080/web/
```

`installer\publish-all.ps1` (مرحلهٔ ۹) با `npm run build` می‌سازد و در `dist\api\wwwroot\web` می‌گذارد؛
`server.iss` کلِ `dist\api\*` را برمی‌دارد ⇒ خودکار داخلِ Setup می‌آید (+ میان‌برِ «کلاینت وب» در منویِ استارت).

- `base='/web/'` در `vite.config.ts` و `BrowserRouter basename` از `import.meta.env.BASE_URL` خوانده می‌شود.
- `.env.production` آدرسِ API را **خالی** می‌گذارد ⇒ هم‌مبدأ (`/api/...`). هرگز `localhost` را در باندلِ production هاردکد نکنید.
- SPA-fallbackِ `/web/*` در `Program.cs` است (وگرنه رفرشِ لینکِ عمیق ۴۰۴ می‌دهد).
- پیش‌نیازِ ساخت: **Node.js/npm** رویِ ماشینِ builder.

## ساختار
- `src/api/client.ts` — fetch wrapperِ JSON با هدرِ Authorization خودکار + تمدیدِ توکن روی ۴۰۱.
- `src/auth/AuthContext.tsx` — نگه‌داریِ نشست (JWT access/refresh در localStorage).
- `src/components/Shell.tsx` — پوستهٔ تاپ‌بار/سایدبار.
- `src/pages/` — صفحات (ورود، داشبورد، فهرستِ مشتریان، کارتِ مشتری).
- `src/design-tokens.css` / `src/design-components.css` — کپیِ توکن‌ها/کامپوننت‌هایِ `design-system/` ریشهٔ ریپو (رنگ/تایپ/دکمه/اینپوت مشترک با WPF).

## وضعیت (@2026-07-17)
هستهٔ ERP رویِ وب کامل است (`todo.rm` آیتم #۲): ورود (JWT) · داشبورد · مشتریان (فهرست+کارت) ·
تأمین‌کنندگان · کالاها (فهرست+کارت) · انبار (موجودی+انتقال) · فاکتورِ فروش/خرید (فهرست + فرمِ نو) ·
خزانه (دریافتنی/پرداختنی + دریافت/پرداختِ سریع) · تابلویِ چک · اسنادِ حسابداری · تراز آزمایشی · دفترِ کل.
ثبتِ واقعیِ فاکتور رویِ DB با تستِ زنده تأیید شده (سندِ حسابداریِ متوازن + کاهشِ موجودی).

باقی‌مانده: فرمِ مرجوعی در UI (فعلاً فقط API) · date-pickerِ شمسی (همیشه «امروز» ثبت می‌شود) ·
ویرایش/حذفِ رکوردها · ماژول‌هایِ اختیاری (POS/رستوران/گردشگری/HR/CRM) · هم‌شکل‌سازیِ دقیق با
مکاپ‌هایِ `design-system/screens/*.html`.
