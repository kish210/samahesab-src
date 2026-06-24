using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SamaHesab.Infrastructure.Data;

/// <summary>
/// کلیدِ کشِ مدلِ EF را به مجموعهٔ ماژول‌های فعال وابسته می‌کند (ماژولارسازی/removability).
/// بدونِ این، EF مدلِ ساخته‌شده با یک مجموعهٔ ماژول را برای مجموعه‌ای دیگر بازاستفاده می‌کرد؛
/// با این، نصب/حذفِ یک ماژول → کلیدِ متفاوت → بازساختِ مدل (جدول‌های ماژول می‌آیند/می‌روند).
/// </summary>
public sealed class ModuleAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => (context.GetType(),
            (context as ApplicationDbContext)?.ActiveModuleKeys ?? string.Empty,
            designTime);
}
