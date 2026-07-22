using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Hotel.Domain;

namespace SamaHesab.Modules.Hotel.Application;

/// <summary>U-WEB-HOTEL — فولیو (صورتحسابِ مهمان): شارژ/پرداخت + مانده.</summary>
public record FolioChargeDto(int Id, FolioChargeType Type, decimal Amount, string Description, string Date);
public record FolioPaymentDto(int Id, FolioPaymentMethod Method, decimal Amount, string Description, string Date);
public record FolioDto(int Id, int ReservationId, string OpenDate, string? CloseDate, FolioStatus Status,
    decimal TotalCharges, decimal TotalPayments, decimal AppliedDeposit, decimal Balance,
    List<FolioChargeDto> Charges, List<FolioPaymentDto> Payments);

public record GetFolioByReservationQuery(int ReservationId) : IRequest<FolioDto?>;

public class GetFolioByReservationQueryHandler : IRequestHandler<GetFolioByReservationQuery, FolioDto?>
{
    private readonly IFolioRepository _repo;
    private readonly ICurrentUserService _user;
    public GetFolioByReservationQueryHandler(IFolioRepository repo, ICurrentUserService user) { _repo = repo; _user = user; }

    public async Task<FolioDto?> Handle(GetFolioByReservationQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var f = await _repo.FindSingleWithLinesAsync(x => x.ReservationId == req.ReservationId && x.CompanyId == companyId, ct);
        return f is null ? null : Map(f);
    }

    internal static FolioDto Map(Folio f) => new(
        f.Id, f.ReservationId, f.OpenDate, f.CloseDate, f.Status,
        f.TotalCharges, f.TotalPayments, f.AppliedDeposit, f.Balance,
        f.Charges.Select(c => new FolioChargeDto(c.Id, c.Type, c.Amount, c.Description, c.Date)).ToList(),
        f.Payments.Select(p => new FolioPaymentDto(p.Id, p.Method, p.Amount, p.Description, p.Date)).ToList());
}

public record AddFolioChargeCommand(int FolioId, FolioChargeType Type, decimal Amount, string Description, string Date) : IRequest<Result>;

public class AddFolioChargeCommandHandler : IRequestHandler<AddFolioChargeCommand, Result>
{
    private readonly IFolioRepository _repo;
    private readonly IUnitOfWork _uow;
    public AddFolioChargeCommandHandler(IFolioRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(AddFolioChargeCommand r, CancellationToken ct)
    {
        try
        {
            var folio = await _repo.GetByIdAsync(r.FolioId, ct) ?? throw new InvalidOperationException("فولیو یافت نشد.");
            folio.AddCharge(r.Type, r.Amount, r.Description, r.Date);
            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}

public record AddFolioPaymentCommand(int FolioId, FolioPaymentMethod Method, decimal Amount, string Description, string Date) : IRequest<Result>;

public class AddFolioPaymentCommandHandler : IRequestHandler<AddFolioPaymentCommand, Result>
{
    private readonly IFolioRepository _repo;
    private readonly IUnitOfWork _uow;
    public AddFolioPaymentCommandHandler(IFolioRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(AddFolioPaymentCommand r, CancellationToken ct)
    {
        try
        {
            var folio = await _repo.GetByIdAsync(r.FolioId, ct) ?? throw new InvalidOperationException("فولیو یافت نشد.");
            folio.AddPayment(r.Method, r.Amount, r.Description, r.Date);
            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}
