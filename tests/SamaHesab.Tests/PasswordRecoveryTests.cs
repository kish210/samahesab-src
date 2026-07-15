using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Security.Commands;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-SEC-RECOVERY — بازیابیِ رمزِ فراموش‌شده با کدِ ساخته‌شده در ویزاردِ راه‌اندازیِ اولیه
/// (بدونِ ایمیل/پیامک؛ این برنامه آفلاین است). درخواستِ صریحِ کاربر @2026-07-15.</summary>
public class PasswordRecoveryTests
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

    private static User NewUser(int companyId = 1, string username = "admin") =>
        User.Create(companyId, null, username, "x", "y", "مدیرِ سیستم");

    [Fact]
    public async Task SetRecoveryCode_Stores_Hash_Not_Raw_Code()
    {
        var users = new FakeRepo<User>();
        await users.AddAsync(NewUser());
        var handler = new SetRecoveryCodeCommandHandler(users, new FakeUow());

        var res = await handler.Handle(new SetRecoveryCodeCommand(1, "ABCD-EFGH-2345-6789"), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var user = users.Items.Single();
        Assert.True(user.HasRecoveryCode);
        Assert.NotEqual("ABCD-EFGH-2345-6789", user.RecoveryCodeHash);
    }

    [Fact]
    public async Task SetRecoveryCode_Rejects_Too_Short_Code()
    {
        var users = new FakeRepo<User>();
        await users.AddAsync(NewUser());
        var handler = new SetRecoveryCodeCommandHandler(users, new FakeUow());

        var res = await handler.Handle(new SetRecoveryCodeCommand(1, "short"), default);

        Assert.False(res.Succeeded);
        Assert.False(users.Items.Single().HasRecoveryCode);
    }

    [Fact]
    public async Task ResetPassword_With_Correct_Recovery_Code_Sets_New_Password_And_Unlocks()
    {
        var users = new FakeRepo<User>();
        var user = NewUser();
        await users.AddAsync(user);
        var setHandler = new SetRecoveryCodeCommandHandler(users, new FakeUow());
        await setHandler.Handle(new SetRecoveryCodeCommand(user.Id, "ABCD-EFGH-2345-6789"), default);
        user.RecordFailedAttempt(); user.RecordFailedAttempt(); user.RecordFailedAttempt();
        user.RecordFailedAttempt(); user.RecordFailedAttempt();   // ۵ باره → قفل
        Assert.True(user.IsLocked);

        var resetHandler = new ResetPasswordWithRecoveryCodeCommandHandler(users, new FakeUow());
        var res = await resetHandler.Handle(
            new ResetPasswordWithRecoveryCodeCommand(1, "admin", "ABCD-EFGH-2345-6789", "NewPass123"), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        Assert.False(user.IsLocked);
        Assert.False(user.MustChangePass);
    }

    [Fact]
    public async Task ResetPassword_With_Wrong_Recovery_Code_Fails_With_Generic_Message()
    {
        var users = new FakeRepo<User>();
        var user = NewUser();
        await users.AddAsync(user);
        var setHandler = new SetRecoveryCodeCommandHandler(users, new FakeUow());
        await setHandler.Handle(new SetRecoveryCodeCommand(user.Id, "ABCD-EFGH-2345-6789"), default);

        var resetHandler = new ResetPasswordWithRecoveryCodeCommandHandler(users, new FakeUow());
        var res = await resetHandler.Handle(
            new ResetPasswordWithRecoveryCodeCommand(1, "admin", "WRONG-CODE-0000-0000", "NewPass123"), default);

        Assert.False(res.Succeeded);
        Assert.Contains("نامِ کاربری یا کدِ بازیابی", res.ErrorMessage);
    }

    [Fact]
    public async Task ResetPassword_Fails_When_No_Recovery_Code_Was_Ever_Set()
    {
        var users = new FakeRepo<User>();
        await users.AddAsync(NewUser());
        var resetHandler = new ResetPasswordWithRecoveryCodeCommandHandler(users, new FakeUow());

        var res = await resetHandler.Handle(
            new ResetPasswordWithRecoveryCodeCommand(1, "admin", "ANY-CODE-AT-ALL", "NewPass123"), default);

        Assert.False(res.Succeeded);
    }

    [Fact]
    public async Task ResetPassword_Rejects_Weak_New_Password_Even_With_Correct_Code()
    {
        var users = new FakeRepo<User>();
        var user = NewUser();
        await users.AddAsync(user);
        var setHandler = new SetRecoveryCodeCommandHandler(users, new FakeUow());
        await setHandler.Handle(new SetRecoveryCodeCommand(user.Id, "ABCD-EFGH-2345-6789"), default);

        var resetHandler = new ResetPasswordWithRecoveryCodeCommandHandler(users, new FakeUow());
        var res = await resetHandler.Handle(
            new ResetPasswordWithRecoveryCodeCommand(1, "admin", "ABCD-EFGH-2345-6789", "weak"), default);

        Assert.False(res.Succeeded);
    }
}
