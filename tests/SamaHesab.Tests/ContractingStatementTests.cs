using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Contracting;
using SamaHesab.Application.Contracting.Commands;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Contracting;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>CON — آبشارِ صورت‌وضعیت + سندِ متوازنِ Post + بازیافتِ سقف‌دارِ پیش‌پرداخت.</summary>
public class ContractingStatementTests
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
        public Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default) => Task.FromResult("5001");
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

    // ── موتورِ آبشار ──
    [Fact]
    public void Waterfall_Computes_Period_Gross_Deductions_Net()
    {
        var r = StatementWaterfallEngine.Compute(new WaterfallInput(
            CumulativeGrossWork: 1000, PreviousCumulative: 600, AdjustmentAmount: 0, MaterialDiffAmount: 0,
            AdvancePercent: 25, RetentionPercent: 5, InsurancePercent: 5, TaxPercent: 5,
            Penalty: 0, Other: 0, AdvanceOutstanding: 1000));

        Assert.Equal(400, r.PeriodWork);
        Assert.Equal(400, r.GrossThisPeriod);
        Assert.Equal(100, r.AdvanceRecovery);   // ۲۵٪×۴۰۰
        Assert.Equal(20, r.Retention);
        Assert.Equal(20, r.Insurance);
        Assert.Equal(20, r.Tax);
        Assert.Equal(240, r.NetPayable);        // ۴۰۰−۱۰۰−۲۰−۲۰−۲۰
    }

    [Fact]
    public void Waterfall_Caps_AdvanceRecovery_To_Outstanding()
    {
        var r = StatementWaterfallEngine.Compute(new WaterfallInput(
            1000, 600, 0, 0, AdvancePercent: 25, RetentionPercent: 0, InsurancePercent: 0, TaxPercent: 0,
            Penalty: 0, Other: 0, AdvanceOutstanding: 50));   // raw=100 ولی مانده ۵۰
        Assert.Equal(50, r.AdvanceRecovery);
    }

    // ── Post: سندِ متوازن + بازیافتِ پیش‌پرداخت ──
    private const int Receivable = 101, RetDep = 120, InsDep = 121, PrepaidTax = 122, AdvLiab = 300, Penalty = 801, Revenue = 601, Bank = 102;

    [Fact]
    public async Task Post_Builds_Balanced_Voucher_And_Recovers_Advance()
    {
        var projects = new FakeRepo<ContractProject>();
        projects.AddAsync(ContractProject.Create(1, "P1", "ساختمان", employerPartyId: 5, ContractType.UnitPrice,
            contractAmount: 1_000_000, startDate: "1404/01/01",
            advancePercent: 25, retentionPercent: 5, insuranceWithholdPercent: 5, taxWithholdPercent: 5,
            projectDimensionId: 77)).Wait(); // Id=1

        var advances = new FakeRepo<AdvancePayment>();
        advances.AddAsync(AdvancePayment.Create(1, 1, 200_000, "1404/01/01")).Wait(); // outstanding 200000

        var settings = new FakeRepo<ContractingSetting>();
        var set = ContractingSetting.Create(1);
        set.Update(Receivable, RetDep, InsDep, PrepaidTax, AdvLiab, Penalty, Revenue, Bank, 0, 0, 0, 0, false);
        settings.AddAsync(set).Wait();

        var statements = new FakeRepo<ProgressStatement>();
        var uow = new FakeUow(); var user = new FakeUser();

        var saveRes = await new SaveProgressStatementCommandHandler(projects, settings, advances, statements, uow, user)
            .Handle(new SaveProgressStatementCommand(1, 1, StatementType.Interim, "1404/02/01",
                CumulativeGrossWork: 400_000, PreviousCumulative: 0), default);
        Assert.True(saveRes.Succeeded, saveRes.ErrorMessage);

        var vouchers = new FakeVoucherRepo();
        var postRes = await new PostProgressStatementCommandHandler(statements, projects, settings, advances,
            vouchers, new FakeRepo<FiscalYear>(), uow, user)
            .Handle(new PostProgressStatementCommand(saveRes.Value, 1, 1), default);

        Assert.True(postRes.Succeeded, postRes.ErrorMessage);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        Assert.Equal(240_000m, v.Items.Where(i => i.AccountId == Receivable).Sum(i => i.Debit));   // خالص
        Assert.Equal(20_000m, v.Items.Where(i => i.AccountId == RetDep).Sum(i => i.Debit));          // سپردهٔ حسن‌انجام
        Assert.Equal(20_000m, v.Items.Where(i => i.AccountId == InsDep).Sum(i => i.Debit));
        Assert.Equal(20_000m, v.Items.Where(i => i.AccountId == PrepaidTax).Sum(i => i.Debit));
        Assert.Equal(100_000m, v.Items.Where(i => i.AccountId == AdvLiab).Sum(i => i.Debit));        // بازیافتِ پیش‌پرداخت بدهیِ پیش‌پرداخت را کم می‌کند
        Assert.Equal(400_000m, v.Items.Where(i => i.AccountId == Revenue).Sum(i => i.Credit));       // درآمدِ پیمان
        Assert.Equal(77, v.Items.First(i => i.AccountId == Revenue).ProjectId);                      // تگِ بُعدِ پروژه

        Assert.Equal(100_000m, advances.Items[0].RecoveredToDate);   // پیش‌پرداخت بازیافت شد
        Assert.Equal(100_000m, advances.Items[0].Outstanding);
        Assert.Equal(StatementStatus.Posted, statements.Items[0].Status);
    }
}
