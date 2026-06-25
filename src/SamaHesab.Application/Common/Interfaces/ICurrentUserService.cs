namespace SamaHesab.Application.Common.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    int? CompanyId { get; }
    int? BranchId { get; }
    string? Username { get; }
    string? FullName { get; }
    /// <summary>SP-1 — اگر کاربر به یک «فروشنده» (Party) نگاشته شده باشد. پنلِ فروشِ گردشگری
    /// فروشنده را خودکار از همین می‌گیرد. null = کاربرِ غیرفروشنده (مثلِ ادمین).
    /// پیش‌فرضِ interface = null تا پیاده‌سازی‌های موجود (و fakeهای تست) بدونِ تغییر بمانند.</summary>
    int? SalespersonPartyId => null;
    bool IsAuthenticated { get; }
    bool HasPermission(string moduleCode, string featureCode, string action);
    IEnumerable<string> GetRoles();
}
