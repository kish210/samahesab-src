using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Support.Queries;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Support.Commands;

public record RemoteSessionDto(int Id, string Code, string StatusText, string? RequestedBy,
    DateTime? StartedAt, DateTime? EndedAt, DateTime ExpiresAt, string? LogPath);

/// <summary>🆘 HC-6 — تولیدِ کدِ پشتیبانیِ یک‌بارمصرف و ثبتِ نشست.</summary>
public record GenerateSupportCodeCommand(string? Note, int ValidMinutes = 60) : IRequest<Result<RemoteSessionDto>>;

public class GenerateSupportCodeCommandHandler : IRequestHandler<GenerateSupportCodeCommand, Result<RemoteSessionDto>>
{
    private readonly IRepository<RemoteSupportSession> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public GenerateSupportCodeCommandHandler(IRepository<RemoteSupportSession> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

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

        var session = RemoteSupportSession.Open(companyId, code, _user.FullName ?? _user.Username, req.Note, req.ValidMinutes);
        await _repo.AddAsync(session, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<RemoteSessionDto>.Success(Map(session));
    }

    internal static RemoteSessionDto Map(RemoteSupportSession s) =>
        new(s.Id, s.Code, SupportLabels.RemoteStatus(s.Status), s.RequestedBy, s.StartedAt, s.EndedAt, s.ExpiresAt, s.LogPath);
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
        return all.OrderByDescending(s => s.CreatedAt).Select(GenerateSupportCodeCommandHandler.Map).ToList();
    }
}
