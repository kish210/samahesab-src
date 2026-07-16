using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.HRM;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-BRANCH-BASEDATA (@2026-07-16) — BranchId اختیاری روی دادهٔ پایه (Product تازه اضافه شد؛
/// Party/Warehouse/Employee از قبل داشتند) + فیلترِ اختیاریِ شعبه در کوئری‌های لیست + گزارشِ per-branch.</summary>
public class BranchBaseDataTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        private static void SetId(T e, int value) => typeof(T).GetProperty("Id")!.SetValue(e, value);
        public Task AddAsync(T e, CancellationToken ct = default) { SetId(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default) { foreach (var e in es) { SetId(e, ++_seq); Items.Add(e); } return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<System.Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<System.Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(T e) { }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> es) { foreach (var x in es.ToList()) Items.Remove(x); }
    }

    private sealed class FakeProductRepo : IProductRepository
    {
        public readonly List<Product> Items = new();
        private int _seq;
        public Task AddAsync(Product e, CancellationToken ct = default) { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Product> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));
        public Task<List<Product>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Product>> FindAsync(Expression<System.Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Product?> FindSingleAsync(Expression<System.Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(Product e) { }
        public void Remove(Product e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Product> es) { }
        public Task<Product?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(p => p.CompanyId == companyId && p.Code == code));
        public Task<Product?> GetByBarcodeAsync(int companyId, string barcode, CancellationToken ct = default) => Task.FromResult<Product?>(null);
        public Task<List<Product>> SearchAsync(int companyId, string searchText, CancellationToken ct = default)
            => Task.FromResult(Items.Where(p => p.CompanyId == companyId && (p.Code.Contains(searchText) || p.Name.Contains(searchText))).ToList());
        public Task<List<Product>> GetByGroupAsync(int groupId, CancellationToken ct = default) => Task.FromResult(new List<Product>());
        public Task<List<Product>> GetLowStockAsync(int companyId, CancellationToken ct = default) => Task.FromResult(new List<Product>());
    }

    private sealed class FakeWarehouseRepo : IWarehouseRepository
    {
        public readonly List<Warehouse> Items = new();
        private int _seq;
        public Task AddAsync(Warehouse e, CancellationToken ct = default) { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Warehouse> es, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Warehouse?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(w => w.Id == id));
        public Task<List<Warehouse>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Warehouse>> FindAsync(Expression<System.Func<Warehouse, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Warehouse?> FindSingleAsync(Expression<System.Func<Warehouse, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<System.Func<Warehouse, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<System.Func<Warehouse, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(Warehouse e) { }
        public void Remove(Warehouse e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Warehouse> es) { }
        public Task<List<Warehouse>> GetByCompanyAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.Where(w => w.CompanyId == companyId).ToList());
        public Task<Warehouse?> GetDefaultAsync(int companyId, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(w => w.CompanyId == companyId));
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "a"; public string? FullName => "ا"; public bool IsAuthenticated => true;
        public int? SalespersonPartyId => null;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    [Fact]
    public async Task GetProductsQuery_Filters_By_BranchId_But_Includes_Shared_Products()
    {
        var products = new FakeProductRepo();
        var p1 = Product.Create(1, "P1", "کالایِ شعبهٔ ۱", 1, 1000, 500);
        p1.SetBranch(10);
        var p2 = Product.Create(1, "P2", "کالایِ شعبهٔ ۲", 1, 1000, 500);
        p2.SetBranch(20);
        var p3 = Product.Create(1, "P3", "کالایِ مشترک", 1, 1000, 500);
        await products.AddAsync(p1); await products.AddAsync(p2); await products.AddAsync(p3);

        var handler = new GetProductsQueryHandler(products, new FakeUser());
        var branch10 = await handler.Handle(new GetProductsQuery(BranchId: 10), default);
        var all = await handler.Handle(new GetProductsQuery(), default);

        Assert.Equal(2, branch10.Count); // P1 (شعبه‌اش) + P3 (مشترک)
        Assert.Contains(branch10, p => p.Code == "P1");
        Assert.Contains(branch10, p => p.Code == "P3");
        Assert.DoesNotContain(branch10, p => p.Code == "P2");
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task GetWarehousesQuery_Filters_By_BranchId_But_Includes_Shared_Warehouses()
    {
        var warehouses = new FakeWarehouseRepo();
        await warehouses.AddAsync(Warehouse.Create(1, "W1", "انبارِ شعبه ۱", branchId: 10));
        await warehouses.AddAsync(Warehouse.Create(1, "W2", "انبارِ شعبه ۲", branchId: 20));
        await warehouses.AddAsync(Warehouse.Create(1, "W3", "انبارِ مشترک"));

        var handler = new GetWarehousesQueryHandler(warehouses, new FakeUser());
        var branch10 = await handler.Handle(new GetWarehousesQuery(BranchId: 10), default);

        Assert.Equal(2, branch10.Count);
        Assert.Contains(branch10, w => w.Name == "انبارِ شعبه ۱");
        Assert.Contains(branch10, w => w.Name == "انبارِ مشترک");
    }

    [Fact]
    public async Task GetEmployeesQuery_Filters_By_BranchId_But_Includes_Shared_Employees()
    {
        var employees = new FakeRepo<Employee>();
        await employees.AddAsync(Employee.Create(1, 10, "E1", "0011223344", "علی", "احمدی", "1405/01/01", 50_000_000));
        await employees.AddAsync(Employee.Create(1, 20, "E2", "0011223345", "رضا", "رضایی", "1405/01/01", 50_000_000));
        await employees.AddAsync(Employee.Create(1, null, "E3", "0011223346", "مشترک", "مشترکی", "1405/01/01", 50_000_000));

        var handler = new GetEmployeesQueryHandler(employees, new FakeUser());
        var branch10 = await handler.Handle(new GetEmployeesQuery(BranchId: 10), default);

        Assert.Equal(2, branch10.Count);
        Assert.Contains(branch10, e => e.Code == "E1");
        Assert.Contains(branch10, e => e.Code == "E3");
    }

    [Fact]
    public async Task GetBranchSummaryQuery_Counts_BaseData_Per_Branch_Plus_Shared_Row()
    {
        var branches = new FakeRepo<Branch>();
        var b1 = Branch.Create(1, "B1", "شعبهٔ یک");
        var b2 = Branch.Create(1, "B2", "شعبهٔ دو");
        await branches.AddAsync(b1); await branches.AddAsync(b2);

        var parties = new FakeRepo<Party>();
        var cust1 = Party.Create(1, "C1", "حقیقی", "مشتریِ", "شعبه۱", isCustomer: true);
        cust1.SetBranch(b1.Id);
        var supp1 = Party.Create(1, "S1", "حقیقی", "تأمین‌کنندهٔ", "شعبه۱", isSupplier: true);
        supp1.SetBranch(b1.Id);
        var custShared = Party.Create(1, "C2", "حقیقی", "مشتریِ", "مشترک", isCustomer: true);
        await parties.AddAsync(cust1); await parties.AddAsync(supp1); await parties.AddAsync(custShared);

        var products = new FakeProductRepo();
        var prod1 = Product.Create(1, "P1", "کالایِ شعبه۱", 1, 1000, 500);
        prod1.SetBranch(b1.Id);
        await products.AddAsync(prod1);

        var warehouses = new FakeWarehouseRepo();
        await warehouses.AddAsync(Warehouse.Create(1, "W1", "انبارِ شعبه۱", branchId: b1.Id));

        var employees = new FakeRepo<Employee>();
        await employees.AddAsync(Employee.Create(1, b1.Id, "E1", "0011223344", "علی", "احمدی", "1405/01/01", 50_000_000));

        var handler = new GetBranchSummaryQueryHandler(branches, parties, products, warehouses, employees, new FakeUser());
        var rows = await handler.Handle(new GetBranchSummaryQuery(), default);

        Assert.Equal(3, rows.Count); // شعبه۱ + شعبه۲ + مشترک
        var row1 = rows.Single(r => r.BranchId == b1.Id);
        Assert.Equal(1, row1.CustomerCount);
        Assert.Equal(1, row1.SupplierCount);
        Assert.Equal(1, row1.ProductCount);
        Assert.Equal(1, row1.WarehouseCount);
        Assert.Equal(1, row1.EmployeeCount);

        var row2 = rows.Single(r => r.BranchId == b2.Id);
        Assert.Equal(0, row2.CustomerCount);

        var shared = rows.Single(r => r.BranchId == null);
        Assert.Equal(1, shared.CustomerCount);
    }
}
