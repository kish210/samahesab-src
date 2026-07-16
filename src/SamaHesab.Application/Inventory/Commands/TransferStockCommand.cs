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
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;
    private readonly IWarehouseRepository _warehouses;
    private readonly IRepository<Domain.Entities.Accounting.FiscalYear> _fiscalYears;

    public TransferStockCommandHandler(IStockItemRepository stock,
        IRepository<StockTransaction> ledger, IUnitOfWork uow, ICurrentUserService currentUser,
        IAccountRepository accounts, IVoucherRepository vouchers, IWarehouseRepository warehouses,
        IRepository<Domain.Entities.Accounting.FiscalYear> fiscalYears)
    {
        _stock = stock; _ledger = ledger; _uow = uow; _currentUser = currentUser;
        _accounts = accounts; _vouchers = vouchers; _warehouses = warehouses; _fiscalYears = fiscalYears;
    }

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

            // U-INV-ACCT-WH (backlog #7) — سندِ حسابداری فقط وقتی انبارهایِ مبدأ/مقصد حسابِ موجودیِ
            // GLِ متفاوت دارند (مثلاً هرکدام حسابِ اختصاصیِ خودشان). اگر هر دو هنوز به حسابِ مشترکِ
            // پیش‌فرضِ شرکت resolve شوند (رفتارِ پیش‌فرض/فعلیِ بدونِ تغییر)، سندی زده نمی‌شود —
            // چون از دیدِ GL چیزی جابه‌جا نشده (هر دو یک حساب‌اند).
            var amount = req.Quantity * unitCost;
            if (amount > 0)
            {
                var fromAcc = await InventoryAccounting.ResolveInventoryAccountAsync(_accounts, _warehouses, companyId, req.FromWarehouseId, ct);
                var toAcc = await InventoryAccounting.ResolveInventoryAccountAsync(_accounts, _warehouses, companyId, req.ToWarehouseId, ct);
                if (fromAcc != null && toAcc != null && fromAcc.Id != toAcc.Id)
                {
                    await InventoryAccounting.TryPostAsync(_vouchers, _fiscalYears, companyId, branchId ?? 1, req.Date,
                        toAcc, fromAcc, amount, $"انتقالِ انبار{(string.IsNullOrWhiteSpace(req.Description) ? "" : ": " + req.Description)}",
                        _currentUser.UserId ?? 0, ct);
                }
            }

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
