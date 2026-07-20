using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Commands;

/// <summary>
/// U-WEB-DEACTIVATE — غیرفعال/فعال‌سازیِ شخص (مشتری یا تأمین‌کننده، هر دو یک Party هستند).
/// حذفِ واقعی عمداً پیاده نشد: فاکتورها/چک‌هایِ تاریخی به همین رکورد ارجاع می‌دهند؛ حذفِ سخت
/// یا این ارجاع‌ها را می‌شکند یا باید Cascade بزند که یکپارچگیِ حسابداری را به خطر می‌اندازد.
/// `Party.Deactivate`/`Activate` از قبل در Domain بودند ولی هیچ Command/APIای صدایشان نمی‌زد.
/// </summary>
public record SetPartyActiveCommand(int Id, bool IsActive) : IRequest<Result<int>>;

public class SetPartyActiveCommandHandler : IRequestHandler<SetPartyActiveCommand, Result<int>>
{
    private readonly IRepository<Party> _parties;
    private readonly IUnitOfWork _uow;
    public SetPartyActiveCommandHandler(IRepository<Party> parties, IUnitOfWork uow)
    { _parties = parties; _uow = uow; }

    public async Task<Result<int>> Handle(SetPartyActiveCommand req, CancellationToken ct)
    {
        var p = await _parties.GetByIdAsync(req.Id, ct);
        if (p == null) return Result<int>.Failure("شخص یافت نشد.");

        if (req.IsActive) p.Activate(); else p.Deactivate();
        _parties.Update(p);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(p.Id);
    }
}
