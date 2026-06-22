using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Contracting.Commands;
using SamaHesab.Application.Contracting.Queries;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Contracting;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>CON-C1-4/6 — سندِ پیش‌پرداختِ دریافتی و داشبوردِ مالیِ پیمان.</summary>
public class ContractingFlowsTests
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
        public Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default) => Task.FromResult("6001");
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

    private const int AdvLiab = 300, Bank = 102, Revenue = 601, Dim = 77;

    [Fact]
    public async Task ReceiveAdvance_Posts_Bank_To_Liability_And_Records()
    {
        var projects = new FakeRepo<ContractProject>();
        projects.AddAsync(ContractProject.Create(1, "P1", "پروژه", 5, ContractType.UnitPrice, 1_000_000, "1404/01/01")).Wait();
        var settings = new FakeRepo<ContractingSetting>();
        var set = ContractingSetting.Create(1);
        set.Update(101, 120, 121, 122, AdvLiab, 801, Revenue, Bank, 0, 0, 0, 0, false);
        settings.AddAsync(set).Wait();
        var advances = new FakeRepo<AdvancePayment>();
        var vouchers = new FakeVoucherRepo();

        var res = await new ReceiveAdvanceCommandHandler(projects, settings, advances, vouchers,
            new FakeRepo<FiscalYear>(), new FakeUow(), new FakeUser())
            .Handle(new ReceiveAdvanceCommand(1, 1, "1404/01/05", 1, 200_000), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        Assert.Equal(200_000m, v.Items.Where(i => i.AccountId == Bank).Sum(i => i.Debit));
        Assert.Equal(200_000m, v.Items.Where(i => i.AccountId == AdvLiab).Sum(i => i.Credit));
        var adv = Assert.Single(advances.Items);
        Assert.Equal(200_000m, adv.Outstanding);
    }

    [Fact]
    public async Task Dashboard_Computes_Progress_Deposits_And_Profit()
    {
        var projects = new FakeRepo<ContractProject>();
        projects.AddAsync(ContractProject.Create(1, "P1", "ساختمان", 5, ContractType.UnitPrice,
            contractAmount: 1_000_000, startDate: "1404/01/01", projectDimensionId: Dim)).Wait(); // Id=1

        var statements = new FakeRepo<ProgressStatement>();
        var st = ProgressStatement.Create(1, 1, 1, StatementType.Interim, "1404/02/01", 700_000, 0);
        st.SetComputed(700_000, 700_000, advanceRecovery: 0, retention: 35_000, insurance: 35_000, tax: 35_000, penalty: 0, other: 0, netPayable: 595_000);
        st.MarkPosted(9001);
        statements.AddAsync(st).Wait();

        var advances = new FakeRepo<AdvancePayment>();
        var adv = AdvancePayment.Create(1, 1, 200_000, "1404/01/05");
        adv.Recover(100_000);
        advances.AddAsync(adv).Wait();

        var items = new FakeRepo<VoucherItem>();
        items.AddAsync(VoucherItem.Create(0, 1, Revenue, 0, 700_000, "درآمد", projectId: Dim)).Wait();
        items.AddAsync(VoucherItem.Create(0, 2, 701, 500_000, 0, "هزینهٔ پروژه", projectId: Dim)).Wait();

        var dto = await new GetProjectDashboardQueryHandler(projects, statements, advances, items, new FakeUser())
            .Handle(new GetProjectDashboardQuery(1), default);

        Assert.NotNull(dto);
        Assert.Equal(70m, dto!.ProgressPercent);          // ۷۰۰هزار از ۱میلیون
        Assert.Equal(35_000m, dto.RetentionHeld);
        Assert.Equal(35_000m, dto.InsuranceHeld);
        Assert.Equal(100_000m, dto.AdvanceOutstanding);   // ۲۰۰ − ۱۰۰ بازیافت
        Assert.Equal(200_000m, dto.Profit);               // ۷۰۰ درآمد − ۵۰۰ هزینه
    }
}
