using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// کپی سند (Copy Voucher): یک سند موجود را به‌عنوان «سند پیش‌نویس جدید» تکثیر می‌کند
/// تا کاربر فقط مبلغ/تاریخ را تغییر دهد و ثبت کند — کلید سرعت برای اسناد تکرارشونده.
/// سند جدید پیش‌نویس می‌ماند (قطعی نمی‌شود) تا قبل از ثبت قابل ویرایش باشد.
/// </summary>
public record CopyVoucherCommand(int SourceVoucherId, string Date, string? Description = null)
    : IRequest<Result<int>>;

public class CopyVoucherCommandHandler : IRequestHandler<CopyVoucherCommand, Result<int>>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public CopyVoucherCommandHandler(IVoucherRepository vouchers, IUnitOfWork uow, ICurrentUserService currentUser)
    { _vouchers = vouchers; _uow = uow; _currentUser = currentUser; }

    public async Task<Result<int>> Handle(CopyVoucherCommand req, CancellationToken ct)
    {
        var src = await _vouchers.GetWithItemsAsync(req.SourceVoucherId, ct);
        if (src is null) return Result<int>.Failure("سند مبدأ یافت نشد.");
        if (!src.Items.Any()) return Result<int>.Failure("سند مبدأ فاقد ردیف است.");

        try
        {
            var companyId = _currentUser.CompanyId ?? src.CompanyId;
            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var copy = Voucher.Create(companyId, src.BranchId, src.FiscalYearId, number, req.Date,
                src.VoucherTypeId, req.Description ?? src.Description, src.Reference);

            int row = 1;
            foreach (var i in src.Items.OrderBy(i => i.RowNumber))
                copy.AddItem(VoucherItem.Create(0, row++, i.AccountId, i.Debit, i.Credit,
                    i.Description, i.CurrencyId, i.ExchangeRate, i.CostCenterId, i.ProjectId));

            await _vouchers.AddAsync(copy, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(copy.Id);   // پیش‌نویس — کاربر ویرایش و ثبت می‌کند
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}
