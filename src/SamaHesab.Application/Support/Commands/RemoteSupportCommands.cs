using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Support.Queries;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Support.Commands;

public record RemoteSessionDto(int Id, string Code, string StatusText, string? RequestedBy,
    DateTime? StartedAt, DateTime? EndedAt, DateTime ExpiresAt, string? LogPath,
    string? ConnectId, string SyncText, string Message);

/// <summary>
/// 🆘 HC-6b — تولیدِ کدِ پشتیبانیِ یک‌بارمصرف + ثبتِ نشست + ارسال به سرورِ vendor (وردپرس)
/// تا کارشناس درخواست را ببیند. <paramref name="ConnectId"/> = شناسهٔ RustDesکِ مشتری برای اتصال.
/// </summary>
public record GenerateSupportCodeCommand(string? Note, string? ConnectId = null,
    int ValidMinutes = 60, string? DiagnosticsJson = null) : IRequest<Result<RemoteSessionDto>>;

public class GenerateSupportCodeCommandHandler : IRequestHandler<GenerateSupportCodeCommand, Result<RemoteSessionDto>>
{
    /// <summary>ابزارِ ریموتِ پیش‌فرض (انتخابِ کاربر: RustDesk — متن‌باز و قابلِ self-host).</summary>
    public const string Tool = "rustdesk";

    private readonly IRepository<RemoteSupportSession> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly ISupportApiClient _api;

    public GenerateSupportCodeCommandHandler(IRepository<RemoteSupportSession> repo, IUnitOfWork uow,
        ICurrentUserService user, ISupportApiClient api)
    { _repo = repo; _uow = uow; _user = user; _api = api; }

    /// <summary>کدِ خوانا: «SH-XXXX-NN» (Base36 + ۲ رقم).</summary>
    public static string NewCode()
    {
        var g = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var part = g.Substring(0, 4);
        var num = (Math.Abs(g.GetHashCode()) % 90) + 10;
        return $"SH-{part}-{num}";
    }

    public async Task<Result<RemoteSessionDto>> Handle(GenerateSupportCodeCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        string code;
        do { code = NewCode(); }
        while (await _repo.AnyAsync(s => s.CompanyId == companyId && s.Code == code, ct));

        var requestedBy = _user.FullName ?? _user.Username;
        var session = RemoteSupportSession.Open(companyId, code, requestedBy, req.Note, req.ValidMinutes, req.ConnectId);

        var message = "کدِ پشتیبانی ساخته شد. آن را (و شناسهٔ RustDesk) به کارشناس بدهید.";
        if (_api.IsConfigured)
        {
            var sent = await _api.SubmitRemoteSessionAsync(
                new RemoteSessionSubmitDto(code, requestedBy, req.Note, Tool, req.ConnectId, req.DiagnosticsJson), ct);
            if (sent.Succeeded && sent.Value is { Length: > 0 })
            {
                session.MarkSynced(sent.Value);
                message = "درخواست برای کارشناسِ پشتیبانی ارسال شد. کدِ پیگیری: " + sent.Value;
            }
            else session.MarkQueued();
        }
        else session.MarkQueued();

        await _repo.AddAsync(session, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<RemoteSessionDto>.Success(Map(session, message));
    }

    internal static RemoteSessionDto Map(RemoteSupportSession s, string? message = null) =>
        new(s.Id, s.Code, SupportLabels.RemoteStatus(s.Status), s.RequestedBy, s.StartedAt, s.EndedAt, s.ExpiresAt,
            s.LogPath, s.ConnectId, SupportLabels.Sync(s.Sync), message ?? "");
}

/// <summary>🆘 HC-6 — پایان‌دادن به نشستِ پشتیبانی (+ مسیرِ لاگِ اختیاری).</summary>
public record EndRemoteSessionCommand(int Id, string? LogPath) : IRequest<Result>;

public class EndRemoteSessionCommandHandler : IRequestHandler<EndRemoteSessionCommand, Result>
{
    private readonly IRepository<RemoteSupportSession> _repo;
    private readonly IUnitOfWork _uow;
    public EndRemoteSessionCommandHandler(IRepository<RemoteSupportSession> repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(EndRemoteSessionCommand req, CancellationToken ct)
    {
        var s = await _repo.GetByIdAsync(req.Id, ct);
        if (s is null) return Result.Failure("نشست یافت نشد.");
        s.End(req.LogPath);
        _repo.Update(s);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>🆘 HC-6 — فهرستِ نشست‌های پشتیبانیِ ریموت.</summary>
public record GetRemoteSessionsQuery : IRequest<IReadOnlyList<RemoteSessionDto>>;

public class GetRemoteSessionsQueryHandler : IRequestHandler<GetRemoteSessionsQuery, IReadOnlyList<RemoteSessionDto>>
{
    private readonly IRepository<RemoteSupportSession> _repo;
    public GetRemoteSessionsQueryHandler(IRepository<RemoteSupportSession> repo) => _repo = repo;

    public async Task<IReadOnlyList<RemoteSessionDto>> Handle(GetRemoteSessionsQuery req, CancellationToken ct)
    {
        var all = await _repo.GetAllAsync(ct);
        return all.OrderByDescending(s => s.CreatedAt).Select(s => GenerateSupportCodeCommandHandler.Map(s)).ToList();
    }
}
