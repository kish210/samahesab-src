using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>T17 — گزارشِ نقطهٔ سفارش (`GetReorderReportQuery`).</summary>
public class ReorderReportQueryTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default)
        { typeof(Domain.Common.BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault());
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Update(T e) { }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> e) { foreach (var x in e) Items.Remove(x); }
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "admin"; public string? FullName => "ادمین"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static Product NewProduct(string code, string name, decimal min, decimal? reorder, decimal? max)
    {
        var p = Product.Create(1, code, name, 1, 100, 80);
        p.SetStockLimits(min, max, reorder);
        return p;
    }

    private static async Task<(GetReorderReportQueryHandler h, FakeRepo<Product> pr, FakeRepo<StockItem> sr)> SutAsync()
    {
        var pr = new FakeRepo<Product>();
        var sr = new FakeRepo<StockItem>();
        var h = new GetReorderReportQueryHandler(pr, sr, new FakeUser());
        return await Task.FromResult((h, pr, sr));
    }

    private static async Task StockAsync(FakeRepo<StockItem> sr, int productId, decimal qty)
    {
        var s = StockItem.Create(productId, 1);
        s.Adjust(qty);
        await sr.AddAsync(s);
    }

    [Fact]
    public async Task Lists_Only_Items_At_Or_Below_Threshold_With_Shortage_And_Suggestion()
    {
        var (h, pr, sr) = await SutAsync();
        await pr.AddAsync(NewProduct("K1", "روغن", min: 3, reorder: 10, max: 30)); // آستانه ۱۰
        await pr.AddAsync(NewProduct("K2", "نمک", min: 5, reorder: 10, max: 40));  // کافی
        var oil = pr.Items[0].Id; var salt = pr.Items[1].Id;
        await StockAsync(sr, oil, 5);    // ۵ ≤ ۱۰ → نیازمند
        await StockAsync(sr, salt, 50);  // ۵۰ > ۱۰ → خارج

        var dto = await h.Handle(new GetReorderReportQuery(), default);

        Assert.Equal(1, dto.ItemCount);
        var row = dto.Rows.Single();
        Assert.Equal("K1", row.Code);
        Assert.Equal(10, row.Threshold);
        Assert.Equal(5, row.Shortage);        // ۱۰ − ۵
        Assert.Equal(25, row.SuggestedQty);   // ۳۰ − ۵
        Assert.Equal(25, dto.TotalSuggestedQty);
    }

    [Fact]
    public async Task Search_Filters_By_Code_Or_Name()
    {
        var (h, pr, sr) = await SutAsync();
        await pr.AddAsync(NewProduct("K1", "روغن", 3, 10, 30));
        await pr.AddAsync(NewProduct("K2", "برنج", 3, 10, 30));
        await StockAsync(sr, pr.Items[0].Id, 1);
        await StockAsync(sr, pr.Items[1].Id, 1);

        var dto = await h.Handle(new GetReorderReportQuery("برنج"), default);

        Assert.Single(dto.Rows);
        Assert.Equal("برنج", dto.Rows[0].Name);
    }

    [Fact]
    public async Task Product_With_No_Threshold_Is_Ignored()
    {
        var (h, pr, sr) = await SutAsync();
        await pr.AddAsync(NewProduct("K1", "بدون آستانه", min: 0, reorder: null, max: null));
        await StockAsync(sr, pr.Items[0].Id, 0);

        var dto = await h.Handle(new GetReorderReportQuery(), default);

        Assert.Empty(dto.Rows);
    }
}
