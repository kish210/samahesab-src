using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Purchase.Queries;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>UX-CRM-SUPPLIER-2 — کارتِ ۳۶۰°ِ اشخاصِ چندوجهی: پیش‌تر کارت نمی‌دانست شخص تأمین‌کننده
/// هم هست یا نه («فاکتورِ جدید» همیشه فروش می‌ساخت، تبِ فاکتورهایِ خرید اصلاً نبود).</summary>
public class CustomerCardSupplierAwarenessTests
{
    private sealed class FakeRepo<T> : IRepository<T> where T : class
    {
        public readonly List<T> Items = new();
        private int _seq;
        private static void SetId(T e, int value)
        {
            var prop = typeof(T).GetProperty("Id");
            if (prop != null) prop.SetValue(e, System.Convert.ChangeType(value, prop.PropertyType));
        }
        public Task AddAsync(T e, CancellationToken ct = default)
        { SetId(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<T> es, CancellationToken ct = default)
        { foreach (var e in es) { SetId(e, ++_seq); Items.Add(e); } return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(e => (int)(typeof(T).GetProperty("Id")!.GetValue(e) ?? 0) == id));
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public void Update(T e) { }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> es) { foreach (var x in es.ToList()) Items.Remove(x); }
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "admin"; public string? FullName => "مدیر"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    [Fact]
    public async Task CustomerCardDto_Exposes_IsCustomer_And_IsSupplier_From_Party()
    {
        var parties = new FakeRepo<Party>();
        var cheques = new FakeRepo<Cheque>();
        var party = Party.Create(1, "P1", "حقیقی", "علی", "رضایی", null, isCustomer: true, isSupplier: true);
        await parties.AddAsync(party);
        var handler = new GetCustomerCardQueryHandler(parties, cheques);

        var dto = await handler.Handle(new GetCustomerCardQuery(party.Id), default);

        Assert.NotNull(dto);
        Assert.True(dto!.IsCustomer);
        Assert.True(dto.IsSupplier);
    }

    [Fact]
    public async Task CustomerCardDto_SupplierOnly_Party_Has_IsCustomer_False()
    {
        var parties = new FakeRepo<Party>();
        var cheques = new FakeRepo<Cheque>();
        var party = Party.Create(1, "S1", "حقوقی", null, null, "شرکتِ تأمین‌کننده", isCustomer: false, isSupplier: true);
        await parties.AddAsync(party);
        var handler = new GetCustomerCardQueryHandler(parties, cheques);

        var dto = await handler.Handle(new GetCustomerCardQuery(party.Id), default);

        Assert.NotNull(dto);
        Assert.False(dto!.IsCustomer);
        Assert.True(dto.IsSupplier);
    }

    [Fact]
    public async Task GetPurchaseInvoicesQuery_Filters_By_SupplierId_When_Given()
    {
        var invoices = new FakeRepo<PurchaseInvoice>();
        var suppliers = new FakeRepo<Party>();
        var s1 = Party.Create(1, "S1", "حقوقی", null, null, "تأمین‌کنندهٔ یک", isSupplier: true);
        var s2 = Party.Create(1, "S2", "حقوقی", null, null, "تأمین‌کنندهٔ دو", isSupplier: true);
        await suppliers.AddAsync(s1); await suppliers.AddAsync(s2);

        await invoices.AddAsync(PurchaseInvoice.Create(1, 1, 1, "PI-1", "1404/01/01", s1.Id, 1));
        await invoices.AddAsync(PurchaseInvoice.Create(1, 1, 1, "PI-2", "1404/01/02", s2.Id, 1));
        await invoices.AddAsync(PurchaseInvoice.Create(1, 1, 1, "PI-3", "1404/01/03", s1.Id, 1));

        var handler = new GetPurchaseInvoicesQueryHandler(invoices, suppliers, new FakeUser());

        var forS1 = await handler.Handle(new GetPurchaseInvoicesQuery(SupplierId: s1.Id), default);
        var all = await handler.Handle(new GetPurchaseInvoicesQuery(), default);

        Assert.Equal(2, forS1.Count);
        Assert.All(forS1, r => Assert.Contains(r.Number, new[] { "PI-1", "PI-3" }));
        Assert.Equal(3, all.Count);
    }
}
