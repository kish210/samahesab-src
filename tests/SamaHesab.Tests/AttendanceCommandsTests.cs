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

/// <summary>ATT-C1-2 — فرمان‌های ثبتِ تردد/مرخصی (Upsert/Batch/Request/Decide).</summary>
public class AttendanceCommandsTests
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
        public int? UserId => 7; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "admin"; public string? FullName => "ادمین"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static FakeRepo<Employee> WithEmployee(int id = 1)
    {
        var repo = new FakeRepo<Employee>();
        var e = Employee.Create(1, 1, "E1", "001", "علی", "احمدی", "1404/01/01", 20_000_000m);
        repo.AddAsync(e).Wait();   // Id=1
        return repo;
    }

    [Fact]
    public async Task Upsert_Creates_Then_Updates_Same_Record()
    {
        var emps = WithEmployee(); var recs = new FakeRepo<AttendanceRecord>(); var uow = new FakeUow();
        var h = new UpsertAttendanceCommandHandler(emps, recs, uow, new FakeUser());

        var r1 = await h.Handle(new UpsertAttendanceCommand(1, "1404/01/10",
            new TimeOnly(8, 0), new TimeOnly(17, 0)), default);
        Assert.True(r1.Succeeded);
        Assert.Single(recs.Items);
        Assert.Equal(8m, recs.Items[0].WorkHours);          // سقفِ ۸ ساعت
        Assert.Equal(1m, recs.Items[0].OvertimeHours);      // مازاد = اضافه‌کاری

        var r2 = await h.Handle(new UpsertAttendanceCommand(1, "1404/01/10", Status: "غایب"), default);
        Assert.True(r2.Succeeded);
        Assert.Single(recs.Items);                          // همان رکورد به‌روز شد
        Assert.Equal("غایب", recs.Items[0].Status);
    }

    [Fact]
    public async Task Batch_Marks_Multiple_Employees_Absent()
    {
        var emps = new FakeRepo<Employee>();
        foreach (var nc in new[] { "1", "2", "3" })
            emps.AddAsync(Employee.Create(1, 1, "E" + nc, nc, "ن", nc, "1404/01/01", 10_000_000m)).Wait();
        var recs = new FakeRepo<AttendanceRecord>();
        var h = new MarkBatchAttendanceCommandHandler(emps, recs, new FakeUow(), new FakeUser());

        var res = await h.Handle(new MarkBatchAttendanceCommand(new[] { 1, 2, 3 }, "1404/01/12", "غایب"), default);

        Assert.True(res.Succeeded);
        Assert.Equal(3, res.Value);
        Assert.Equal(3, recs.Items.Count);
        Assert.All(recs.Items, r => Assert.Equal("غایب", r.Status));
    }

    [Fact]
    public async Task RequestLeave_Within_Balance_Succeeds()
    {
        var emps = WithEmployee(); var leaves = new FakeRepo<LeaveRequest>();
        var h = new RequestLeaveCommandHandler(emps, leaves, new FakeUow(), new FakeUser());

        var res = await h.Handle(new RequestLeaveCommand(1, LeaveRequest.TypeAnnual,
            "1404/06/01", "1404/06/02", Days: 2), default);

        Assert.True(res.Succeeded);
        Assert.Single(leaves.Items);
        Assert.Equal(LeaveRequest.StatusPending, leaves.Items[0].Status);
    }

    [Fact]
    public async Task RequestLeave_Over_Balance_Fails()
    {
        var emps = WithEmployee(); var leaves = new FakeRepo<LeaveRequest>();
        var h = new RequestLeaveCommandHandler(emps, leaves, new FakeUow(), new FakeUser());

        // فروردین (ماه ۱): استحقاقِ تجمیعی ≈ ۲.۱۷ روز؛ درخواستِ ۱۰ روز باید رد شود.
        var res = await h.Handle(new RequestLeaveCommand(1, LeaveRequest.TypeAnnual,
            "1404/01/01", "1404/01/10", Days: 10), default);

        Assert.False(res.Succeeded);
        Assert.Empty(leaves.Items);
    }

    [Fact]
    public async Task DecideLeave_Approves_And_Blocks_Second_Decision()
    {
        var emps = WithEmployee(); var leaves = new FakeRepo<LeaveRequest>(); var user = new FakeUser();
        await new RequestLeaveCommandHandler(emps, leaves, new FakeUow(), user)
            .Handle(new RequestLeaveCommand(1, LeaveRequest.TypeSick, "1404/02/01", "1404/02/03", Days: 3), default);
        var id = leaves.Items[0].Id;
        var dh = new DecideLeaveCommandHandler(leaves, new FakeUow(), user);

        var ok = await dh.Handle(new DecideLeaveCommand(id, true, "1404/01/31", "تأیید"), default);
        Assert.True(ok.Succeeded);
        Assert.Equal(LeaveRequest.StatusApproved, leaves.Items[0].Status);

        var again = await dh.Handle(new DecideLeaveCommand(id, false, "1404/01/31"), default);
        Assert.False(again.Succeeded);                      // تصمیمِ دوباره ممنوع
    }
}
