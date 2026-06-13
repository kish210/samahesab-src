using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Purchase.Queries;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کارِ ۶ — صورت‌حساب/ماندهٔ تأمین‌کننده: ماندهٔ غلتان روی فاکتور خرید/پرداخت/مرجوعی.</summary>
public class SupplierStatementTests
{
    private sealed class InMemoryRepo<T> : IRepository<T> where T : BaseEntity
    {
        private readonly List<T> _items = new();
        private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default)
        { typeof(BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); _items.Add(e); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().FirstOrDefault(p));
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Where(p).ToList());
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_items.ToList());
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { _items.AddRange(e); return Task.CompletedTask; }
        public void Update(T e) { }
        public void Remove(T e) => _items.Remove(e);
        public void RemoveRange(IEnumerable<T> e) { foreach (var x in e) _items.Remove(x); }
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1;
        public int? CompanyId => 1;
        public int? BranchId => 1;
        public string? Username => "admin";
        public string? FullName => "Admin";
        public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static PurchaseInvoice Invoice(string date, string number, decimal qty, decimal price,
        decimal paid = 0, string type = "خرید")
    {
        var inv = PurchaseInvoice.Create(1, 1, 1, number, date, supplierId: 1, warehouseId: 2, invoiceType: type);
        inv.AddItem(PurchaseInvoiceItem.Create(0, 1, productId: 9, quantity: qty, unitPrice: price));
        inv.SetShipping(0, 0);
        if (paid > 0) inv.SetPaid(paid);
        return inv;
    }

    private static async Task<(GetSupplierStatementQueryHandler sut, InMemoryRepo<PurchaseInvoice> purchases)> BuildAsync()
    {
        var purchases = new InMemoryRepo<PurchaseInvoice>();
        var suppliers = new InMemoryRepo<Supplier>();
        await suppliers.AddAsync(Supplier.Create(1, "S-001", "حقوقی", companyName: "تأمین‌کنندهٔ آزمون"));
        return (new GetSupplierStatementQueryHandler(purchases, suppliers, new FakeUser()), purchases);
    }

    [Fact]
    public async Task Statement_Invoice_Then_Payment_Tracks_Running_Balance()
    {
        var (sut, purchases) = await BuildAsync();
        await purchases.AddAsync(Invoice("1404/01/10", "PF000001", qty: 2, price: 5000, paid: 4000)); // خرید ۱۰٬۰۰۰، پرداخت ۴٬۰۰۰

        var res = await sut.Handle(new GetSupplierStatementQuery(1), default);

        Assert.True(res.Succeeded);
        var st = res.Value!;
        Assert.Equal(2, st.Rows.Count);                 // یک ردیف فاکتور + یک ردیف پرداخت
        Assert.Equal(10000, st.TotalCredit);            // افزایش بدهی
        Assert.Equal(4000, st.TotalDebit);              // کاهش بدهی
        Assert.Equal(6000, st.ClosingBalance);          // ماندهٔ بدهیِ ما به تأمین‌کننده
        Assert.Equal(6000, st.Rows[^1].Balance);
    }

    [Fact]
    public async Task Statement_Purchase_Return_Reduces_Balance()
    {
        var (sut, purchases) = await BuildAsync();
        await purchases.AddAsync(Invoice("1404/02/01", "PF000002", qty: 1, price: 8000));            // خرید ۸٬۰۰۰
        await purchases.AddAsync(Invoice("1404/02/05", "PR000001", qty: 1, price: 3000, type: "برگشت خرید")); // مرجوعی ۳٬۰۰۰

        var res = await sut.Handle(new GetSupplierStatementQuery(1), default);

        var st = res.Value!;
        Assert.Equal(8000, st.TotalCredit);
        Assert.Equal(3000, st.TotalDebit);
        Assert.Equal(5000, st.ClosingBalance);
    }

    [Fact]
    public async Task Statement_Unknown_Supplier_Fails()
    {
        var (sut, _) = await BuildAsync();
        var res = await sut.Handle(new GetSupplierStatementQuery(999), default);
        Assert.False(res.Succeeded);
    }

    [Fact]
    public async Task Statement_Respects_Date_Range()
    {
        var (sut, purchases) = await BuildAsync();
        await purchases.AddAsync(Invoice("1404/01/10", "PF000001", 1, 1000));
        await purchases.AddAsync(Invoice("1404/03/10", "PF000002", 1, 2000));

        var res = await sut.Handle(new GetSupplierStatementQuery(1, FromDate: "1404/02/01", ToDate: "1404/04/01"), default);

        var st = res.Value!;
        Assert.Single(st.Rows);                 // فقط فاکتور اسفندِ بازه (PF000002)
        Assert.Equal(2000, st.ClosingBalance);
    }
}
