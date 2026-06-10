using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Commands;

// ── شروع انبارگردانی: snapshot موجودی سیستمی انبار ────────────────────────────
public record StartStockCountCommand(int WarehouseId, string Date) : IRequest<Result<int>>;

public class StartStockCountCommandValidator : AbstractValidator<StartStockCountCommand>
{
    public StartStockCountCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ الزامی است.");
    }
}

public class StartStockCountCommandHandler : IRequestHandler<StartStockCountCommand, Result<int>>
{
    private readonly IStockCountRepository _sessions;
    private readonly IStockItemRepository _stock;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public StartStockCountCommandHandler(IStockCountRepository sessions, IStockItemRepository stock,
        IProductRepository products, IUnitOfWork uow, ICurrentUserService user)
    { _sessions = sessions; _stock = stock; _products = products; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(StartStockCountCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;
            var session = StockCountSession.Create(companyId, _user.BranchId ?? 1, req.WarehouseId, req.Date);

            var items = await _stock.GetByWarehouseAsync(req.WarehouseId, ct);
            var names = (await _products.SearchAsync(companyId, string.Empty, ct))
                .ToDictionary(p => p.Id, p => p.Name);
            foreach (var s in items)
                session.AddLine(StockCountLine.Create(0, s.ProductId,
                    names.TryGetValue(s.ProductId, out var n) ? n : $"#{s.ProductId}", s.Quantity));

            await _sessions.AddAsync(session, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(session.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── ثبت تعداد شمرده‌شده‌ی یک ردیف ─────────────────────────────────────────────
public record SetStockCountLineCommand(int SessionId, int ProductId, decimal CountedQty) : IRequest<Result>;

public class SetStockCountLineCommandHandler : IRequestHandler<SetStockCountLineCommand, Result>
{
    private readonly IStockCountRepository _sessions;
    private readonly IUnitOfWork _uow;
    public SetStockCountLineCommandHandler(IStockCountRepository sessions, IUnitOfWork uow)
    { _sessions = sessions; _uow = uow; }

    public async Task<Result> Handle(SetStockCountLineCommand req, CancellationToken ct)
    {
        var session = await _sessions.GetWithLinesAsync(req.SessionId, ct);
        if (session is null) return Result.Failure("سند انبارگردانی یافت نشد.");
        if (session.IsPosted) return Result.Failure("سند نهایی‌شده قابل ویرایش نیست.");
        var line = session.Lines.FirstOrDefault(l => l.ProductId == req.ProductId);
        if (line is null) return Result.Failure("کالا در این سند انبارگردانی نیست.");
        try
        {
            line.SetCounted(req.CountedQty);
            _sessions.Update(session);
            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}

// ── نهایی‌سازی: تبدیل مغایرت‌ها به تعدیل موجودی ───────────────────────────────
public record PostStockCountCommand(int SessionId) : IRequest<Result<StockCountResult>>;
public record StockCountResult(int AdjustedItems, decimal TotalVariance);

public class PostStockCountCommandHandler : IRequestHandler<PostStockCountCommand, Result<StockCountResult>>
{
    private readonly IStockCountRepository _sessions;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;

    public PostStockCountCommandHandler(IStockCountRepository sessions, IMediator mediator, IUnitOfWork uow)
    { _sessions = sessions; _mediator = mediator; _uow = uow; }

    public async Task<Result<StockCountResult>> Handle(PostStockCountCommand req, CancellationToken ct)
    {
        var session = await _sessions.GetWithLinesAsync(req.SessionId, ct);
        if (session is null) return Result<StockCountResult>.Failure("سند انبارگردانی یافت نشد.");
        if (session.IsPosted) return Result<StockCountResult>.Failure("این سند قبلاً نهایی شده است.");

        try
        {
            var varianceLines = session.VarianceLines().ToList();
            decimal totalVar = 0;
            foreach (var l in varianceLines)
            {
                // تعدیل به مقدار شمرده‌شده (با سند حسابداری خودکار از مسیر موجود)
                var adj = await _mediator.Send(new AdjustStockCommand(
                    session.WarehouseId, l.ProductId, l.CountedQty, session.Date,
                    $"انبارگردانی #{session.Id}"), ct);
                if (!adj.Succeeded) return Result<StockCountResult>.Failure(adj.ErrorMessage);
                totalVar += l.Variance;
            }

            session.Post();
            _sessions.Update(session);
            await _uow.SaveChangesAsync(ct);
            return Result<StockCountResult>.Success(new StockCountResult(varianceLines.Count, totalVar));
        }
        catch (Exception ex) { return Result<StockCountResult>.Failure(ex.GetBaseException().Message); }
    }
}
