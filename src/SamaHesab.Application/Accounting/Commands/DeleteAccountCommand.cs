using MediatR;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// حذفِ حساب از نمودار حساب‌ها (هستهٔ ERP) — تنها وقتی نه تراکنش دارد نه زیرحساب.
/// در غیرِ این صورت با پیامِ روشن رد می‌شود (حفاظت از یکپارچگیِ دفترِ کل).
/// </summary>
public record DeleteAccountCommand(int Id) : IRequest<Result>;

public class DeleteAccountCommandHandler : IRequestHandler<DeleteAccountCommand, Result>
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;

    public DeleteAccountCommandHandler(IAccountRepository accounts, IUnitOfWork uow)
    { _accounts = accounts; _uow = uow; }

    public async Task<Result> Handle(DeleteAccountCommand req, CancellationToken ct)
    {
        var acc = await _accounts.GetByIdAsync(req.Id, ct);
        if (acc == null) return Result.Failure("حساب یافت نشد.");

        if (await _accounts.HasTransactionsAsync(req.Id, ct))
            return Result.Failure("این حساب دارای تراکنش (آرتیکلِ سند) است و قابل حذف نیست.");

        var children = await _accounts.GetChildrenAsync(req.Id, ct);
        if (children.Count > 0)
            return Result.Failure("این حساب دارای زیرحساب است؛ ابتدا زیرحساب‌ها را حذف کنید.");

        _accounts.Remove(acc);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
