using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Infrastructure.Data;

namespace SamaHesab.Infrastructure.Services;

/// <summary>U-MULTI-COMPANY-1 — پیاده‌سازیِ <see cref="ICompanyProvisioningService"/> با ری‌اجرایِ
/// مستقیمِ اسکریپت‌هایِ per-company-safe از طریقِ <see cref="DatabaseMigrator"/>.</summary>
public sealed class CompanyProvisioningService : ICompanyProvisioningService
{
    private readonly string _connectionString;

    public CompanyProvisioningService(string connectionString) => _connectionString = connectionString;

    public Task ProvisionAsync(CancellationToken ct = default) =>
        DatabaseMigrator.RunCompanyProvisioningScriptsAsync(_connectionString, log: null, ct);
}
