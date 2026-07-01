using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Tourism.Application.Itinerary;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>MOD-TIT-BILL — تأییدِ مهمانِ برنامهٔ اقامتی = خرید: سندِ متوازن (نسیه→دریافتنی) +
/// رکوردِ TourismSale (برای گزارش) صادر می‌شود؛ تأییدِ دوباره سند دوبل نمی‌زند.</summary>
public class ItineraryBillingTests
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
        public Voucher? Saved; private int _seq;
        public Task AddAsync(Voucher e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Saved = e; return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Voucher> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Voucher?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Saved);
        public Task<List<Voucher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<List<Voucher>> FindAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<Voucher?> FindSingleAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult<Voucher?>(null);
        public Task<bool> AnyAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CountAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(0);
        public void Update(Voucher e) { } public void Remove(Voucher e) { } public void RemoveRange(IEnumerable<Voucher> es) { }
        public Task<List<Voucher>> GetByDateRangeAsync(int c, int f, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<List<Voucher>> GetByDateRangeWithItemsAsync(int c, string from, string to, CancellationToken ct = default) => Task.FromResult(new List<Voucher>());
        public Task<Voucher?> GetWithItemsAsync(int id, CancellationToken ct = default) => Task.FromResult(Saved);
        public Task<string> GetNextNumberAsync(int c, CancellationToken ct = default) => Task.FromResult("2001");
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public IRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeCalendar : IPersianCalendarService
    {
        public string ToPersianDate(DateTime d, string f = "yyyy/MM/dd") => "1404/06/15";
        public DateTime ToGregorianDate(string p) => new(2025, 9, 6);
        public string GetCurrentPersianDate() => "1404/06/15";
        public string GetCurrentPersianDateTime() => "1404/06/15 12:00";
        public string GetPersianMonthName(int m) => ""; public int GetPersianYear(DateTime d) => 1404;
        public int GetPersianMonth(DateTime d) => 6; public int GetPersianDay(DateTime d) => 15;
        public string FormatCurrency(decimal a, bool t = false) => a.ToString("N0"); public string NumberToWords(decimal n) => "";
    }

    private const int Cash = 101, Receivable = 102, Revenue = 601, Discount = 602, Cogs = 701, Deposit = 150;

    private static (SubmitGuestItineraryCommandHandler H, FakeVoucherRepo V, FakeRepo<TourismSale> S,
        FakeRepo<GuestItinerary> I, GuestItinerary It) Build()
    {
        var settings = new FakeRepo<TourismSetting>();
        var set = TourismSetting.Create(1);
        set.Update(Cash, Receivable, Revenue, Cogs, Deposit, Discount, null, null, null, null, true, 0, true, true);
        settings.AddAsync(set).Wait();

        var products = new FakeRepo<TourismProduct>();
        products.AddAsync(TourismProduct.Create(1, "گشت A", supplierPartyId: 11, purchasePrice: 100, defaultSalePrice: 150)).Wait(); // Id=1
        products.AddAsync(TourismProduct.Create(1, "گشت B", supplierPartyId: 12, purchasePrice: 200, defaultSalePrice: 300)).Wait(); // Id=2

        var fys = new FakeRepo<FiscalYear>();
        fys.AddAsync(FiscalYear.Create(1, "۱۴۰۴", "1404/01/01", "1404/12/29")).Wait();

        var itins = new FakeRepo<GuestItinerary>();
        var it = GuestItinerary.Create(1, "علی مهمان", 2, "1404/06/10", guestPartyId: 77, branchId: 1, salespersonPartyId: 5);
        itins.AddAsync(it).Wait();   // Id=1

        var stopsRepo = new FakeRepo<ItineraryStop>();
        void AddStop(int prodId, int day, int sort, decimal sale, decimal cost)
        {
            var st = ItineraryStop.Create(prodId, sessionId: 1, dayNumber: day, sortOrder: sort, startMinute: 540, endMinute: 600, salePrice: sale, cost: cost);
            typeof(ItineraryStop).GetProperty("ItineraryId")!.SetValue(st, it.Id);
            stopsRepo.AddAsync(st).Wait();
        }
        AddStop(1, 1, 1, 150, 100);
        AddStop(2, 2, 1, 300, 200);

        var vouchers = new FakeVoucherRepo();
        var sales = new FakeRepo<TourismSale>();
        var h = new SubmitGuestItineraryCommandHandler(itins, stopsRepo, settings, products, sales,
            vouchers, fys, new FakeCalendar(), new FakeUow());
        return (h, vouchers, sales, itins, it);
    }

    [Fact]
    public async Task Guest_Confirm_Issues_Balanced_Receivable_Voucher_And_Sale()
    {
        var (h, vouchers, sales, _, it) = Build();

        var res = await h.Handle(new SubmitGuestItineraryCommand(it.Token, new List<int>(), Confirm: true), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        // نسیه: کلِ فروش (150+300=450) بدهکارِ دریافتنی = «درخواستِ پول از مهمان»
        Assert.Equal(450m, v.Items.Where(i => i.AccountId == Receivable).Sum(i => i.Debit));
        Assert.Equal(450m, v.Items.Where(i => i.AccountId == Revenue).Sum(i => i.Credit));
        Assert.Equal(300m, v.Items.Where(i => i.AccountId == Cogs).Sum(i => i.Debit));       // COGS = 100+200
        Assert.Equal(300m, v.Items.Where(i => i.AccountId == Deposit).Sum(i => i.Credit));    // ودیعه

        var sale = sales.Items.Single();
        Assert.Equal(450m, sale.TotalSale);
        Assert.Equal("نسیه", sale.PaymentMethod);
        Assert.Equal(sale.Id, it.SaleId);   // itinerary به سندِ فروش لینک شد
        Assert.True(it.IsBilled);
        Assert.Equal(ItineraryStatus.Confirmed, it.Status);
    }

    [Fact]
    public async Task Second_Confirm_Does_Not_Double_Bill()
    {
        var (h, vouchers, sales, _, it) = Build();
        await h.Handle(new SubmitGuestItineraryCommand(it.Token, new List<int>(), Confirm: true), default);
        var firstVoucher = vouchers.Saved;

        var res2 = await h.Handle(new SubmitGuestItineraryCommand(it.Token, new List<int>(), Confirm: true), default);

        Assert.False(res2.Succeeded);                 // قبلاً تأیید شده
        Assert.Single(sales.Items);                    // فقط یک سند فروش
        Assert.Same(firstVoucher, vouchers.Saved);     // سندِ دوم ساخته نشد
    }
}
