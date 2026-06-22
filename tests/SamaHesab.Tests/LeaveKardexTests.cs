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

/// <summary>ATTP-C1-4 — کاردکسِ مرخصی: فهرست + مصرفِ تجمعی + ماندهٔ استحقاقی.</summary>
public class LeaveKardexTests
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

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static LeaveRequest Approved(int emp, string type, string start, string end, decimal days)
    {
        var l = LeaveRequest.Create(1, emp, type, start, end, days);
        l.Approve(9, "1404/01/01");
        return l;
    }

    [Fact]
    public async Task Kardex_Lists_Running_Used_And_Remaining_Annual()
    {
        var emps = new FakeRepo<Employee>();
        emps.AddAsync(Employee.Create(1, 1, "E1", "001", "علی", "احمدی", "1404/01/01", 10_000_000m)).Wait(); // Id=1
        var leaves = new FakeRepo<LeaveRequest>();
        leaves.AddAsync(Approved(1, LeaveRequest.TypeAnnual, "1404/02/01", "1404/02/03", 3)).Wait();
        leaves.AddAsync(Approved(1, LeaveRequest.TypeAnnual, "1404/05/10", "1404/05/12", 2)).Wait();
        leaves.AddAsync(Approved(1, LeaveRequest.TypeSick, "1404/06/01", "1404/06/02", 2)).Wait();   // استعلاجی — در ماندهٔ سالانه نیست
        leaves.AddAsync(Approved(1, LeaveRequest.TypeAnnual, "1403/12/20", "1403/12/22", 5)).Wait();  // سالِ دیگر

        var dto = await new GetLeaveKardexQueryHandler(emps, leaves, new FakeUser())
            .Handle(new GetLeaveKardexQuery(1, "1404"), default);

        Assert.Equal("علی احمدی", dto.EmployeeName);
        Assert.Equal(3, dto.Rows.Count);                       // فقط سالِ ۱۴۰۴
        Assert.Equal(5m, dto.UsedDays);                        // ۳+۲ استحقاقی (استعلاجی حساب نمی‌شود)
        Assert.Equal(26m, dto.EntitlementDays);                // استحقاقِ سالانه
        Assert.Equal(21m, dto.RemainingDays);                  // ۲۶−۵
        // مصرفِ تجمعیِ ردیفِ دومِ استحقاقی = ۵.
        var secondAnnual = dto.Rows.Last(r => r.LeaveType == LeaveRequest.TypeAnnual);
        Assert.Equal(5m, secondAnnual.RunningUsedDays);
    }
}
