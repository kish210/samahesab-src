# -*- coding: utf-8 -*-
r"""
تولیدِ «خودآموزِ سما حساب» (PDF فارسیِ راست‌به‌چپ) — همتای چاپیِ docs/خودآموز.md.
اجرا:  py -3 tools/make_tutorial.py
خروجی: docs\SamaHesab-Tutorial.pdf
"""
import os
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_RIGHT, TA_CENTER
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer, HRFlowable
import arabic_reshaper
from bidi.algorithm import get_display

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs", "SamaHesab-Tutorial.pdf")
os.makedirs(os.path.dirname(OUT), exist_ok=True)

pdfmetrics.registerFont(TTFont("Fa", r"C:\Windows\Fonts\tahoma.ttf"))
pdfmetrics.registerFont(TTFont("Fa-Bold", r"C:\Windows\Fonts\tahomabd.ttf"))

BLUE = colors.HexColor("#324A7A")
GOLD = colors.HexColor("#B18A5A")
GRAY = colors.HexColor("#6B7280")


def fa(t: str) -> str:
    return get_display(arabic_reshaper.reshape(t))


h1 = ParagraphStyle("h1", fontName="Fa-Bold", fontSize=20, alignment=TA_CENTER, textColor=BLUE, leading=28, spaceAfter=4)
sub = ParagraphStyle("sub", fontName="Fa", fontSize=11, alignment=TA_CENTER, textColor=GRAY, leading=16, spaceAfter=16)
h2 = ParagraphStyle("h2", fontName="Fa-Bold", fontSize=14, alignment=TA_RIGHT, textColor=BLUE, leading=22, spaceBefore=14, spaceAfter=6)
body = ParagraphStyle("body", fontName="Fa", fontSize=11, alignment=TA_RIGHT, textColor=colors.HexColor("#1F2937"), leading=20, spaceAfter=4)
bullet = ParagraphStyle("bullet", fontName="Fa", fontSize=11, alignment=TA_RIGHT, textColor=colors.HexColor("#1F2937"), leading=19, rightIndent=10, spaceAfter=2)


def P(t, s=body):
    return Paragraph(fa(t), s)


def B(t):
    return Paragraph(fa("•  " + t), bullet)


def section(story, title, lines):
    story.append(P(title, h2))
    story.append(HRFlowable(width="100%", thickness=0.6, color=GOLD, spaceAfter=6))
    for ln in lines:
        story.append(B(ln) if not ln.startswith("§") else P(ln[1:], body))


doc = SimpleDocTemplate(OUT, pagesize=A4, rightMargin=20 * mm, leftMargin=20 * mm,
                        topMargin=18 * mm, bottomMargin=16 * mm,
                        title="خودآموز سما حساب", author="سما نرم‌افزار")
S = []
S.append(P("سما حساب — خودآموزِ گام‌به‌گام", h1))
S.append(P("راهنمای یادگیری از اولین اجرا تا گزارش‌ها — برای کاربرِ تازه‌کار", sub))

