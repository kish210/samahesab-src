using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Infrastructure.Mediator;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// BUG-8 — اثباتِ قراردادِ ScopedMediator: هر عملیاتِ سطحِ بالا DI scopeِ مستقل می‌گیرد
/// (جداسازیِ DbContext در هم‌زمان‌ها)، ولی sub-commandِ تو‌در‌تو همان scope را بازاستفاده می‌کند
/// (تا تراکنش‌های تو‌در‌تو مثلِ فروش→بچ/سریال نشکنند).
/// </summary>
public class ScopedMediatorTests
{
    // یک سرویسِ Scoped که هویتِ scope را نشان می‌دهد (نمایندهٔ ApplicationDbContext per scope).
    private sealed class ScopeMarker { public int Id; }
    private static int _seq;

    // هندلرِ والد: شناسهٔ scope خود را ثبت می‌کند و سپس یک درخواستِ فرزند را از طریقِ IMediator می‌فرستد.
    public record Parent : IRequest<int>;
    public record Child : IRequest<int>;

    private sealed class ParentHandler : IRequestHandler<Parent, int>
    {
        private readonly ScopeMarker _marker; private readonly IMediator _mediator;
        public ParentHandler(ScopeMarker marker, IMediator mediator) { _marker = marker; _mediator = mediator; }
        public async Task<int> Handle(Parent r, CancellationToken ct)
        {
            Recorder.Parent.Add(_marker.Id);
            await _mediator.Send(new Child(), ct);   // nested send
            return _marker.Id;
        }
    }
    private sealed class ChildHandler : IRequestHandler<Child, int>
    {
        private readonly ScopeMarker _marker;
        public ChildHandler(ScopeMarker marker) { _marker = marker; }
        public Task<int> Handle(Child r, CancellationToken ct) { Recorder.Child.Add(_marker.Id); return Task.FromResult(_marker.Id); }
    }

    private static class Recorder
    {
        public static readonly ConcurrentBag<int> Parent = new();
        public static readonly ConcurrentBag<int> Child = new();
        public static void Reset() { Parent.Clear(); Child.Clear(); }
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new ScopeMarker { Id = Interlocked.Increment(ref _seq) });
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(ScopedMediatorTests).Assembly));
        // تایپِ بتنیِ Mediator تا ScopedMediator بدونِ حلقه آن را در هر scope resolve کند.
        services.AddTransient<Mediator>();
        // IMediatorِ بیرونی = ScopedMediator (همان چیزی که WPF تزریق می‌کند)
        services.AddSingleton<IMediator, ScopedMediator>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Nested_Send_Reuses_Same_Scope_As_Parent()
    {
        Recorder.Reset();
        var sp = BuildProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        await mediator.Send(new Parent());

        var parentScope = Assert.Single(Recorder.Parent);
        var childScope = Assert.Single(Recorder.Child);
        Assert.Equal(parentScope, childScope);   // والد و فرزند → یک scope/DbContext (تراکنش حفظ می‌شود)
    }

    [Fact]
    public async Task Concurrent_TopLevel_Operations_Get_Distinct_Scopes()
    {
        Recorder.Reset();
        var sp = BuildProvider();
        var mediator = sp.GetRequiredService<IMediator>();

        // هر عملیات روی جریانِ async مستقل (مثلِ فرمان‌های هم‌زمانِ WPF) تا ambient برای هرکدام null آغاز شود.
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => mediator.Send(new Parent()))));

        // ۸ عملیاتِ سطحِ بالا → ۸ scopeِ متمایز (هیچ DbContextِ مشترکی بین هم‌زمان‌ها)
        Assert.Equal(8, Recorder.Parent.Distinct().Count());
        // و هر فرزند با والدِ خودش هم‌scope است
        Assert.Equal(Recorder.Parent.OrderBy(x => x), Recorder.Child.OrderBy(x => x));
    }
}
