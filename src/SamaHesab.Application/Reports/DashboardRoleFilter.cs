namespace SamaHesab.Application.Reports;

/// <summary>نقشِ کاربر برای «کارهای امروزِ من» — مدیر همه را می‌بیند، بقیه فقط موارد مرتبط.</summary>
public enum DashboardRole
{
    Manager = 0,          // مدیر — همهٔ هشدارها
    Accountant = 1,       // حسابدار
    Treasurer = 2,        // خزانه‌دار
    InventoryManager = 3, // انباردار
    Sales = 4,            // فروش
    TourismOperator = 5,  // اپراتورِ گردشگری
    ProjectManager = 6,   // مدیرِ پروژه (پیمانکاری)
}

/// <summary>
/// فیلترِ هشدارهای داشبورد بر اساسِ نقش — «کارهای امروزِ منِ» نقش‌محور (رودمپ-تجربه).
/// منطقِ خالص و تست‌پذیر: هر کلیدِ هشدار به نقش‌هایی که برایشان قابل‌اقدام است نگاشت می‌شود؛
/// مدیر همه را می‌بیند. ترتیبِ ورودی (شدت→مبلغ) حفظ می‌شود.
/// </summary>
public static class DashboardRoleFilter
{
    // کلیدِ هشدار → نقش‌هایی که برایشان مرتبط است (مدیر همیشه شامل، جداگانه اعمال می‌شود).
    private static readonly Dictionary<string, DashboardRole[]> Map = new()
    {
        ["cheque-overdue"]      = new[] { DashboardRole.Accountant, DashboardRole.Treasurer },
        ["cheque-due-soon"]     = new[] { DashboardRole.Accountant, DashboardRole.Treasurer },
        ["receivable-overdue"]  = new[] { DashboardRole.Accountant, DashboardRole.Sales },
        ["stock-low"]           = new[] { DashboardRole.InventoryManager },
        ["tourism-deposit-low"] = new[] { DashboardRole.TourismOperator },
        ["guarantee-expiring"]  = new[] { DashboardRole.ProjectManager },
    };

    public static List<ActionableAlert> For(DashboardRole role, IEnumerable<ActionableAlert> alerts)
    {
        if (role == DashboardRole.Manager) return alerts.ToList();
        return alerts
            .Where(a => Map.TryGetValue(a.Key, out var roles) && roles.Contains(role))
            .ToList();
    }
}
