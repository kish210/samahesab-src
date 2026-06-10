using SamaHesab.Domain.Entities.Settings;

namespace SamaHesab.Domain.Interfaces.Repositories;

public interface IUserItemRefRepository : IRepository<UserItemRef>
{
    /// <summary>یک ارجاع مشخص کاربر به یک آیتم (برای upsert در Touch/Pin).</summary>
    Task<UserItemRef?> FindAsync(int companyId, int userId, string entityType, int entityId, CancellationToken ct = default);

    /// <summary>اخیرترین آیتم‌های یک نوع برای کاربر.</summary>
    Task<List<UserItemRef>> RecentAsync(int companyId, int userId, string entityType, int top, CancellationToken ct = default);

    /// <summary>آیتم‌های سنجاق‌شده‌ی یک نوع برای کاربر.</summary>
    Task<List<UserItemRef>> PinnedAsync(int companyId, int userId, string entityType, CancellationToken ct = default);
}
