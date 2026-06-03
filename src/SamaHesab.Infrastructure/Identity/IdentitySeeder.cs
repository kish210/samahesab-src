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
            // Self-heal the documented default admin during the pre-production phase:
            // if its stored credential is missing/corrupt (or the account got locked),
            // reset it to the default so the system is never unusable.
            if (!PasswordHasher.Verify("admin123", existing.PasswordHash, existing.PasswordSalt) || existing.IsLocked)
            {
                var (h, s) = PasswordHasher.Create("admin123");
                existing.SetPassword(h, s);
                existing.Unlock();
                users.Update(existing);
                await uow.SaveChangesAsync();
            }
            return;
        }

        var (hash, salt) = PasswordHasher.Create("admin123");
        var admin = User.Create(companyId, 1, "admin", hash, salt, "مدیر سیستم");
        await users.AddAsync(admin);
        await uow.SaveChangesAsync();
    }
}
