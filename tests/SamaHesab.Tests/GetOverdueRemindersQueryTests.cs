using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Automation;
using SamaHesab.Application.Automation.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>P2 — مونتاژِ یادآور از فاکتورِ معوق + چکِ دریافتیِ نزدیکِ سررسید (با موبایلِ طرف).</summary>
public class GetOverdueRemindersQueryTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default) { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(T e) { } public void Remove(T e) => Items.Remove(e); public void RemoveRange(IEnumerable<T> e) { }
    }

    private sealed class FakeChequeRepo : IChequeRepository
    {
        public readonly List<Cheque> Items = new();
        private int _seq;
        public Task AddAsync(Cheque e, CancellationToken ct = default) { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<List<Cheque>> GetByStatusAsync(int companyId, ChequeStatus s, CancellationToken ct = default) => Task.FromResult(Items.Where(c => c.CompanyId == companyId && c.Status == s).ToList());
        public Task<List<Cheque>> GetDueTodayAsync(int c, CancellationToken ct = default) => Task.FromResult(new List<Cheque>());
        public Task<List<Cheque>> GetOverdueAsync(int c, CancellationToken ct = default) => Task.FromResult(new List<Cheque>());
        public Task<Cheque?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<Cheque>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Cheque>> FindAsync(Expression<Func<Cheque, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Cheque?> FindSingleAsync(Expression<Func<Cheque, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Cheque, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Cheque, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Cheque> e, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(Cheque e) { } public void Remove(Cheque e) { } public void RemoveRange(IEnumerable<Cheque> e) { }
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    /// <summary>تقویمِ ساختگی: bound = «1405/03/20» ثابت (today+7) تا چکِ ≤ این تاریخ یادآوری شود.</summary>
    private sealed class FakeCalendar : IPersianCalendarService
    {
        public string ToPersianDate(DateTime date, string format = "yyyy/MM/dd") => "1405/03/20";
        public DateTime ToGregorianDate(string persianDate) => new(2026, 6, 13);
        public string GetCurrentPersianDate() => "1405/03/13";
        public string GetCurrentPersianDateTime() => "1405/03/13 12:00";
        public string GetPersianMonthName(int m) => ""; public int GetPersianYear(DateTime d) => 1405;
        public int GetPersianMonth(DateTime d) => 3; public int GetPersianDay(DateTime d) => 13;
        public string FormatCurrency(decimal a, bool t = false) => a.ToString("N0"); public string NumberToWords(decimal n) => "";
    }

    [Fact]
    public async Task Builds_Reminders_For_Overdue_Invoice_And_Due_Cheque_With_Mobile()
    {
        var parties = new FakeRepo<Party>();
        var cust = Party.Create(1, "C1", "حقیقی", "علی", "خریدار"); cust.MarkCustomer();
        typeof(Party).GetProperty("Mobile")!.SetValue(cust, "09120000001");
        parties.AddAsync(cust).Wait();   // Id=1

        var sales = new FakeRepo<SalesInvoice>();
        var inv = SalesInvoice.Create(1, 1, 1, "FAC-1", "1405/02/01", customerId: 1, warehouseId: 1, dueDate: "1405/03/01"); // سررسیدگذشته
        typeof(SalesInvoice).GetProperty("Status")!.SetValue(inv, InvoiceStatus.Posted);
        typeof(SalesInvoice).GetProperty("RemainAmount")!.SetValue(inv, 3_000_000m);
        sales.AddAsync(inv).Wait();

        var cheques = new FakeChequeRepo();
        var chq = Cheque.Create(1, 1, ChequeType.Received, "777", "ملت", 5_000_000m, "1405/03/18"); // ≤ bound(1405/03/20)
        chq.SetParty(1, "Customer");
        cheques.AddAsync(chq).Wait();

        var h = new GetOverdueRemindersQueryHandler(sales, parties, cheques, new FakeCalendar(), new FakeUser());
        var res = await h.Handle(new GetOverdueRemindersQuery("1405/03/13", "سما حساب"), default);

        Assert.Equal(2, res.Count);
        Assert.Contains(res, r => r.Kind == ReminderKind.OverdueDebt && r.Amount == 3_000_000m && r.Mobile == "09120000001");
        Assert.Contains(res, r => r.Kind == ReminderKind.ChequeDueSoon && r.Amount == 5_000_000m);
    }

    [Fact]
    public async Task Skips_Not_Overdue_Invoice_And_Far_Cheque()
    {
        var parties = new FakeRepo<Party>();
        var cust = Party.Create(1, "C1", "حقیقی", "س", "خ"); cust.MarkCustomer();
        typeof(Party).GetProperty("Mobile")!.SetValue(cust, "0912");
        parties.AddAsync(cust).Wait();

        var sales = new FakeRepo<SalesInvoice>();
        var inv = SalesInvoice.Create(1, 1, 1, "FAC-2", "1405/03/10", customerId: 1, warehouseId: 1, dueDate: "1405/04/10");   // هنوز سررسید نشده
        typeof(SalesInvoice).GetProperty("Status")!.SetValue(inv, InvoiceStatus.Posted);
        typeof(SalesInvoice).GetProperty("RemainAmount")!.SetValue(inv, 1_000_000m);
        sales.AddAsync(inv).Wait();

        var cheques = new FakeChequeRepo();
        var chq = Cheque.Create(1, 1, ChequeType.Received, "888", "صادرات", 2_000_000m, "1405/06/01"); // دور، > bound
        chq.SetParty(1, "Customer");
        cheques.AddAsync(chq).Wait();

        var h = new GetOverdueRemindersQueryHandler(sales, parties, cheques, new FakeCalendar(), new FakeUser());
        var res = await h.Handle(new GetOverdueRemindersQuery("1405/03/13", "سما حساب"), default);

        Assert.Empty(res);
    }
}
