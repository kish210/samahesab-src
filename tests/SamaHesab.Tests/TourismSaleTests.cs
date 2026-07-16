using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Tourism.Application.Commands;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>TUR-C1-3 — سندِ متوازنِ فروشِ گردشگری: COGS به ودیعهٔ تأمین‌کننده می‌خورد (نه پرداختنی).</summary>
public class TourismSaleTests
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

    private sealed class FakeVoucherRepo : IVoucherRepository
    {
        public Voucher? Saved;
        private int _seq;
        public Task AddAsync(Voucher e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Saved = e; return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Voucher> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Voucher?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Saved);
        public Task<List<Voucher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<List<Voucher>> FindAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<Voucher?> FindSingleAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult<Voucher?>(null);
        public Task<bool> AnyAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CountAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(0);
        public void Update(Voucher e) { }
        public void Remove(Voucher e) { }
        public void RemoveRange(IEnumerable<Voucher> es) { }
        public Task<List<Voucher>> GetByDateRangeAsync(int companyId, int fiscalYearId, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<List<Voucher>> GetByDateRangeWithItemsAsync(int companyId, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<Voucher?> GetWithItemsAsync(int voucherId, CancellationToken ct = default) => Task.FromResult(Saved);
        public Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default) => Task.FromResult("1001");
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

    private const int Cash = 101, Receivable = 102, Revenue = 601, Discount = 602, Cogs = 701, Deposit = 150;

    [Fact]
    public async Task Sale_Posts_Balanced_Voucher_With_Deposit_Drawdown()
    {
        var settings = new FakeRepo<TourismSetting>();
        var set = TourismSetting.Create(1);
        set.Update(Cash, Receivable, Revenue, Cogs, Deposit, Discount, null, null, null, null, true, 0, true, true);
        settings.AddAsync(set).Wait();

        var products = new FakeRepo<TourismProduct>();
        products.AddAsync(TourismProduct.Create(1, "بلیط A", supplierPartyId: 11, purchasePrice: 100, defaultSalePrice: 150)).Wait(); // Id=1
        products.AddAsync(TourismProduct.Create(1, "بلیط B", supplierPartyId: 12, purchasePrice: 200, defaultSalePrice: 300)).Wait(); // Id=2

        var vouchers = new FakeVoucherRepo();
        var sales = new FakeRepo<TourismSale>();
        var handler = new CreateTourismSaleCommandHandler(settings, products, new FakeRepo<CommissionRule>(),
            sales, new FakeRepo<SalesCommissionEntry>(), vouchers, new FakeRepo<FiscalYear>(), new FakeRepo<Party>(),
            new FakeUow(), new FakeUser(), new FakeRepo<PartyLedgerEntry>());

        var cmd = new CreateTourismSaleCommand(1, 1, "1404/06/15", SalespersonPartyId: 5, CustomerPartyId: null,
            PaymentMethod: "نقدی", Lines: new[]
            {
                new TourismSaleLineDto(1, Quantity: 2, UnitSalePrice: 150, DiscountAmount: 0),
                new TourismSaleLineDto(2, Quantity: 1, UnitSalePrice: 300, DiscountAmount: 50),
            });

        var res = await handler.Handle(cmd, default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());                                   // سند متوازن
        Assert.Equal(v.Items.Sum(i => i.Debit), v.Items.Sum(i => i.Credit));

        // COGS = 2*100 + 1*200 = 400 → بستانکارِ ودیعه (نه پرداختنی)
        var depositCredit = v.Items.Where(i => i.AccountId == Deposit).Sum(i => i.Credit);
        Assert.Equal(400m, depositCredit);
        Assert.Equal(400m, v.Items.Where(i => i.AccountId == Cogs).Sum(i => i.Debit));   // COGS بدهکار
        Assert.Equal(600m, v.Items.Where(i => i.AccountId == Revenue).Sum(i => i.Credit)); // درآمد = کلِ فروش
        Assert.Equal(550m, v.Items.Where(i => i.AccountId == Cash).Sum(i => i.Debit));     // نقدِ دریافتی = فروش − تخفیف
        Assert.Equal(50m, v.Items.Where(i => i.AccountId == Discount).Sum(i => i.Debit));  // تخفیف

        var sale = sales.Items.Single();
        Assert.Equal(600m, sale.TotalSale);
        Assert.Equal(400m, sale.TotalCost);
        Assert.Equal(150m, sale.TotalProfit);   // 600 − 50 − 400
    }

    [Fact]
    public async Task Sale_Fails_When_Accounts_Not_Configured()
    {
        var settings = new FakeRepo<TourismSetting>();   // خالی → تنظیمات نیست
        var products = new FakeRepo<TourismProduct>();
        products.AddAsync(TourismProduct.Create(1, "X", 11, 100, 150)).Wait();
        var handler = new CreateTourismSaleCommandHandler(settings, products, new FakeRepo<CommissionRule>(),
            new FakeRepo<TourismSale>(), new FakeRepo<SalesCommissionEntry>(), new FakeVoucherRepo(),
            new FakeRepo<FiscalYear>(), new FakeRepo<Party>(), new FakeUow(), new FakeUser(), new FakeRepo<PartyLedgerEntry>());

        var res = await handler.Handle(new CreateTourismSaleCommand(1, 1, "1404/06/15", 5, null, "نقدی",
            new[] { new TourismSaleLineDto(1, 1, 150) }), default);

        Assert.False(res.Succeeded);
    }

    [Fact]
    public async Task Sale_Requires_Passenger_List_When_Product_Demands()
    {
        var settings = new FakeRepo<TourismSetting>();
        var set = TourismSetting.Create(1);
        set.Update(Cash, Receivable, Revenue, Cogs, Deposit, Discount, null, null, null, null, true, 0, true, true);
        settings.AddAsync(set).Wait();
        var products = new FakeRepo<TourismProduct>();
        products.AddAsync(TourismProduct.Create(1, "گشت دور جزیره", 11, 100, 150, requiresPassengerList: true)).Wait();
        var handler = new CreateTourismSaleCommandHandler(settings, products, new FakeRepo<CommissionRule>(),
            new FakeRepo<TourismSale>(), new FakeRepo<SalesCommissionEntry>(), new FakeVoucherRepo(),
            new FakeRepo<FiscalYear>(), new FakeRepo<Party>(), new FakeUow(), new FakeUser(), new FakeRepo<PartyLedgerEntry>());

        var res = await handler.Handle(new CreateTourismSaleCommand(1, 1, "1404/06/15", 5, null, "نقدی",
            new[] { new TourismSaleLineDto(1, 1, 150) }), default);   // بدونِ مسافر

        Assert.False(res.Succeeded);
        Assert.Contains("مسافر", res.ErrorMessage);
    }

    /// <summary>U-PARTY-BAL (پیگیریِ گردشگری) — فروشِ نسیه باید بدهیِ جدید را به Party.Balanceِ مشتری اضافه کند
    /// (هم‌راستا با فروش/خریدِ هسته)؛ فروشِ نقدی هیچ‌کدام از تست‌های بالا Balance را دست نمی‌زند.</summary>
    [Fact]
    public async Task Credit_Sale_Increases_Customer_Party_Balance()
    {
        var settings = new FakeRepo<TourismSetting>();
        var set = TourismSetting.Create(1);
        set.Update(Cash, Receivable, Revenue, Cogs, Deposit, Discount, null, null, null, null, true, 0, true, true);
        settings.AddAsync(set).Wait();

        var products = new FakeRepo<TourismProduct>();
        products.AddAsync(TourismProduct.Create(1, "بلیط A", supplierPartyId: 11, purchasePrice: 100, defaultSalePrice: 150)).Wait(); // Id=1

        var parties = new FakeRepo<Party>();
        var customer = Party.Create(1, "C-9", "حقیقی", firstName: "رضا", lastName: "مشتری", isCustomer: true);
        customer.UpdateBalance(500);   // مانده‌ی اولیه، برای تأییدِ اینکه Balance جمع می‌شود نه بازنویسی
        parties.AddAsync(customer).Wait();

        var vouchers = new FakeVoucherRepo();
        var sales = new FakeRepo<TourismSale>();
        var handler = new CreateTourismSaleCommandHandler(settings, products, new FakeRepo<CommissionRule>(),
            sales, new FakeRepo<SalesCommissionEntry>(), vouchers, new FakeRepo<FiscalYear>(), parties,
            new FakeUow(), new FakeUser(), new FakeRepo<PartyLedgerEntry>());

        var cmd = new CreateTourismSaleCommand(1, 1, "1404/06/15", SalespersonPartyId: 5, CustomerPartyId: 9,
            PaymentMethod: "نسیه", Lines: new[] { new TourismSaleLineDto(1, Quantity: 1, UnitSalePrice: 150, DiscountAmount: 10) });

        var res = await handler.Handle(cmd, default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        // نسیه: بدهیِ جدید = فروش−تخفیف = 150−10 = 140 باید به Balance اضافه شود (500+140=640)
        Assert.Equal(640m, parties.Items.Single().Balance);
    }
}
