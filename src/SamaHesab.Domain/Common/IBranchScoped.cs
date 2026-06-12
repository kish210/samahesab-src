namespace SamaHesab.Domain.Common;

/// <summary>
/// موجودیتی که به یک شعبه تعلق دارد و باید در جداسازیِ دادهٔ چندشعبه (MB-1) لحاظ شود.
/// کاربرِ بدون مجوز «Security.AllBranches» فقط ردیف‌های شعبهٔ خود را می‌بیند.
/// </summary>
public interface IBranchScoped
{
    int BranchId { get; }
}
