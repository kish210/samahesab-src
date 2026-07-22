using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Contracting.Domain;

namespace SamaHesab.Modules.Contracting.Application.Commands;

// ════════════════════════════════════════════════════════════════════════════
// U-WEB-CONTRACTING — ساخت/ویرایشِ پیمان (پیش‌تر هیچ Commandی برایِ این وجود نداشت،
// نه در وب و نه در دسکتاپ — فقط GetContractProjectsQuery برایِ خواندن بود).
// ════════════════════════════════════════════════════════════════════════════
public record SaveContractProjectCommand(
    int Id, string Code, string Title, int EmployerPartyId, ContractType ContractType, decimal ContractAmount,
    string StartDate, int DurationDays = 0, decimal AdvancePercent = 0, decimal RetentionPercent = 0,
    decimal InsuranceWithholdPercent = 0, decimal TaxWithholdPercent = 0, bool AdjustmentEnabled = false)
    : IRequest<Result<int>>;

public class SaveContractProjectCommandHandler : IRequestHandler<SaveContractProjectCommand, Result<int>>
{
    private readonly IRepository<ContractProject> _projects;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveContractProjectCommandHandler(IRepository<ContractProject> projects, IUnitOfWork uow, ICurrentUserService user)
    { _projects = projects; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveContractProjectCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        ContractProject p;
        try
        {
            if (req.Id > 0)
            {
                p = await _projects.FindSingleAsync(x => x.Id == req.Id && x.CompanyId == companyId, ct)
                    ?? throw new InvalidOperationException("پیمان یافت نشد.");
                p.Update(req.Title, req.ContractType, req.ContractAmount, req.StartDate, req.DurationDays,
                    req.AdvancePercent, req.RetentionPercent, req.InsuranceWithholdPercent, req.TaxWithholdPercent,
                    req.AdjustmentEnabled, p.ProjectDimensionId);
                _projects.Update(p);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(req.Code)) return Result<int>.Failure("کدِ پیمان الزامی است.");
                p = ContractProject.Create(companyId, req.Code, req.Title, req.EmployerPartyId, req.ContractType,
                    req.ContractAmount, req.StartDate, req.DurationDays, req.AdvancePercent, req.RetentionPercent,
                    req.InsuranceWithholdPercent, req.TaxWithholdPercent, req.AdjustmentEnabled);
                await _projects.AddAsync(p, ct);
            }
        }
        catch (ArgumentException ex) { return Result<int>.Failure(ex.Message); }
        catch (InvalidOperationException ex) { return Result<int>.Failure(ex.Message); }

        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(p.Id);
    }
}

// ── فهرستِ صورت‌وضعیت‌هایِ یک پیمان (برایِ نمایشِ وب/دسکتاپ) ──
public record ProgressStatementDto(
    int Id, int Number, StatementType Type, string Date, decimal GrossThisPeriod, decimal AdvanceRecovery,
    decimal Retention, decimal Insurance, decimal Tax, decimal Penalty, decimal Other, decimal NetPayable,
    StatementStatus Status, int? VoucherId);

public record GetProgressStatementsQuery(int ContractProjectId) : IRequest<List<ProgressStatementDto>>;

public class GetProgressStatementsQueryHandler : IRequestHandler<GetProgressStatementsQuery, List<ProgressStatementDto>>
{
    private readonly IRepository<ProgressStatement> _statements;
    private readonly ICurrentUserService _user;
    public GetProgressStatementsQueryHandler(IRepository<ProgressStatement> statements, ICurrentUserService user)
    { _statements = statements; _user = user; }

    public async Task<List<ProgressStatementDto>> Handle(GetProgressStatementsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = await _statements.FindAsync(
            s => s.CompanyId == companyId && s.ContractProjectId == req.ContractProjectId, ct);
        return list.OrderByDescending(s => s.Number)
            .Select(s => new ProgressStatementDto(s.Id, s.Number, s.Type, s.Date, s.GrossThisPeriod, s.AdvanceRecovery,
                s.Retention, s.Insurance, s.Tax, s.Penalty, s.Other, s.NetPayable, s.Status, s.VoucherId))
            .ToList();
    }
}

// ── فهرستِ پیش‌پرداخت‌هایِ یک پیمان ──
public record AdvancePaymentDto(int Id, decimal Amount, string Date, decimal RecoveredToDate, decimal Outstanding,
    string PaymentMethod, int? VoucherId, string? Note);

public record GetAdvancePaymentsQuery(int ContractProjectId) : IRequest<List<AdvancePaymentDto>>;

public class GetAdvancePaymentsQueryHandler : IRequestHandler<GetAdvancePaymentsQuery, List<AdvancePaymentDto>>
{
    private readonly IRepository<AdvancePayment> _advances;
    private readonly ICurrentUserService _user;
    public GetAdvancePaymentsQueryHandler(IRepository<AdvancePayment> advances, ICurrentUserService user)
    { _advances = advances; _user = user; }

    public async Task<List<AdvancePaymentDto>> Handle(GetAdvancePaymentsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = await _advances.FindAsync(
            a => a.CompanyId == companyId && a.ContractProjectId == req.ContractProjectId, ct);
        return list.OrderByDescending(a => a.Id)
            .Select(a => new AdvancePaymentDto(a.Id, a.Amount, a.Date, a.RecoveredToDate, a.Outstanding,
                a.PaymentMethod, a.VoucherId, a.Note))
            .ToList();
    }
}