section(S, "۱) اولین اجرا و ورود", [
    "نصب با نصابِ خودکفا (شاملِ .NET)؛ تنها پیش‌نیازِ سرور SQL Server است.",
    "پایگاه‌داده در اولین اجرا خودکار ساخته می‌شود؛ نیازی به اجرای دستیِ اسکریپت نیست.",
    "ورود با admin / admin123؛ در اولین ورود گذرواژه را عوض کنید (حداقل ۸ نویسه شاملِ حرف و رقم).",
])
section(S, "۲) راه‌اندازیِ اولیه", [
    "ویزاردِ راه‌اندازی سه چیز می‌گیرد: مشخصاتِ شرکت، سالِ مالی، گذرواژهٔ مدیر.",
    "مشخصاتِ شرکت بعداً از «سیستم ← تنظیمات» قابلِ ویرایش است و روی سربرگِ چاپ‌ها می‌نشیند.",
])
section(S, "۳) نمودارِ حساب‌ها", [
    "مسیر: حسابداری ← نمودار حساب‌ها. یک نمودارِ استانداردِ پیش‌فرض از قبل ساخته شده.",
    "ساختار درختی؛ افزودنِ حسابِ معین/تفصیلی و دابل‌کلیک برای رفتن به دفترِ کل.",
])
section(S, "۴) ماندهٔ افتتاحیه", [
    "حسابداری ← ثبت سند ← نوع «افتتاحیه».",
    "نمونه: بدهکار صندوق+بانک / بستانکار سرمایه. سند تا متوازن‌نشدن قطعی نمی‌شود.",
])
section(S, "۵) اشخاص (مشتری/تأمین‌کننده)", [
    "اشخاص ← مشتریان/تأمین‌کنندگان ← جدید: نام، نوع، تماس و هویتِ مالیاتی (کدِ ملی/شناسه/کدِ اقتصادی) با اعتبارسنجی.",
    "تعیینِ سقفِ اعتبار برای کنترلِ فروشِ نسیه.",
])
section(S, "۶) کالا و انبار", [
    "انبار ← کالاها ← جدید: نام/کد/واحد/قیمت + روشِ ارزش‌گذاری (میانگین یا FIFO).",
    "انبارهای متعدد و موجودیِ اولیه (سندِ ورودِ انبار یا هنگامِ خرید).",
])
section(S, "۷) چرخهٔ خرید", [
    "خرید ← فاکتور خرید: تأمین‌کننده/انبار/ردیف‌ها.",
    "با ثبت: موجودی افزایش می‌یابد + سندِ حسابداریِ خودکارِ قطعی صادر می‌شود.",
])
section(S, "۸) چرخهٔ فروش", [
    "فروش ← فاکتور فروش: مشتری/انبار/کالاها + روشِ پرداخت (نقدی/بانک/چک/نسیه).",
    "با ثبت: کاهشِ موجودی (با بهای میانگین/FIFO) + سندِ خودکار + کنترلِ سقفِ اعتبار در نسیه.",
])
section(S, "۹) خزانه: دریافت/پرداخت و چک", [
    "خزانه‌داری ← دریافتنی/پرداختنی: وصول از مشتری / پرداخت به تأمین‌کننده با تخصیصِ FIFO و سندِ خودکار.",
    "حسابداری ← مدیریت چک: چرخهٔ وضعیت (وصول/واگذاری/برگشت) با سندِ خودکار، حسابرسی و هشدارِ سررسید.",
])
section(S, "۱۰) سندِ دستی و قفلِ دوره", [
    "ثبتِ کیبوردمحور: F2 ردیف، = توازنِ خودکار، F7 ذخیره.",
    "سندِ پیش‌نویس → قطعی. در سالِ مالیِ بسته یا تاریخِ خارج از بازه، ثبت/قطعی/برگشت مجاز نیست.",
    "سندِ قطعی ویرایش/حذف نمی‌شود؛ فقط با سندِ برگشتی خنثی می‌شود.",
])
section(S, "۱۱) گزارش‌ها", [
    "گزارش‌های مالی: تراز آزمایشی، دفتر کل/معین، سود و زیان، ترازنامه، صورت جریان وجوه نقد.",
    "دفترِ روزنامه: فهرستِ زمانیِ آرتیکل‌های اسناد.",
    "ماندهٔ سنی‌شدهٔ دریافتنی/پرداختنی: سطل‌های ۰–۳۰ / ۳۱–۶۰ / ۶۱–۹۰ / بیش از ۹۰.",
    "خلاصهٔ مالیاتِ ارزش‌افزوده: مالیاتِ فروش منهای خرید (برای اظهارنامه).",
    "اظهارِ حسابِ مشتری/تأمین‌کننده: ریزِ بدهکار/بستانکار با ماندهٔ متحرک.",
    "همهٔ گزارش‌ها خروجیِ اکسل / PDF (فارسیِ راست‌چین) / چاپ دارند.",
])
section(S, "۱۲) پشتیبان‌گیری و بستنِ سال", [
    "سیستم ← تنظیمات ← پشتیبان‌گیری: تهیه/بازگردانی + پشتیبانِ خودکارِ زمان‌بندی‌شده.",
    "حسابداری ← عملیات پایان دوره: بستنِ سالِ مالی (سندِ اختتامیه)؛ پس از آن دوره قفل می‌شود.",
])
section(S, "۱۳) بخش‌های تکمیلی و ماژول‌ها", [
    "منابع انسانی و حقوق: کارکنان، حضوروغیاب، فیشِ حقوق + سندِ حقوقِ خودکار.",
    "امنیت: کاربران/نقش‌ها/مجوزهای دانه‌ریز + لاگِ حسابرسی.",
    "چندشعبه + تسویهٔ بین‌شعبه (دو سندِ قطعیِ متوازن با حساب جاریِ فی‌مابین).",
    "مغایرت‌گیریِ بانکی؛ انبارِ پیشرفته (کاردکس/انبارگردانی/انتقال/بچ‑سریال).",
    "قالبِ اسناد و چاپ (۴۲ قالبِ آماده + QR)؛ داشبوردهای نقش‌محور.",
    "ماژول‌های فروشگاهی: POS (pos.exe/--pos) و رستوران (--restaurant/--waiter/--kitchen).",
    "فعال/غیرفعال‌سازیِ ماژول‌ها: سیستم ← مدیریت ماژول‌ها.",
])
section(S, "۱۴) عیب‌یابیِ سریع", [
    "سند قطعی نمی‌شود؟ متوازن نیست یا تاریخ خارج از سالِ مالیِ باز است.",
    "فروشِ نسیه رد می‌شود؟ سقفِ اعتبارِ مشتری پر شده.",
    "اتصال به سرور نیست؟ آدرسِ سرور و در دسترس‌بودنِ SQL Server را بررسی کنید.",
    "گزارش خالی است؟ بازهٔ تاریخ/فیلترها را بررسی و «نمایش» را بزنید.",
])
S.append(Spacer(1, 10))
S.append(P("§برای مرجعِ کاملِ صفحه‌به‌صفحه به «راهنمای کاربر» (SamaHesab-UserGuide.pdf) مراجعه کنید.", sub))

doc.build(S)
print("OK ->", OUT)
