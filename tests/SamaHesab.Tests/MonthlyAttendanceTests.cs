using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.HRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Entities.Tourism;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ATT-C1-3 — تجمیعِ ماهانهٔ تردد از DB + پلِ تردد→حقوق.</summary>
public class MonthlyAttendanceTests
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
    public void IsFriday_Detects_Shamsi_Friday()
    {
        // ۱۴۰۴/۰۱/۰۱ و ۱۴۰۴/۰۱/۰۸ جمعه‌اند؛ ۱۴۰۴/۰۱/۰۲ شنبه.
        Assert.True(MonthlyAttendanceBuilder.IsFriday("1404/01/01"));
        Assert.True(MonthlyAttendanceBuilder.IsFriday("1404/01/08"));
        Assert.False(MonthlyAttendanceBuilder.IsFriday("1404/01/02"));
    }

    [Fact]
    public async Task Aggregate_Counts_Present_Absent_And_Overtime()
    {
        var emps = new FakeRepo<Employee>();
        emps.AddAsync(Employee.Create(1, 1, "E1", "001", "ع", "ا", "1404/01/01", 30_000_000m)).Wait(); // Id=1
        var recs = new FakeRepo<AttendanceRecord>();
        // دو روزِ حضورِ ۹ساعته (۱ ساعت اضافه‌کاری هرکدام) + یک روز غیبت.
        var d1 = AttendanceRecord.Create(1, "1404/01/02"); d1.SetCheckIn(new TimeOnly(8, 0)); d1.SetCheckOut(new TimeOnly(17, 0));
        var d2 = AttendanceRecord.Create(1, "1404/01/03"); d2.SetCheckIn(new TimeOnly(8, 0)); d2.SetCheckOut(new TimeOnly(17, 0));
        var d3 = AttendanceRecord.Create(1, "1404/01/04"); d3.SetAbsent();
        recs.AddAsync(d1).Wait(); recs.AddAsync(d2).Wait(); recs.AddAsync(d3).Wait();

        var dto = await new GetMonthlyAttendanceQueryHandler(emps, recs, new FakeRepo<Holiday>(), new FakeUser())
            .Handle(new GetMonthlyAttendanceQuery(1, "1404", 1), default);

        Assert.Equal(2, dto.Summary.PresentDays);
        Assert.Equal(1, dto.Summary.AbsentDays);
        Assert.True(dto.Summary.OvertimeHours >= 1);   // مازادِ مؤظف ثبت شد
    }

    [Fact]
    public async Task Payroll_UseAttendance_Deducts_Absence()
    {
        var emps = new FakeRepo<Employee>();
        emps.AddAsync(Employee.Create(1, 1, "E1", "001", "ع", "ا", "1404/01/01", 30_000_000m)).Wait(); // Id=1
        var recs = new FakeRepo<AttendanceRecord>();
        var d = AttendanceRecord.Create(1, "1404/01/04"); d.SetAbsent();
        recs.AddAsync(d).Wait();
        var slips = new FakeRepo<SalarySlip>(); var sets = new FakeRepo<PayrollSetting>();
        var hols = new FakeRepo<Holiday>(); var uow = new FakeUow(); var user = new FakeUser();
        var h = new RunMonthlyPayrollCommandHandler(emps, slips, sets, recs, hols, new FakeRepo<SalesCommissionEntry>(), new FakeRepo<Party>(), uow, user);

        var withAtt = await h.Handle(new RunMonthlyPayrollCommand("1404", 1, UseAttendance: true), default);

        Assert.True(withAtt.Succeeded);
        var slip = slips.Items.Single();
        Assert.True(slip.OtherDeductions > 0);          // کسرِ غیبت اعمال شد
        Assert.Equal(slip.GrossSalary - slip.InsuranceDeduct - slip.TaxDeduct - slip.OtherDeductions, slip.NetSalary);
    }
}
