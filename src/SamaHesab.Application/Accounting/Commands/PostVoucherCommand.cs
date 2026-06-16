using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

public record PostVoucherCommand(int VoucherId) : IRequest<Result>;

public class PostVoucherCommandHandler : IRequestHandler<PostVoucherCommand, Result>
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<FiscalYear> _fiscalYears;

    public PostVoucherCommandHandler(IVoucherRepository voucherRepository,
        IUnitOfWork unitOfWork, ICurrentUserService currentUser, IRepository<FiscalYear> fiscalYears)
    {
        _voucherRepository = voucherRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _fiscalYears = fiscalYears;
    }

    public async Task<Result> Handle(PostVoucherCommand request, CancellationToken ct)
    {
        try
        {
            var voucher = await _voucherRepository.GetWithItemsAsync(request.VoucherId, ct);
            if (voucher == null) return Result.Failure("سند یافت نشد.");
            if (voucher.CompanyId != _currentUser.CompanyId)
                return Result.Failure("دسترسی غیرمجاز.");

            // قفل دوره: قطعی‌سازی در سال مالیِ بسته یا با تاریخِ خارج از بازه مجاز نیست.
            var fy = await _fiscalYears.GetByIdAsync(voucher.FiscalYearId, ct);
            var lockMsg = FiscalPeriodGuard.Check(fy, voucher.VoucherDate);
            if (lockMsg is not null) return Result.Failure(lockMsg);

            voucher.Post(_currentUser.UserId!.Value);
            _voucherRepository.Update(voucher);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
