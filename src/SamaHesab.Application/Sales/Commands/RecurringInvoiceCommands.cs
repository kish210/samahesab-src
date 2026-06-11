using FluentValidation;
using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Commands;

// ── DTO ──────────────────────────────────────────────────────────────────────
public record RecurringInvoiceLineDto(int ProductId, decimal Quantity, decimal UnitPrice, decimal TaxPct = 0);

// ── Save (create) ────────────────────────────────────────────────────────────
public record SaveRecurringInvoiceCommand(
    string Name, int CustomerId, int WarehouseId, int Frequency, string NextDate,
    List<RecurringInvoiceLineDto> Lines, string PriceLevel = "خرده", string? Description = null)
    : IRequest<Result<int>>;

public class SaveRecurringInvoiceCommandValidator : AbstractValidator<SaveRecurringInvoiceCommand>
{
    public SaveRecurringInvoiceCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام الزامی است.");
        RuleFor(x => x.NextDate).NotEmpty().WithMessage("تاریخ سررسید الزامی است.");
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری الزامی است.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("حداقل یک ردیف لازم است.");
    }
}

public class SaveRecurringInvoiceCommandHandler : IRequestHandler<SaveRecurringInvoiceCommand, Result<int>>
{
    private readonly IRepository<RecurringInvoice> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveRecurringInvoiceCommandHandler(IRepository<RecurringInvoice> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveRecurringInvoiceCommand req, CancellationToken ct)
    {
        var entity = RecurringInvoice.Create(_user.CompanyId ?? 1, _user.BranchId ?? 1,
            req.Name, req.CustomerId, req.WarehouseId, req.Frequency, req.NextDate, req.PriceLevel, req.Description);
        foreach (var l in req.Lines)
            entity.AddLine(l.ProductId, l.Quantity, l.UnitPrice, l.TaxPct);

        await _repo.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(entity.Id);
    }
}

// ── List ─────────────────────────────────────────────────────────────────────
public record GetRecurringInvoicesQuery() : IRequest<List<RecurringInvoiceDto>>;
public record RecurringInvoiceDto(int Id, string Name, int CustomerId, int Frequency,
    string NextDate, string? LastGeneratedDate, bool IsActive);

public class GetRecurringInvoicesQueryHandler : IRequestHandler<GetRecurringInvoicesQuery, List<RecurringInvoiceDto>>
{
    private readonly IRepository<RecurringInvoice> _repo;
    private readonly ICurrentUserService _user;
    public GetRecurringInvoicesQueryHandler(IRepository<RecurringInvoice> repo, ICurrentUserService user)
    { _repo = repo; _user = user; }

    public async Task<List<RecurringInvoiceDto>> Handle(GetRecurringInvoicesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = await _repo.FindAsync(r => r.CompanyId == companyId, ct);
        return list.OrderBy(r => r.NextDate)
            .Select(r => new RecurringInvoiceDto(r.Id, r.Name, r.CustomerId, r.Frequency,
                r.NextDate, r.LastGeneratedDate, r.IsActive))
            .ToList();
    }
}

// ── Generate due (catch-up) ──────────────────────────────────────────────────
public record GenerateDueRecurringInvoicesCommand(string Today) : IRequest<Result<RecurringInvoiceRunResult>>;
public record RecurringInvoiceRunResult(int Generated, List<int> InvoiceIds);

public class GenerateDueRecurringInvoicesCommandHandler
    : IRequestHandler<GenerateDueRecurringInvoicesCommand, Result<RecurringInvoiceRunResult>>
{
    private readonly IRepository<RecurringInvoice> _repo;
    private readonly IRepository<RecurringInvoiceLine> _lines;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    private const int MaxCatchUp = 24;   // سقف تولیدِ دوره‌های عقب‌افتاده برای هر تعریف

    public GenerateDueRecurringInvoicesCommandHandler(IRepository<RecurringInvoice> repo,
        IRepository<RecurringInvoiceLine> lines, IMediator mediator, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _lines = lines; _mediator = mediator; _uow = uow; _user = user; }

    public async Task<Result<RecurringInvoiceRunResult>> Handle(
        GenerateDueRecurringInvoicesCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var due = await _repo.FindAsync(
            r => r.CompanyId == companyId && r.IsActive, ct);

        var generatedIds = new List<int>();
        foreach (var def in due.Where(d => RecurrenceSchedule.IsDue(d.NextDate, req.Today)))
        {
            var lines = await _lines.FindAsync(l => l.RecurringInvoiceId == def.Id, ct);
            if (lines.Count == 0) continue;
            var items = lines.Select(l => new SalesInvoiceItemDto(
                l.ProductId, l.Quantity, l.UnitPrice, 0, l.TaxPct, null, null, null)).ToList();

            var guard = 0;
            while (RecurrenceSchedule.IsDue(def.NextDate, req.Today) && guard++ < MaxCatchUp)
            {
                var cmd = new CreateSalesInvoiceCommand(
                    def.BranchId, 1, def.NextDate, def.CustomerId, def.WarehouseId,
                    InvoiceType.Sale, def.PriceLevel, null, null,
                    $"تکرارشونده: {def.Name}", 0, 0, items, 0, 0, "نسیه");
                var r = await _mediator.Send(cmd, ct);
                if (!r.Succeeded) return Result<RecurringInvoiceRunResult>.Failure(r.ErrorMessage);

                generatedIds.Add(r.Value);
                var generatedDate = def.NextDate;
                def.MarkGenerated(generatedDate,
                    RecurrenceSchedule.NextAfter(def.NextDate, (RecurrenceFrequency)def.Frequency));
                await _uow.SaveChangesAsync(ct);
            }
        }

        return Result<RecurringInvoiceRunResult>.Success(
            new RecurringInvoiceRunResult(generatedIds.Count, generatedIds));
    }
}
