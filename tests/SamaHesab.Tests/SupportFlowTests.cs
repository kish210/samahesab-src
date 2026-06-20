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
using SamaHesab.Application.Support.Queries;
using SamaHesab.Domain.Common;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>🆘 HC-4 — تیکت/درخواستِ قابلیت/«درخواست‌های من».</summary>
public class SupportFlowTests
{
    private sealed class Repo<T> : IRepository<T> where T : BaseEntity
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

    private sealed class Uow : IUnitOfWork
    {
        public IRepository<T> GetRepository<T>() where T : class => throw new NotImplementedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task BeginTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task RollbackTransactionAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class User : ICurrentUserService
    {
        public int? UserId => 1; public int? CompanyId => 1; public int? BranchId => 1;
        public string? Username => "admin"; public string? FullName => "مدیر"; public bool IsAuthenticated => true;
        public bool HasPermission(string m, string f, string a) => true;
        public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
    }

    private sealed class OfflineApi : ISupportApiClient
    {
        public bool IsConfigured => false;
        public Task<Result<string>> SubmitBugAsync(BugSubmitDto d, CancellationToken ct = default) => Task.FromResult(Result<string>.Failure("x"));
        public Task<Result<string>> SubmitFeatureAsync(FeatureSubmitDto d, CancellationToken ct = default) => Task.FromResult(Result<string>.Failure("x"));
        public Task<Result<string>> SubmitTicketAsync(TicketSubmitDto d, CancellationToken ct = default) => Task.FromResult(Result<string>.Failure("x"));
        public Task<Result<IReadOnlyList<ReleaseDto>>> GetReleasesAsync(CancellationToken ct = default) => Task.FromResult(Result<IReadOnlyList<ReleaseDto>>.Failure("x"));
        public Task<Result<IReadOnlyList<ArticleDto>>> GetArticlesAsync(string? s, CancellationToken ct = default) => Task.FromResult(Result<IReadOnlyList<ArticleDto>>.Failure("x"));
        public Task<Result<RemoteStatusDto>> GetStatusAsync(string id, CancellationToken ct = default) => Task.FromResult(Result<RemoteStatusDto>.Failure("x"));
        public Task<Result<string>> SubmitRemoteSessionAsync(RemoteSessionSubmitDto d, CancellationToken ct = default) => Task.FromResult(Result<string>.Failure("x"));
    }

    [Fact]
    public async Task Create_Ticket_Offline_Is_Queued_With_First_Message()
    {
        var repo = new Repo<SupportTicket>();
        var sut = new CreateSupportTicketCommandHandler(repo, new Uow(), new User(), new OfflineApi());
        var res = await sut.Handle(new CreateSupportTicketCommand("نمی‌توانم وارد شوم", "خطای اتصال", SupportCategory.Security), default);

        Assert.True(res.Succeeded);
        Assert.Equal(SyncState.Queued, repo.Items[0].Sync);
        Assert.Single(repo.Items[0].Messages);
    }

    [Fact]
    public async Task AddMessage_Appends_And_Reopens_WaitingCustomer()
    {
        var repo = new Repo<SupportTicket>();
        var create = new CreateSupportTicketCommandHandler(repo, new Uow(), new User(), new OfflineApi());
        await create.Handle(new CreateSupportTicketCommand("س", "ب", SupportCategory.System), default);
        repo.Items[0].ChangeStatus(SupportStatus.WaitingCustomer);

        var add = new AddTicketMessageCommandHandler(repo, new Uow(), new User());
        var res = await add.Handle(new AddTicketMessageCommand(repo.Items[0].Id, "پاسخِ من"), default);

        Assert.True(res.Succeeded);
        Assert.Equal(2, repo.Items[0].Messages.Count);
        Assert.Equal(SupportStatus.Open, repo.Items[0].Status);
    }

    [Fact]
    public async Task Create_Feature_Offline_Is_Queued()
    {
        var repo = new Repo<FeatureRequest>();
        var sut = new CreateFeatureRequestCommandHandler(repo, new Uow(), new User(), new OfflineApi());
        var res = await sut.Handle(new CreateFeatureRequestCommand("ماژولِ گزارشِ سفارشی", "گزارش‌ساز", "صرفه‌جویی در زمان", FeaturePriority.High, null), default);

        Assert.True(res.Succeeded);
        Assert.Equal(SyncState.Queued, repo.Items[0].Sync);
    }

