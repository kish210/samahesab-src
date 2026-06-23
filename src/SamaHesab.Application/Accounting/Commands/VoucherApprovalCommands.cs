using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// T22 — گردش‌کارِ تأییدِ سند (روی موتورِ خالصِ ApprovalWorkflow + گذارهای دامنهٔ Voucher).
/// ارسال: Draft→PendingApproval · تأیید: →Approved · رد: →Rejected · بازگشایی: Rejected→خارج از گردش‌کار.
/// کنترلِ مجوزِ تأیید/رد (Accounting.Voucher.Approve) و حسابرسی، توسطِ AuditBehavior انجام می‌شود.
/// </summary>
public record SubmitVoucherForApprovalCommand(int VoucherId) : IRequest<Result>;
public record ApproveVoucherCommand(int VoucherId) : IRequest<Result>;
public record RejectVoucherCommand(int VoucherId) : IRequest<Result>;
public record ReopenVoucherApprovalCommand(int VoucherId) : IRequest<Result>;

public class VoucherApprovalCommandHandlers :
    IRequestHandler<SubmitVoucherForApprovalCommand, Result>,
    IRequestHandler<ApproveVoucherCommand, Result>,
    IRequestHandler<RejectVoucherCommand, Result>,
    IRequestHandler<ReopenVoucherApprovalCommand, Result>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly IRepository<SamaHesab.Domain.Entities.Settings.CompanySetting> _companySettings;

    public VoucherApprovalCommandHandlers(IVoucherRepository vouchers, IUnitOfWork uow, ICurrentUserService user,
        IRepository<SamaHesab.Domain.Entities.Settings.CompanySetting> companySettings)
    { _vouchers = vouchers; _uow = uow; _user = user; _companySettings = companySettings; }

    public Task<Result> Handle(SubmitVoucherForApprovalCommand r, CancellationToken ct)
        => MutateAsync(r.VoucherId, v => v.SubmitForApproval(), ct);

    public async Task<Result> Handle(ApproveVoucherCommand r, CancellationToken ct)
    {
        // CR-SoD — تفکیکِ وظایف (اختیاری، از تنظیماتِ شرکت): ثبت‌کنندهٔ سند نمی‌تواند خودش آن را تأیید کند.
        var companyId = _user.CompanyId ?? 1;
        if (await Settings.CompanySettingsReader.GetBoolAsync(_companySettings, companyId,
                Settings.CompanySettingKeys.EnforceSoD, fallback: false, ct))
        {
            var v = await _vouchers.GetByIdAsync(r.VoucherId, ct);
            if (v is not null && v.CreatedByUserId is int creator && creator != 0 && creator == (_user.UserId ?? -1))
                return Result.Failure("تفکیکِ وظایف: ثبت‌کنندهٔ سند نمی‌تواند آن را تأیید کند.");
        }
        return await MutateAsync(r.VoucherId, v => v.ApproveBy(_user.UserId ?? 0), ct);
    }

    public Task<Result> Handle(RejectVoucherCommand r, CancellationToken ct)
        => MutateAsync(r.VoucherId, v => v.RejectApproval(), ct);

    public Task<Result> Handle(ReopenVoucherApprovalCommand r, CancellationToken ct)
        => MutateAsync(r.VoucherId, v => v.ReopenApproval(), ct);

    private async Task<Result> MutateAsync(int voucherId, Action<Domain.Entities.Accounting.Voucher> mutate, CancellationToken ct)
    {
        try
        {
            var voucher = await _vouchers.GetByIdAsync(voucherId, ct);
            if (voucher is null) return Result.Failure("سند یافت نشد.");
            if (voucher.CompanyId != (_user.CompanyId ?? voucher.CompanyId))
                return Result.Failure("دسترسی غیرمجاز.");

            mutate(voucher);
            _vouchers.Update(voucher);
            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.GetBaseException().Message);
        }
    }
}
