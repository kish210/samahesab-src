using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// سند برگشتی (Reversing Entry): یک سند قطعی را با جابه‌جایی بدهکار/بستانکار خنثی می‌کند،
/// سند جدید را قطعی و به سند اصلی لینک می‌کند و سند اصلی را «برگشت‌خورده» علامت می‌زند.
/// </summary>
public record ReverseVoucherCommand(int VoucherId, string Date, string? Description = null)
    : IRequest<Result<int>>;

public class ReverseVoucherCommandHandler : IRequestHandler<ReverseVoucherCommand, Result<int>>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public ReverseVoucherCommandHandler(IVoucherRepository vouchers, IUnitOfWork uow, ICurrentUserService currentUser)
    { _vouchers = vouchers; _uow = uow; _currentUser = currentUser; }

    public async Task<Result<int>> Handle(ReverseVoucherCommand req, CancellationToken ct)
    {
        var original = await _vouchers.GetWithItemsAsync(req.VoucherId, ct);
        if (original is null) return Result<int>.Failure("سند یافت نشد.");
        if (!original.CanReverse())
            return Result<int>.Failure("فقط سند قطعی و برگشت‌نخورده قابل برگشت است.");
        if (!original.Items.Any())
            return Result<int>.Failure("سند فاقد ردیف است.");

        var companyId = _currentUser.CompanyId ?? original.CompanyId;
        var userId = _currentUser.UserId ?? 0;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var reversal = Voucher.Create(companyId, original.BranchId, original.FiscalYearId,
                number, req.Date, original.VoucherTypeId,
                req.Description ?? $"برگشت سند {original.VoucherNumber}", original.VoucherNumber);

            int row = 1;
            foreach (var i in original.Items.OrderBy(i => i.RowNumber))
                reversal.AddItem(VoucherItem.Create(0, row++, i.AccountId,
                    debit: i.Credit, credit: i.Debit,   // جابه‌جایی بدهکار/بستانکار
                    description: $"برگشت: {i.Description}"));

            reversal.SetAsReversalOf(original.Id);
            reversal.Post(userId);          // متوازن است (جابه‌جاشده) → قطعی می‌شود + رویداد
            await _vouchers.AddAsync(reversal, ct);

            original.MarkAsReversed();
            _vouchers.Update(original);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(reversal.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}
