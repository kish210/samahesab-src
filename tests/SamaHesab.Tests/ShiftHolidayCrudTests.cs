using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.HRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ATTP-C1-1/2 — CRUDِ شیفت و تقویمِ تعطیلات.</summary>
public class ShiftHolidayCrudTests
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
    public async Task Shift_Create_Edit_List_Deactivate()
    {
        var repo = new FakeRepo<Shift>(); var uow = new FakeUow(); var user = new FakeUser();
        var save = new SaveShiftCommandHandler(repo, uow, user);

        // ساخت (با ارقامِ فارسیِ زمان)
        var c = await save.Handle(new SaveShiftCommand(0, "اداری", "۰۸:۰۰", "۱۶:۰۰", BreakMinutes: 30), default);
        Assert.True(c.Succeeded, c.ErrorMessage);
        Assert.Equal(new TimeOnly(8, 0), repo.Items[0].StartTime);
        Assert.Equal(30, repo.Items[0].BreakMinutes);

        // ویرایش
        await save.Handle(new SaveShiftCommand(c.Value, "اداری", "08:30", "16:30", BreakMinutes: 45), default);
        Assert.Equal(new TimeOnly(8, 30), repo.Items[0].StartTime);
        Assert.Equal(45, repo.Items[0].BreakMinutes);

        var list = await new GetShiftsQueryHandler(repo, user).Handle(new GetShiftsQuery(), default);
        Assert.Single(list);
        Assert.Equal("08:30", list[0].Start);

        await new DeleteShiftCommandHandler(repo, uow, user).Handle(new DeleteShiftCommand(c.Value), default);
        Assert.False(repo.Items[0].IsActive);   // حذفِ نرم
        Assert.Empty(await new GetShiftsQueryHandler(repo, user).Handle(new GetShiftsQuery(ActiveOnly: true), default));
    }

    [Fact]
    public async Task Shift_Invalid_Time_Fails()
    {
        var res = await new SaveShiftCommandHandler(new FakeRepo<Shift>(), new FakeUow(), new FakeUser())
            .Handle(new SaveShiftCommand(0, "بد", "25:99", "16:00"), default);
        Assert.False(res.Succeeded);
    }

    [Fact]
    public async Task Holiday_Create_Blocks_Duplicate_And_Filters_By_Year()
    {
        var repo = new FakeRepo<Holiday>(); var uow = new FakeUow(); var user = new FakeUser();
        var save = new SaveHolidayCommandHandler(repo, uow, user);

        Assert.True((await save.Handle(new SaveHolidayCommand(0, "1404/01/01", "نوروز"), default)).Succeeded);
        Assert.True((await save.Handle(new SaveHolidayCommand(0, "1403/12/29", "پایانِ سال"), default)).Succeeded);
        // تکراری بر همان تاریخ
        Assert.False((await save.Handle(new SaveHolidayCommand(0, "1404/01/01", "تکراری"), default)).Succeeded);

        var y1404 = await new GetHolidaysQueryHandler(repo, user).Handle(new GetHolidaysQuery("1404"), default);
        Assert.Single(y1404);
        Assert.Equal("نوروز", y1404[0].Title);

        var firstId = repo.Items[0].Id;
        await new DeleteHolidayCommandHandler(repo, uow, user).Handle(new DeleteHolidayCommand(firstId), default);
        Assert.Single(repo.Items);   // یکی حذف شد
    }
}
