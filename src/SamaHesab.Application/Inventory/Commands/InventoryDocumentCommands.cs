using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Commands;

// ─────────────────────────────────────────────────────────────────────────────
// رسید ورود انبار (Receiving) — افزایش موجودی + به‌روزرسانی بهای میانگین موزون
// ─────────────────────────────────────────────────────────────────────────────
public record ReceiveStockLine(int ProductId, decimal Quantity, decimal UnitCost);
public record ReceiveStockCommand(int WarehouseId, string Date, string? Description,
    List<ReceiveStockLine> Items) : IRequest<Result>;

public class ReceiveStockCommandValidator : AbstractValidator<ReceiveStockCommand>
{
    public ReceiveStockCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("رسید باید حداقل یک ردیف داشته باشد.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(l => l.ProductId).GreaterThan(0);
            i.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("مقدار باید بزرگ‌تر از صفر باشد.");
            i.RuleFor(l => l.UnitCost).GreaterThanOrEqualTo(0);
        });
    }
}

public class ReceiveStockCommandHandler : IRequestHandler<ReceiveStockCommand, Result>
{
    private readonly IStockItemRepository _stock;
    private readonly IRepository<StockTransaction> _ledger;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public ReceiveStockCommandHandler(IStockItemRepository stock, IRepository<StockTransaction> ledger,
        IUnitOfWork uow, ICurrentUserService user)
    { _stock = stock; _ledger = ledger; _uow = uow; _user = user; }

    public async Task<Result> Handle(ReceiveStockCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var branchId = _user.BranchId ?? 1;
        var doc = "R-" + DateTime.Now.ToString("yyMMddHHmmss");

        await _uow.BeginTransactionAsync(ct);
        try
        {
            foreach (var l in req.Items)
            {
                var stock = await _stock.GetByProductAndWarehouseAsync(l.ProductId, req.WarehouseId, ct);
                var isNew = stock is null;
                stock ??= StockItem.Create(l.ProductId, req.WarehouseId);
                stock.AddStock(l.Quantity, l.UnitCost);          // میانگین موزون
                if (isNew) await _stock.AddAsync(stock, ct); else _stock.Update(stock);

                await _ledger.AddAsync(StockTransaction.Create(
                    companyId, branchId, "رسید ورود", doc, req.Date, l.ProductId, req.WarehouseId,
                    l.Quantity, l.UnitCost, stock.Quantity, stock.Quantity * stock.AverageCost,
                    "Receive", null, req.Description), ct);
            }
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { await _uow.RollbackTransactionAsync(ct); return Result.Failure(ex.GetBaseException().Message); }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// حواله خروج انبار (Issuing) — کاهش موجودی به بهای میانگین
// ─────────────────────────────────────────────────────────────────────────────
public record IssueStockLine(int ProductId, decimal Quantity);
public record IssueStockCommand(int WarehouseId, string Date, string? Description,
    List<IssueStockLine> Items) : IRequest<Result>;

public class IssueStockCommandValidator : AbstractValidator<IssueStockCommand>
{
    public IssueStockCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("حواله باید حداقل یک ردیف داشته باشد.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(l => l.ProductId).GreaterThan(0);
            i.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("مقدار باید بزرگ‌تر از صفر باشد.");
        });
    }
}

public class IssueStockCommandHandler : IRequestHandler<IssueStockCommand, Result>
{
    private readonly IStockItemRepository _stock;
    private readonly IRepository<StockTransaction> _ledger;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public IssueStockCommandHandler(IStockItemRepository stock, IRepository<StockTransaction> ledger,
        IProductRepository products, IUnitOfWork uow, ICurrentUserService user)
    { _stock = stock; _ledger = ledger; _products = products; _uow = uow; _user = user; }

    public async Task<Result> Handle(IssueStockCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var branchId = _user.BranchId ?? 1;
        var doc = "I-" + DateTime.Now.ToString("yyMMddHHmmss");

        await _uow.BeginTransactionAsync(ct);
        try
        {
            foreach (var l in req.Items)
            {
                var stock = await _stock.GetByProductAndWarehouseAsync(l.ProductId, req.WarehouseId, ct);
                if (stock is null || stock.Quantity < l.Quantity)
                {
                    var p = await _products.GetByIdAsync(l.ProductId, ct);
                    return Result.Failure($"موجودی کافی برای «{p?.Name ?? ("#" + l.ProductId)}» نیست (موجودی: {stock?.Quantity ?? 0}).");
                }
                var unitCost = stock.AverageCost;
                stock.RemoveStock(l.Quantity);
                _stock.Update(stock);

                await _ledger.AddAsync(StockTransaction.Create(
                    companyId, branchId, "حواله خروج", doc, req.Date, l.ProductId, req.WarehouseId,
                    -l.Quantity, unitCost, stock.Quantity, stock.Quantity * stock.AverageCost,
                    "Issue", null, req.Description), ct);
            }
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { await _uow.RollbackTransactionAsync(ct); return Result.Failure(ex.GetBaseException().Message); }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// تعدیل موجودی (Stock Adjustment / نتیجه‌ی انبارگردانی) — تنظیم مقدار مطلق
// ─────────────────────────────────────────────────────────────────────────────
public record AdjustStockCommand(int WarehouseId, int ProductId, decimal NewQuantity,
    string Date, string? Description) : IRequest<Result>;

public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("کالا الزامی است.");
        RuleFor(x => x.NewQuantity).GreaterThanOrEqualTo(0).WithMessage("موجودی نمی‌تواند منفی باشد.");
    }
}

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Result>
{
    private readonly IStockItemRepository _stock;
    private readonly IRepository<StockTransaction> _ledger;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public AdjustStockCommandHandler(IStockItemRepository stock, IRepository<StockTransaction> ledger,
        IUnitOfWork uow, ICurrentUserService user)
    { _stock = stock; _ledger = ledger; _uow = uow; _user = user; }

    public async Task<Result> Handle(AdjustStockCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var branchId = _user.BranchId ?? 1;
        var doc = "ADJ-" + DateTime.Now.ToString("yyMMddHHmmss");

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var stock = await _stock.GetByProductAndWarehouseAsync(req.ProductId, req.WarehouseId, ct);
            var isNew = stock is null;
            stock ??= StockItem.Create(req.ProductId, req.WarehouseId);
            var delta = req.NewQuantity - stock.Quantity;
            var unitCost = stock.AverageCost;
            stock.Adjust(req.NewQuantity);
            if (isNew) await _stock.AddAsync(stock, ct); else _stock.Update(stock);

            await _ledger.AddAsync(StockTransaction.Create(
                companyId, branchId, "تعدیل انبار", doc, req.Date, req.ProductId, req.WarehouseId,
                delta, unitCost, stock.Quantity, stock.Quantity * stock.AverageCost,
                "Adjust", null, req.Description), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { await _uow.RollbackTransactionAsync(ct); return Result.Failure(ex.GetBaseException().Message); }
    }
}
