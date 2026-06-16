using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Common.Behaviors;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>T19/T21 — رفتارِ حسابرسی + کنترلِ دسترسیِ عملیاتِ حساس (`AuditBehavior`).</summary>
public class AuditBehaviorTests
{
    private sealed class FakeUser : ICurrentUserService
    {
        private readonly bool _allow;
        public FakeUser(bool allow) => _allow = allow;
        public int? UserId => 5; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "u"; public string? FullName => "کاربر"; public bool IsAuthenticated => true;
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

    // ── انبار (enforce=true) ──
    private static readonly AdjustStockCommand Move = new(1, 2, 10, "1403/03/15", "x");

    [Fact]
    public async Task Inventory_Without_Permission_Denies_And_Skips_Handler()
    {
        var audit = new FakeAuditRepo();
        var sut = new AuditBehavior<AdjustStockCommand, Result>(new FakeUser(false), audit, new FakeUow());
        var called = false;

        var res = await sut.Handle(Move, () => { called = true; return Task.FromResult(Result.Success()); }, default);

        Assert.False(res.Succeeded);
        Assert.False(called);
        Assert.Empty(audit.Items);
    }

    [Fact]
    public async Task Inventory_With_Permission_Runs_And_Audits()
    {
        var audit = new FakeAuditRepo();
        var sut = new AuditBehavior<AdjustStockCommand, Result>(new FakeUser(true), audit, new FakeUow());

        var res = await sut.Handle(Move, () => Task.FromResult(Result.Success()), default);

        Assert.True(res.Succeeded);
        Assert.Equal("تعدیلِ موجودی", Assert.Single(audit.Items).Action);
    }

    // ── حسابداری (enforce=false → فقط حسابرسی، بدونِ منع) ──
    [Fact]
    public async Task Accounting_AuditOnly_Runs_Even_Without_Permission_And_Audits()
    {
        var audit = new FakeAuditRepo();
        var sut = new AuditBehavior<PostVoucherCommand, Result>(new FakeUser(false), audit, new FakeUow());
        var called = false;

        var res = await sut.Handle(new PostVoucherCommand(7),
            () => { called = true; return Task.FromResult(Result.Success()); }, default);

        Assert.True(res.Succeeded);                 // منع نشد (audit-only)
        Assert.True(called);
        Assert.Equal("قطعیِ سند", Assert.Single(audit.Items).Action);
    }

    [Fact]
    public async Task Failed_Operation_Is_Not_Audited()
    {
        var audit = new FakeAuditRepo();
        var sut = new AuditBehavior<PostVoucherCommand, Result>(new FakeUser(true), audit, new FakeUow());

        var res = await sut.Handle(new PostVoucherCommand(7), () => Task.FromResult(Result.Failure("خطا")), default);

        Assert.False(res.Succeeded);
        Assert.Empty(audit.Items);
    }

    // ── امنیت: تغییرِ رمز audit می‌شود ولی رمز در payload لاگ نمی‌شود (Sensitive) ──
    [Fact]
    public async Task ChangePassword_Is_Audited_Without_Leaking_The_Password()
    {
        var audit = new FakeAuditRepo();
        var sut = new AuditBehavior<SamaHesab.Application.Security.Commands.ChangeUserPasswordCommand, Result>(
            new FakeUser(false), audit, new FakeUow());

        const string secret = "S3cret-PlainText-Pw";
        var res = await sut.Handle(
            new SamaHesab.Application.Security.Commands.ChangeUserPasswordCommand(5, secret),
            () => Task.FromResult(Result.Success()), default);

        Assert.True(res.Succeeded);                          // audit-only، منع نمی‌کند
        var log = Assert.Single(audit.Items);
        Assert.Equal("تغییرِ رمزِ کاربر", log.Action);
        Assert.Null(log.NewValues);                          // payload سریال نشده
        Assert.DoesNotContain(secret, log.NewValues ?? "");  // رمز هیچ‌جای لاگ نیست
    }

    [Fact]
    public async Task Unmapped_Command_Passes_Through()
    {
        var audit = new FakeAuditRepo();
        var sut = new AuditBehavior<StartStockCountCommand, Result<int>>(new FakeUser(false), audit, new FakeUow());

        var res = await sut.Handle(new StartStockCountCommand(1, "1403/03/15"),
            () => Task.FromResult(Result<int>.Success(3)), default);

        Assert.True(res.Succeeded);
        Assert.Empty(audit.Items);
    }
}
