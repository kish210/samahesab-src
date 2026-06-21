using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Support;
using SamaHesab.Application.Support.Commands;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>🆘 HC-3 — هندلرِ ثبتِ گزارشِ باگ: ذخیرهٔ محلی + همگام‌سازی/صف بسته به پیکربندیِ سرور.</summary>
public class CreateBugReportTests
{
    private sealed class InMemoryRepo<T> : IRepository<T> where T : BaseEntity
    {
        public readonly List<T> Items = new();
        private int _seq;
        public Task AddAsync(T e, CancellationToken ct = default)
        { typeof(BaseEntity).GetProperty("Id")!.SetValue(e, ++_seq); Items.Add(e); return Task.CompletedTask; }
        public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<T?> FindSingleAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().FirstOrDefault(p));
        public void Update(T e) { }
        public Task<List<T>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(Items.ToList());
        public Task<List<T>> FindAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Where(p).ToList());
        public Task<bool> AnyAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Any(p));
        public Task<int> CountAsync(Expression<Func<T, bool>> p, CancellationToken ct = default) => Task.FromResult(Items.AsQueryable().Count(p));
        public Task AddRangeAsync(IEnumerable<T> e, CancellationToken ct = default) { Items.AddRange(e); return Task.CompletedTask; }
        public void Remove(T e) => Items.Remove(e);
        public void RemoveRange(IEnumerable<T> e) { foreach (var x in e) Items.Remove(x); }
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

    /// <summary>کلاینتِ ساختگی: <paramref name="configured"/>/<paramref name="ok"/> رفتارِ سرور را شبیه‌سازی می‌کند.</summary>
    private sealed class FakeApi : ISupportApiClient
    {
        private readonly bool _configured, _ok;
        public FakeApi(bool configured, bool ok) { _configured = configured; _ok = ok; }
        public bool IsConfigured => _configured;
        public Task<Result<string>> SubmitBugAsync(BugSubmitDto d, CancellationToken ct = default)
            => Task.FromResult(_ok ? Result<string>.Success("WP-555") : Result<string>.Failure("offline"));
        public Task<Result<string>> SubmitFeatureAsync(FeatureSubmitDto d, CancellationToken ct = default) => Task.FromResult(Result<string>.Failure("n/a"));
        public Task<Result<string>> SubmitTicketAsync(TicketSubmitDto d, CancellationToken ct = default) => Task.FromResult(Result<string>.Failure("n/a"));
        public Task<Result<IReadOnlyList<ReleaseDto>>> GetReleasesAsync(CancellationToken ct = default) => Task.FromResult(Result<IReadOnlyList<ReleaseDto>>.Failure("n/a"));
        public Task<Result<IReadOnlyList<ArticleDto>>> GetArticlesAsync(string? s, CancellationToken ct = default) => Task.FromResult(Result<IReadOnlyList<ArticleDto>>.Failure("n/a"));
        public Task<Result<RemoteStatusDto>> GetStatusAsync(string id, CancellationToken ct = default) => Task.FromResult(Result<RemoteStatusDto>.Failure("n/a"));
        public Task<Result<string>> SubmitRemoteSessionAsync(RemoteSessionSubmitDto d, CancellationToken ct = default) => Task.FromResult(Result<string>.Failure("n/a"));
        public Task<Result<InstallStatusDto>> RegisterInstallAsync(InstallInfo info, CancellationToken ct = default) => Task.FromResult(Result<InstallStatusDto>.Failure("n/a"));
    }

    private static CreateBugReportCommand Cmd() => new(
        "کرشِ چاپ", "هنگامِ چاپ بسته می‌شود", BugSeverity.High, SupportCategory.Sales,
        null, null, "۱) چاپ بزن", "{\"ErpVersion\":\"2.5\"}", "فاکتور فروش", null);

    [Fact]
    public async Task Offline_Saves_Locally_As_Queued()
    {
        var repo = new InMemoryRepo<BugReport>();
        var sut = new CreateBugReportCommandHandler(repo, new FakeUow(), new FakeUser(), new FakeApi(configured: false, ok: false));

        var res = await sut.Handle(Cmd(), default);

        Assert.True(res.Succeeded);
        Assert.Single(repo.Items);
        Assert.Equal(SyncState.Queued, repo.Items[0].Sync);
        Assert.Equal(SyncState.Queued, res.Value!.Sync);
    }

    [Fact]
    public async Task Configured_And_Online_Marks_Synced_With_RemoteId()
    {
        var repo = new InMemoryRepo<BugReport>();
        var sut = new CreateBugReportCommandHandler(repo, new FakeUow(), new FakeUser(), new FakeApi(configured: true, ok: true));

        var res = await sut.Handle(Cmd(), default);

        Assert.Equal(SyncState.Synced, repo.Items[0].Sync);
        Assert.Equal("WP-555", repo.Items[0].RemoteId);
        Assert.Contains("WP-555", res.Value!.Message);
    }

    [Fact]
    public async Task Configured_But_Server_Fails_Falls_Back_To_Queued()
    {
        var repo = new InMemoryRepo<BugReport>();
        var sut = new CreateBugReportCommandHandler(repo, new FakeUow(), new FakeUser(), new FakeApi(configured: true, ok: false));

        var res = await sut.Handle(Cmd(), default);

        Assert.Equal(SyncState.Queued, repo.Items[0].Sync);
        Assert.True(res.Succeeded);
    }

    [Fact]
    public async Task Empty_Title_Is_Rejected()
    {
        var sut = new CreateBugReportCommandHandler(new InMemoryRepo<BugReport>(), new FakeUow(), new FakeUser(), new FakeApi(false, false));
        var res = await sut.Handle(Cmd() with { Title = "  " }, default);
        Assert.False(res.Succeeded);
    }
}
