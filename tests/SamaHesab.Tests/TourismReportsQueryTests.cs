using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Tourism.Queries;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Tourism;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>TUR-C2-5 — کوئریِ گزارش‌های گردشگری: چهار جدولِ آماده با تجمیعِ درست از داده‌ی Tur.*.</summary>
public class TourismReportsQueryTests
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

    private static void SetId<T>(T e, int id) where T : class
        => typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, id);

    [Fact]
    public async Task Builds_Four_Tables_With_Correct_Aggregations()
    {
        // طرف‌حساب‌ها: فروشنده ۲۱ + تأمین‌کننده ۱۱.
        var parties = new FakeRepo<Party>();
        foreach (var (pid, name) in new[] { (21, "فروشندهٔ الف"), (11, "تأمین‌کنندهٔ ب") })
        {
            var p = Party.Create(1, "P" + pid, "حقوقی", companyName: name);
            SetId(p, pid); parties.Items.Add(p);
        }

        // محصولات ۳۱/۳۲ از تأمین‌کننده ۱۱.
        var products = new FakeRepo<TourismProduct>();
        var p31 = TourismProduct.Create(1, "تور کیش", 11, 100, 200); SetId(p31, 31); products.Items.Add(p31);
        var p32 = TourismProduct.Create(1, "بلیت پرواز", 11, 300, 500); SetId(p32, 32); products.Items.Add(p32);

        // خطوط: محصول ۳۱ (۲ واحد، فروش ۲۰۰، بها ۱۰۰ → خالص ۴۰۰/بها ۲۰۰/سود ۲۰۰) +
        //        محصول ۳۲ (۱ واحد، فروش ۵۰۰، بها ۳۰۰ → خالص ۵۰۰/بها ۳۰۰/سود ۲۰۰).
        var l1 = TourismSaleLine.Create(31, 11, 2, unitSalePrice: 200, discountAmount: 0, unitCost: 100);
        SetId(l1, 1); typeof(TourismSaleLine).GetProperty("SaleId")!.SetValue(l1, 100);
        var l2 = TourismSaleLine.Create(32, 11, 1, unitSalePrice: 500, discountAmount: 0, unitCost: 300);
        SetId(l2, 2); typeof(TourismSaleLine).GetProperty("SaleId")!.SetValue(l2, 100);

        // یک فروش از فروشندهٔ ۲۱ — با AddLine تا جمع‌های سربرگ (مثلِ دادهٔ واقعیِ command) محاسبه شود.
        var sales = new FakeRepo<TourismSale>();
        var sale = TourismSale.Create(1, 1, "1404/06/10", salespersonPartyId: 21);
        sale.AddLine(l1); sale.AddLine(l2);
        SetId(sale, 100); sales.Items.Add(sale);

        var lines = new FakeRepo<TourismSaleLine>();
        lines.Items.Add(l1); lines.Items.Add(l2);

        // ودیعه: شارژِ ۲۰۰۰ برای تأمین‌کننده ۱۱ → برداشت ۲×۱۰۰+۱×۳۰۰=۵۰۰ → مانده ۱۵۰۰
        var deposits = new FakeRepo<SupplierDeposit>();
        await deposits.AddAsync(SupplierDeposit.Create(1, 11, 2000, "1404/06/01"));

        // پورسانت: دو رکورد در دورهٔ 140406 برای فروشندهٔ ۲۱ (روی خط ۱ و ۲)، جمعِ ۷۰.
        var commissions = new FakeRepo<SalesCommissionEntry>();
        await commissions.AddAsync(SalesCommissionEntry.Create(1, 1, 21, CommissionBasis.PercentOfSale, 400, 10, 40, "140406"));
        await commissions.AddAsync(SalesCommissionEntry.Create(1, 2, 21, CommissionBasis.PercentOfSale, 500, 6, 30, "140406"));
        // پورسانتِ دورهٔ دیگر (نباید در فیلترِ 140406 بیاید)
        await commissions.AddAsync(SalesCommissionEntry.Create(1, 1, 21, CommissionBasis.PercentOfSale, 400, 10, 99, "140405"));

        var handler = new GetTourismReportsQueryHandler(sales, lines, commissions, deposits, products, parties, new FakeUser());

        var dto = await handler.Handle(new GetTourismReportsQuery("140406"), default);

        // هر جدول یک ردیفِ «جمع» در انتها دارد. اعداد با جداکنندهٔ هزارگان («1,500»).

        // ۱) ماندهٔ ودیعه: یک تأمین‌کننده + جمع. شارژ ۲۰۰۰ − برداشت ۵۰۰ = ۱۵۰۰ (ستونِ آخر).
        Assert.Equal(2, dto.SupplierDeposits.Rows.Count);
        var depRow = dto.SupplierDeposits.Rows[0];
        Assert.Equal("تأمین‌کنندهٔ ب", depRow[0]);
        Assert.Equal("1,500", depRow[^1]);

        // ۲) سودِ محصول: دو محصول + جمع = ۳ ردیف؛ جمعِ سود ۴۰۰ (ستونِ سود = اندیس ۴).
        Assert.Equal(3, dto.ProductProfit.Rows.Count);
        var profitTotal = dto.ProductProfit.Rows[^1];
        Assert.Equal("جمع", profitTotal[0]);
        Assert.Equal("400", profitTotal[4]);

        // ۳) پورسانتِ ماهانه: یک فروشنده + جمع؛ جمعِ پورسانت ۷۰ (نه ۱۶۹) چون دورهٔ ۱۴۰۴۰۵ فیلتر شد.
        Assert.Equal(2, dto.MonthlyCommission.Rows.Count);
        var comRow = dto.MonthlyCommission.Rows[0];
        Assert.Equal("فروشندهٔ الف", comRow[0]);
        Assert.Equal("70", comRow[^1]);          // پورسانت = ستونِ آخر

        // ۴) عملکردِ فروشنده: یک فروش + جمع؛ خالص ۹۰۰ (اندیس ۲)، سود ۴۰۰ (اندیس ۳).
        Assert.Equal(2, dto.SellerPerformance.Rows.Count);
        var perfRow = dto.SellerPerformance.Rows[0];
        Assert.Equal("فروشندهٔ الف", perfRow[0]);
        Assert.Equal("900", perfRow[2]);
        Assert.Equal("400", perfRow[3]);
    }
}
