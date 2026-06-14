using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Behaviors;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>T19 — رفتارِ RBAC + حسابرسیِ حرکاتِ انبار (`InventoryAuditBehavior`).</summary>
public class InventoryAuditBehaviorTests
{
    private sealed class FakeUser : ICurrentUserService
    {
        private readonly bool _allow;
        public FakeUser(bool allow) => _allow = allow;
        public int? UserId => 5; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "anbar"; public string? FullName => "انباردار"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => _allow;
        public IEnumerable<string> GetRoles() => _allow ? new[] { "ADMIN" } : Array.Empty<string>();
    }

    private sealed class FakeAuditRepo : IRepository<AuditLog>
    {
        public readonly List<AuditLog> Items = new();
        public Task AddAsync(AuditLog e, CancellationToken ct = default) { Items.Add(e); return Task.CompletedTask; }
        public Task<AuditLog?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<AuditLog?>(null);
        public Task<List<AuditLog>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<AuditLog>> FindAsync(Expression<Func<AuditLog, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<AuditLog?> FindSingleAsync(Expression<Func<AuditLog, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<AuditLog, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<AuditLog, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<AuditLog> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Update(AuditLog e) { }
        public void Remove(AuditLog e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<AuditLog> e) { }
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public IRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private static readonly AdjustStockCommand SampleMove = new(1, 2, 10, "1403/03/15", "اصلاح");

    [Fact]
    public async Task Without_Permission_Denies_And_Skips_Handler()
    {
        var audit = new FakeAuditRepo();
        var sut = new InventoryAuditBehavior<AdjustStockCommand, Result>(new FakeUser(false), audit, new FakeUow());
        var handlerCalled = false;

        var res = await sut.Handle(SampleMove, () => { handlerCalled = true; return Task.FromResult(Result.Success()); }, default);

        Assert.False(res.Succeeded);            // Result.Failure (نه استثنا)
        Assert.False(handlerCalled);            // فرمان اجرا نشد
        Assert.Empty(audit.Items);              // لاگی ثبت نشد
    }

    [Fact]
    public async Task With_Permission_Runs_Handler_And_Writes_Audit()
    {
        var audit = new FakeAuditRepo();
        var sut = new InventoryAuditBehavior<AdjustStockCommand, Result>(new FakeUser(true), audit, new FakeUow());

        var res = await sut.Handle(SampleMove, () => Task.FromResult(Result.Success()), default);

        Assert.True(res.Succeeded);
        var log = Assert.Single(audit.Items);
        Assert.Equal("تعدیلِ موجودی", log.Action);
        Assert.Equal(5, log.UserId);
        Assert.Equal("Inv", log.TableName);
    }

    [Fact]
    public async Task Failed_Handler_Does_Not_Write_Audit()
    {
        var audit = new FakeAuditRepo();
        var sut = new InventoryAuditBehavior<AdjustStockCommand, Result>(new FakeUser(true), audit, new FakeUow());

        var res = await sut.Handle(SampleMove, () => Task.FromResult(Result.Failure("خطا")), default);

        Assert.False(res.Succeeded);
        Assert.Empty(audit.Items);              // فقط در صورتِ موفقیت لاگ می‌شود
    }

    [Fact]
    public async Task NonInventory_Command_Passes_Through_Untouched()
    {
        var audit = new FakeAuditRepo();
        // فرمانی که در فهرستِ حرکاتِ انبار نیست (حتی با کاربرِ بدونِ مجوز) باید عبور کند.
        var sut = new InventoryAuditBehavior<StartStockCountCommand, Result<int>>(new FakeUser(false), audit, new FakeUow());

        var res = await sut.Handle(new StartStockCountCommand(1, "1403/03/15"),
            () => Task.FromResult(Result<int>.Success(99)), default);

        Assert.True(res.Succeeded);
        Assert.Equal(99, res.Value);
        Assert.Empty(audit.Items);
    }
}
