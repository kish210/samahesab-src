using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.DiscountTiers;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کارِ ۷/U6 — حلِ تخفیفِ پلکانیِ مقداری: بهترین پله (بزرگ‌ترین MinQty ≤ مقدار).</summary>
public class DiscountTierTests
{
    private sealed class InMemoryRepo<T> : IRepository<T> where T : BaseEntity
    {
        private readonly List<T> _items = new(); private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default) { typeof(BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); _items.Add(e); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().FirstOrDefault(p));
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Where(p).ToList());
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_items.ToList());
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { _items.AddRange(e); return Task.CompletedTask; }
        public void Update(T e) { }
        public void Remove(T e) => _items.Remove(e);
        public void RemoveRange(IEnumerable<T> e) { foreach (var x in e) _items.Remove(x); }
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "admin"; public string? FullName => "Admin"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static async Task<ResolveQtyDiscountQueryHandler> BuildAsync()
    {
        var repo = new InMemoryRepo<ProductDiscountTier>();
        await repo.AddAsync(ProductDiscountTier.Create(1, productId: 5, minQty: 10, discountPercent: 5));
        await repo.AddAsync(ProductDiscountTier.Create(1, productId: 5, minQty: 50, discountPercent: 12));
        await repo.AddAsync(ProductDiscountTier.Create(1, productId: 5, minQty: 100, discountPercent: 20));
        return new ResolveQtyDiscountQueryHandler(repo, new FakeUser());
    }

    [Theory]
    [InlineData(3, 0)]      // زیر اولین پله
    [InlineData(10, 5)]     // دقیقاً پلهٔ اول
    [InlineData(49, 5)]     // بین پله‌ها → پلهٔ پایین‌تر
    [InlineData(50, 12)]    // پلهٔ دوم
    [InlineData(250, 20)]   // بالاترین پله
    public async Task Resolves_Best_Tier_For_Quantity(int qty, decimal expected)
    {
        var sut = await BuildAsync();
        var d = await sut.Handle(new ResolveQtyDiscountQuery(5, qty), default);
        Assert.Equal(expected, d);
    }

    [Fact]
    public async Task Unknown_Product_Has_No_Discount()
    {
        var sut = await BuildAsync();
        Assert.Equal(0, await sut.Handle(new ResolveQtyDiscountQuery(999, 100), default));
    }

    [Fact]
    public void Tier_Rejects_Invalid_Values()
    {
        Assert.Throws<ArgumentException>(() => ProductDiscountTier.Create(1, 5, minQty: 0, discountPercent: 5));
        Assert.Throws<ArgumentException>(() => ProductDiscountTier.Create(1, 5, minQty: 10, discountPercent: 150));
    }
}
