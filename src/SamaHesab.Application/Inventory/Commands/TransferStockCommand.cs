using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Commands;

public record TransferStockCommand(
    int FromWarehouseId,
    int ToWarehouseId,
    int ProductId,
    decimal Quantity,
    string Date,
    string? Description
) : IRequest<Result>;

public class TransferStockCommandValidator : AbstractValidator<TransferStockCommand>
{
    public TransferStockCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("کالا الزامی است.");
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("مقدار باید بزرگتر از صفر باشد.");
        RuleFor(x => x.FromWarehouseId).GreaterThan(0).WithMessage("انبار مبدأ الزامی است.");
        RuleFor(x => x.ToWarehouseId).GreaterThan(0).WithMessage("انبار مقصد الزامی است.");
        RuleFor(x => x).Must(x => x.FromWarehouseId != x.ToWarehouseId)
            .WithMessage("انبار مبدأ و مقصد نمی‌توانند یکسان باشند.");
    }
}

public class TransferStockCommandHandler : IRequestHandler<TransferStockCommand, Result>
{
    private readonly IStockItemRepository _stock;
    private readonly IRepository<StockTransaction> _ledger;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;

    public TransferStockCommandHandler(IStockItemRepository stock,
        IRepository<StockTransaction> ledger, IUnitOfWork uow, ICurrentUserService currentUser)
    { _stock = stock; _ledger = ledger; _uow = uow; _currentUser = currentUser; }

    public async Task<Result> Handle(TransferStockCommand req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var branchId = _currentUser.BranchId;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var source = await _stock.GetByProductAndWarehouseAsync(req.ProductId, req.FromWarehouseId, ct);
            if (source == null || source.Quantity < req.Quantity)
                return Result.Failure($"موجودی کافی در انبار مبدأ وجود ندارد (موجودی: {source?.Quantity ?? 0}).");

            var unitCost = source.AverageCost;

            // ── source: remove ──
            source.RemoveStock(req.Quantity);
            _stock.Update(source);

            // ── destination: add (create if missing) ──
            var dest = await _stock.GetByProductAndWarehouseAsync(req.ProductId, req.ToWarehouseId, ct);
            var destIsNew = dest == null;
            dest ??= StockItem.Create(req.ProductId, req.ToWarehouseId);
            dest.AddStock(req.Quantity, unitCost);
            if (destIsNew) await _stock.AddAsync(dest, ct);
            else _stock.Update(dest);

            // ── kardex ledger (two legs) ──
            await _ledger.AddAsync(StockTransaction.Create(
                companyId, branchId, "انتقال (خروج)", null, req.Date, req.ProductId, req.FromWarehouseId,
                -req.Quantity, unitCost, source.Quantity, source.Quantity * source.AverageCost,
                "Transfer", null, req.Description), ct);
            await _ledger.AddAsync(StockTransaction.Create(
                companyId, branchId, "انتقال (ورود)", null, req.Date, req.ProductId, req.ToWarehouseId,
                req.Quantity, unitCost, dest.Quantity, dest.Quantity * dest.AverageCost,
                "Transfer", null, req.Description), ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result.Failure(ex.GetBaseException().Message);
        }
    }
}
