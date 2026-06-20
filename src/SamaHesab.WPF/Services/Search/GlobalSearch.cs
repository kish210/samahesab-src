using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Application.Accounting.Queries;

namespace SamaHesab.WPF.Services.Search;

/// <summary>
/// CC-1 (UX_ROADMAP) — جست‌وجوی سراسری. یک نتیجه؛ NavKey/Param مستقیم به MainViewModel.NavigateTo داده می‌شود.
/// </summary>
public record GlobalSearchResult(string Group, string Icon, string Title, string Subtitle, string NavKey, object? Param = null);

/// <summary>
/// هر lane providerهای خود را register می‌کند: حساب/چک = C1 (pc)؛ فروش/کالا/مشتری/انبار = C2 (laptop).
/// </summary>
public interface IGlobalSearchProvider
{
    Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string term, CancellationToken ct);
}

public interface IGlobalSearchService
{
    Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string term, int perGroupCap, CancellationToken ct);
}

/// <summary>
/// providerها را تجمیع می‌کند. هر جست‌وجو در یک DI scopeِ مستقل اجرا می‌شود (DbContextِ جدا از صفحاتِ باز)
/// و providerها **ترتیبی** صدا زده می‌شوند تا DbContextِ مشترکِ همان scope هم‌زمان استفاده نشود.
/// </summary>
public sealed class GlobalSearchService : IGlobalSearchService
{
    private readonly IServiceScopeFactory _scopes;
    public GlobalSearchService(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string term, int perGroupCap, CancellationToken ct)
    {
        term = (term ?? string.Empty).Trim();
        if (term.Length < 2) return Array.Empty<GlobalSearchResult>();   // حداقل دو نویسه

        using var scope = _scopes.CreateScope();
        var results = new List<GlobalSearchResult>();
        foreach (var p in scope.ServiceProvider.GetServices<IGlobalSearchProvider>())
        {
            if (ct.IsCancellationRequested) break;
            try { results.AddRange((await p.SearchAsync(term, ct)).Take(perGroupCap)); }
            catch { /* یک providerِ خراب نباید کلِ جست‌وجو را بشکند */ }
        }
        return results;
    }
}

// ── providerهای lane pc (C1) — حساب/چک. (فروش/کالا/مشتری/انبار را C2 اضافه می‌کند.) ──

/// <summary>C1 — جست‌وجوی حساب‌های نمودار حساب‌ها بر اساسِ کد/نام.</summary>
public sealed class AccountsSearchProvider : IGlobalSearchProvider
{
    private readonly IMediator _mediator;
    public AccountsSearchProvider(IMediator mediator) => _mediator = mediator;

    public async Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var all = await _mediator.Send(new GetAccountsQuery(), ct);
        return all
            .Where(a => (a.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                     || (a.Code?.Contains(term) ?? false))
            .Select(a => new GlobalSearchResult("حساب‌ها", "📒", a.Name, $"کد {a.Code}", "ChartOfAccounts"))
            .ToList();
    }
}

/// <summary>C1 — جست‌وجوی چک بر اساسِ شماره/بانک.</summary>
public sealed class ChequesSearchProvider : IGlobalSearchProvider
{
    private readonly IMediator _mediator;
    public ChequesSearchProvider(IMediator mediator) => _mediator = mediator;

    public async Task<IReadOnlyList<GlobalSearchResult>> SearchAsync(string term, CancellationToken ct)
    {
        var all = await _mediator.Send(new GetChequesQuery(), ct);
        return all
            .Where(c => (c.ChequeNumber?.Contains(term) ?? false)
                     || (c.BankName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(c => new GlobalSearchResult("چک‌ها", "🧾", $"چک {c.ChequeNumber}", c.BankName, "ChequeBoard"))
            .ToList();
    }
}
