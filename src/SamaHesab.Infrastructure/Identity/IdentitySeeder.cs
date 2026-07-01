using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Application.Common.Security;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Infrastructure.Identity;

/// <summary>Ensures a default admin user exists so the system is usable on a fresh DB.</summary>
public static class IdentitySeeder
{
    public static async Task EnsureDefaultAdminAsync(IServiceProvider services, int companyId = 1)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IRepository<User>>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var existing = await users.FindSingleAsync(u => u.CompanyId == companyId && u.Username == "admin");
        if (existing != null)
        {
            // فقط رکوردِ **خراب** را ترمیم کن — نه رمزِ عمداً‌تغییریافته را.
            // ⚠️ ریسکِ فروش (رفع‌شده): نسخهٔ قبلی هر رمزِ ادمینِ عوض‌شده را در استارت‌آپ به
            // «admin123» بازمی‌گرداند (چون Verify("admin123") شکست می‌خورد → reset). حالا فقط
            // وقتی hash/salt واقعاً خالی/ناقص است (رکوردِ فاسد، نه رمزِ سالمِ متفاوت) ریست می‌شود.
            var credentialBroken = string.IsNullOrEmpty(existing.PasswordHash)
                                   || string.IsNullOrEmpty(existing.PasswordSalt);
            var dirty = false;
            if (credentialBroken)   // فقط رکوردِ فاسد → رمزِ پیش‌فرض (نه رمزِ سالمِ عوض‌شده)
            {
                var (h, s) = PasswordHasher.Create("admin123");
                existing.SetPassword(h, s);
                dirty = true;
            }
            // SR-5: بازکردنِ ادمینِ قفل‌شده در استارت‌آپ فقط وقتی که cooldownِ ۱۵دقیقه‌ایِ ضدِ brute-force
            // واقعاً منقضی شده باشد (همان شرطی که AuthenticateCommand هنگامِ ورود چک می‌کند). پیش‌تر این‌جا
            // بدونِ قید باز می‌شد که یعنی یک ری‌استارتِ سرور، قفلِ حمله‌ی brute-force را کاملاً بی‌اثر می‌کرد.
            if (existing.IsLocked && (existing.LockoutEnd == null || existing.LockoutEnd <= DateTime.Now))
            { existing.Unlock(); dirty = true; }
            if (dirty) { users.Update(existing); await uow.SaveChangesAsync(); }
            return;
        }

        var (hash, salt) = PasswordHasher.Create("admin123");
        var admin = User.Create(companyId, 1, "admin", hash, salt, "مدیر سیستم");
        // SR-6: ادمینِ تازه‌ساخته‌شده هنوز رمزِ پیش‌فرضِ شناخته‌شده (admin123) را دارد؛
        // اولین ورود باید تغییرِ رمز را اجبار کند.
        admin.RequirePasswordChangeOnNextLogin();
        await users.AddAsync(admin);
        await uow.SaveChangesAsync();
    }
}
