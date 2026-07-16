using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Commands;

public record CreateSalesInvoiceCommand(
    int BranchId,
    int FiscalYearId,
    string InvoiceDate,
    int CustomerId,
    int WarehouseId,
    InvoiceType InvoiceType,
    string PriceLevel,
    int? SalesRepId,
    string? DueDate,
    string? Description,
    decimal Shipping,
    decimal OtherCosts,
    List<SalesInvoiceItemDto> Items,
    decimal InvoiceDiscount = 0,        // amount-based whole-invoice discount
    decimal PaidAmount = 0,             // amount received at invoice time
    string PaymentMethod = "نسیه",      // نقدی / بانک / چک / نسیه
    decimal CommissionPercent = 0,      // sales-rep commission %
    bool AllowOverCredit = false,       // عبور مجاز از سقف اعتبار (با تأیید کاربر مجاز)
    string? Reference = null,           // ارجاع (شمارهٔ مرجع/سفارش)
    string? Title = null,               // عنوانِ فاکتور
    int? ProjectId = null,              // پروژهٔ مرتبط
    // U-ACCT-1.4: اگر تعیین شود و روش پرداخت «بانک» باشد، به‌جایِ بانکِ پیش‌فرضِ تک‌بانکی از
    // حسابِ GLِ همین BankAccount استفاده می‌شود.
    int? BankAccountId = null
) : IRequest<Result<int>>;

public record SalesInvoiceItemDto(
    int ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal TaxPct,
    string? Description,
    int? BatchId,
    int? SerialId
);

public class CreateSalesInvoiceCommandValidator : AbstractValidator<CreateSalesInvoiceCommand>
{
    public CreateSalesInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceDate).NotEmpty().WithMessage("تاریخ فاکتور الزامی است.");
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری الزامی است.");
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("فاکتور باید حداقل یک ردیف داشته باشد.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("کالا الزامی است.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("مقدار باید بزرگتر از صفر باشد.");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("قیمت واحد نمی‌تواند منفی باشد.");
            // RC-val — کرانهٔ درصد: تخفیفِ >۱۰۰٪ مبلغِ منفی/سندِ خراب می‌سازد.
            item.RuleFor(i => i.DiscountPct).InclusiveBetween(0, 100).WithMessage("درصدِ تخفیف باید بینِ ۰ تا ۱۰۰ باشد.");
            item.RuleFor(i => i.TaxPct).InclusiveBetween(0, 100).WithMessage("درصدِ مالیات باید بینِ ۰ تا ۱۰۰ باشد.");
        });
    }
}

public class CreateSalesInvoiceCommandHandler : IRequestHandler<CreateSalesInvoiceCommand, Result<int>>
{
    private readonly IRepository<SalesInvoice> _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IStockItemRepository _stockRepository;
    private readonly IProductRepository _productRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IRepository<Domain.Entities.Inventory.StockTransaction> _ledger;
    private readonly IRepository<Domain.Entities.CRM.Party> _customers;
    private readonly IRepository<Domain.Entities.Accounting.FiscalYear> _fiscalYears;
    private readonly IRepository<Domain.Entities.Accounting.BankAccount> _bankAccounts;
    private readonly IMediator _mediator;
    private readonly IWarehouseRepository _warehouses;
    private readonly IRepository<Domain.Entities.CRM.PartyLedgerEntry> _partyLedger;

    public CreateSalesInvoiceCommandHandler(
        IRepository<SalesInvoice> invoiceRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IPersianCalendarService calendar,
        IStockItemRepository stockRepository,
        IProductRepository productRepository,
        IAccountRepository accountRepository,
        IVoucherRepository voucherRepository,
        IRepository<Domain.Entities.Inventory.StockTransaction> ledger,
        IRepository<Domain.Entities.CRM.Party> customers,
        IRepository<Domain.Entities.Accounting.FiscalYear> fiscalYears,
        IRepository<Domain.Entities.Accounting.BankAccount> bankAccounts,
        IMediator mediator,
        IWarehouseRepository warehouses,
        IRepository<Domain.Entities.CRM.PartyLedgerEntry> partyLedger)
    {
        _invoiceRepository = invoiceRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _calendar = calendar;
        _stockRepository = stockRepository;
        _productRepository = productRepository;
        _accountRepository = accountRepository;
        _voucherRepository = voucherRepository;
        _ledger = ledger;
        _customers = customers;
        _fiscalYears = fiscalYears;
        _bankAccounts = bankAccounts;
        _mediator = mediator;
        _warehouses = warehouses;
        _partyLedger = partyLedger;
    }

