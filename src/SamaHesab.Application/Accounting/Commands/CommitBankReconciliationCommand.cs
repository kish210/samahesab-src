using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// U-BANK-RECON-WEB — ثبتِ ماندگارِ ردیف‌های تطبیق‌شدهٔ مغایرت‌گیری بانکی در دیتابیس،
/// تا در مغایرت‌گیری‌های بعدی دوباره نمایش داده نشوند (جایگزینِ فایلِ محلیِ دسکتاپ).
/// </summary>
public record CommitBankReconciliationCommand(int BankAccountId, List<int> VoucherItemIds, string Date)
    : IRequest<Result<int>>;

public class CommitBankReconciliationCommandHandler : IRequestHandler<CommitBankReconciliationCommand, Result<int>>
{
    private readonly IRepository<BankReconciledItem> _reconciled;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public CommitBankReconciliationCommandHandler(IRepository<BankReconciledItem> reconciled,
        IUnitOfWork uow, ICurrentUserService user)
    { _reconciled = reconciled; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(CommitBankReconciliationCommand req, CancellationToken ct)
    {
        if (req.BankAccountId <= 0 || req.VoucherItemIds.Count == 0)
            return Result<int>.Failure("هیچ ردیف تطبیق‌شده‌ای برای ثبت وجود ندارد.");

        var companyId = _user.CompanyId ?? 1;
        var existing = await _reconciled.FindAsync(
            x => x.BankAccountId == req.BankAccountId && x.CompanyId == companyId, ct);
        var known = new HashSet<int>(existing.Select(x => x.VoucherItemId));

        var added = 0;
        foreach (var id in req.VoucherItemIds.Distinct())
        {
            if (id <= 0 || known.Contains(id)) continue;
            await _reconciled.AddAsync(
                BankReconciledItem.Create(companyId, req.BankAccountId, id, req.Date), ct);
            known.Add(id);
            added++;
        }

        if (added == 0)
            return Result<int>.Success(0);

        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(added);
    }
}
