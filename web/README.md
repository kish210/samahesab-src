# SamaHesab — کلاینتِ وب (فازِ ۶)

کلاینتِ وبِ سما حساب — Vite + React + TypeScript، مصرف‌کنندهٔ همان `SamaHesab.API` که کلاینتِ
دسکتاپ/PWA ازش استفاده می‌کنند (بدونِ بک‌اندِ جدا).

## اجرا (توسعه)

1. مطمئن شو `SamaHesab.API` روی `http://localhost:5080` در حالِ اجراست (`dotnet run --project ../src/SamaHesab.API`).
2. `npm install`
3. `npm run dev` → `http://localhost:5173`

آدرسِ API از `.env` خوانده می‌شود (`VITE_API_BASE_URL`).

## ساختار
- `src/api/client.ts` — fetch wrapperِ JSON با هدرِ Authorization خودکار + تمدیدِ توکن روی ۴۰۱.
- `src/auth/AuthContext.tsx` — نگه‌داریِ نشست (JWT access/refresh در localStorage).
- `src/components/Shell.tsx` — پوستهٔ تاپ‌بار/سایدبار.
- `src/pages/` — صفحات (ورود، داشبورد، فهرستِ مشتریان، کارتِ مشتری).
- `src/design-tokens.css` / `src/design-components.css` — کپیِ توکن‌ها/کامپوننت‌هایِ `design-system/` ریشهٔ ریپو (رنگ/تایپ/دکمه/اینپوت مشترک با WPF).

## وضعیت (@2026-07-17)
شروعِ فازِ ۶ (`todo.rm` آیتم #۲): ورود (JWT) → داشبوردِ خلاصه → فهرستِ مشتریان → کارتِ مشتری.
صفحاتِ باقی‌مانده (فاکتور فروش/خرید، انبار، حسابداری، …) هنوز اضافه نشده‌اند.
