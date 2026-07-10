using FluentValidation;
using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Contracting.Domain;

namespace SamaHesab.Modules.Contracting.Application.Commands;

// ── Save (create) ────────────────────────────────────────────────────────────
public record SaveServiceContractCommand(
    string Title, int CustomerId, int WarehouseId, int ServiceProductId, decimal MonthlyAmount,
    int Frequency, string NextInvoiceDate, decimal TaxPct = 0, string PriceLevel = "خرده", string? Description = null)
    : IRequest<Result<int>>;

public class SaveServiceContractCommandValidator : AbstractValidator<SaveServiceContractCommand>
{
    public SaveServiceContractCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("عنوانِ قرارداد الزامی است.");
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری الزامی است.");
        RuleFor(x => x.ServiceProductId).GreaterThan(0).WithMessage("خدمتِ قرارداد الزامی است.");
        RuleFor(x => x.MonthlyAmount).GreaterThan(0).WithMessage("مبلغِ ماهانه باید بزرگ‌تر از صفر باشد.");
        RuleFor(x => x.NextInvoiceDate).NotEmpty().WithMessage("تاریخِ سررسید الزامی است.");
    }
}

public class SaveServiceContractCommandHandler : IRequestHandler<SaveServiceContractCommand, Result<int>>
{
    private readonly IRepository<ServiceContract> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveServiceContractCommandHandler(IRepository<ServiceContract> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveServiceContractCommand req, CancellationToken ct)
    {
        var entity = ServiceContract.Create(_user.CompanyId ?? 1, _user.BranchId ?? 1,
            req.Title, req.CustomerId, req.WarehouseId, req.ServiceProductId, req.MonthlyAmount,
            req.Frequency, req.NextInvoiceDate, req.TaxPct, req.PriceLevel, req.Description);

        await _repo.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(entity.Id);
    }
}

// ── List ─────────────────────────────────────────────────────────────────────
public record GetServiceContractsQuery() : IRequest<List<ServiceContractDto>>;
public record ServiceContractDto(int Id, string Title, int CustomerId, int Frequency,
    string NextInvoiceDate, string? LastGeneratedDate, bool IsActive, decimal MonthlyAmount, int PendingExtraItems);

public class GetServiceContractsQueryHandler : IRequestHandler<GetServiceContractsQuery, List<ServiceContractDto>>
{
    private readonly IRepository<ServiceContract> _repo;
    private readonly IRepository<ServiceContractExtraItem> _extras;
    private readonly ICurrentUserService _user;

    public GetServiceContractsQueryHandler(IRepository<ServiceContract> repo,
        IRepository<ServiceContractExtraItem> extras, ICurrentUserService user)
    { _repo = repo; _extras = extras; _user = user; }

    public async Task<List<ServiceContractDto>> Handle(GetServiceContractsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = await _repo.FindAsync(r => r.CompanyId == companyId, ct);
        var pendingByContract = (await _extras.FindAsync(e => e.CompanyId == companyId && !e.IsConsumed, ct))
            .GroupBy(e => e.ServiceContractId).ToDictionary(g => g.Key, g => g.Count());

        return list.OrderBy(r => r.NextInvoiceDate)
            .Select(r => new ServiceContractDto(r.Id, r.Title, r.CustomerId, r.Frequency,
                r.NextInvoiceDate, r.LastGeneratedDate, r.IsActive, r.MonthlyAmount,
                pendingByContract.GetValueOrDefault(r.Id, 0)))
            .ToList();
    }
}

// ── Add extra item (ad-hoc, یک‌باره برایِ فاکتورِ دورهٔ بعدی) ──────────────────
public record AddServiceContractExtraItemCommand(int ServiceContractId, int ProductId, decimal Quantity,
    decimal UnitPrice, decimal TaxPct = 0) : IRequest<Result>;

public class AddServiceContractExtraItemCommandHandler : IRequestHandler<AddServiceContractExtraItemCommand, Result>
{
    private readonly IRepository<ServiceContractExtraItem> _extras;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public AddServiceContractExtraItemCommandHandler(IRepository<ServiceContractExtraItem> extras,
        IUnitOfWork uow, ICurrentUserService user)
    { _extras = extras; _uow = uow; _user = user; }

