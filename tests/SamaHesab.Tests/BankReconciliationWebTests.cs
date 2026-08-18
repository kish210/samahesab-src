using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-BANK-RECON-WEB — مغایرت‌گیری بانکیِ وب: ثبتِ ماندگارِ ردیف‌های تطبیق‌شده +
/// فیلترِ ردیف‌های ازقبل‌تطبیق‌شده در اجرای دوباره.</summary>
public class BankReconciliationWebTests
{
    private sealed class FakeBankReconciledRepo : IRepository<BankReconciledItem>
    {
        public readonly List<BankReconciledItem> Items = new();
        private int _seq;
        public Task AddAsync(BankReconciledItem e, CancellationToken ct = default)
        { typeof(BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<BankReconciledItem> es, CancellationToken ct = default)
        { Items.AddRange(es); return Task.CompletedTask; }
        public Task<BankReconciledItem?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<List<BankReconciledItem>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<BankReconciledItem>> FindAsync(Expression<Func<BankReconciledItem, bool>> p, CancellationToken ct = default)
            => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<BankReconciledItem?> FindSingleAsync(Expression<Func<BankReconciledItem, bool>> p, CancellationToken ct = default)
            => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<BankReconciledItem, bool>> p, CancellationToken ct = default)
            => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<BankReconciledItem, bool>> p, CancellationToken ct = default)
            => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(BankReconciledItem e) { }
        public void Remove(BankReconciledItem e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<BankReconciledItem> es) { foreach (var x in es) Items.Remove(x); }
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public int SaveCount;
        public IRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) { SaveCount++; return Task.CompletedTask; }
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 7; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "acc"; public string? FullName => "حسابدار"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private sealed class LedgerMediator : IMediator
    {
        private readonly BankLedgerResult _ledger;
        public LedgerMediator(BankLedgerResult ledger) => _ledger = ledger;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            if (request is GetBankLedgerQuery)
                return Task.FromResult((TResponse)(object)_ledger);
            throw new NotImplementedException("این تست فقط GetBankLedgerQuery را صدا می‌زند.");
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => Task.FromResult<object?>(null);
        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest => Task.CompletedTask;
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> r, CancellationToken ct = default) => null!;
        public IAsyncEnumerable<object?> CreateStream(object r, CancellationToken ct = default) => null!;
        public Task Publish(object n, CancellationToken ct = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification n, CancellationToken ct = default) where TNotification : INotification => Task.CompletedTask;
    }

    [Fact]
    public void Create_Validates_VoucherItemId_And_Date()
    {
        Assert.Throws<ArgumentException>(() => BankReconciledItem.Create(1, 2, 0, "1404/05/01"));
        Assert.Throws<ArgumentException>(() => BankReconciledItem.Create(1, 2, 3, "  "));

        var item = BankReconciledItem.Create(1, 2, 3, " 1404/05/01 ");
        Assert.Equal(2, item.BankAccountId);
        Assert.Equal(3, item.VoucherItemId);
        Assert.Equal("1404/05/01", item.ReconciledDate);
    }

    [Fact]
    public async Task Commit_Rejects_Empty_List()
    {
        var repo = new FakeBankReconciledRepo();
        var handler = new CommitBankReconciliationCommandHandler(repo, new FakeUow(), new FakeUser());

        var r = await handler.Handle(new CommitBankReconciliationCommand(1, new List<int>(), "1404/05/01"), default);

        Assert.False(r.Succeeded);
    }

    [Fact]
    public async Task Commit_Adds_Only_New_Ids()
    {
        var repo = new FakeBankReconciledRepo();
        await repo.AddAsync(BankReconciledItem.Create(1, 2, 101, "1404/05/01"));
        var uow = new FakeUow();
        var handler = new CommitBankReconciliationCommandHandler(repo, uow, new FakeUser());

        var r = await handler.Handle(new CommitBankReconciliationCommand(2, new List<int> { 101, 102, 102, 103 }, "1404/05/02"), default);

        Assert.True(r.Succeeded);
        Assert.Equal(2, r.Value);              // فقط 102 و 103 افزوده شدند (101 تکراری، 102 دوبار)
        Assert.Equal(1, uow.SaveCount);
        Assert.Equal(3, repo.Items.Count);     // 101 (موجود) + 102 + 103
    }

    [Fact]
    public async Task Run_Query_Filters_Already_Reconciled_Rows()
    {
        var ledger = new BankLedgerResult("بانک ملت", 5, new List<BankLedgerLineDto>
        {
            new(101, "1404/05/01", 1_000_000, "واریز"),
            new(102, "1404/05/02", 500_000, "برداشت"),
        });
        var repo = new FakeBankReconciledRepo();
        await repo.AddAsync(BankReconciledItem.Create(1, 2, 101, "1404/05/01"));   // از قبل تطبیق‌شده

        var handler = new RunBankReconciliationQueryHandler(new LedgerMediator(ledger), repo, new FakeUser());

        var r = await handler.Handle(
            new RunBankReconciliationQuery(2, "1404/01/01", "1404/12/29", "1404/05/02,500000"), default);

        Assert.Equal("بانک ملت", r.BankName);
        Assert.Equal(1, r.AlreadyReconciledCount);
        Assert.Equal("1404/05/01", r.LastReconciledDate);
        Assert.Equal(1, r.MatchedCount);            // فقط ردیفِ 102 (باز) منطبق شد
        Assert.Equal(102, r.Matched[0].VoucherItemId);
        Assert.Equal(0, r.UnmatchedLedgerCount);
        Assert.Equal(0, r.UnmatchedStatementCount);
    }
}
