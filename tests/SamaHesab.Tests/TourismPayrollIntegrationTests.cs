using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.HRM;
using SamaHesab.Modules.Tourism.Application.Commands;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// تستِ end-to-endِ مسیرهای C1: سندِ شارژِ ودیعه (Dr ودیعه/Cr بانک) و تزریقِ پورسانت به فیشِ حقوق.
/// </summary>
public class TourismPayrollIntegrationTests
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
        public Task<string> GetNextNumberAsync(int companyId, CancellationToken ct = default) => Task.FromResult("3001");
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

    private const int Deposit = 150, Bank = 102;

    [Fact]
    public async Task TopUp_Posts_Balanced_Deposit_Voucher_And_Records_Deposit()
    {
        var settings = new FakeRepo<TourismSetting>();
        var set = TourismSetting.Create(1);
        set.Update(null, null, null, null, Deposit, null, null, null, null, Bank, true, 0, true, true);
        settings.AddAsync(set).Wait();
        var deposits = new FakeRepo<SupplierDeposit>();
        var vouchers = new FakeVoucherRepo();
        var handler = new TopUpSupplierDepositCommandHandler(settings, deposits, vouchers,
            new FakeRepo<FiscalYear>(), new FakeUow(), new FakeUser());

        var res = await handler.Handle(new TopUpSupplierDepositCommand(
            1, 1, "1404/06/15", SupplierPartyId: 11, Amount: 1000, PaymentMethod: "بانک"), default);

        Assert.True(res.Succeeded, res.ErrorMessage);
        var v = vouchers.Saved!;
        Assert.True(v.IsBalanced());
        Assert.Equal(1000m, v.Items.Where(i => i.AccountId == Deposit).Sum(i => i.Debit));   // ودیعه بدهکار (دارایی↑)
        Assert.Equal(1000m, v.Items.Where(i => i.AccountId == Bank).Sum(i => i.Credit));      // بانک بستانکار
        var dep = Assert.Single(deposits.Items);
        Assert.Equal(1000m, dep.Amount);
        Assert.Equal(v.Id, dep.VoucherId);
    }

    [Fact]
    public async Task Payroll_Injects_Commission_Into_Payslip_When_Enabled()
    {
        var emps = new FakeRepo<Employee>();
        emps.AddAsync(Employee.Create(1, 1, "E1", "999", "علی", "فروشنده", "1404/01/01", 20_000_000m)).Wait(); // کدملی ۹۹۹

        var parties = new FakeRepo<Party>();
        var seller = Party.Create(1, "P999", "حقیقی", "علی", "فروشنده");
        typeof(Party).GetProperty("NationalCode")!.SetValue(seller, "999");
        parties.AddAsync(seller).Wait();

        var commissions = new FakeRepo<SalesCommissionEntry>();
        commissions.AddAsync(SalesCommissionEntry.Create(1, 1, seller.Id,
            CommissionBasis.PercentOfSale, 40_000_000, 5, 2_000_000, "140406")).Wait();

        var slips = new FakeRepo<SalarySlip>();
        var handler = new RunMonthlyPayrollCommandHandler(emps, slips, new FakeRepo<PayrollSetting>(),
            new FakeRepo<AttendanceRecord>(), new FakeRepo<Holiday>(), new FakeUow(), new FakeUser(),
            new SamaHesab.Modules.Tourism.TourismSalesCommissionProvider(commissions, parties));

        var res = await handler.Handle(new RunMonthlyPayrollCommand("1404", 6, IncludeCommission: true), default);

        Assert.True(res.Succeeded);
        var slip = Assert.Single(slips.Items);
        Assert.Equal(2_000_000m, slip.Bonuses);                       // پورسانت در فیش
        Assert.True(slip.GrossSalary >= 22_000_000m);                 // ناخالص شاملِ پورسانت
        Assert.Equal(slip.GrossSalary - slip.InsuranceDeduct - slip.TaxDeduct - slip.OtherDeductions, slip.NetSalary);
    }

    [Fact]
    public async Task Payroll_Without_Commission_Flag_Has_No_Bonus()
    {
        var emps = new FakeRepo<Employee>();
        emps.AddAsync(Employee.Create(1, 1, "E1", "999", "علی", "فروشنده", "1404/01/01", 20_000_000m)).Wait();
        var parties = new FakeRepo<Party>();
        var seller = Party.Create(1, "P999", "حقیقی", "علی", "فروشنده");
        typeof(Party).GetProperty("NationalCode")!.SetValue(seller, "999");
        parties.AddAsync(seller).Wait();
        var commissions = new FakeRepo<SalesCommissionEntry>();
        commissions.AddAsync(SalesCommissionEntry.Create(1, 1, seller.Id, CommissionBasis.PercentOfSale, 40_000_000, 5, 2_000_000, "140406")).Wait();
        var slips = new FakeRepo<SalarySlip>();
        var handler = new RunMonthlyPayrollCommandHandler(emps, slips, new FakeRepo<PayrollSetting>(),
            new FakeRepo<AttendanceRecord>(), new FakeRepo<Holiday>(), new FakeUow(), new FakeUser(),
            new SamaHesab.Modules.Tourism.TourismSalesCommissionProvider(commissions, parties));

        // بدونِ پرچمِ IncludeCommission → پورسانت اعمال نمی‌شود.
        await handler.Handle(new RunMonthlyPayrollCommand("1404", 6), default);

        Assert.Equal(0m, slips.Items.Single().Bonuses);
    }
}
