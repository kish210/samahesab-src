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

/// <summary>ATT-C1-4 — تجزیهٔ فایلِ تردد + ایمپورت + گزارشِ کارکردِ ماهانه.</summary>
public class AttendanceImportTests
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
    public void Parser_Skips_Header_And_Normalizes_Persian_Digits()
    {
        var csv = "کد,تاریخ,ورود,خروج,وضعیت\nE1,۱۴۰۴/۰۱/۰۲,۰۸:۰۰,۱۷:۰۰,حاضر\nE2,1404/01/02,,,غایب";
        var rows = AttendanceImportParser.Parse(csv);
        Assert.Equal(2, rows.Count);
        Assert.Equal("1404/01/02", rows[0].Date);          // رقمِ فارسی نرمال شد
        Assert.Equal(new TimeOnly(8, 0), rows[0].CheckIn);
        Assert.Equal("غایب", rows[1].Status);
    }

    [Fact]
    public async Task Import_Applies_Rows_And_Reports_Unknown_Code()
    {
        var emps = new FakeRepo<Employee>();
        emps.AddAsync(Employee.Create(1, 1, "E1", "001", "ع", "ا", "1404/01/01", 20_000_000m)).Wait();
        var recs = new FakeRepo<AttendanceRecord>();
        var csv = "E1,1404/01/02,08:00,17:00,حاضر\nE9,1404/01/02,08:00,16:00,حاضر";  // E9 ناشناخته

        var res = await new ImportAttendanceCommandHandler(emps, recs, new FakeUow(), new FakeUser())
            .Handle(new ImportAttendanceCommand(csv), default);

        Assert.True(res.Succeeded);
        Assert.Equal(1, res.Value!.Imported);
        Assert.Equal(1, res.Value.Skipped);
        Assert.Contains(res.Value.Errors, e => e.Contains("E9"));
        Assert.Single(recs.Items);
        Assert.Equal(8m, recs.Items[0].WorkHours);
    }

    [Fact]
    public async Task Report_Aggregates_Per_Employee()
    {
        var emps = new FakeRepo<Employee>();
        emps.AddAsync(Employee.Create(1, 1, "E1", "001", "ع", "احمدی", "1404/01/01", 20_000_000m)).Wait(); // Id=1
        var recs = new FakeRepo<AttendanceRecord>();
        var d1 = AttendanceRecord.Create(1, "1404/01/02"); d1.SetCheckIn(new TimeOnly(8, 0)); d1.SetCheckOut(new TimeOnly(16, 0));
        var d2 = AttendanceRecord.Create(1, "1404/01/03"); d2.SetAbsent();
        recs.AddAsync(d1).Wait(); recs.AddAsync(d2).Wait();

        var rep = await new GetAttendanceReportQueryHandler(emps, recs, new FakeRepo<Holiday>(), new FakeUser())
            .Handle(new GetAttendanceReportQuery("1404", 1), default);

        var row = Assert.Single(rep);
        Assert.Equal("ع احمدی", row.EmployeeName);
        Assert.Equal(1, row.PresentDays);
        Assert.Equal(1, row.AbsentDays);
    }
}
