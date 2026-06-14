# -*- coding: utf-8 -*-
"""
تولیدِ راهنمای کاربرِ «سما حساب» (PDF فارسیِ راست‌به‌چپ).
اجرا:  py -3 tools/make_userguide.py
خروجی: docs\SamaHesab-UserGuide.pdf
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
OUT = os.path.join(ROOT, "docs", "SamaHesab-UserGuide.pdf")
os.makedirs(os.path.dirname(OUT), exist_ok=True)

# فونت فارسی (Tahoma — دارای گلیف‌های فارسی)
pdfmetrics.registerFont(TTFont("Fa", r"C:\Windows\Fonts\tahoma.ttf"))
pdfmetrics.registerFont(TTFont("Fa-Bold", r"C:\Windows\Fonts\tahomabd.ttf"))

BLUE = colors.HexColor("#324A7A")
GOLD = colors.HexColor("#B18A5A")
GRAY = colors.HexColor("#6B7280")


def fa(t: str) -> str:
    """شکل‌دهی + جهت‌دهیِ راست‌به‌چپ برای متنِ فارسی."""
    return get_display(arabic_reshaper.reshape(t))


h1 = ParagraphStyle("h1", fontName="Fa-Bold", fontSize=20, alignment=TA_CENTER,
                    textColor=BLUE, leading=28, spaceAfter=4)
sub = ParagraphStyle("sub", fontName="Fa", fontSize=11, alignment=TA_CENTER,
                     textColor=GRAY, leading=16, spaceAfter=16)
h2 = ParagraphStyle("h2", fontName="Fa-Bold", fontSize=14, alignment=TA_RIGHT,
                    textColor=BLUE, leading=22, spaceBefore=14, spaceAfter=6)
body = ParagraphStyle("body", fontName="Fa", fontSize=11, alignment=TA_RIGHT,
                      textColor=colors.HexColor("#1F2937"), leading=20, spaceAfter=4)
bullet = ParagraphStyle("bullet", fontName="Fa", fontSize=11, alignment=TA_RIGHT,
                        textColor=colors.HexColor("#1F2937"), leading=19,
                        rightIndent=10, spaceAfter=2)


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
                        title="راهنمای کاربر سما حساب", author="سما نرم‌افزار")
S = []

S.append(P("سما حساب — راهنمای کاربر", h1))
S.append(P("سامانهٔ جامع مدیریت کسب‌وکار · نسخهٔ ۲.۰.۰", sub))

section(S, "۱) معرفی", [
    "§«سما حساب» یک نرم‌افزار جامع حسابداری و مدیریت کسب‌وکار (ERP) است که هستهٔ آن شاملِ حسابداری، خزانه‌داری، انبار، فروش، خرید، اشخاص و گزارش‌هاست.",
    "معماری مبتنی بر سرور مرکزی (Web API) است؛ کلاینت‌ها از طریق شبکه به سرور وصل می‌شوند، نه مستقیم به پایگاه‌داده.",
    "پشتیبانی کامل از زبان فارسی، تقویم شمسی، و ارقام فارسی.",
])

section(S, "۲) نصب و راه‌اندازی", [
    "نصبِ سرور: فایلِ نصابِ سرور را روی سیستمِ مرکزی اجرا کنید (پایگاه‌داده و Web API نصب می‌شود).",
    "نصبِ کلاینت: روی هر سیستمِ کاربر، نصابِ کلاینت را اجرا و آدرسِ سرور را وارد کنید (مثلاً http://192.168.1.10:5080).",
    "نیازی به نصبِ جداگانهٔ .NET یا SQL Server روی کلاینت‌ها نیست (نسخهٔ خودکفا).",
    "کاربرِ پیش‌فرض برای نخستین ورود: نام‌کاربری admin و گذرواژهٔ admin123 (پس از ورود حتماً تغییر دهید).",
])

section(S, "۳) ورود به سیستم و دسترسی‌ها", [
    "با نام‌کاربری و گذرواژه وارد شوید؛ دسترسی‌ها بر اساسِ نقشِ شما (نقش‌محور/RBAC) تعیین می‌شود.",
    "مدیرِ سیستم می‌تواند از «سیستم ← امنیت و دسترسی» نقش‌ها و مجوزها را تعریف و به کاربران تخصیص دهد.",
    "هر شعبه داده‌های خود را می‌بیند؛ کاربرِ دارای دسترسیِ «همهٔ شعب» به همه دسترسی دارد.",
])

section(S, "۴) میز کار (داشبورد)", [
    "نمای کلیِ روز: فروش امروز، دریافتنی، چک‌های سررسید نزدیک، کالاهای کم‌موجود.",
    "بخش «دسترسی سریع» برای پرش به عملیاتِ پرکاربرد با میان‌برهای Ctrl+1 تا Ctrl+6.",
    "پنل‌های «کارهای امروز» و «چک‌های نزدیک» وضعیتِ جاری را نشان می‌دهند.",
])

section(S, "۵) ثبت سند حسابداری", [
    "از نوار ابزار: «جدید» (F2)، «ذخیره» (F7)، «قطعی‌سازی» (F9)، «چاپ» (F8).",
    "نوار «دسترسی سریع» حساب‌های اخیر و موردعلاقه (★) را برای انتخابِ تک‌کلیکی نشان می‌دهد (مانده هم کنارش دیده می‌شود).",
    "حساب را با جست‌وجوی هوشمند (کد یا نام) انتخاب کنید؛ مبلغ بدهکار/بستانکار را وارد و با Enter ردیف را بیفزایید.",
    "کلید «=» سمتِ خالی را برای توازنِ خودکار پر می‌کند. تا تراز نشدن، دکمهٔ «قطعی‌سازی» غیرفعال است.",
    "«کپی سند» و «سند معکوس» و «الگو/تکراری» برای بهره‌وریِ بیشتر در دسترس‌اند.",
])

section(S, "۶) اسناد حسابداری", [
    "فهرستِ اسناد با فیلترِ بازهٔ تاریخ، نوع و وضعیت و جست‌وجو.",
    "کلیدهای ↑↓ برای پیمایش، Enter یا دابل‌کلیک برای باز کردن، F2 برای سند جدید.",
    "خروجیِ «اکسل» (CSV) و «چاپ» (F8) از فهرستِ فیلترشده.",
])

section(S, "۷) نمودار حساب‌ها", [
    "درختِ حساب‌ها با جست‌وجوی کد/نام و نمایشِ ماندهٔ هر حساب کنارِ آن.",
    "«حساب جدید» (زیرمجموعهٔ حسابِ انتخاب‌شده)، «ویرایش» و «حذف» (تنها اگر حساب نه تراکنش دارد نه زیرحساب).",
    "«مشاهده دفتر کل» شما را مستقیم به دفتر کلِ همان حساب می‌برد.",
])

section(S, "۸) خزانه‌داری و مدیریت چک", [
    "مدیریت چک: کارت‌های آماری (همه/در جریان/واگذار/وصول‌شده/برگشتی) برای فیلترِ سریع.",
    "عملیاتِ «وصول»، «واگذاری به بانک» و «برگشت» روی چکِ انتخاب‌شده با سندِ حسابداریِ خودکار.",
    "ستونِ «مانده تا سررسید» و هایلایتِ چک‌های نزدیکِ سررسید.",
    "مغایرت‌گیری بانکی، دریافتنی/پرداختنی و تابلوی چک نیز در دسترس‌اند.",
])

section(S, "۹) گزارش‌های مالی", [
    "تراز آزمایشی، دفتر کل/معین، سود و زیان، ترازنامه، و صورت جریان وجوه نقد.",
    "فیلترِ بازهٔ تاریخ، شعبه، مرکز هزینه و پروژه.",
    "خروجیِ اکسل (CSV) و چاپ (HTML)؛ برای PDF از «چاپ ← ذخیره به‌صورت PDF» در مرورگر استفاده کنید.",
    "فایل‌های خروجی در مسیر «اسناد من ← SamaHesab ← گزارش‌ها» ذخیره می‌شوند.",
])

section(S, "۱۰) کلیدهای میان‌بر پرکاربرد", [
    "F2 سند/ردیفِ جدید · F7 ذخیره · F8 چاپ · F9 قطعی‌سازی · «=» توازنِ خودکار.",
    "Ctrl+1..6 دسترسی سریع از میز کار · ↑↓ پیمایش فهرست · Enter باز کردن.",
])

section(S, "۱۱) حالت‌های اجرا", [
    "حسابداری (پیش‌فرض) · صندوق فروشگاه (POS) · صندوق رستوران · گارسون · آشپزخانه · انبار.",
    "هر حالت با میان‌برِ مخصوصِ خود از منوی نصب‌شده اجرا می‌شود.",
])

section(S, "۱۲) پشتیبانی", [
    "§برای پشتیبانی با تأمین‌کنندهٔ نرم‌افزار تماس بگیرید. پیش از تماس، شمارهٔ نسخه (۲.۰.۰) و شرحِ مشکل را آماده کنید.",
    "§© سما نرم‌افزار — همهٔ حقوق محفوظ است.",
])

doc.build(S)
print("OK ->", OUT)
