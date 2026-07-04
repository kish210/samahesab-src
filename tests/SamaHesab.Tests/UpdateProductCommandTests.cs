using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-INV-3 — فرمِ «ویرایشِ کالا» قبلاً هیچ‌جا کالای موجود را لود نمی‌کرد و همیشه Create می‌زد؛ این تست‌ها مسیرِ Update/Load را پوشش می‌دهند.</summary>
public class UpdateProductCommandTests
{
    private sealed class FakeProductRepo : IProductRepository
    {
        public readonly List<Product> Items = new();
        public Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));
        public Task<List<Product>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<Product>> FindAsync(Expression<Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<Product?> FindSingleAsync(Expression<Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<Product, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddAsync(Product e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, Items.Count + 1); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<Product> e, CancellationToken ct = default) => Task.CompletedTask;
        public void Update(Product e) { }
        public void Remove(Product e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<Product> e) { }
        public Task<Product?> GetByCodeAsync(int companyId, string code, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(p => p.CompanyId == companyId && p.Code == code));
        public Task<Product?> GetByBarcodeAsync(int companyId, string barcode, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(p => p.CompanyId == companyId && p.Barcode == barcode));
        public Task<List<Product>> SearchAsync(int companyId, string searchText, CancellationToken ct = default) => Task.FromResult(new List<Product>());
        public Task<List<Product>> GetByGroupAsync(int groupId, CancellationToken ct = default) => Task.FromResult(new List<Product>());
        public Task<List<Product>> GetLowStockAsync(int companyId, CancellationToken ct = default) => Task.FromResult(new List<Product>());
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
        public string? Username => "admin"; public string? FullName => "مدیر"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static Product MakeProduct(FakeProductRepo repo, string code = "P1", string? barcode = null)
    {
        var p = Product.Create(1, code, "کالای اول", 1, 1000m, 800m, ProductType.Product);
        if (barcode is not null) p.UpdateDetails(p.Name, null, null, null, barcode, null, null);
        repo.AddAsync(p).GetAwaiter().GetResult();
        return p;
    }

    [Fact]
    public async Task Update_ChangesFieldsAndPersists()
    {
        var repo = new FakeProductRepo();
        var p = MakeProduct(repo);
        var handler = new UpdateProductCommandHandler(repo, new FakeUow(), new FakeUser());

        var result = await handler.Handle(new UpdateProductCommand(
            p.Id, "P1", null, "کالای ویرایش‌شده", null, null, 1,
            900m, 1200m, 0m, 0m, 5m, null, false, false, false,
            ValuationMethod.WeightedAverage, 9m, "توضیحِ جدید", null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("کالای ویرایش‌شده", p.Name);
        Assert.Equal(1200m, p.SalePrice);
        Assert.Equal(9m, p.TaxRate);
    }

    [Fact]
    public async Task Update_DuplicateCodeOfAnotherProduct_Fails()
    {
        var repo = new FakeProductRepo();
        MakeProduct(repo, "P1");
        var p2 = MakeProduct(repo, "P2");
        var handler = new UpdateProductCommandHandler(repo, new FakeUow(), new FakeUser());

        var result = await handler.Handle(new UpdateProductCommand(
            p2.Id, "P1", null, p2.Name, null, null, 1,
            800m, 1000m, 0m, 0m, 0m, null, false, false, false,
            ValuationMethod.WeightedAverage, 0m, null, null), CancellationToken.None);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Update_SameCodeAsSelf_Succeeds()
    {
        var repo = new FakeProductRepo();
        var p = MakeProduct(repo, "P1");
        var handler = new UpdateProductCommandHandler(repo, new FakeUow(), new FakeUser());

        var result = await handler.Handle(new UpdateProductCommand(
            p.Id, "P1", null, "نامِ به‌روزشده", null, null, 1,
            800m, 1000m, 0m, 0m, 0m, null, false, false, false,
            ValuationMethod.WeightedAverage, 0m, null, null), CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task GetById_ReturnsFullDetailForOwnedProduct()
    {
        var repo = new FakeProductRepo();
        var p = MakeProduct(repo, "P1", "12345");
        var handler = new GetProductByIdQueryHandler(repo, new FakeUser());

        var dto = await handler.Handle(new GetProductByIdQuery(p.Id), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("P1", dto!.Code);
        Assert.Equal("12345", dto.Barcode);
    }

    [Fact]
    public async Task GetById_UnknownId_ReturnsNull()
    {
        var repo = new FakeProductRepo();
        var handler = new GetProductByIdQueryHandler(repo, new FakeUser());

        var dto = await handler.Handle(new GetProductByIdQuery(999), CancellationToken.None);

        Assert.Null(dto);
    }
}
