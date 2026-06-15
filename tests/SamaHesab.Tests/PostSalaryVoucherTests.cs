using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.HRM;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>M7 فاز۲ — سندِ خودکارِ پرداختِ حقوق (`PostSalaryVoucherCommand`).</summary>
public class PostSalaryVoucherTests
{
    private sealed class FakeEmpRepo : IRepository<Employee>
    {
        public readonly List<Employee> Items = new();
        private int _seq;
        public Task AddAsync(Employee e, CancellationToken ct = default){ typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<Employee?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<List<Employee>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Employee>> FindAsync(Expression<Func<Employee, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Employee?> FindSingleAsync(Expression<Func<Employee, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Employee, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Employee, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Employee> e, CancellationToken ct = default){ Items.AddRange(e); return Task.CompletedTask; }
        public void Update(Employee e) { } public void Remove(Employee e) => Items.Remove(e); public void RemoveRange(IEnumerable<Employee> e) { }
    }
    private sealed class FakeAccRepo : IAccountRepository
    {
        public readonly List<Account> Items = new(); private int _seq;
        public Task AddAsync(Account e, CancellationToken ct = default){ typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<Account?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<Account?> GetByCodeAsync(int c, string code, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Code == code));
        public void Update(Account e) { }
        public Task<List<Account>> GetByCompanyAsync(int c, CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> GetChildrenAsync(int p, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<List<Account>> GetLeafAccountsAsync(int c, CancellationToken ct = default) => Task.FromResult(new List<Account>());
        public Task<bool> HasTransactionsAsync(int a, CancellationToken ct = default) => Task.FromResult(false);
        public Task<decimal> GetBalanceAsync(int a, CancellationToken ct = default) => Task.FromResult(0m);
        public Task<List<Account>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Account>> FindAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Account?> FindSingleAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Account, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<Account> e, CancellationToken ct = default){ Items.AddRange(e); return Task.CompletedTask; }
        public void Remove(Account e) { } public void RemoveRange(IEnumerable<Account> e) { }
    }
    private sealed class FakeVchRepo : IVoucherRepository
    {
        public readonly List<Voucher> Items = new(); private int _seq;
        public Task AddAsync(Voucher e, CancellationToken ct = default){ typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<string> GetNextNumberAsync(int c, CancellationToken ct = default) => Task.FromResult((Items.Count+1).ToString());
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
        public Task AddRangeAsync(IEnumerable<Voucher> e, CancellationToken ct = default){ Items.AddRange(e); return Task.CompletedTask; }
        public void Remove(Voucher e) { } public void RemoveRange(IEnumerable<Voucher> e) { }
    }
    private sealed class FakeFy : IRepository<FiscalYear>
    {
        private readonly FiscalYear? _fy;
        public FakeFy(bool active){ if(active){ _fy = FiscalYear.Create(1,"1403","1403/01/01","1403/12/29"); typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(_fy,1);} }
        public Task<FiscalYear?> FindSingleAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(_fy);
        public Task<FiscalYear?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_fy);
        public Task<List<FiscalYear>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<FiscalYear>());
        public Task<List<FiscalYear>> FindAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(new List<FiscalYear>());
        public Task<bool> AnyAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CountAsync(Expression<Func<FiscalYear, bool>> p, CancellationToken ct = default) => Task.FromResult(0);
        public Task AddAsync(FiscalYear e, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<FiscalYear> e, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(FiscalYear e) { } public void Remove(FiscalYear e) { } public void RemoveRange(IEnumerable<FiscalYear> e) { }
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

    private static (PostSalaryVoucherCommandHandler h, FakeVchRepo v) Sut(bool fy = true, params decimal[] salaries)
    {
        var emps = new FakeEmpRepo();
        foreach (var s in salaries) emps.AddAsync(Employee.Create(1, 1, "E"+s, ""+s, "ع", "الف", "1403/01/01", s)).Wait();
        var acc = new FakeAccRepo();
        foreach (var code in new[] { "8-01-001", "3-05-001", "3-05-002", "3-04-002" })
            acc.AddAsync(Account.Create(1, code, code, AccountLevel.Subsidiary, AccountNature.Debit, "x")).Wait();
        var vch = new FakeVchRepo();
        var h = new PostSalaryVoucherCommandHandler(emps, acc, vch, new FakeFy(fy), new FakeUow(), new FakeUser());
        return (h, vch);
    }

    [Fact]
    public async Task Posts_Balanced_Voucher_Splitting_Gross_Into_Net_Insurance_Tax()
    {
        var (h, vch) = Sut(true, 20_000_000m, 8_000_000m);

        var res = await h.Handle(new PostSalaryVoucherCommand("1403/03/31"), default);

        Assert.True(res.Succeeded);
        Assert.Equal(2, res.Value!.EmployeeCount);
        var v = Assert.Single(vch.Items);
        Assert.Equal(VoucherStatus.Posted, v.Status);
        Assert.True(v.IsBalanced());                       // بد(ناخالص) = بس(خالص+بیمه+مالیات)
        Assert.Equal(res.Value.Gross, v.TotalDebit);
        Assert.Equal(v.TotalDebit, v.TotalCredit);
    }

    [Fact]
    public async Task No_Active_Fiscal_Year_Fails_Without_Voucher()
    {
        var (h, vch) = Sut(false, 10_000_000m);
        var res = await h.Handle(new PostSalaryVoucherCommand("1403/03/31"), default);
        Assert.False(res.Succeeded);
        Assert.Empty(vch.Items);
    }

    [Fact]
    public async Task No_Active_Employees_Fails()
    {
        var (h, vch) = Sut(true);   // بدون کارمند
        var res = await h.Handle(new PostSalaryVoucherCommand("1403/03/31"), default);
        Assert.False(res.Succeeded);
        Assert.Empty(vch.Items);
    }
}
