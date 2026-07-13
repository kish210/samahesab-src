using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Behaviors;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Security.Commands;
using SamaHesab.Application.Settings.Commands;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-SEC-1/2 — پیگیریِ ممیزیِ امنیتیِ Settings/System: فرمان‌های تغییرِ سطحِ دسترسی حالا واقعاً
/// enforce می‌شوند (نه فقط audit-only)، و اعتبارسنجیِ ناموفق دیگر throw نمی‌کند (که در UI بی‌صدا گم می‌شد).
/// (رفعِ U-SEC-4 در ModuleService است — لایهٔ WPF، بدونِ رفرنس از این پروژهٔ تست.)</summary>
public class SecurityHardeningTests
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
        public IRepository<T> GetRepository<T>() where T : class => throw new System.NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public bool IsAdmin;
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "tester"; public string? FullName => "Tester"; public bool IsAuthenticated => true;
        public int? SalespersonPartyId => null;
        public bool HasPermission(string m, string f, string a) => IsAdmin;
        public IEnumerable<string> GetRoles() => IsAdmin ? new[] { "ADMIN" } : System.Array.Empty<string>();
    }

    // ── U-SEC-1: AuditBehavior باید فرمان‌های حساسِ enforce=true را واقعاً مسدود کند ──────────────
    [Fact]
    public async Task SetUserRolesCommand_Is_Denied_For_NonAdmin_Without_Permission()
    {
        var user = new FakeUser { IsAdmin = false };
        var behavior = new AuditBehavior<SetUserRolesCommand, Result>(user, new FakeRepo<AuditLog>(), new FakeUow());
        var called = false;

        var result = await behavior.Handle(new SetUserRolesCommand(2, new[] { 1 }),
            () => { called = true; return Task.FromResult(Result.Success()); }, default);

        Assert.False(result.Succeeded);
        Assert.False(called);   // هندلرِ واقعی اصلاً صدا زده نشد
    }

    [Fact]
    public async Task SetUserRolesCommand_Proceeds_For_Admin()
    {
        var user = new FakeUser { IsAdmin = true };
        var behavior = new AuditBehavior<SetUserRolesCommand, Result>(user, new FakeRepo<AuditLog>(), new FakeUow());
        var called = false;

        var result = await behavior.Handle(new SetUserRolesCommand(2, new[] { 1 }),
            () => { called = true; return Task.FromResult(Result.Success()); }, default);

        Assert.True(result.Succeeded);
        Assert.True(called);
    }

    [Fact]
    public async Task SaveShareholderCommand_Is_Denied_For_NonAdmin_Without_Permission()
    {
        var user = new FakeUser { IsAdmin = false };
        var behavior = new AuditBehavior<SaveShareholderCommand, Result<int>>(user, new FakeRepo<AuditLog>(), new FakeUow());

        var result = await behavior.Handle(new SaveShareholderCommand(0, "علی", null, 10, 1000, null, null),
            () => Task.FromResult(Result<int>.Success(1)), default);

        Assert.False(result.Succeeded);
    }

    // ── U-SEC-8: SetUserRolesCommand نباید تنها ادمینِ سیستم را از نقشِ ADMIN بیندازد ──────────────
    [Fact]
    public async Task SetUserRolesCommand_Denies_Removing_Admin_From_Only_Admin()
    {
        var roles = new FakeRepo<Role>();
        var adminRole = Role.Create(1, "ADMIN", "مدیرِ سیستم", isSystem: true);
        await roles.AddAsync(adminRole);   // Id=1
        var otherRole = Role.Create(1, "CASHIER", "صندوق‌دار");
        await roles.AddAsync(otherRole);   // Id=2

        var userRoles = new FakeRepo<UserRole>();
        await userRoles.AddAsync(UserRole.Create(userId: 1, roleId: adminRole.Id));   // تنها ادمین

        var handler = new SetUserRolesCommandHandler(userRoles, roles, new FakeUow());

        var result = await handler.Handle(new SetUserRolesCommand(1, new[] { otherRole.Id }), default);

        Assert.False(result.Succeeded);
        Assert.Contains("تنها کاربرِ دارایِ نقشِ ادمین", result.ErrorMessage);
    }

    [Fact]
    public async Task SetUserRolesCommand_Allows_Removing_Admin_When_Another_Admin_Exists()
    {
        var roles = new FakeRepo<Role>();
        var adminRole = Role.Create(1, "ADMIN", "مدیرِ سیستم", isSystem: true);
        await roles.AddAsync(adminRole);   // Id=1
        var otherRole = Role.Create(1, "CASHIER", "صندوق‌دار");
        await roles.AddAsync(otherRole);   // Id=2

        var userRoles = new FakeRepo<UserRole>();
        await userRoles.AddAsync(UserRole.Create(userId: 1, roleId: adminRole.Id));
        await userRoles.AddAsync(UserRole.Create(userId: 2, roleId: adminRole.Id));   // ادمینِ دیگر هم هست

        var handler = new SetUserRolesCommandHandler(userRoles, roles, new FakeUow());

        var result = await handler.Handle(new SetUserRolesCommand(1, new[] { otherRole.Id }), default);

        Assert.True(result.Succeeded);
    }

    // ── U-SEC-2: ValidationBehavior دیگر throw نمی‌کند؛ Result.Failure برمی‌گرداند ──────────────────
    private record SampleCommand(int Value) : IRequest<Result<int>>;
    private class SampleCommandValidator : AbstractValidator<SampleCommand>
    {
        public SampleCommandValidator() => RuleFor(x => x.Value).GreaterThanOrEqualTo(0).WithMessage("مقدار نمی‌تواند منفی باشد.");
    }

    [Fact]
    public async Task ValidationBehavior_Returns_Failure_Instead_Of_Throwing()
    {
        var behavior = new ValidationBehavior<SampleCommand, Result<int>>(new[] { new SampleCommandValidator() });

        var result = await behavior.Handle(new SampleCommand(-5), () => Task.FromResult(Result<int>.Success(1)), default);

        Assert.False(result.Succeeded);
        Assert.Contains("منفی", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidationBehavior_Proceeds_When_Valid()
    {
        var behavior = new ValidationBehavior<SampleCommand, Result<int>>(new[] { new SampleCommandValidator() });

        var result = await behavior.Handle(new SampleCommand(5), () => Task.FromResult(Result<int>.Success(42)), default);

        Assert.True(result.Succeeded);
        Assert.Equal(42, result.Value);
    }
}
