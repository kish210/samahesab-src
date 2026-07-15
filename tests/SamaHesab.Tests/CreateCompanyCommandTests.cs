using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Settings.Commands;
using SamaHesab.Application.Settings.Queries;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-MULTI-COMPANY-1 — ساختِ شرکتِ نو (چند شرکت در یک DBِ مشترک، درخواستِ صریحِ کاربر
/// @2026-07-15) + رفعِ باگِ «صفحهٔ ورود نامِ شرکتِ ساخته‌شده را نشان نمی‌داد».</summary>
public class CreateCompanyCommandTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        private static void SetId(T e, int value)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop != null) prop.SetValue(e, System.Convert.ChangeType(value, prop.PropertyType));
        }
        public Task AddAsync(T e, CancellationToken ct = default)
        { SetId(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default)
        { foreach (var e in es) { SetId(e, ++_seq); Items.Add(e); } return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(e => (int)(typeof(T).GetProperty("Id")!.GetValue(e) ?? 0) == id));
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(T e) { }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> es) { foreach (var x in es.ToList()) Items.Remove(x); }
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public IRepository<T> GetRepository<T>() where T : class => throw new System.NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    /// <summary>شبیه‌سازیِ 68_CompanyBaseChartTemplate.sql: فقط شعبهٔ HQ برایِ شرکت‌هایِ بدونِ آن می‌سازد
    /// (کافی برایِ تستِ اینکه Handler بعد از provisioning شعبه را پیدا و به کاربر متصل می‌کند).</summary>
    private sealed class FakeProvisioningService : ICompanyProvisioningService
    {
        private readonly FakeRepo<Company> _companies;
        private readonly FakeRepo<Branch> _branches;
        public FakeProvisioningService(FakeRepo<Company> companies, FakeRepo<Branch> branches)
        { _companies = companies; _branches = branches; }

        public Task ProvisionAsync(CancellationToken ct = default)
        {
            foreach (var c in _companies.Items)
                if (!_branches.Items.Any(b => b.CompanyId == c.Id))
                    _branches.AddAsync(Branch.Create(c.Id, "HQ", "دفتر مرکزی", true), ct);
            return Task.CompletedTask;
        }
    }

    private static (CreateCompanyCommandHandler handler, FakeRepo<Company> companies, FakeRepo<Branch> branches,
        FakeRepo<FiscalYear> fiscalYears, FakeRepo<User> users) Build()
    {
        var companies = new FakeRepo<Company>();
        var branches = new FakeRepo<Branch>();
        var fiscalYears = new FakeRepo<FiscalYear>();
        var users = new FakeRepo<User>();
        var provisioning = new FakeProvisioningService(companies, branches);
        var handler = new CreateCompanyCommandHandler(companies, branches, fiscalYears, users, new FakeUow(), provisioning);
        return (handler, companies, branches, fiscalYears, users);
    }

    [Fact]
    public async Task Creates_Company_With_Auto_Incremented_Code()
    {
        var (handler, companies, _, _, _) = Build();
        await companies.AddAsync(Company.Create("001", "شرکت نمونه", "1403/01/01", "1403/12/29"));

        var res = await handler.Handle(new CreateCompanyCommand(
            "فروشگاهِ دوم", null, null, null, null, "سالِ ۱۴۰۴", "1404/01/01", "1404/12/29", "Passw0rd"), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        Assert.Equal("002", res.Value!.Code);
        Assert.Equal("فروشگاهِ دوم", res.Value.Name);
    }

    [Fact]
    public async Task Creates_Admin_User_Named_Admin_Scoped_To_New_Company()
    {
        var (handler, companies, _, _, users) = Build();
        await companies.AddAsync(Company.Create("001", "شرکت نمونه", "1403/01/01", "1403/12/29"));

        var res = await handler.Handle(new CreateCompanyCommand(
            "فروشگاهِ دوم", null, null, null, null, "سالِ ۱۴۰۴", "1404/01/01", "1404/12/29", "Passw0rd"), default);

        var admin = users.Items.Single();
        Assert.Equal("admin", admin.Username);
        Assert.Equal(res.Value!.CompanyId, admin.CompanyId);
        Assert.NotEqual(1, admin.CompanyId);   // شرکتِ نو، نه شرکتِ اول
    }

    [Fact]
    public async Task Provisions_HQ_Branch_And_FiscalYear_For_New_Company()
    {
        var (handler, companies, branches, fiscalYears, _) = Build();
        await companies.AddAsync(Company.Create("001", "شرکت نمونه", "1403/01/01", "1403/12/29"));

        var res = await handler.Handle(new CreateCompanyCommand(
            "فروشگاهِ دوم", null, null, null, null, "سالِ ۱۴۰۴", "1404/01/01", "1404/12/29", "Passw0rd"), default);

        Assert.Contains(branches.Items, b => b.CompanyId == res.Value!.CompanyId && b.Code == "HQ");
        Assert.Contains(fiscalYears.Items, f => f.CompanyId == res.Value!.CompanyId && f.Title == "سالِ ۱۴۰۴");
    }

    [Fact]
    public async Task Rejects_Weak_Admin_Password()
    {
        var (handler, companies, _, _, users) = Build();
        await companies.AddAsync(Company.Create("001", "شرکت نمونه", "1403/01/01", "1403/12/29"));

        var res = await handler.Handle(new CreateCompanyCommand(
            "فروشگاهِ دوم", null, null, null, null, "سالِ ۱۴۰۴", "1404/01/01", "1404/12/29", "weak"), default);

        Assert.False(res.Succeeded);
        Assert.Empty(users.Items);
    }

    [Fact]
    public async Task GetCompaniesQuery_Returns_All_Active_Companies_Ordered_By_Id()
    {
        var companies = new FakeRepo<Company>();
        await companies.AddAsync(Company.Create("001", "شرکتِ الف", "1403/01/01", "1403/12/29"));
        await companies.AddAsync(Company.Create("002", "شرکتِ ب", "1404/01/01", "1404/12/29"));
        var handler = new GetCompaniesQueryHandler(companies);

        var rows = await handler.Handle(new GetCompaniesQuery(), default);

        Assert.Equal(2, rows.Count);
        Assert.Equal("شرکتِ الف", rows[0].Name);
        Assert.Equal("شرکتِ ب", rows[1].Name);
    }

    [Fact]
    public async Task UpdateCompanyCommand_Renames_Existing_Company_Row()
    {
        var companies = new FakeRepo<Company>();
        await companies.AddAsync(Company.Create("001", "شرکت نمونه", "1403/01/01", "1403/12/29"));
        var handler = new UpdateCompanyCommandHandler(companies, new FakeUow());

        var res = await handler.Handle(new UpdateCompanyCommand(1, "فروشگاهِ واقعیِ من", "1234567890", null, null, null), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        Assert.Equal("فروشگاهِ واقعیِ من", companies.Items.Single().Name);
    }
}
