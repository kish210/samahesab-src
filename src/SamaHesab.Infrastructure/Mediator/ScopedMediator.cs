using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace SamaHesab.Infrastructure.Mediator;

/// <summary>
/// رفعِ BUG-8 (خطای «A second operation was started on this context instance…»).
///
/// در WPF — برخلافِ ASP.NET که هر درخواست یک DI scope دارد — یک IServiceProviderِ ریشه‌ای وجود دارد،
/// پس سرویس‌های Scoped مثلِ <c>ApplicationDbContext</c> در عملِ کلِ عمرِ برنامه مشترک می‌شوند و دو عملیاتِ
/// هم‌زمان روی یک DbContext تصادم می‌کنند.
///
/// این پوشش <b>هر Send/Publishِ سطحِ بالا را در یک DI scopeِ مستقل</b> اجرا می‌کند تا هر عملیات DbContextِ
/// خودش را بگیرد. برای آنکه sub-commandهای داخلِ یک تراکنش (مثلِ فروش که حوالهٔ بچ/سریال می‌فرستد و انتظار
/// دارد روی همان DbContext و تراکنش اجرا شوند) نشکنند، با <see cref="AsyncLocal{T}"/> تشخیص می‌دهد که آیا
/// داخلِ یک scopeِ فعال هستیم: اگر بله، همان scope بازاستفاده می‌شود؛ اگر نه، scopeِ نو ساخته می‌شود.
///
/// تنها در ترکیبِ WPF ثبت می‌شود (API از scope-per-request خود استفاده می‌کند).
/// </summary>
public sealed class ScopedMediator : IMediator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private static readonly AsyncLocal<IServiceProvider?> _ambient = new();

    public ScopedMediator(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    private async Task<T> RunAsync<T>(Func<IServiceProvider, Task<T>> op)
    {
        if (_ambient.Value is { } sp) return await op(sp);          // nested → همان scope/DbContext
        using var scope = _scopeFactory.CreateScope();
        _ambient.Value = scope.ServiceProvider;
        try { return await op(scope.ServiceProvider); }
        finally { _ambient.Value = null; }
    }

    private async Task RunAsync(Func<IServiceProvider, Task> op)
    {
        if (_ambient.Value is { } sp) { await op(sp); return; }
        using var scope = _scopeFactory.CreateScope();
        _ambient.Value = scope.ServiceProvider;
        try { await op(scope.ServiceProvider); }
        finally { _ambient.Value = null; }
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        => RunAsync(sp => sp.GetRequiredService<MediatR.Mediator>().Send(request, ct));

    public Task<object?> Send(object request, CancellationToken ct = default)
        => RunAsync(sp => sp.GetRequiredService<MediatR.Mediator>().Send(request, ct));

    public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
        => RunAsync(sp => sp.GetRequiredService<MediatR.Mediator>().Send(request, ct));

    public Task Publish(object notification, CancellationToken ct = default)
        => RunAsync(sp => sp.GetRequiredService<MediatR.Mediator>().Publish(notification, ct));

    public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
        where TNotification : INotification
        => RunAsync(sp => sp.GetRequiredService<MediatR.Mediator>().Publish(notification, ct));

    // CreateStream در این برنامه استفاده نمی‌شود؛ scope را تا پایانِ پیمایش باز نگه می‌داریم.
    public async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_ambient.Value is { } amb)
        {
            await foreach (var item in amb.GetRequiredService<MediatR.Mediator>().CreateStream(request, ct).WithCancellation(ct))
                yield return item;
            yield break;
        }
        using var scope = _scopeFactory.CreateScope();
        await foreach (var item in scope.ServiceProvider.GetRequiredService<MediatR.Mediator>().CreateStream(request, ct).WithCancellation(ct))
            yield return item;
    }

    public async IAsyncEnumerable<object?> CreateStream(
        object request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_ambient.Value is { } amb)
        {
            await foreach (var item in amb.GetRequiredService<MediatR.Mediator>().CreateStream(request, ct).WithCancellation(ct))
                yield return item;
            yield break;
        }
        using var scope = _scopeFactory.CreateScope();
        await foreach (var item in scope.ServiceProvider.GetRequiredService<MediatR.Mediator>().CreateStream(request, ct).WithCancellation(ct))
            yield return item;
    }
}
