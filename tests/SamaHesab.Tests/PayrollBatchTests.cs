using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.HRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Entities.Tourism;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>PAY-C1-3/4/5 — تنظیماتِ حقوق، محاسبهٔ دسته‌ای + ذخیرهٔ فیش‌ها، و گزارش‌های خروجی.</summary>
public class PayrollBatchTests
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

    private sealed class FakeUow : IUnitOfWork
    {
        public int Saves;
        public IRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }
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

    private static Employee Emp(FakeRepo<Employee> repo, string nc, decimal salary, int children = 0)
    {
        var e = Employee.Create(1, 1, "E" + nc, nc, "نام", nc, "1403/01/01", salary);
        e.UpdatePayrollInfo("INS" + nc, children, 0);
        e.UpdateBankInfo("بانک", "100", "IR" + nc);
        repo.AddAsync(e).Wait();
        return e;
    }

    // ── PAY-C1-5: تنظیمات round-trip ──
    [Fact]
    public async Task Settings_Save_Then_Get_RoundTrips()
    {
        var repo = new FakeRepo<PayrollSetting>(); var uow = new FakeUow(); var user = new FakeUser();
        var dto = PayrollSettingsDto.Default("1404") with { HousingAllowance = 9_000_000m, FoodAllowance = 11_000_000m };

        var save = await new SavePayrollSettingsCommandHandler(repo, uow, user).Handle(new SavePayrollSettingsCommand(dto), default);
        Assert.True(save.Succeeded);

        var got = await new GetPayrollSettingsQueryHandler(repo, user).Handle(new GetPayrollSettingsQuery("1404"), default);
        Assert.Equal(9_000_000m, got.HousingAllowance);
        Assert.Equal(11_000_000m, got.FoodAllowance);
        Assert.Equal(0.07m, got.InsuranceEmployeeRate);
    }

    [Fact]
    public async Task Settings_Get_Returns_Default_When_None()
    {
        var got = await new GetPayrollSettingsQueryHandler(new FakeRepo<PayrollSetting>(), new FakeUser())
            .Handle(new GetPayrollSettingsQuery("1404"), default);
        Assert.Equal("1404", got.Year);
        Assert.Equal(100_000_000m, got.MonthlyTaxExemption);
    }

    // ── PAY-C1-3: محاسبهٔ دسته‌ای ──
    [Fact]
    public async Task RunPayroll_Creates_Slips_For_Active_Employees()
    {
        var emps = new FakeRepo<Employee>(); var slips = new FakeRepo<SalarySlip>();
        var sets = new FakeRepo<PayrollSetting>(); var uow = new FakeUow(); var user = new FakeUser();
        Emp(emps, "111", 20_000_000m);
        Emp(emps, "222", 30_000_000m, children: 2);

        var res = await new RunMonthlyPayrollCommandHandler(emps, slips, sets, new FakeRepo<AttendanceRecord>(), new FakeRepo<Holiday>(), new FakeRepo<SalesCommissionEntry>(), new FakeRepo<Party>(), uow, user)
            .Handle(new RunMonthlyPayrollCommand("1404", 1), default);

        Assert.True(res.Succeeded);
        Assert.Equal(2, res.Value!.Created);
        Assert.Equal(2, slips.Items.Count);
        Assert.True(res.Value.TotalGross > 0);
        Assert.True(res.Value.TotalNet > 0);
        Assert.True(res.Value.TotalEmployerInsurance > 0);   // سهمِ کارفرما محاسبه شد
        // فیش با اجزای تفکیکی ذخیره شد + خالص = ناخالص − بیمه − مالیات.
        var s = slips.Items.First();
        Assert.Equal(s.GrossSalary - s.InsuranceDeduct - s.TaxDeduct - s.OtherDeductions, s.NetSalary);
    }

    [Fact]
    public async Task RunPayroll_Is_Idempotent_And_Overwrites()
    {
        var emps = new FakeRepo<Employee>(); var slips = new FakeRepo<SalarySlip>();
        var sets = new FakeRepo<PayrollSetting>(); var uow = new FakeUow(); var user = new FakeUser();
        Emp(emps, "111", 20_000_000m);
        var h = new RunMonthlyPayrollCommandHandler(emps, slips, sets, new FakeRepo<AttendanceRecord>(), new FakeRepo<Holiday>(), new FakeRepo<SalesCommissionEntry>(), new FakeRepo<Party>(), uow, user);

        await h.Handle(new RunMonthlyPayrollCommand("1404", 1), default);
        var second = await h.Handle(new RunMonthlyPayrollCommand("1404", 1), default);   // بدونِ Overwrite
        Assert.Equal(0, second.Value!.Created);
        Assert.Equal(1, second.Value.Skipped);
        Assert.Single(slips.Items);                          // فیشِ تکراری ساخته نشد

        var third = await h.Handle(new RunMonthlyPayrollCommand("1404", 1, Overwrite: true), default);
        Assert.Equal(1, third.Value!.Created);
        Assert.Single(slips.Items);                          // جایگزین شد، نه افزوده
    }

    // ── PAY-C1-4: گزارش‌های خروجی ──
    [Fact]
    public async Task Export_Builds_NonEmpty_Files_From_Saved_Slips()
    {
        var emps = new FakeRepo<Employee>(); var slips = new FakeRepo<SalarySlip>();
        var sets = new FakeRepo<PayrollSetting>(); var uow = new FakeUow(); var user = new FakeUser();
        Emp(emps, "111", 20_000_000m);
        Emp(emps, "222", 30_000_000m);
        await new RunMonthlyPayrollCommandHandler(emps, slips, sets, new FakeRepo<AttendanceRecord>(), new FakeRepo<Holiday>(), new FakeRepo<SalesCommissionEntry>(), new FakeRepo<Party>(), uow, user)
            .Handle(new RunMonthlyPayrollCommand("1404", 1), default);

        var exp = await new GetPayrollExportQueryHandler(emps, slips, user)
            .Handle(new GetPayrollExportQuery("1404", 1, "WS1", "کارفرما"), default);

        Assert.Equal(2, exp.EmployeeCount);
        Assert.True(exp.TotalNet > 0);
        Assert.False(string.IsNullOrWhiteSpace(exp.InsuranceListCsv));
        Assert.False(string.IsNullOrWhiteSpace(exp.TaxListCsv));
        Assert.False(string.IsNullOrWhiteSpace(exp.BankFileCsv));
        Assert.Contains("IR111", exp.BankFileCsv);           // شبا در فایلِ بانک
    }
}
