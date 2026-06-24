using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Tourism.Application.Commands;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>TUR-C1-5 — آشتیِ گزارشِ روزانه: اختلافِ کسر، سندِ تعدیل به حسابِ اختلاف می‌زند.</summary>
public class TourismReconcileTests
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
        public Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default) => Task.FromResult("2001");
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

    private const int Deposit = 150, Difference = 160;

    [Fact]
    public async Task Reconcile_With_Mismatch_Posts_Adjustment_To_Difference_Account()
    {
        var reports = new FakeRepo<SupplierDailyReport>();
        reports.AddAsync(SupplierDailyReport.Create(1, supplierPartyId: 11, date: "1404/06/15",
            totalCost: 400, lineCount: 3, passengerCount: 0)).Wait();   // Id=1
        var id = reports.Items[0].Id;

        var settings = new FakeRepo<TourismSetting>();
        var set = TourismSetting.Create(1);
        set.Update(null, null, null, null, Deposit, null, Difference, null, null, null, true, 0, true, true);
        settings.AddAsync(set).Wait();

        var vouchers = new FakeVoucherRepo();
        var handler = new ReconcileSupplierDailyReportCommandHandler(reports, settings, vouchers,
            new FakeRepo<FiscalYear>(), new FakeUow(), new FakeUser());

        // تأمین‌کننده ۴۵۰ کسر کرد، ما ۴۰۰ ثبت کرده بودیم → اختلافِ ۵۰.
        var res = await handler.Handle(new ReconcileSupplierDailyReportCommand(
            id, SupplierDeductedAmount: 450, BranchId: 1, FiscalYearId: 1, Date: "1404/06/16"), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        Assert.Equal(50m, v.Items.Where(i => i.AccountId == Difference).Sum(i => i.Debit));   // زیانِ اختلاف بدهکار
        Assert.Equal(50m, v.Items.Where(i => i.AccountId == Deposit).Sum(i => i.Credit));     // ودیعه بستانکار (کم می‌شود)

        var report = reports.Items[0];
        Assert.Equal(DailyReportStatus.Reconciled, report.Status);
        Assert.Equal(450m, report.SupplierDeductedAmount);
    }

    [Fact]
    public async Task Reconcile_Without_Mismatch_Marks_Reconciled_Without_Voucher()
    {
        var reports = new FakeRepo<SupplierDailyReport>();
        reports.AddAsync(SupplierDailyReport.Create(1, 11, "1404/06/15", totalCost: 400, lineCount: 2, passengerCount: 0)).Wait();
        var vouchers = new FakeVoucherRepo();
        var handler = new ReconcileSupplierDailyReportCommandHandler(reports, new FakeRepo<TourismSetting>(),
            vouchers, new FakeRepo<FiscalYear>(), new FakeUow(), new FakeUser());

        var res = await handler.Handle(new ReconcileSupplierDailyReportCommand(
            reports.Items[0].Id, 400, 1, 1, "1404/06/16"), default);

        Assert.True(res.Succeeded);
        Assert.Null(vouchers.Saved);                       // بدونِ اختلاف، سندی زده نشد
        Assert.Equal(DailyReportStatus.Reconciled, reports.Items[0].Status);
    }
}
