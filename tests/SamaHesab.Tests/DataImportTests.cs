using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Import;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ G4 — تستِ ورودِ دادهٔ مشتری/تأمین‌کننده از اکسل (نگاشت + idempotency + کدِ خودکار).</summary>
public class DataImportTests
{
    private sealed class InMemoryRepo<T> : IRepository<T> where T : BaseEntity
    {
        private readonly List<T> _items = new();
        private int _seq;
        public IReadOnlyList<T> Items => _items;
        public Task AddAsync(T e, CancellationToken ct = default)
        { typeof(BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); _items.Add(e); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(_items.FirstOrDefault(x => x.Id == id));
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().FirstOrDefault(p));
        public void Update(T e) { }
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Where(p).ToList());
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(_items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { _items.AddRange(e); return Task.CompletedTask; }
        public void Remove(T e) => _items.Remove(e);
        public void RemoveRange(IEnumerable<T> e) { foreach (var x in e) _items.Remove(x); }
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
        public string? Username => "admin"; public string? FullName => "Admin"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private static Dictionary<string, string> Row(params (string k, string v)[] cells)
        => cells.ToDictionary(c => c.k, c => c.v, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task Imports_Customers_And_Maps_Contact_Fields()
    {
        var repo = new InMemoryRepo<Customer>();
        var sut = new ImportCustomersCommandHandler(repo, new FakeUow(), new FakeUser());
        var rows = new List<IReadOnlyDictionary<string, string>>
        {
            Row(("کد", "C100"), ("نام", "علی"), ("نام خانوادگی", "احمدی"), ("موبایل", "0912"), ("شهر", "تهران")),
            Row(("نام شرکت", "پارس کالا")),   // بدونِ کد → کدِ خودکار؛ بدونِ نام → حقوقی
        };

        var res = await sut.Handle(new ImportCustomersCommand(rows), default);

        Assert.Equal(2, res.Imported);
        Assert.Equal(0, res.Failed);
        var ali = repo.Items.First(c => c.Code == "C100");
        Assert.Equal("علی", ali.FirstName);
        Assert.Equal("0912", ali.Mobile);
        Assert.Equal("تهران", ali.City);
        Assert.Contains(repo.Items, c => c.CompanyName == "پارس کالا" && c.CustomerType == "حقوقی");
    }

    [Fact]
    public async Task Skips_Duplicate_Codes_Idempotent()
    {
        var repo = new InMemoryRepo<Customer>();
        var sut = new ImportCustomersCommandHandler(repo, new FakeUow(), new FakeUser());
        var rows = new List<IReadOnlyDictionary<string, string>> { Row(("کد", "C1"), ("نام", "الف")) };

        await sut.Handle(new ImportCustomersCommand(rows), default);
        var second = await sut.Handle(new ImportCustomersCommand(rows), default);   // اجرای دوبارهٔ همان

        Assert.Equal(0, second.Imported);
        Assert.Equal(1, second.Skipped);
        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task Imports_Suppliers()
    {
        var repo = new InMemoryRepo<Supplier>();
        var sut = new ImportSuppliersCommandHandler(repo, new FakeUow(), new FakeUser());
        var rows = new List<IReadOnlyDictionary<string, string>>
        {
            Row(("کد", "S1"), ("نام شرکت", "تأمینِ البرز"), ("تلفن", "021")),
        };

        var res = await sut.Handle(new ImportSuppliersCommand(rows), default);

        Assert.Equal(1, res.Imported);
        Assert.Contains(repo.Items, s => s.Code == "S1" && s.Phone == "021");
    }

    private sealed class FakeUnitLookup : SamaHesab.Application.Common.Interfaces.IUnitLookup
    {
        public System.Collections.Generic.IReadOnlyDictionary<string, int> All()
            => new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase) { ["عدد"] = 1, ["کیلوگرم"] = 2 };
        public int? Resolve(string? name) => All().TryGetValue((name ?? "").Trim(), out var id) ? id : null;
        public int? DefaultUnitId() => 1;
    }

    [Fact]
    public async Task Imports_Products_With_Unit_And_Prices()
    {
        var repo = new InMemoryRepo<SamaHesab.Domain.Entities.Inventory.Product>();
        var sut = new ImportProductsCommandHandler(repo, new FakeUow(), new FakeUser(), new FakeUnitLookup());
        var rows = new List<IReadOnlyDictionary<string, string>>
        {
            Row(("کد", "K1"), ("نام", "خودکار"), ("واحد", "عدد"), ("قیمت فروش", "۱۲٬۵۰۰"), ("قیمت خرید", "9000")),
            Row(("نام", "برنج"), ("واحد", "کیلوگرم"), ("قیمت فروش", "85000")),   // بدونِ کد → خودکار
        };

        var res = await sut.Handle(new ImportProductsCommand(rows), default);

        Assert.Equal(2, res.Imported);
        Assert.Equal(0, res.Failed);
        var pen = repo.Items.First(p => p.Code == "K1");
        Assert.Equal("خودکار", pen.Name);
        Assert.Equal(1, pen.UnitId);
        Assert.Equal(12500m, pen.SalePrice);     // رقمِ فارسی + جداکنندهٔ هزار پارس شد
        Assert.Contains(repo.Items, p => p.Name == "برنج" && p.UnitId == 2);
    }
}
