using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Accounting;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-ACCT-1.7 — پیش‌تر ۱۰ نقطهٔ مختلفِ کد (چک/انبار/فروش/رستوران/الگویِ سند) به‌جایِ
/// خواندنِ سالِ مالیِ فعالِ واقعی، مستقیماً FiscalYearId=۱ را هاردکد می‌زدند؛ برایِ هر شرکتی که
/// سالِ مالیِ فعالش Id≠۱ باشد سند به سالِ مالیِ اشتباه/بسته متصل می‌شد. این منبعِ واحد جایگزینِ
/// همهٔ آن هاردکدهاست.</summary>
public class FiscalYearResolverTests
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

    [Fact]
    public async Task Returns_The_Active_FiscalYear_Even_When_Its_Id_Is_Not_1()
    {
        var repo = new FakeRepo<FiscalYear>();
        await repo.AddAsync(FiscalYear.Create(1, "۱۴۰۴", "1404/01/01", "1404/12/29"));   // Id=1، فعال
        var active = FiscalYear.Create(1, "۱۴۰۵", "1405/01/01", "1405/12/29");
        await repo.AddAsync(active);                                                      // Id=2، فعال هم هست

        // شبیه‌سازیِ «سالِ مالیِ قبلی بسته شده، جدید فعال است» — هر دو IsActive پیش‌فرض true‌اند؛
        // در دنیایِ واقعی فقط یکی فعال می‌ماند، پس اولیِ را غیرفعال می‌کنیم.
        repo.Items[0].Deactivate();

        var id = await FiscalYearResolver.ResolveActiveIdAsync(repo, companyId: 1);

        Assert.Equal(2, active.Id);   // اطمینان از اینکه این تست واقعاً Idِ غیرِ۱ را می‌سنجد
        Assert.Equal(active.Id, id);
    }

    [Fact]
    public async Task Falls_Back_To_Latest_By_StartDate_When_None_Is_Active()
    {
        var repo = new FakeRepo<FiscalYear>();
        var old = FiscalYear.Create(1, "۱۴۰۳", "1403/01/01", "1403/12/29");
        var newer = FiscalYear.Create(1, "۱۴۰۴", "1404/01/01", "1404/12/29");
        await repo.AddAsync(old);
        await repo.AddAsync(newer);
        foreach (var fy in repo.Items) fy.Deactivate();   // هیچ‌کدام فعال نیست

        var id = await FiscalYearResolver.ResolveActiveIdAsync(repo, companyId: 1);

        Assert.Equal(newer.Id, id);
    }

    [Fact]
    public async Task Falls_Back_To_1_When_Company_Has_No_FiscalYear_At_All()
    {
        var repo = new FakeRepo<FiscalYear>();

        var id = await FiscalYearResolver.ResolveActiveIdAsync(repo, companyId: 1);

        Assert.Equal(1, id);
    }

    [Fact]
    public async Task Ignores_Other_Companies_FiscalYears()
    {
        var repo = new FakeRepo<FiscalYear>();
        await repo.AddAsync(FiscalYear.Create(2, "شرکتِ دیگر", "1405/01/01", "1405/12/29"));

        var id = await FiscalYearResolver.ResolveActiveIdAsync(repo, companyId: 1);

        Assert.Equal(1, id);   // چیزی برایِ شرکتِ ۱ نبود → fallback
    }
}
