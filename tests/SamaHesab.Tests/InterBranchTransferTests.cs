using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Treasury.Commands;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>MB-1 گام۴: سندِ تسویهٔ بین‌شعبه — `CreateInterBranchTransferCommand`.</summary>
public class InterBranchTransferTests
{
    private sealed class FakeAccountRepo : IAccountRepository
    {
        public readonly List<Account> Items = new();
        private int _seq;
        public Task AddAsync(Account e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<Account?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<Account?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.CompanyId == companyId && x.Code == code));
        public void Update(Account e) { }
        public Task<List<Account>> GetByCompanyAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> GetChildrenAsync(int parentId, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<List<Account>> GetLeafAccountsAsync(int companyId, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<bool> HasTransactionsAsync(int accountId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<decimal> GetBalanceAsync(int accountId, CancellationToken ct = default) => Task.FromResult(0m);
        public Task<List<Account>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> FindAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Account?> FindSingleAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Account> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Remove(Account e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Account> e) { foreach (var x in e) Items.Remove(x); }
    }

    private sealed class FakeVoucherRepo : IVoucherRepository
    {
        public readonly List<Voucher> Items = new();
        private int _seq;
        public Task AddAsync(Voucher e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default) => Task.FromResult((Items.Count + 1).ToString());
        public Task<Voucher?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<Voucher?> GetWithItemsAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<List<Voucher>> GetByDateRangeAsync(int c, int fy, string f, string t, CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Voucher>> GetByDateRangeWithItemsAsync(int c, string f, string t, CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public void Update(Voucher e) { }
        public Task<List<Voucher>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Voucher>> FindAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Voucher?> FindSingleAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Voucher, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Voucher> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Remove(Voucher e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Voucher> e) { foreach (var x in e) Items.Remove(x); }
    }

    // سال مالی تعریف‌نشده → قفلِ دوره رد می‌شود (مسیرِ سازگار با نصب‌های قدیمی).
    private sealed class FakeFiscalRepo : IRepository<FiscalYear>
    {
        public Task<FiscalYear?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult<FiscalYear?>(null);
        public Task<List<FiscalYear>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<FiscalYear>());
        public Task<List<FiscalYear>> FindAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(new List<FiscalYear>());
        public Task<FiscalYear?> FindSingleAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult<FiscalYear?>(null);
        public Task<bool> AnyAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CountAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(0);
        public Task AddAsync(FiscalYear e, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<FiscalYear> e, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(FiscalYear e) { }
        public void Remove(FiscalYear e) { }
        public void RemoveRange(IEnumerable<FiscalYear> e) { }
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
        public string? Username => "admin"; public string? FullName => "ادمین"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static (CreateInterBranchTransferCommandHandler h, FakeVoucherRepo v, FakeAccountRepo a) NewSut()
    {
        var acc = new FakeAccountRepo();
        acc.AddAsync(Account.Create(1, "1-07-001", "فی‌مابین شعب", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        acc.AddAsync(Account.Create(1, "1-01-001", "صندوق شعبه ۱", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        acc.AddAsync(Account.Create(1, "1-01-009", "صندوق شعبه ۲", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        var vch = new FakeVoucherRepo();
        var h = new CreateInterBranchTransferCommandHandler(new FakeUow(), new FakeUser(), acc, vch, new FakeFiscalRepo());
        return (h, vch, acc);
    }

    [Fact]
    public async Task Transfer_Creates_Two_Balanced_Posted_Vouchers()
    {
        var (h, vch, acc) = NewSut();
        int interId = acc.Items[0].Id, fromId = acc.Items[1].Id, toId = acc.Items[2].Id;

        var res = await h.Handle(new CreateInterBranchTransferCommand(
            FromBranchId: 1, ToBranchId: 2, FiscalYearId: 1, Date: "1403/03/15",
            Amount: 5_000_000m, FromAccountId: fromId, ToAccountId: toId), default);

        Assert.True(res.Succeeded);
        Assert.Equal(2, vch.Items.Count);
        Assert.All(vch.Items, v => Assert.Equal(VoucherStatus.Posted, v.Status));
        Assert.All(vch.Items, v => Assert.True(v.IsBalanced()));

        var fromV = vch.Items.Single(v => v.Id == res.Value!.FromVoucherId);
        var toV = vch.Items.Single(v => v.Id == res.Value!.ToVoucherId);
        Assert.Equal(1, fromV.BranchId);
        Assert.Equal(2, toV.BranchId);
        // هر دو سند به یک reference گره خورده‌اند.
        Assert.Equal(fromV.Reference, toV.Reference);
    }

    [Fact]
    public async Task InterBranch_Account_Nets_To_Zero_Across_Both_Vouchers()
    {
        var (h, vch, acc) = NewSut();
        int interId = acc.Items[0].Id, fromId = acc.Items[1].Id, toId = acc.Items[2].Id;

        await h.Handle(new CreateInterBranchTransferCommand(1, 2, 1, "1403/03/15", 5_000_000m, fromId, toId), default);

        var interItems = vch.Items.SelectMany(v => v.Items).Where(i => i.AccountId == interId).ToList();
        Assert.Equal(5_000_000m, interItems.Sum(i => i.Debit));   // مبدأ بدهکار
        Assert.Equal(5_000_000m, interItems.Sum(i => i.Credit));  // مقصد بستانکار
        Assert.Equal(0m, interItems.Sum(i => i.Debit) - interItems.Sum(i => i.Credit)); // خالص = ۰
    }

    [Fact]
    public async Task Missing_InterBranch_Account_Fails()
    {
        var acc = new FakeAccountRepo();
        acc.AddAsync(Account.Create(1, "1-01-001", "صندوق ۱", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        acc.AddAsync(Account.Create(1, "1-01-009", "صندوق ۲", AccountLevel.Subsidiary, AccountNature.Debit, "دارایی")).Wait();
        var vch = new FakeVoucherRepo();
        var h = new CreateInterBranchTransferCommandHandler(new FakeUow(), new FakeUser(), acc, vch, new FakeFiscalRepo());

        var res = await h.Handle(new CreateInterBranchTransferCommand(
            1, 2, 1, "1403/03/15", 1000m, acc.Items[0].Id, acc.Items[1].Id), default);

        Assert.False(res.Succeeded);
        Assert.Empty(vch.Items);
    }

    [Fact]
    public void Validator_Rejects_Same_Source_And_Destination_Branch()
    {
        var v = new CreateInterBranchTransferCommandValidator();
        var r = v.Validate(new CreateInterBranchTransferCommand(3, 3, 1, "1403/03/15", 1000m, 10, 11));
        Assert.False(r.IsValid);
    }

    [Fact]
    public void Validator_Rejects_NonPositive_Amount()
    {
        var v = new CreateInterBranchTransferCommandValidator();
        var r = v.Validate(new CreateInterBranchTransferCommand(1, 2, 1, "1403/03/15", 0m, 10, 11));
        Assert.False(r.IsValid);
    }
}
