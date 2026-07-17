import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// base='/web/' — سرورِ API این کلاینت را زیرِ http://<server>:5080/web/ سرو می‌کند
// (هم‌الگو با پنلِ Blazorِ فروش روی /seller/). در حالتِ dev هم همین مسیر است تا
// آدرس/رفتار بینِ dev و نسخهٔ نصب‌شده یکسان بماند و باگِ مسیر دیر کشف نشود.
// https://vite.dev/config/
export default defineConfig({
  base: '/web/',
  plugins: [react()],
})