    public async Task<Result> Handle(AddServiceContractExtraItemCommand req, CancellationToken ct)
    {
        var item = ServiceContractExtraItem.Create(_user.CompanyId ?? 1, req.ServiceContractId,
            req.ProductId, req.Quantity, req.UnitPrice, req.TaxPct);
        await _extras.AddAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── Terminate (فسخ/عدمِ تمدید) ────────────────────────────────────────────────
public record TerminateServiceContractCommand(int Id) : IRequest<Result>;

public class TerminateServiceContractCommandHandler : IRequestHandler<TerminateServiceContractCommand, Result>
{
    private readonly IRepository<ServiceContract> _repo;
    private readonly IUnitOfWork _uow;

    public TerminateServiceContractCommandHandler(IRepository<ServiceContract> repo, IUnitOfWork uow)
    { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(TerminateServiceContractCommand req, CancellationToken ct)
    {
        var entity = await _repo.GetByIdAsync(req.Id, ct);
        if (entity == null) return Result.Failure("قرارداد یافت نشد.");
        entity.Terminate();
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── Generate due (تولیدِ پیش‌فاکتورهایِ سررسیدشده + آیتم‌هایِ اضافیِ همان‌دوره) ──
public record GenerateDueServiceContractInvoicesCommand(string Today) : IRequest<Result<ServiceContractRunResult>>;
public record ServiceContractRunResult(int Generated, List<int> InvoiceIds);

public class GenerateDueServiceContractInvoicesCommandHandler
    : IRequestHandler<GenerateDueServiceContractInvoicesCommand, Result<ServiceContractRunResult>>
{
    private readonly IRepository<ServiceContract> _repo;
    private readonly IRepository<ServiceContractExtraItem> _extras;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    private const int MaxCatchUp = 24;   // سقفِ تولیدِ دوره‌هایِ عقب‌افتاده برایِ هر قرارداد

    public GenerateDueServiceContractInvoicesCommandHandler(IRepository<ServiceContract> repo,
        IRepository<ServiceContractExtraItem> extras, IMediator mediator, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _extras = extras; _mediator = mediator; _uow = uow; _user = user; }

    public async Task<Result<ServiceContractRunResult>> Handle(
        GenerateDueServiceContractInvoicesCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var due = await _repo.FindAsync(c => c.CompanyId == companyId && c.IsActive, ct);

        var generatedIds = new List<int>();
        foreach (var contract in due.Where(c => RecurrenceSchedule.IsDue(c.NextInvoiceDate, req.Today)))
        {
            var guard = 0;
            while (RecurrenceSchedule.IsDue(contract.NextInvoiceDate, req.Today) && guard++ < MaxCatchUp)
            {
                var items = new List<SalesInvoiceItemDto>
                {
                    new(contract.ServiceProductId, 1, contract.MonthlyAmount, 0, contract.TaxPct, contract.Title, null, null)
                };

                // آیتم‌هایِ اضافیِ مصرف‌نشدهٔ همین قرارداد را هم به همین فاکتور اضافه کن.
                var pendingExtras = (await _extras.FindAsync(
                    e => e.ServiceContractId == contract.Id && !e.IsConsumed, ct)).ToList();
                foreach (var extra in pendingExtras)
                    items.Add(new SalesInvoiceItemDto(extra.ProductId, extra.Quantity, extra.UnitPrice, 0, extra.TaxPct, null, null, null));

                var cmd = new CreateSalesInvoiceCommand(
                    contract.BranchId, 1, contract.NextInvoiceDate, contract.CustomerId, contract.WarehouseId,
                    InvoiceType.Sale, contract.PriceLevel, null, null,
                    $"قراردادِ خدماتی: {contract.Title}", 0, 0, items, 0, 0, "نسیه");
                var r = await _mediator.Send(cmd, ct);
                if (!r.Succeeded) return Result<ServiceContractRunResult>.Failure(r.ErrorMessage);

                generatedIds.Add(r.Value);
                foreach (var extra in pendingExtras) extra.MarkConsumed();

                var generatedDate = contract.NextInvoiceDate;
                contract.MarkGenerated(generatedDate,
                    RecurrenceSchedule.NextAfter(contract.NextInvoiceDate, (RecurrenceFrequency)contract.Frequency));
                await _uow.SaveChangesAsync(ct);
            }
        }

        return Result<ServiceContractRunResult>.Success(new ServiceContractRunResult(generatedIds.Count, generatedIds));
    }
}