    [Fact]
    public async Task MyRequests_Aggregates_All_Kinds_Sorted_Desc()
    {
        var bugs = new Repo<BugReport>();
        var feats = new Repo<FeatureRequest>();
        var tickets = new Repo<SupportTicket>();
        await bugs.AddAsync(BugReport.Create(1, "باگ‌۱", "د", BugSeverity.Low, SupportCategory.Sales), default);
        await feats.AddAsync(FeatureRequest.Create(1, "قابلیت‌۱", "د"), default);
        await tickets.AddAsync(SupportTicket.Create(1, "تیکت‌۱", "ب", SupportCategory.System), default);

        var handler = new GetMyRequestsQueryHandler(bugs, feats, tickets);
        var list = await handler.Handle(new GetMyRequestsQuery(), default);

        Assert.Equal(3, list.Count);
        Assert.Contains(list, i => i.Kind == "باگ");
        Assert.Contains(list, i => i.Kind == "قابلیت");
        Assert.Contains(list, i => i.Kind == "تیکت");
        // مرتب‌سازی نزولیِ تاریخ: جدیدترین (تیکت) اول.
        Assert.True(list.Zip(list.Skip(1), (a, b) => a.CreatedAt >= b.CreatedAt).All(x => x));
    }

    // ── HC-5 ──
    [Fact]
    public async Task SyncReleases_Offline_Returns_Cached_Current_First()
    {
        var repo = new Repo<ReleaseNote>();
        await repo.AddAsync(ReleaseNote.Create(1, "r1", "2.4.0", "h", "b", "k", DateTime.Now.AddDays(-30), false), default);
        await repo.AddAsync(ReleaseNote.Create(1, "r2", "2.5.0", "h2", "b2", "k2", DateTime.Now, true), default);

        var sut = new SyncReleaseNotesCommandHandler(repo, new Uow(), new User(), new OfflineApi());
        var list = await sut.Handle(new SyncReleaseNotesCommand(), default);

        Assert.Equal(2, list.Count);
        Assert.True(list[0].IsCurrent);          // نسخهٔ فعلی اول
        Assert.Equal("2.5.0", list[0].Version);
    }

    [Fact]
    public async Task SyncArticles_Offline_Filters_Cache_By_Search()
    {
        var repo = new Repo<KnowledgeArticle>();
        await repo.AddAsync(KnowledgeArticle.Create(1, "a1", "تنظیمِ مالیات", SupportCategory.Accounting, "نرخِ مالیات", null, null, "article", DateTime.Now), default);
        await repo.AddAsync(KnowledgeArticle.Create(1, "a2", "افزودنِ کالا", SupportCategory.Inventory, "کالای نو", null, null, "guide", DateTime.Now), default);

        var sut = new SyncKnowledgeArticlesCommandHandler(repo, new Uow(), new User(), new OfflineApi());
        var list = await sut.Handle(new SyncKnowledgeArticlesCommand("مالیات"), default);

        Assert.Single(list);
        Assert.Equal("تنظیمِ مالیات", list[0].Title);
    }

    // ── HC-6 ──
    [Fact]
    public void RemoteCode_Has_Expected_Shape()
    {
        var code = GenerateSupportCodeCommandHandler.NewCode();
        Assert.Matches(@"^SH-[0-9A-Z]{4}-\d{2}$", code);
    }

    [Fact]
    public async Task Generate_And_End_Remote_Session()
    {
        var repo = new Repo<RemoteSupportSession>();
        var gen = new GenerateSupportCodeCommandHandler(repo, new Uow(), new User(), new OfflineApi());
        var res = await gen.Handle(new GenerateSupportCodeCommand("بررسیِ گزارش", "123456789", 30), default);

        Assert.True(res.Succeeded);
        Assert.Equal("در انتظار", res.Value!.StatusText);
        Assert.Equal("123456789", res.Value.ConnectId);   // HC-6b — شناسهٔ RustDesk حفظ شد
        Assert.Equal("در صفِ ارسال", res.Value.SyncText);  // آفلاین → صف
        Assert.Single(repo.Items);

        var end = new EndRemoteSessionCommandHandler(repo, new Uow());
        var endRes = await end.Handle(new EndRemoteSessionCommand(res.Value.Id, null), default);

        Assert.True(endRes.Succeeded);
        Assert.Equal(RemoteSessionStatus.Ended, repo.Items[0].Status);
        Assert.NotNull(repo.Items[0].EndedAt);
    }
}
