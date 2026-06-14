using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.POS;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.POS;

// ── باز کردن شیفت ─────────────────────────────────────────────────────────────
public record OpenShiftCommand(decimal OpeningFloat) : IRequest<Result<int>>;

public class OpenShiftCommandHandler : IRequestHandler<OpenShiftCommand, Result<int>>
{
    private readonly IRepository<CashShift> _shifts;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public OpenShiftCommandHandler(IRepository<CashShift> shifts, IUnitOfWork uow, ICurrentUserService user)
    { _shifts = shifts; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(OpenShiftCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1; var userId = _user.UserId ?? 0;
        var existing = await _shifts.FindSingleAsync(s => s.UserId == userId && s.Status == 0, ct);
        if (existing != null) return Result<int>.Failure("یک شیفت باز برای این کاربر وجود دارد؛ ابتدا آن را ببندید.");
        try
        {
            var shift = CashShift.Open(companyId, _user.BranchId ?? 1, userId, req.OpeningFloat);
            await _shifts.AddAsync(shift, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(shift.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── ثبت فروش در شیفت جاری ──────────────────────────────────────────────────────
public record RecordShiftSaleCommand(decimal Amount, bool IsCash) : IRequest<Result>;

public class RecordShiftSaleCommandHandler : IRequestHandler<RecordShiftSaleCommand, Result>
{
    private readonly IRepository<CashShift> _shifts;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public RecordShiftSaleCommandHandler(IRepository<CashShift> shifts, IUnitOfWork uow, ICurrentUserService user)
    { _shifts = shifts; _uow = uow; _user = user; }

    public async Task<Result> Handle(RecordShiftSaleCommand req, CancellationToken ct)
    {
        var userId = _user.UserId ?? 0;
        var shift = await _shifts.FindSingleAsync(s => s.UserId == userId && s.Status == 0, ct);
        if (shift is null) return Result.Success();   // شیفتی باز نیست → بی‌صدا رد شو (فروش بدون شیفت مجاز است)
        try { shift.RecordSale(req.Amount, req.IsCash); _shifts.Update(shift); await _uow.SaveChangesAsync(ct); return Result.Success(); }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}

// ── بستن شیفت ─────────────────────────────────────────────────────────────────
public record CloseShiftCommand(decimal CountedCash, string? Notes = null) : IRequest<Result<ShiftSummaryDto>>;
public record ShiftSummaryDto(int Id, decimal OpeningFloat, decimal CashSales, decimal CardSales,
    int SalesCount, decimal ExpectedCash, decimal CountedCash, decimal Variance, int? VarianceVoucherId = null);

/// <summary>
/// T18 — بستنِ شیفت + سندِ حسابداریِ خودکارِ «مغایرتِ نقدیِ صندوق» (Z-report).
/// نکتهٔ مهم: فروشِ نقد/کارت از قبل per-فاکتور در `CreateSalesInvoiceCommand` سند خورده
/// (بد صندوق/بانک، بس فروش+مالیات) — پس اینجا دوباره ثبت نمی‌شود (جلوگیری از دوباره‌شماری).
/// تنها رویدادِ ثبت‌نشده = اختلافِ شمارش با موردانتظار است:
///   • کسری (counted &lt; expected): بد «کسریِ صندوق» (هزینه) / بس «صندوق».
///   • اضافه (counted &gt; expected): بد «صندوق» / بس «اضافاتِ صندوق» (درآمد).
/// </summary>
public class CloseShiftCommandHandler : IRequestHandler<CloseShiftCommand, Result<ShiftSummaryDto>>
{
    private const string CashCode = "1-01-001";      // صندوق
    private const string ShortageCode = "8-11-001";  // کسریِ صندوق (هزینه)
    private const string SurplusCode = "6-03-001";   // اضافاتِ صندوق (درآمد)
    private const int GeneralVoucherTypeId = 9;       // GEN/عمومی

    private readonly IRepository<CashShift> _shifts;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<Domain.Entities.Accounting.FiscalYear> _fiscalYears;
    private readonly IPersianCalendarService _calendar;

    public CloseShiftCommandHandler(IRepository<CashShift> shifts, IUnitOfWork uow, ICurrentUserService user,
        IAccountRepository accounts, IVoucherRepository vouchers,
        IRepository<Domain.Entities.Accounting.FiscalYear> fiscalYears, IPersianCalendarService calendar)
    {
        _shifts = shifts; _uow = uow; _user = user;
        _accounts = accounts; _vouchers = vouchers; _fiscalYears = fiscalYears; _calendar = calendar;
    }

    public async Task<Result<ShiftSummaryDto>> Handle(CloseShiftCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var userId = _user.UserId ?? 0;
        var shift = await _shifts.FindSingleAsync(s => s.UserId == userId && s.Status == 0, ct);
        if (shift is null) return Result<ShiftSummaryDto>.Failure("شیفت بازی برای بستن وجود ندارد.");

        await _uow.BeginTransactionAsync(ct);
        try
        {
            shift.Close(req.CountedCash, req.Notes);
            _shifts.Update(shift);

            var varVoucher = await TryCreateVarianceVoucherAsync(companyId, shift, ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<ShiftSummaryDto>.Success(new ShiftSummaryDto(shift.Id, shift.OpeningFloat,
                shift.CashSales, shift.CardSales, shift.SalesCount, shift.ExpectedCash, shift.CountedCash,
                shift.Variance, varVoucher?.Id));
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<ShiftSummaryDto>.Failure(ex.GetBaseException().Message);
        }
    }

    /// <summary>سندِ مغایرت را در صورتِ اختلاف می‌سازد؛ نبودِ سال مالی/حساب → بی‌سند (بدونِ شکست).</summary>
    private async Task<Domain.Entities.Accounting.Voucher?> TryCreateVarianceVoucherAsync(
        int companyId, CashShift shift, CancellationToken ct)
    {
        if (shift.Variance == 0) return null;

        var fy = await _fiscalYears.FindSingleAsync(f => f.CompanyId == companyId && f.IsActive && !f.IsClosed, ct);
        if (fy is null) return null;   // سال مالیِ فعالی نیست → از سند صرف‌نظر کن (بستنِ شیفت نباید بلاک شود)

        var cash = await _accounts.GetByCodeAsync(companyId, CashCode, ct);
        var amount = Math.Abs(shift.Variance);
        var isShortage = shift.Variance < 0;
        var other = await _accounts.GetByCodeAsync(companyId, isShortage ? ShortageCode : SurplusCode, ct);
        if (cash is null || other is null) return null;

        var date = _calendar.GetCurrentPersianDate();
        var number = await _vouchers.GetNextNumberAsync(companyId, ct);
        var v = Domain.Entities.Accounting.Voucher.Create(companyId, shift.BranchId, fy.Id, number, date,
            GeneralVoucherTypeId, $"مغایرتِ نقدیِ بستنِ شیفت #{shift.Id} ({(isShortage ? "کسری" : "اضافه")})",
            $"SHIFT-{shift.Id}");

        if (isShortage)
        {
            v.AddItem(Domain.Entities.Accounting.VoucherItem.Create(0, 1, other.Id, amount, 0, "کسریِ صندوق"));
            v.AddItem(Domain.Entities.Accounting.VoucherItem.Create(0, 2, cash.Id, 0, amount, "کاهشِ موجودیِ صندوق"));
        }
        else
        {
            v.AddItem(Domain.Entities.Accounting.VoucherItem.Create(0, 1, cash.Id, amount, 0, "افزایشِ موجودیِ صندوق"));
            v.AddItem(Domain.Entities.Accounting.VoucherItem.Create(0, 2, other.Id, 0, amount, "اضافاتِ صندوق"));
        }
        v.Post(_user.UserId ?? 0);
        await _vouchers.AddAsync(v, ct);
        return v;   // Id پس از SaveChangesِ فراخواننده مقداردهی می‌شود.
    }
}

// ── شیفت باز جاری (Z-report زنده) ──────────────────────────────────────────────
public record GetOpenShiftQuery() : IRequest<ShiftSummaryDto?>;

public class GetOpenShiftQueryHandler : IRequestHandler<GetOpenShiftQuery, ShiftSummaryDto?>
{
    private readonly IRepository<CashShift> _shifts;
    private readonly ICurrentUserService _user;
    public GetOpenShiftQueryHandler(IRepository<CashShift> shifts, ICurrentUserService user)
    { _shifts = shifts; _user = user; }

    public async Task<ShiftSummaryDto?> Handle(GetOpenShiftQuery req, CancellationToken ct)
    {
        var userId = _user.UserId ?? 0;
        var s = await _shifts.FindSingleAsync(x => x.UserId == userId && x.Status == 0, ct);
        if (s is null) return null;
        var expected = s.OpeningFloat + s.CashSales;
        return new ShiftSummaryDto(s.Id, s.OpeningFloat, s.CashSales, s.CardSales, s.SalesCount, expected, 0, 0);
    }
}
