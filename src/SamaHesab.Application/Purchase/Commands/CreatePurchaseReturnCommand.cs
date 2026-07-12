using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Commands;

/// <summary>
/// C2-C — مرجوعی خرید (Purchase Return): قرینهٔ مرجوعی فروش.
/// کالا از انبار خارج می‌شود (به تأمین‌کننده برمی‌گردد) و سندِ معکوسِ خرید صادر می‌شود
/// (بستانکار: موجودی کالا، بدهکار: حساب‌های پرداختنیِ تأمین‌کننده یا صندوق در صورت بازپرداخت نقدی).
/// Command مستقل است و مسیر خرید اصلی را تغییر نمی‌دهد.
/// </summary>
public record PurchaseReturnItemDto(int ProductId, decimal Quantity, decimal UnitPrice, decimal TaxPct);

public record CreatePurchaseReturnCommand(
    int BranchId, int FiscalYearId, string Date, int SupplierId, int WarehouseId,
    List<PurchaseReturnItemDto> Items, string? Description = null,
    bool RefundCash = false, int? OriginalInvoiceId = null) : IRequest<Result<int>>;

public class CreatePurchaseReturnCommandValidator : AbstractValidator<CreatePurchaseReturnCommand>
{
    public CreatePurchaseReturnCommandValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.SupplierId).GreaterThan(0).WithMessage("تأمین‌کننده الزامی است.");
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("مرجوعی باید حداقل یک ردیف داشته باشد.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(l => l.ProductId).GreaterThan(0);
            i.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("مقدار باید بزرگ‌تر از صفر باشد.");
        });
    }
}

public class CreatePurchaseReturnCommandHandler : IRequestHandler<CreatePurchaseReturnCommand, Result<int>>
{
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly IStockItemRepository _stock;
    private readonly IProductRepository _products;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<StockTransaction> _ledger;
    private readonly IRepository<SamaHesab.Domain.Entities.CRM.Party> _suppliers;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public CreatePurchaseReturnCommandHandler(IRepository<PurchaseInvoice> invoices, IStockItemRepository stock,
        IProductRepository products, IAccountRepository accounts, IVoucherRepository vouchers,
        IRepository<StockTransaction> ledger, IRepository<SamaHesab.Domain.Entities.CRM.Party> suppliers,
        IUnitOfWork uow, ICurrentUserService user)
    { _invoices = invoices; _stock = stock; _products = products; _accounts = accounts; _vouchers = vouchers; _ledger = ledger; _suppliers = suppliers; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(CreatePurchaseReturnCommand req, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var companyId = _user.CompanyId ?? 1;
            const string returnType = "برگشت خرید";
            var count = await _invoices.CountAsync(i => i.CompanyId == companyId
                && i.FiscalYearId == req.FiscalYearId && i.InvoiceType == returnType, ct);
            var number = $"PR{(count + 1):D6}";

            var inv = PurchaseInvoice.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                req.SupplierId, req.WarehouseId, returnType, null, null, req.Description);
            for (int i = 0; i < req.Items.Count; i++)
            {
                var d = req.Items[i];
                inv.AddItem(PurchaseInvoiceItem.Create(0, i + 1, d.ProductId, d.Quantity, d.UnitPrice, 0, d.TaxPct, null));
            }
            inv.SetShipping(0, 0);
            if (req.OriginalInvoiceId is int orig) inv.SetReturnedFrom(orig);
            await _invoices.AddAsync(inv, ct);
            await _uow.SaveChangesAsync(ct);

            // ── خروجِ کالا از انبار (بازگشت به تأمین‌کننده؛ فقط کالاهای انباری) ──
            foreach (var d in req.Items)
            {
                var product = await _products.GetByIdAsync(d.ProductId, ct);
                if (product == null || product.ProductType != Domain.Enums.ProductType.Product) continue;

                var stock = await _stock.GetByProductAndWarehouseAsync(d.ProductId, req.WarehouseId, ct);
                if (stock == null || stock.Quantity < d.Quantity)
                    throw new InvalidOperationException(
                        $"موجودی کافی برای «{product.Name}» جهت مرجوعی به تأمین‌کننده وجود ندارد (موجودی: {stock?.Quantity ?? 0}).");

                var unitCost = stock.AverageCost;
                stock.RemoveStock(d.Quantity);
                _stock.Update(stock);

                await _ledger.AddAsync(StockTransaction.Create(companyId, req.BranchId, "برگشت خرید", number,
                    req.Date, d.ProductId, req.WarehouseId, -d.Quantity, unitCost, stock.Quantity,
                    stock.Quantity * stock.AverageCost, "PurchaseReturn", inv.Id, req.Description), ct);
            }

            // ── سند حسابداریِ معکوسِ خرید ──
            // خرید: بدهکار موجودی / بستانکار پرداختنی(یا نقد). مرجوعی: بستانکار موجودی / بدهکار پرداختنی(یا نقد).
            var inventory = await _accounts.GetByCodeAsync(companyId, "1-05-001", ct);
            var payable = await _accounts.GetByCodeAsync(companyId, "3-01-001", ct);
            var cash = await _accounts.GetByCodeAsync(companyId, "1-01-001", ct);
            var debitAccount = req.RefundCash ? cash : payable;
            if (inventory != null && debitAccount != null && inv.GrandTotal > 0)
            {
                var num = await _vouchers.GetNextNumberAsync(companyId, ct);
                var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, num, req.Date, 4 /*خرید*/,
                    $"سند خودکار مرجوعی خرید {number}", number);
                int row = 1;
                v.AddItem(VoucherItem.Create(0, row++, debitAccount.Id, inv.GrandTotal, 0,
                    req.RefundCash ? "دریافت نقدی از تأمین‌کننده" : "کاهش بدهی به تأمین‌کننده"));
                v.AddItem(VoucherItem.Create(0, row++, inventory.Id, 0, inv.GrandTotal, "برگشت کالا به تأمین‌کننده"));
                await _vouchers.AddAsync(v, ct);
                await _uow.SaveChangesAsync(ct);
                if (v.CanPost()) v.Post(_user.UserId ?? 1);
                inv.Post(v.Id);
                _invoices.Update(inv);
            }

            // U-PARTY-BAL: هم‌راستا با رفعِ مشابه در CreateSalesReturnCommand — دریافتِ نقدی چیزی از
            // بدهیِ ما به تأمین‌کننده کم نمی‌کند (پول مستقیم برگشته)؛ اگر نقدی نبوده، همان مبلغ باید
            // پرداختنیِ ما را کاهش دهد.
            if (inv.GrandTotal > 0 && !req.RefundCash)
            {
                var supplier = await _suppliers.GetByIdAsync(req.SupplierId, ct);
                if (supplier != null)
                    supplier.UpdateBalance(supplier.Balance - inv.GrandTotal);
            }

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(inv.Id);
        }
        catch (Exception ex) { await _uow.RollbackTransactionAsync(ct); return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}
