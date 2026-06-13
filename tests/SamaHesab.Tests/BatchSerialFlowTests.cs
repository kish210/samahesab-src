using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// جریانِ اتصالِ بچ/سریال در ثبت خرید/فروش (INV-1 گام۳ — لِین C2).
/// هندلرها با یک ریپازیتوریِ درون‌حافظه آزمایش می‌شوند (بدون EF)؛ همان مسیری که
/// CreatePurchaseInvoiceCommand/CreateSalesInvoiceCommand آن را صدا می‌زنند.
/// </summary>
public class BatchSerialFlowTests
{
    // ── ریپازیتوریِ سادهٔ درون‌حافظه؛ Id را روی AddAsync با reflection می‌دهد (مثل EF). ──
    private sealed class InMemoryRepo<T> : IRepository<T> where T : BaseEntity
    {
        private readonly List<T> _items = new();
        private int _seq;
        public IReadOnlyList<T> Items => _items;

        public Task AddAsync(T entity, CancellationToken ct = default)
        {
            typeof(BaseEntity).GetProperty("Id")!.SetValue(entity, ++_seq);
            _items.Add(entity);
            return Task.CompletedTask;
        }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default)
            => Task.FromResult(_items.AsQueryable().FirstOrDefault(p));
        public void Update(T entity) { /* درون‌حافظه: همان شیء قبلاً تغییر کرده */ }

        // اعضای استفاده‌نشده در این آزمون:
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default)
            => Task.FromResult(_items.AsQueryable().Where(p).ToList());
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default)
            => Task.FromResult(_items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default)
            => Task.FromResult(_items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { _items.AddRange(e); return Task.CompletedTask; }
        public void Remove(T entity) => _items.Remove(entity);
        public void RemoveRange(IEnumerable<T> e) { foreach (var x in e) _items.Remove(x); }
    }

    // ────────────────────────── ReceiveBatch (مسیر خرید) ──────────────────────────

    [Fact]
    public async Task ReceiveBatch_Creates_New_When_None_Exists()
    {
        var repo = new InMemoryRepo<Batch>();
        var sut = new ReceiveBatchCommandHandler(repo);

        var res = await sut.Handle(new ReceiveBatchCommand(
            ProductId: 7, BatchNumber: "B-100", Quantity: 12, ExpiryDate: "1405/01/01", PurchasePrice: 5000), default);

        Assert.True(res.Succeeded);
        var b = Assert.Single(repo.Items);
        Assert.Equal("B-100", b.BatchNumber);
        Assert.Equal(12, b.Quantity);
    }

    [Fact]
    public async Task ReceiveBatch_Increases_Existing_Same_Number()
    {
        var repo = new InMemoryRepo<Batch>();
        await repo.AddAsync(Batch.Create(7, "B-100", quantity: 10));
        var sut = new ReceiveBatchCommandHandler(repo);

        var res = await sut.Handle(new ReceiveBatchCommand(7, "B-100", 5), default);

        Assert.True(res.Succeeded);
        var b = Assert.Single(repo.Items);   // بچ جدید ساخته نشد
        Assert.Equal(15, b.Quantity);
    }

    [Fact]
    public async Task ReceiveBatch_Empty_Number_Fails()
    {
        var sut = new ReceiveBatchCommandHandler(new InMemoryRepo<Batch>());
        var res = await sut.Handle(new ReceiveBatchCommand(7, "  ", 5), default);
        Assert.False(res.Succeeded);
    }

    // ────────────────────────── IssueBatch (مسیر فروش) ──────────────────────────

    [Fact]
    public async Task IssueBatch_Decreases_Quantity()
    {
        var repo = new InMemoryRepo<Batch>();
        await repo.AddAsync(Batch.Create(7, "B-100", quantity: 10));
        var id = repo.Items[0].Id;
        var sut = new IssueBatchCommandHandler(repo);

        var res = await sut.Handle(new IssueBatchCommand(id, 4), default);

        Assert.True(res.Succeeded);
        Assert.Equal(6, repo.Items[0].Quantity);
    }

    [Fact]
    public async Task IssueBatch_Beyond_Quantity_Fails_Gracefully()
    {
        var repo = new InMemoryRepo<Batch>();
        await repo.AddAsync(Batch.Create(7, "B-100", quantity: 3));
        var id = repo.Items[0].Id;
        var sut = new IssueBatchCommandHandler(repo);

        var res = await sut.Handle(new IssueBatchCommand(id, 5), default);

        Assert.False(res.Succeeded);           // خطا گرفته شد، throw نشد
        Assert.Equal(3, repo.Items[0].Quantity); // موجودی دست‌نخورده
    }

    [Fact]
    public async Task IssueBatch_Missing_Batch_Fails()
    {
        var sut = new IssueBatchCommandHandler(new InMemoryRepo<Batch>());
        var res = await sut.Handle(new IssueBatchCommand(999, 1), default);
        Assert.False(res.Succeeded);
    }

    // ────────────────────────── SellSerial (مسیر فروش) ──────────────────────────

    [Fact]
    public async Task SellSerial_Marks_Sold_With_Date()
    {
        var repo = new InMemoryRepo<Serial>();
        await repo.AddAsync(Serial.Create(7, "SN-1"));
        var id = repo.Items[0].Id;
        var sut = new SellSerialCommandHandler(repo);

        var res = await sut.Handle(new SellSerialCommand(id, "1404/06/01"), default);

        Assert.True(res.Succeeded);
        Assert.Equal(SerialStatus.Sold, repo.Items[0].Status);
        Assert.Equal("1404/06/01", repo.Items[0].SaleDate);
    }

    [Fact]
    public async Task SellSerial_Missing_Serial_Fails()
    {
        var sut = new SellSerialCommandHandler(new InMemoryRepo<Serial>());
        var res = await sut.Handle(new SellSerialCommand(999, "1404/06/01"), default);
        Assert.False(res.Succeeded);
    }
}
