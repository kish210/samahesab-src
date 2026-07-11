using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Commands;

/// <summary>
/// کار #۳۲ — مرجوعی فروش (Sales Return): کالا به انبار برمی‌گردد و سند حسابداریِ معکوسِ فروش
/// صادر می‌شود (بدهکار: درآمد فروش + مالیات، بستانکار: حساب‌های دریافتنی یا صندوق در صورت بازپرداخت نقدی).
/// Command مستقل است و مسیر فروش اصلی را تغییر نمی‌دهد.
/// </summary>
public record SalesReturnItemDto(int ProductId, decimal Quantity, decimal UnitPrice, decimal TaxPct);

public record CreateSalesReturnCommand(
    int BranchId, int FiscalYearId, string Date, int CustomerId, int WarehouseId,
    List<SalesReturnItemDto> Items, string? Description = null,
    bool RefundCash = false, int? OriginalInvoiceId = null) : IRequest<Result<int>>;

public class CreateSalesReturnCommandValidator : AbstractValidator<CreateSalesReturnCommand>
{
    public CreateSalesReturnCommandValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری الزامی است.");
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("مرجوعی باید حداقل یک ردیف داشته باشد.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(l => l.ProductId).GreaterThan(0);
            i.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("مقدار باید بزرگ‌تر از صفر باشد.");
        });
    }
}

public class CreateSalesReturnCommandHandler : IRequestHandler<CreateSalesReturnCommand, Result<int>>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IStockItemRepository _stock;
    private readonly IProductRepository _products;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<StockTransaction> _ledger;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public CreateSalesReturnCommandHandler(IRepository<SalesInvoice> invoices, IStockItemRepository stock,
        IProductRepository products, IAccountRepository accounts, IVoucherRepository vouchers,
        IRepository<StockTransaction> ledger, IUnitOfWork uow, ICurrentUserService user)
    { _invoices = invoices; _stock = stock; _products = products; _accounts = accounts; _vouchers = vouchers; _ledger = ledger; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(CreateSalesReturnCommand req, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var companyId = _user.CompanyId ?? 1;
            var count = await _invoices.CountAsync(i => i.CompanyId == companyId
                && i.FiscalYearId == req.FiscalYearId && i.InvoiceType == InvoiceType.SaleReturn, ct);
            var number = $"BR{(count + 1):D6}";

            var inv = SalesInvoice.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                req.CustomerId, req.WarehouseId, InvoiceType.SaleReturn, "خرده", null, null, req.Description);
            for (int i = 0; i < req.Items.Count; i++)
            {
                var d = req.Items[i];
                inv.AddItem(SalesInvoiceItem.Create(0, i + 1, d.ProductId, d.Quantity, d.UnitPrice, 0, d.TaxPct, null, null, null));
            }
            inv.SetShipping(0, 0);
            if (req.OriginalInvoiceId is int orig) inv.SetReturnedFrom(orig);
            await _invoices.AddAsync(inv, ct);
            await _uow.SaveChangesAsync(ct);

            // ── بازگرداندن کالا به انبار (فقط کالاهای انباری) ──
            decimal totalCost = 0;   // INV-1 گام۴ — بهایِ بازگشتیِ موجودی (برایِ معکوسِ COGS در سندِ زیر)
            foreach (var d in req.Items)
            {
                var product = await _products.GetByIdAsync(d.ProductId, ct);
                if (product == null || product.ProductType != ProductType.Product) continue;
                var stock = await _stock.GetByProductAndWarehouseAsync(d.ProductId, req.WarehouseId, ct);
                var isNew = stock is null;
                stock ??= StockItem.Create(d.ProductId, req.WarehouseId);
                var cost = stock.Quantity > 0 ? stock.AverageCost : d.UnitPrice;
                stock.AddStock(d.Quantity, cost);
                totalCost += cost * d.Quantity;
                if (isNew) await _stock.AddAsync(stock, ct); else _stock.Update(stock);

                await _ledger.AddAsync(StockTransaction.Create(companyId, req.BranchId, "برگشت از فروش", number,
                    req.Date, d.ProductId, req.WarehouseId, d.Quantity, cost, stock.Quantity,
                    stock.Quantity * stock.AverageCost, "SalesReturn", inv.Id, req.Description), ct);
            }

            // ── سند حسابداری معکوس فروش ──
            var receivable = await _accounts.GetByCodeAsync(companyId, "1-03-001", ct);
            var cash = await _accounts.GetByCodeAsync(companyId, "1-01-001", ct);
            var sales = await _accounts.GetByCodeAsync(companyId, "6-01-001", ct);
            var vat = await _accounts.GetByCodeAsync(companyId, "3-04-001", ct);
            var creditAccount = req.RefundCash ? cash : receivable;
            if (sales != null && creditAccount != null && inv.GrandTotal > 0)
            {
                var num = await _vouchers.GetNextNumberAsync(companyId, ct);
                var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, num, req.Date, 4 /*برگشت فروش*/,
                    $"سند خودکار مرجوعی فروش {number}", number);
                var salesAmount = inv.GrandTotal - inv.TotalTax;
                int row = 1;
                v.AddItem(VoucherItem.Create(0, row++, sales.Id, salesAmount, 0, "برگشت از فروش"));
                if (inv.TotalTax > 0 && vat != null)
                    v.AddItem(VoucherItem.Create(0, row++, vat.Id, inv.TotalTax, 0, "برگشت مالیات"));
                v.AddItem(VoucherItem.Create(0, row++, creditAccount.Id, 0, inv.GrandTotal,
                    req.RefundCash ? "بازپرداخت نقدی" : "کاهش بدهی مشتری"));

                // INV-1 گام۴ — معکوسِ COGS: کالا به انبار بازگشت ⇒ بد «موجودی کالا» / بس «بهای تمام‌شده».
                // پیش از این رفع، این مسیرِ برگشت (بر خلافِ مسیرِ توکارِ CreateSalesInvoiceCommand) هرگز
                // این دو ردیف را نمی‌زد — موجودیِ فیزیکی برمی‌گشت ولی ارزشِ آن در دفترِ کل اصلاح نمی‌شد.
                if (totalCost > 0)
                {
                    var cogsAcc = await _accounts.GetByCodeAsync(companyId, Inventory.Commands.InventoryAccounting.Cogs, ct);
                    var invAcc = await _accounts.GetByCodeAsync(companyId, Inventory.Commands.InventoryAccounting.Inventory, ct);
                    if (cogsAcc != null && invAcc != null)
                        foreach (var line in Inventory.PerpetualCogs.Build(totalCost, cogsAcc.Id, invAcc.Id, reverse: true))
                            v.AddItem(VoucherItem.Create(0, row++, line.AccountId, line.Debit, line.Credit, line.Description));
                }

                await _vouchers.AddAsync(v, ct);
                await _uow.SaveChangesAsync(ct);
                inv.SetVoucher(v.Id);
                _invoices.Update(inv);
            }

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(inv.Id);
        }
        catch (Exception ex) { await _uow.RollbackTransactionAsync(ct); return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}
