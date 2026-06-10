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
    int SalesCount, decimal ExpectedCash, decimal CountedCash, decimal Variance);

public class CloseShiftCommandHandler : IRequestHandler<CloseShiftCommand, Result<ShiftSummaryDto>>
{
    private readonly IRepository<CashShift> _shifts;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public CloseShiftCommandHandler(IRepository<CashShift> shifts, IUnitOfWork uow, ICurrentUserService user)
    { _shifts = shifts; _uow = uow; _user = user; }

    public async Task<Result<ShiftSummaryDto>> Handle(CloseShiftCommand req, CancellationToken ct)
    {
        var userId = _user.UserId ?? 0;
        var shift = await _shifts.FindSingleAsync(s => s.UserId == userId && s.Status == 0, ct);
        if (shift is null) return Result<ShiftSummaryDto>.Failure("شیفت بازی برای بستن وجود ندارد.");
        try
        {
            shift.Close(req.CountedCash, req.Notes);
            _shifts.Update(shift);
            await _uow.SaveChangesAsync(ct);
            return Result<ShiftSummaryDto>.Success(new ShiftSummaryDto(shift.Id, shift.OpeningFloat,
                shift.CashSales, shift.CardSales, shift.SalesCount, shift.ExpectedCash, shift.CountedCash, shift.Variance));
        }
        catch (Exception ex) { return Result<ShiftSummaryDto>.Failure(ex.GetBaseException().Message); }
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