    public async Task<Result<int>> Handle(CreateSalesInvoiceCommand request, CancellationToken ct)
    {
        // RC-fix — قفلِ دورهٔ مالی (قبل از تراکنش): فاکتور در دورهٔ بسته/خارج از بازه ثبت نشود.
        var fyLock = Accounting.FiscalPeriodGuard.Check(
            await _fiscalYears.GetByIdAsync(request.FiscalYearId, ct), request.InvoiceDate);
        if (fyLock is not null) return Result<int>.Failure(fyLock);

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var companyId = _currentUser.CompanyId!.Value;

            // Generate invoice number
            var invoiceNumber = await GenerateInvoiceNumberAsync(companyId, request.FiscalYearId, request.InvoiceType, ct);

            var invoice = SalesInvoice.Create(
                companyId, request.BranchId, request.FiscalYearId,
                invoiceNumber, request.InvoiceDate, request.CustomerId, request.WarehouseId,
                request.InvoiceType, request.PriceLevel, request.SalesRepId,
                request.DueDate, request.Description,
                request.Reference, request.Title, request.ProjectId);

            for (int i = 0; i < request.Items.Count; i++)
            {
                var dto = request.Items[i];
                var item = SalesInvoiceItem.Create(
                    0, i + 1, dto.ProductId, dto.Quantity, dto.UnitPrice,
                    dto.DiscountPct, dto.TaxPct, dto.Description, dto.BatchId, dto.SerialId);
                invoice.AddItem(item);
            }

            invoice.SetShipping(request.Shipping, request.OtherCosts);
            // U-INV-DISC: تخفیفِ کلِ فاکتور حالا مستقیماً روی خودِ فاکتور اعمال می‌شود (نه فقط سندِ
            // حسابداری) — invoice.GrandTotal از این‌جا به بعد خالص از تخفیفِ سرفاکتور است.
            invoice.SetInvoiceDiscount(request.InvoiceDiscount);

            // ── کنترل سقف اعتبار مشتری برای بخش نسیه (کار #۳۷) ──
            if (request.InvoiceType == Domain.Enums.InvoiceType.Sale && !request.AllowOverCredit)
            {
                var creditPortion = invoice.GrandTotal - request.PaidAmount;
                if (creditPortion > 0)
                {
                    var customer = await _customers.GetByIdAsync(request.CustomerId, ct);
                    if (customer != null && CRM.CreditLimitPolicy.IsBlocked(customer.Balance, creditPortion, customer.CreditLimit))
                    {
                        await _unitOfWork.RollbackTransactionAsync(ct);
                        return Result<int>.Failure(
                            $"سقف اعتبار مشتری کافی نیست (سقف: {customer.CreditLimit:#,##0}، مانده: {customer.Balance:#,##0}).");
                    }
                }
            }

            // U-POS-7: PaidAmount/RemainAmount خودِ فاکتور تا این‌جا هرگز از request.PaidAmount ست
            // نمی‌شد — یعنی هر فاکتورِ فروش (صرف‌نظر از نقدی/کارت‌خوان) روی PaidAmount=۰ می‌ماند و
            // گزارش‌های «مانده/سررسید» و IsFullyPaid همیشه غلط بودند، حتی وقتی سندِ حسابداری‌اش
            // درست دریافتِ نقد/بانک را ثبت می‌کرد. مبلغِ دریافتی (سقف‌زده به GrandTotal) اینجا
            // روی خودِ فاکتور هم اعمال می‌شود.
            if (request.PaidAmount > 0)
                invoice.AddPayment(System.Math.Min(request.PaidAmount, invoice.GrandTotal));

            await _invoiceRepository.AddAsync(invoice, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // INV-1 گام۴: جمعِ بهای تمام‌شدهٔ کالای فروش‌رفته برای ثبتِ دائمیِ COGS در سند.
            decimal totalCost = 0;

            // ── Reduce warehouse stock (only for inventory-tracked products = کالا) ──
            if (request.InvoiceType == Domain.Enums.InvoiceType.Sale
                || request.InvoiceType == Domain.Enums.InvoiceType.Consignment)
            {
                foreach (var dto in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(dto.ProductId, ct);
                    if (product == null || product.ProductType != Domain.Enums.ProductType.Product) continue;

                    var stock = await _stockRepository.GetByProductAndWarehouseAsync(dto.ProductId, request.WarehouseId, ct);
                    if (stock == null || stock.Quantity < dto.Quantity)
                        throw new InvalidOperationException(
                            $"موجودی کافی برای «{product.Name}» در انبار وجود ندارد (موجودی: {stock?.Quantity ?? 0}).");

                    // U10: بهای خروج برای کالاهای روشِ FIFO از لایه‌های کاردکس؛ وگرنه میانگین.
                    var unitCost = await ComputeIssueUnitCostAsync(companyId, dto.ProductId,
                        request.WarehouseId, dto.Quantity, stock.AverageCost, product.ValuationMethod, ct);
                    stock.RemoveStock(dto.Quantity);
                    _stockRepository.Update(stock);
                    totalCost += unitCost * dto.Quantity;   // INV-1 گام۴: انباشتِ COGS

                    // ── بچ/سریال (INV-1 گام۳): حواله/فروشِ بچ و سریالِ انتخاب‌شده ──
                    // SaveChanges را فراخواننده در همین تراکنش انجام می‌دهد؛ خطا → rollback کل فاکتور.
                    if (dto.BatchId.HasValue)
                    {
                        var issueRes = await _mediator.Send(new Inventory.Commands.IssueBatchCommand(
                            dto.BatchId.Value, dto.Quantity), ct);
                        if (!issueRes.Succeeded)
                            throw new InvalidOperationException(issueRes.ErrorMessage);
                    }
                    if (dto.SerialId.HasValue)
                    {
                        var serialRes = await _mediator.Send(new Inventory.Commands.SellSerialCommand(
                            dto.SerialId.Value, request.InvoiceDate), ct);
                        if (!serialRes.Succeeded)
                            throw new InvalidOperationException(serialRes.ErrorMessage);
                    }

                    // kardex ledger entry (outflow) — کنسینمنت برچسبِ جدا می‌گیرد تا با فروشِ واقعی
                    // قاطی نشود (کالا هنوز مالِ شرکت است، فقط جایِ فیزیکی‌اش عوض شده).
                    var kardexLabel = request.InvoiceType == Domain.Enums.InvoiceType.Consignment
                        ? "خروج کنسینمنت" : "خروج فروش";
                    await _ledger.AddAsync(Domain.Entities.Inventory.StockTransaction.Create(
                        companyId, request.BranchId, kardexLabel, invoiceNumber, request.InvoiceDate,
                        dto.ProductId, request.WarehouseId, -dto.Quantity, unitCost,
                        stock.Quantity, stock.Quantity * stock.AverageCost,
                        "SalesInvoice", invoice.Id, null), ct);
                }
            }
            // ── برگشت از فروش (مرجوعی): افزایشِ موجودیِ انبار + ورودِ کاردکس ──
            else if (request.InvoiceType == Domain.Enums.InvoiceType.SaleReturn)
            {
                foreach (var dto in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(dto.ProductId, ct);
                    if (product == null || product.ProductType != Domain.Enums.ProductType.Product) continue;

                    var stock = await _stockRepository.GetByProductAndWarehouseAsync(dto.ProductId, request.WarehouseId, ct);
                    var unitCost = stock?.AverageCost ?? product.PurchasePrice;
                    if (stock == null)
                    {
                        stock = Domain.Entities.Inventory.StockItem.Create(dto.ProductId, request.WarehouseId);
                        await _stockRepository.AddAsync(stock, ct);
                    }
                    stock.AddStock(dto.Quantity, unitCost);
                    _stockRepository.Update(stock);
                    totalCost += unitCost * dto.Quantity;   // INV-1 گام۴: بهای بازگشتیِ موجودی (معکوسِ COGS)

                    await _ledger.AddAsync(Domain.Entities.Inventory.StockTransaction.Create(
                        companyId, request.BranchId, "ورود برگشت از فروش", invoiceNumber, request.InvoiceDate,
                        dto.ProductId, request.WarehouseId, +dto.Quantity, unitCost,
                        stock.Quantity, stock.Quantity * stock.AverageCost,
                        "SalesReturn", invoice.Id, null), ct);
                }
            }

            // ── Automatic accounting voucher ──
            // پیش‌فاکتور (Quotation) سندِ مالی نمی‌سازد. کنسینمنت سندِ reclassification می‌سازد
            // (نه سندِ درآمد/COGS — U-CONSIGN-2).
            if (request.InvoiceType == Domain.Enums.InvoiceType.SaleReturn)
                await TryCreateSalesReturnVoucherAsync(invoice, companyId, request, totalCost, ct);
            else if (request.InvoiceType == Domain.Enums.InvoiceType.Sale)
                await TryCreateSalesVoucherAsync(invoice, companyId, request, totalCost, ct);
            else if (request.InvoiceType == Domain.Enums.InvoiceType.Consignment)
                await TryCreateConsignmentVoucherAsync(invoice, companyId, request, totalCost, ct);

            // کار #۵ — اگر سندِ خودکار به‌خاطرِ نبودِ چارتِ حساب ساخته نشد، فاکتور نباید در «پیش‌نویس» بماند؛
            // حداقل «قطعی» (Confirmed) شود تا در گزارش‌ها/تحلیل‌ها (سود/فروش) لحاظ گردد.
            if (invoice.Status == Domain.Enums.InvoiceStatus.Draft)
            {
                invoice.Confirm();
                _invoiceRepository.Update(invoice);
            }

            // U-PARTY-BAL: پیش‌تر فقط دریافت/پرداختِ خزانه (CreateReceiptCommand/CreatePaymentCommand)
            // Party.Balance را دست می‌زدند — خودِ صدورِ فاکتور هرگز بدهیِ نسیهٔ جدید را به ماندهٔ
            // مرجعِ مشتری اضافه نمی‌کرد. نتیجه: هرچقدر هم فروشِ نسیه انباشته می‌شد، Balance ثابت
            // می‌ماند — سقفِ اعتبار (CreditLimitPolicy رویِ همین Balance) و ماندهٔ نمایش‌داده‌شده در
            // کارتِ مشتری/صورت‌حساب هر دو عملاً بی‌معنا می‌شدند. حالا خودِ صدور هم Balance را به‌روز می‌کند.
            if (request.InvoiceType == Domain.Enums.InvoiceType.Sale
                || request.InvoiceType == Domain.Enums.InvoiceType.SaleReturn)
            {
                var custForBalance = await _customers.GetByIdAsync(request.CustomerId, ct);
                if (custForBalance != null)
                {
                    var delta = request.InvoiceType == Domain.Enums.InvoiceType.Sale
                        ? invoice.RemainAmount : -invoice.RemainAmount;
                    var docType = request.InvoiceType == Domain.Enums.InvoiceType.Sale ? "فاکتور فروش" : "برگشت از فروش";
                    await CRM.PartyLedger.RecordAsync(_partyLedger, custForBalance, delta, request.InvoiceDate,
                        docType, invoice.InvoiceNumber, $"{docType} {invoice.InvoiceNumber}", ct);
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return Result<int>.Success(invoice.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }

    /// <summary>
    /// U10 — بهای تمام‌شدهٔ یک خروج به روشِ FIFO: هزینهٔ نهاییِ مصرفِ <paramref name="qty"/> واحد
    /// از روی لایه‌های موجود در کاردکس (StockTransactions) با موتورِ خالصِ <see cref="Inventory.FifoCostEngine"/>.
    /// برای کالاهای میانگین‌وزنی (یا نبودِ لایه) همان <paramref name="fallbackAverage"/> برگردانده می‌شود.
    /// </summary>
    private async Task<decimal> ComputeIssueUnitCostAsync(int companyId, int productId, int warehouseId,
        decimal qty, decimal fallbackAverage, Domain.Enums.ValuationMethod method, CancellationToken ct)
    {
        if (method != Domain.Enums.ValuationMethod.FIFO || qty <= 0) return fallbackAverage;

        var prior = await _ledger.FindAsync(
            t => t.CompanyId == companyId && t.ProductId == productId && t.WarehouseId == warehouseId, ct);
        var movements = prior.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => (t.Quantity, t.UnitCost)).ToList();
        if (movements.Count == 0) return fallbackAverage;

        // هزینهٔ نهاییِ این خروج = هزینهٔ تجمعیِ پس از افزودنِ آن منهای پیش از آن.
        var before = Inventory.FifoCostEngine.Compute(movements).IssuedCost;
        movements.Add((-qty, 0));
        var after = Inventory.FifoCostEngine.Compute(movements).IssuedCost;
        var marginal = after - before;
        return marginal > 0 ? System.Math.Round(marginal / qty, 4) : fallbackAverage;
    }

    /// <summary>
    /// سندِ خودکارِ «برگشت از فروش» — معکوسِ سندِ فروش: بدهکار کردنِ درآمدِ فروش و مالیات،
    /// بستانکار کردنِ صندوق/بانک (بازپرداخت) یا حساب دریافتنیِ مشتری.
    /// </summary>
    private async Task TryCreateSalesReturnVoucherAsync(SalesInvoice invoice, int companyId,
        CreateSalesInvoiceCommand request, decimal totalCost, CancellationToken ct)
    {
        if (invoice.InvoiceType != Domain.Enums.InvoiceType.SaleReturn || invoice.GrandTotal <= 0) return;

        var receivable = await _accountRepository.GetByCodeAsync(companyId, "1-03-001", ct);
        // U-ACCT-1.2: پیش‌تر برگشت از فروش مستقیماً بدهکارِ حسابِ درآمد (۶-۰۱-۰۰۱) می‌شد، نه یک
        // خطِ contra-revenueِ جدا — یعنی گزارشِ درآمدِ ناخالص هرگز مبلغِ برگشتی را جداگانه نشان
        // نمی‌داد. حالا از ۶-۰۲-۰۰۱ (برگشت از فروش، leafِ نو زیرِ گروهِ contra-revenueِ ازپیش‌seedشده)
        // استفاده می‌شود؛ اگر آن حساب نبود (نصبِ پیش از این مهاجرت)، به رفتارِ قدیمی fallback می‌کند.
        var salesReturn = await _accountRepository.GetByCodeAsync(companyId, "6-02-001", ct)
                          ?? await _accountRepository.GetByCodeAsync(companyId, "6-01-001", ct);
        var vat = await _accountRepository.GetByCodeAsync(companyId, "3-04-001", ct);
        if (receivable == null || salesReturn == null) return; // chart not set up → skip silently

        var number = await _voucherRepository.GetNextNumberAsync(companyId, ct);
        var voucher = Voucher.Create(companyId, request.BranchId, request.FiscalYearId,
            number, request.InvoiceDate, 3 /*Sale*/, $"سند خودکار برگشت از فروش {invoice.InvoiceNumber}", invoice.InvoiceNumber);

        // U-INV-DISC: invoice.GrandTotal از قبل خالص از تخفیفِ سرفاکتور است (SetInvoiceDiscount).
        var grand = invoice.GrandTotal;
        var salesAmount = grand - invoice.TotalTax;
        if (salesAmount < 0) salesAmount = 0;

        // مبلغِ بازپرداختیِ نقد/بانک؛ مابقی از حساب دریافتنیِ مشتری کسر می‌شود.
        var refunded = request.PaidAmount > 0 ? System.Math.Min(request.PaidAmount, grand) : 0;
        var remain = grand - refunded;

        int row = 1;
        // بدهکار: برگشت از فروش (کاهشِ درآمد) + مالیات
        voucher.AddItem(VoucherItem.Create(0, row++, salesReturn.Id, salesAmount, 0, "برگشت از فروش"));
        if (invoice.TotalTax > 0 && vat != null)
            voucher.AddItem(VoucherItem.Create(0, row++, vat.Id, invoice.TotalTax, 0, "برگشتِ مالیات بر ارزش افزوده"));

        // بستانکار: بازپرداختِ نقد/بانک + کاهشِ مطالبه از مشتری
        if (refunded > 0)
        {
            Account? payAcc = request.PaymentMethod switch
            {
                "نقدی" => await _accountRepository.GetByCodeAsync(companyId, "1-01-001", ct),
                "چک"   => await _accountRepository.GetByCodeAsync(companyId, "1-04-001", ct),
                "بانک" => await Inventory.Commands.InventoryAccounting.ResolveBankAccountAsync(
                    _accountRepository, _bankAccounts, companyId, request.BankAccountId, ct),
                _       => await _accountRepository.GetByCodeAsync(companyId, "1-01-001", ct)
            };
            payAcc ??= await _accountRepository.GetByCodeAsync(companyId, "1-01-001", ct);
            if (payAcc != null)
                voucher.AddItem(VoucherItem.Create(0, row++, payAcc.Id, 0, refunded, $"بازپرداخت وجه ({request.PaymentMethod})"));
            else remain = grand;
        }
        if (remain > 0)
            voucher.AddItem(VoucherItem.Create(0, row++, receivable.Id, 0, remain, $"برگشت از فروش {invoice.InvoiceNumber}"));

        // INV-1 گام۴ — معکوسِ COGS: کالا به انبار بازگشت ⇒ بد «موجودی کالا» / بس «بهای تمام‌شده».
        await AddCogsLinesAsync(voucher, companyId, totalCost, reverse: true, row, request.WarehouseId, ct);

        await _voucherRepository.AddAsync(voucher, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        if (voucher.CanPost())
            voucher.Post(_currentUser.UserId ?? 1);
        _voucherRepository.Update(voucher);

        invoice.Post(_currentUser.UserId ?? 1, voucher.Id);
        _invoiceRepository.Update(invoice);
    }

    /// <summary>
    /// U-CONSIGN-2 — کنسینمنت مالکیت را منتقل نمی‌کند (کالا نزدِ کنسینی امانت است): بدونِ درآمد،
    /// بدونِ COGS، بدونِ بدهیِ مشتری. فقط reclassificationِ درونِ داراییِ موجودی به بهایِ تمام‌شده
    /// (نه قیمتِ فروش): بد «کالای امانی نزدِ دیگران» (۱-۰۵-۰۰۳) / بس «موجودی کالا» (۱-۰۵-۰۰۱).
    /// بدونِ اثر بر سود/زیان. تسویهٔ نهایی (وقتی کنسینی واقعاً فروخت) قابلیتِ جداگانه‌ای است که هنوز
    /// پیاده نشده — مستند در todo.rm.
    /// </summary>
    private async Task TryCreateConsignmentVoucherAsync(SalesInvoice invoice, int companyId,
        CreateSalesInvoiceCommand request, decimal totalCost, CancellationToken ct)
    {
        if (invoice.InvoiceType != Domain.Enums.InvoiceType.Consignment || totalCost <= 0) return;

        var consignmentOut = await _accountRepository.GetByCodeAsync(
            companyId, Inventory.Commands.InventoryAccounting.ConsignmentOut, ct);
        // U-INV-ACCT-WH (backlog #7): حسابِ موجودیِ اختصاصیِ انبارِ مبدأِ کنسینمنت، در صورتِ تعیین.
        var inventory = await Inventory.Commands.InventoryAccounting.ResolveInventoryAccountAsync(
            _accountRepository, _warehouses, companyId, request.WarehouseId, ct);
        if (consignmentOut == null || inventory == null) return; // چارت تنظیم نشده → بی‌صدا رد شو

        var number = await _voucherRepository.GetNextNumberAsync(companyId, ct);
        var voucher = Voucher.Create(companyId, request.BranchId, request.FiscalYearId,
            number, request.InvoiceDate, 3 /*Sale*/, $"سندِ خودکارِ ارسالِ کنسینمنت {invoice.InvoiceNumber}",
            invoice.InvoiceNumber);

        voucher.AddItem(VoucherItem.Create(0, 1, consignmentOut.Id, totalCost, 0, "کالای ارسالی به‌صورتِ امانی"));
        voucher.AddItem(VoucherItem.Create(0, 2, inventory.Id, 0, totalCost, "کاهشِ موجودیِ انبار (کنسینمنت)"));

        await _voucherRepository.AddAsync(voucher, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        if (voucher.CanPost())
            voucher.Post(_currentUser.UserId ?? 1);
        _voucherRepository.Update(voucher);

        invoice.Post(_currentUser.UserId ?? 1, voucher.Id);
        _invoiceRepository.Update(invoice);
    }

    /// <summary>
    /// INV-1 گام۴ — افزودنِ دو ردیفِ بهای تمام‌شده/موجودی به سند (perpetual).
    /// فروش (reverse=false): بد «بهای تمام‌شده 7-01-001» / بس «موجودی کالا 1-05-001».
    /// برگشت از فروش (reverse=true): معکوس. اگر بها ≤۰ یا حساب‌ها در چارت نباشند، بی‌صدا رد می‌شود
    /// (سند با همان درآمد/مالیات متوازن می‌ماند).
    /// </summary>
    private async Task AddCogsLinesAsync(Voucher voucher, int companyId, decimal totalCost,
        bool reverse, int row, int warehouseId, CancellationToken ct)
    {
        if (totalCost <= 0) return;
        // منبعِ واحدِ کدِ حساب‌ها = همان helperِ اسنادِ انبار (perpetual یکپارچه).
        var cogs = await _accountRepository.GetByCodeAsync(companyId, Inventory.Commands.InventoryAccounting.Cogs, ct);
        // U-INV-ACCT-WH (backlog #7): حسابِ موجودیِ اختصاصیِ انبارِ فاکتور، در صورتِ تعیین.
        var inventory = await Inventory.Commands.InventoryAccounting.ResolveInventoryAccountAsync(
            _accountRepository, _warehouses, companyId, warehouseId, ct);
        if (cogs == null || inventory == null) return; // چارتِ بهای‌تمام‌شده/موجودی تنظیم نشده → رد

        foreach (var line in Inventory.PerpetualCogs.Build(totalCost, cogs.Id, inventory.Id, reverse))
            voucher.AddItem(VoucherItem.Create(0, row++, line.AccountId, line.Debit, line.Credit, line.Description));
    }

    private async Task TryCreateSalesVoucherAsync(SalesInvoice invoice, int companyId,
        CreateSalesInvoiceCommand request, decimal totalCost, CancellationToken ct)
    {
        if (invoice.InvoiceType != Domain.Enums.InvoiceType.Sale || invoice.GrandTotal <= 0) return;

        var receivable = await _accountRepository.GetByCodeAsync(companyId, "1-03-001", ct);
        var sales = await _accountRepository.GetByCodeAsync(companyId, "6-01-001", ct);
        var vat = await _accountRepository.GetByCodeAsync(companyId, "3-04-001", ct);
        if (receivable == null || sales == null) return; // chart not set up → skip silently

        var number = await _voucherRepository.GetNextNumberAsync(companyId, ct);
        var voucher = Voucher.Create(companyId, request.BranchId, request.FiscalYearId,
            number, request.InvoiceDate, 3 /*Sale*/, $"سند خودکار فاکتور فروش {invoice.InvoiceNumber}", invoice.InvoiceNumber);

        // U-INV-DISC: invoice.GrandTotal از قبل خالص از تخفیفِ سرفاکتور است (SetInvoiceDiscount).
        var grand = invoice.GrandTotal;
        var salesAmount = grand - invoice.TotalTax;
        if (salesAmount < 0) salesAmount = 0;

        // split the debit between received cash/bank/cheque and the remaining receivable
        var paid = request.PaidAmount > 0 ? System.Math.Min(request.PaidAmount, grand) : 0;
        var remain = grand - paid;

        int row = 1;
        if (paid > 0)
        {
            Account? payAcc = request.PaymentMethod switch
            {
                "نقدی"  => await _accountRepository.GetByCodeAsync(companyId, "1-01-001", ct),  // صندوق
                "چک"    => await _accountRepository.GetByCodeAsync(companyId, "1-04-001", ct),  // اسناد دریافتنی
                "بانک"  => await Inventory.Commands.InventoryAccounting.ResolveBankAccountAsync(
                    _accountRepository, _bankAccounts, companyId, request.BankAccountId, ct),
                _        => await _accountRepository.GetByCodeAsync(companyId, "1-01-001", ct)
            };
            payAcc ??= await _accountRepository.GetByCodeAsync(companyId, "1-01-001", ct);
            if (payAcc != null)
                voucher.AddItem(VoucherItem.Create(0, row++, payAcc.Id, paid, 0, $"دریافت وجه ({request.PaymentMethod})"));
            else remain = grand; // fallback: everything receivable
        }
        if (remain > 0)
            voucher.AddItem(VoucherItem.Create(0, row++, receivable.Id, remain, 0, $"فاکتور فروش {invoice.InvoiceNumber}"));

        voucher.AddItem(VoucherItem.Create(0, row++, sales.Id, 0, salesAmount, "درآمد فروش"));
        if (invoice.TotalTax > 0 && vat != null)
            voucher.AddItem(VoucherItem.Create(0, row++, vat.Id, 0, invoice.TotalTax, "مالیات بر ارزش افزوده"));

        // INV-1 گام۴ — ثبتِ دائمیِ بهای تمام‌شدهٔ کالای فروش‌رفته (perpetual COGS):
        // بد «بهای تمام‌شده» / بس «موجودی کالا». با هر دو سمت متوازن می‌ماند و موجودیِ GL را کاهش می‌دهد.
        await AddCogsLinesAsync(voucher, companyId, totalCost, reverse: false, row, request.WarehouseId, ct);

        await _voucherRepository.AddAsync(voucher, ct);
        await _unitOfWork.SaveChangesAsync(ct);   // assigns voucher.Id

        // Auto-post the voucher (Id now exists so the audit event records the real number).
        // Only when balanced — otherwise leave it Draft for manual review instead of failing the invoice.
        if (voucher.CanPost())
            voucher.Post(_currentUser.UserId ?? 1);
        _voucherRepository.Update(voucher);

        // کار #۲۵: فاکتور فروشِ نهایی‌شده باید Posted شود (نه Draft) — هم‌راستا با فاکتور خرید.
        // در غیر این صورت در تحلیل‌ها/گزارش‌ها به‌عنوان پیش‌نویس نادیده گرفته می‌شود.
        invoice.Post(_currentUser.UserId ?? 1, voucher.Id);
        _invoiceRepository.Update(invoice);

        // ── Sales-rep commission → expense voucher ──
        if (request.SalesRepId.HasValue && request.CommissionPercent > 0 && salesAmount > 0)
        {
            var commission = salesAmount * request.CommissionPercent / 100m;
            // U-ACCT-1.6: حساب‌هایِ اختصاصیِ پورسانت (۸-۱۰-۰۰۱ هزینه / ۳-۰۸-۰۰۱ پرداختنی) — پیش‌تر از
            // هزینهٔ عمومی (۸-۰۱-۰۰۱) و پرداختنیِ عمومی (۳-۰۱-۰۰۱) استفاده می‌شد که پورسانت را در
            // گزارش‌هایِ هزینه/بدهیِ عمومی گم می‌کرد. fallback به کدهایِ قدیمی اگر حسابِ اختصاصی نبود.
            var expense = await _accountRepository.GetByCodeAsync(companyId, "8-10-001", ct)
                ?? await _accountRepository.GetByCodeAsync(companyId, "8-01-001", ct);
            var payable = await _accountRepository.GetByCodeAsync(companyId, "3-08-001", ct)
                ?? await _accountRepository.GetByCodeAsync(companyId, "3-01-001", ct);
            if (commission > 0 && expense != null && payable != null)
            {
                var cnum = await _voucherRepository.GetNextNumberAsync(companyId, ct);
                var cv = Voucher.Create(companyId, request.BranchId, request.FiscalYearId,
                    cnum, request.InvoiceDate, 9, $"پورسانت بازاریاب فاکتور {invoice.InvoiceNumber}");
                cv.AddItem(VoucherItem.Create(0, 1, expense.Id, commission, 0, "هزینه پورسانت فروش"));
                cv.AddItem(VoucherItem.Create(0, 2, payable.Id, 0, commission, "بدهی پورسانت به بازاریاب"));
                await _voucherRepository.AddAsync(cv, ct);
                await _unitOfWork.SaveChangesAsync(ct);   // assigns cv.Id
                if (cv.CanPost())
                {
                    cv.Post(_currentUser.UserId ?? 1);
                    _voucherRepository.Update(cv);
                }
            }
        }
    }

    private async Task<string> GenerateInvoiceNumberAsync(int companyId, int fiscalYearId,
        InvoiceType type, CancellationToken ct)
    {
        // U-CONSIGN-1: Consignmentِ قبلاً پیشوندِ "F" (مشترک با Sale) می‌گرفت — چون جست‌وجویِ
        // بیشینه‌عدد جدا-به‌ازایِ-نوع است ولی رشتهٔ نهایی (پیشوند+عدد) با Sale یکسان می‌شد، دو
        // فاکتورِ کاملاً متفاوت (یک فروشِ واقعی + یک حوالهٔ کنسینمنت) شمارهٔ رسمیِ یکسان می‌گرفتند —
        // با تستِ زندهٔ رویِ DBِ واقعی بازتولید و تأیید شد (هر دو "F000001").
        var prefix = type switch
        {
            InvoiceType.Sale => "F",
            InvoiceType.SaleReturn => "BR",
            InvoiceType.Quotation => "PF",
            InvoiceType.Consignment => "HV",
            _ => "F"
        };
        // بیشترین شمارهٔ موجود + ۱ (نه COUNT) — تا حذفِ یک فاکتور شمارهٔ تکراری تولید نکند
        // (شمارهٔ فاکتورِ رسمیِ تکراری = نقصِ قانونی). رقم‌های پیشوند جدا و بیشینه گرفته می‌شود.
        var existing = await _invoiceRepository.FindAsync(
            i => i.CompanyId == companyId && i.FiscalYearId == fiscalYearId && i.InvoiceType == type, ct);
        int max = 0;
        foreach (var inv in existing)
        {
            var digits = new string((inv.InvoiceNumber ?? "").Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var x) && x > max) max = x;
        }
        return $"{prefix}{(max + 1):D6}";
    }
}
