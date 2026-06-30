using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>P1 — صورت‌حسابِ مشتری باید با ماندهٔ مرجعِ `Party.Balance` آشتی شود
/// (پیش‌پرداخت/ماندهٔ اولیه/تعدیل‌های غیرفاکتوری در «ماندهٔ اول دوره» و مانده در پایان = ماندهٔ مرجع).</summary>
public class CustomerStatementReconcileTests
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

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static SalesInvoice Inv(string number, string date, decimal grand, decimal paid)
    {
        var inv = SalesInvoice.Create(1, 1, 1, number, date, customerId: 1, warehouseId: 1, dueDate: date);
        typeof(SalesInvoice).GetProperty("GrandTotal")!.SetValue(inv, grand);
        typeof(SalesInvoice).GetProperty("PaidAmount")!.SetValue(inv, paid);
        return inv;
    }

    private static (FakeRepo<SalesInvoice> sales, FakeRepo<Party> parties) Setup(decimal partyBalance, params SalesInvoice[] invoices)
    {
        var parties = new FakeRepo<Party>();
        var cust = Party.Create(1, "C1", "حقیقی", "علی", "خریدار"); cust.MarkCustomer();
        cust.UpdateBalance(partyBalance);
        parties.AddAsync(cust).Wait();   // Id=1
        var sales = new FakeRepo<SalesInvoice>();
        foreach (var i in invoices) sales.AddAsync(i).Wait();
        return (sales, parties);
    }

    [Fact]
    public async Task ClosingBalance_Reconciles_To_PartyBalance_With_Opening_Row()
    {
        // ماندهٔ مرجع 8M ولی فعالیتِ فاکتورها فقط 5M است → 3M غیرفاکتوری باید در «مانده اول دوره» بیاید.
        var (sales, parties) = Setup(8_000_000m, Inv("1001", "1404/01/10", 5_000_000m, 0m));
        var h = new GetCustomerStatementQueryHandler(sales, parties, new FakeUser());

        var res = await h.Handle(new GetCustomerStatementQuery(1), default);

        Assert.True(res.Succeeded);
        var st = res.Value!;
        Assert.Equal(8_000_000m, st.ClosingBalance);                                           // آشتی با ماندهٔ مرجع
        Assert.Contains(st.Rows, r => r.DocType == "مانده اول دوره" && r.Debit == 3_000_000m);  // باقی‌ماندهٔ غیرفاکتوری
    }

    [Fact]
    public async Task No_Opening_Row_When_PartyBalance_Equals_Invoice_Activity()
    {
        var (sales, parties) = Setup(5_000_000m, Inv("1001", "1404/01/10", 5_000_000m, 0m));
        var h = new GetCustomerStatementQueryHandler(sales, parties, new FakeUser());

        var res = await h.Handle(new GetCustomerStatementQuery(1), default);

        var st = res.Value!;
        Assert.Equal(5_000_000m, st.ClosingBalance);
        Assert.DoesNotContain(st.Rows, r => r.DocType == "مانده اول دوره");
    }

    [Fact]
    public async Task Standalone_Receipt_Reflected_Via_PaidAmount_And_Balance_Matches()
    {
        // فاکتور 5M که دریافتِ مستقلِ خزانه 2M را FIFO خورده (PaidAmount=2M)؛ ماندهٔ مرجع = 3M.
        var (sales, parties) = Setup(3_000_000m, Inv("1001", "1404/01/10", 5_000_000m, 2_000_000m));
        var h = new GetCustomerStatementQueryHandler(sales, parties, new FakeUser());

        var res = await h.Handle(new GetCustomerStatementQuery(1), default);

        var st = res.Value!;
        Assert.Equal(3_000_000m, st.ClosingBalance);                       // 5M بدهی − 2M دریافت
        Assert.DoesNotContain(st.Rows, r => r.DocType == "مانده اول دوره"); // همه‌چیز فاکتوری است
        Assert.Contains(st.Rows, r => r.DocType == "دریافت" && r.Credit == 2_000_000m);
    }
}
