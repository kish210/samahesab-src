using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Security.Queries;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>T20 — کوئریِ مشاهدهٔ لاگِ حسابرسی (`GetAuditLogQuery`).</summary>
public class AuditLogQueryTests
{
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

    private sealed class FakeCalendar : IPersianCalendarService
    {
        public string ToPersianDate(DateTime date, string format = "yyyy/MM/dd") => "1403/03/15";
        public DateTime ToGregorianDate(string persianDate) => DateTime.Today;
        public string GetCurrentPersianDate() => "1403/03/15";
        public string GetCurrentPersianDateTime() => "1403/03/15 12:00";
        public string GetPersianMonthName(int month) => "خرداد";
        public int GetPersianYear(DateTime date) => 1403;
        public int GetPersianMonth(DateTime date) => 3;
        public int GetPersianDay(DateTime date) => 15;
        public string FormatCurrency(decimal amount, bool showToman = false) => amount.ToString("N0");
        public string NumberToWords(decimal number) => "";
    }

    private static AuditLog Log(string action, DateTime when)
    {
        var a = AuditLog.Create(action, 1, "admin", "Inv", null, "{}");
        typeof(AuditLog).GetProperty("CreatedAt")!.SetValue(a, when);
        return a;
    }

    private static GetAuditLogQueryHandler NewSut(out FakeAuditRepo repo)
    {
        repo = new FakeAuditRepo();
        return new GetAuditLogQueryHandler(repo, new FakeCalendar());
    }

    [Fact]
    public async Task Returns_Recent_First_And_Excludes_Old()
    {
        var h = NewSut(out var repo);
        await repo.AddAsync(Log("قدیمی", DateTime.Now.AddDays(-100)));   // خارج از بازهٔ ۳۰ روز
        await repo.AddAsync(Log("تعدیلِ موجودی", DateTime.Now.AddDays(-1)));
        await repo.AddAsync(Log("انتقال بین انبار", DateTime.Now.AddHours(-1)));

        var rows = await h.Handle(new GetAuditLogQuery(DaysBack: 30), default);

        Assert.Equal(2, rows.Count);                       // قدیمی حذف شد
        Assert.Equal("انتقال بین انبار", rows[0].Action);  // جدیدترین اول
    }

    [Fact]
    public async Task Action_Filter_Limits_Results()
    {
        var h = NewSut(out var repo);
        await repo.AddAsync(Log("تعدیلِ موجودی", DateTime.Now.AddHours(-2)));
        await repo.AddAsync(Log("انتقال بین انبار", DateTime.Now.AddHours(-1)));

        var rows = await h.Handle(new GetAuditLogQuery(Action: "تعدیلِ موجودی"), default);

        Assert.Single(rows);
        Assert.Equal("تعدیلِ موجودی", rows[0].Action);
    }

    [Fact]
    public async Task MaxRows_Caps_Output()
    {
        var h = NewSut(out var repo);
        for (int i = 0; i < 10; i++) await repo.AddAsync(Log("عمل", DateTime.Now.AddMinutes(-i)));

        var rows = await h.Handle(new GetAuditLogQuery(MaxRows: 5), default);

        Assert.Equal(5, rows.Count);
    }
}
