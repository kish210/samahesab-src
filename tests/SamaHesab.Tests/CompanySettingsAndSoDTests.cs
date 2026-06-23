using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Settings;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>CR-X8 — تنظیماتِ شرکتیِ DB (کلید-مقدار) + خوانندهٔ بولین (پایهٔ SoD).</summary>
public class CompanySettingsAndSoDTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default)
        { foreach (var e in es) { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); } return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
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
        public IRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    [Fact]
    public async Task Save_Then_Get_And_BoolReader_RoundTrip()
    {
        var repo = new FakeRepo<CompanySetting>(); var uow = new FakeUow(); var user = new FakeUser();
        var save = new SaveCompanySettingCommandHandler(repo, uow, user);

        await save.Handle(new SaveCompanySettingCommand(CompanySettingKeys.EnforceSoD, "true"), default);
        await save.Handle(new SaveCompanySettingCommand(CompanySettingKeys.CompanyName, "سماع رایانه"), default);
        // به‌روزرسانیِ همان کلید (upsert، نه ردیفِ تکراری)
        await save.Handle(new SaveCompanySettingCommand(CompanySettingKeys.CompanyName, "سماع رایانه کیش"), default);

        Assert.Equal(2, repo.Items.Count);   // دو کلیدِ یکتا
        var dict = await new GetCompanySettingsQueryHandler(repo, user).Handle(new GetCompanySettingsQuery(), default);
        Assert.Equal("سماع رایانه کیش", dict[CompanySettingKeys.CompanyName]);

        Assert.True(await CompanySettingsReader.GetBoolAsync(repo, 1, CompanySettingKeys.EnforceSoD));
        Assert.False(await CompanySettingsReader.GetBoolAsync(repo, 1, "NotSet", fallback: false));
    }
}
