namespace SamaHesab.Application.Common.Security;

/// <summary>یک مجوز در کاتالوگ (کد + برچسب فارسی + ماژول).</summary>
public record PermissionDef(string Code, string Module, string Label);

/// <summary>
/// کاتالوگ ثابتِ مجوزهای هستهٔ ERP (RBAC). کد به‌صورت «Module.Feature.Action».
/// نقش‌ها زیرمجموعه‌ای از این کدها را می‌گیرند؛ ADMIN همهٔ آن‌ها (یا «*») را دارد.
/// </summary>
public static class PermissionCatalog
{
    public const string Wildcard = "*";

    public static readonly IReadOnlyList<PermissionDef> All = new List<PermissionDef>
    {
        // حسابداری
        new("Accounting.Voucher.View",   "حسابداری", "مشاهدهٔ اسناد"),
        new("Accounting.Voucher.Create", "حسابداری", "ثبت سند"),
        new("Accounting.Voucher.Post",   "حسابداری", "قطعی‌کردن/برگشت سند"),
        new("Accounting.Voucher.Approve","حسابداری", "تأیید/ردِ سند (گردش‌کار)"),
        new("Accounting.Setup.Manage",   "حسابداری", "مدیریت حساب‌ها/ابعاد/سال مالی"),
        // خزانه
        new("Treasury.View",   "خزانه", "مشاهدهٔ خزانه (چک/بانک)"),
        new("Treasury.Manage", "خزانه", "عملیات خزانه (وصول/پرداخت/مغایرت)"),
        // فروش
        new("Sales.Invoice.View",   "فروش", "مشاهدهٔ فاکتور فروش"),
        new("Sales.Invoice.Create", "فروش", "ثبت فاکتور فروش/مرجوعی"),
        // خرید
        new("Purchase.Invoice.View",   "خرید", "مشاهدهٔ فاکتور خرید"),
        new("Purchase.Invoice.Create", "خرید", "ثبت فاکتور خرید/مرجوعی"),
        // انبار
        new("Inventory.View",   "انبار", "مشاهدهٔ انبار/کالا"),
        new("Inventory.Manage", "انبار", "عملیات انبار (رسید/حواله/انتقال/انبارگردانی)"),
        // مشتریان/تأمین‌کنندگان
        new("Customers.View",   "مشتریان", "مشاهدهٔ مشتری/تأمین‌کننده"),
        new("Customers.Manage", "مشتریان", "مدیریت مشتری/تأمین‌کننده"),
        // گزارش‌ها
        new("Reports.View",   "گزارش‌ها", "مشاهدهٔ گزارش‌ها"),
        new("Reports.Export", "گزارش‌ها", "خروجی اکسل/PDF"),
        // امنیت/تنظیمات
        new("Security.Manage", "امنیت", "مدیریت کاربران و نقش‌ها"),
        new("Security.AllBranches", "امنیت", "دسترسی به دادهٔ همهٔ شعب (نه فقط شعبهٔ خود)"),
        new("Settings.Manage", "تنظیمات", "تنظیمات سیستم و ماژول‌ها"),
        // ── CR-SoD/مجوزهای ریزدانهٔ تجاری (قابلِ تخصیص؛ برای کنترل‌های حساسِ آینده) ──
        new("Sales.Discount.Override",   "فروش", "اعمالِ تخفیفِ بالاتر از حد"),
        new("Treasury.Payment.Approve",  "خزانه", "تأییدِ پرداخت"),
        new("Accounting.Voucher.Adjust", "حسابداری", "تعدیل/ویرایشِ سندِ قطعی‌شده"),
        new("Accounting.SoD.Bypass",     "حسابداری", "عبور از تفکیکِ وظایف (استثنا)"),
        new("Customers.Balance.View",    "مشتریان", "مشاهدهٔ ماندهٔ مالیِ مشتری"),
        new("Inventory.Cost.View",       "انبار", "مشاهدهٔ بهای تمام‌شده/سود"),
    };

    /// <summary>آیا مجموعه‌ی کدهای اعطاشده، مجوز موردنظر را پوشش می‌دهد؟ («*» و ماژولِ wildcard لحاظ می‌شود.)</summary>
    public static bool Grants(IEnumerable<string> granted, string required)
    {
        foreach (var g in granted)
        {
            if (g == Wildcard) return true;
            if (g == required) return true;
            // پشتیبانی از wildcard ماژول: «Treasury.*» همهٔ Treasury.* را پوشش می‌دهد
            if (g.EndsWith(".*") && required.StartsWith(g[..^1])) return true;
        }
        return false;
    }
}
