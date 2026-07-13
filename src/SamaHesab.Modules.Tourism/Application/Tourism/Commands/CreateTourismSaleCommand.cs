using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;
using DomainBasis = SamaHesab.Modules.Tourism.Domain.CommissionBasis;
using EngineBasis = SamaHesab.Modules.Tourism.Application.CommissionBasis;

namespace SamaHesab.Modules.Tourism.Application.Commands;

public record TourismPassengerDto(string FullName, string? NationalIdOrPassport = null, string? Phone = null);
public record TourismSaleLineDto(int ProductId, decimal Quantity, decimal UnitSalePrice,
    decimal DiscountAmount = 0, IReadOnlyList<TourismPassengerDto>? Passengers = null, string? TravelDate = null);

/// <summary>
/// TUR-C1-3 — ثبتِ فروشِ گردشگری + سندِ متوازنِ خودکار (الگوی TryCreateSalesVoucher):
///   Dr نقد/دریافتنی + Dr تخفیف(کاهنده) / Cr درآمد ‖ Dr COGS / Cr ودیعهٔ‌کنترلی(per-supplier).
/// برداشتِ ودیعه = همان خطوطِ فروش (snapshotِ بها). پورسانت per-line از موتورِ TUR-C2-1 ثبت می‌شود.
/// </summary>
public record CreateTourismSaleCommand(
    int BranchId, int FiscalYearId, string Date, int SalespersonPartyId, int? CustomerPartyId,
    string PaymentMethod, IReadOnlyList<TourismSaleLineDto> Lines, string? Note = null)
    : IRequest<Result<int>>;

public class CreateTourismSaleCommandHandler : IRequestHandler<CreateTourismSaleCommand, Result<int>>
{
    private readonly IRepository<TourismSetting> _settings;
    private readonly IRepository<TourismProduct> _products;
    private readonly IRepository<CommissionRule> _rules;
    private readonly IRepository<TourismSale> _sales;
    private readonly IRepository<SalesCommissionEntry> _commissions;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IRepository<Party> _parties;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public CreateTourismSaleCommandHandler(IRepository<TourismSetting> settings, IRepository<TourismProduct> products,
        IRepository<CommissionRule> rules, IRepository<TourismSale> sales, IRepository<SalesCommissionEntry> commissions,
        IVoucherRepository vouchers, IRepository<FiscalYear> fiscalYears, IRepository<Party> parties,
        IUnitOfWork uow, ICurrentUserService user)
    {
        _settings = settings; _products = products; _rules = rules; _sales = sales; _commissions = commissions;
        _vouchers = vouchers; _fiscalYears = fiscalYears; _parties = parties; _uow = uow; _user = user;
    }

    public async Task<Result<int>> Handle(CreateTourismSaleCommand req, CancellationToken ct)
    {
        if (req.Lines is null || req.Lines.Count == 0) return Result<int>.Failure("فروش حداقل یک ردیف لازم دارد.");

        // SP-1 — فروشنده‌ی مؤثر: اگر کاربرِ جاری به یک «فروشنده» نگاشته شده و ADMIN نیست،
        // فروشنده همیشه خودِ اوست (پنلِ فروشنده‌محور؛ مقدارِ ارسالی نادیده گرفته می‌شود).
        // ادمین/مدیر می‌تواند به‌جای هر فروشنده ثبت کند.
        var isAdmin = _user.GetRoles().Contains("ADMIN");
        var sellerPartyId = SellerResolver.Resolve(isAdmin, _user.SalespersonPartyId, req.SalespersonPartyId);
        if (sellerPartyId <= 0) return Result<int>.Failure("فروشنده الزامی است.");
        var companyId = _user.CompanyId ?? 1;

        var fy = await _fiscalYears.GetByIdAsync(req.FiscalYearId, ct);
        var lockMsg = FiscalPeriodGuard.Check(fy, req.Date);
        if (lockMsg is not null) return Result<int>.Failure(lockMsg);

        var set = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        if (set is null) return Result<int>.Failure("تنظیماتِ گردشگری تنظیم نشده (نگاشتِ حساب‌ها).");
        var isCredit = req.PaymentMethod == "نسیه";
        var customerSideAcc = isCredit ? set.ReceivableAccountId : set.CashAccountId;
        if (customerSideAcc is null || set.RevenueAccountId is null || set.CogsAccountId is null
            || set.SupplierDepositAccountId is null)
            return Result<int>.Failure("حساب‌های نقد/دریافتنی، درآمد، COGS و ودیعه در تنظیماتِ گردشگری کامل نیست.");

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // ── ساختِ سربرگ + خطوط (snapshotِ تأمین‌کننده و بها از محصول) ──
            var sale = TourismSale.Create(companyId, req.BranchId, req.Date, sellerPartyId,
                req.CustomerPartyId, req.PaymentMethod, req.Note);

            var products = (await _products.FindAsync(p => p.CompanyId == companyId, ct)).ToDictionary(p => p.Id);
            var lineMeta = new List<(TourismSaleLine Line, int GroupId)>();

            foreach (var dto in req.Lines)
            {
                if (!products.TryGetValue(dto.ProductId, out var prod))
                    return Fail("محصول یافت نشد.");
                if (prod.RequiresPassengerList && (dto.Passengers is null || dto.Passengers.Count == 0))
                    return Fail($"برای «{prod.Name}» لیستِ مسافر الزامی است.");

                var line = TourismSaleLine.Create(prod.Id, prod.SupplierPartyId, dto.Quantity,
                    dto.UnitSalePrice, dto.DiscountAmount, prod.PurchasePrice, dto.TravelDate);
                if (dto.Passengers is not null)
                    foreach (var p in dto.Passengers)
                        line.AddPassenger(SalePassenger.Create(p.FullName, p.NationalIdOrPassport, p.Phone));
                sale.AddLine(line);
                lineMeta.Add((line, prod.ProductGroupId ?? 0));
            }

            if (sale.TotalSale <= 0) return Fail("مبلغِ فروش باید بزرگ‌تر از صفر باشد.");

            // ── سندِ متوازن ──
            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                9 /*عمومی*/, $"فروشِ گردشگری — فروشنده {sellerPartyId}", $"TUR-{number}");
            int row = 1;
            var netToCustomer = sale.TotalSale - sale.TotalDiscount;
            v.AddItem(VoucherItem.Create(0, row++, customerSideAcc.Value, netToCustomer, 0,
                isCredit ? "دریافتنی از مشتری" : $"دریافتِ وجه ({req.PaymentMethod})"));
            if (sale.TotalDiscount > 0)
                v.AddItem(VoucherItem.Create(0, row++, set.SalesDiscountAccountId ?? set.RevenueAccountId!.Value,
                    sale.TotalDiscount, 0, "تخفیفِ فروش"));
            v.AddItem(VoucherItem.Create(0, row++, set.RevenueAccountId.Value, 0, sale.TotalSale, "درآمدِ فروشِ خدمات"));
            v.AddItem(VoucherItem.Create(0, row++, set.CogsAccountId.Value, sale.TotalCost, 0, "بهای تمام‌شدهٔ خدمات"));
            // برداشتِ ودیعه per-supplier (به یک حسابِ کنترلی، خطِ جدا برای هر تأمین‌کننده).
            foreach (var g in sale.Lines.GroupBy(l => l.SupplierPartyId))
            {
                var cost = g.Sum(l => l.Quantity * l.UnitCost);
                if (cost > 0)
                    v.AddItem(VoucherItem.Create(0, row++, set.SupplierDepositAccountId.Value, 0, cost,
                        $"برداشت از ودیعهٔ تأمین‌کننده {g.Key}"));
            }
            v.Post(_user.UserId ?? 0);
            await _vouchers.AddAsync(v, ct);
            await _uow.SaveChangesAsync(ct);   // تا v.Id و sale-line Idها ساخته شوند

            sale.SetVoucher(v.Id);
            await _sales.AddAsync(sale, ct);
            await _uow.SaveChangesAsync(ct);   // تا Idِ خطوطِ فروش برای رکوردِ پورسانت موجود شود

            // ── پورسانت per-line (موتورِ TUR-C2-1) ──
            var rules = (await _rules.FindAsync(r => r.CompanyId == companyId
                && r.SalespersonPartyId == sellerPartyId && r.Active, ct))
                .Where(r => DateInRange(req.Date, r.EffectiveFrom, r.EffectiveTo))
                .Select(r => new CommissionRuleSpec((EngineBasis)(int)r.Basis, r.Rate, r.ProductId, r.ProductGroupId))
                .ToList();
            var ym = PersianYearMonth(req.Date);

            foreach (var (line, groupId) in lineMeta)
            {
                var ctx = new CommissionContext(line.ProductId, groupId, line.Quantity,
                    line.Quantity * line.UnitSalePrice, line.DiscountAmount, line.Quantity * line.UnitCost,
                    set.SaleBaseAfterDiscountDefault);
                var rule = CommissionEngine.Resolve(rules, line.ProductId, groupId);
                var amount = CommissionEngine.Compute(rule, ctx);
                if (amount <= 0) continue;
                var baseAmount = rule!.Basis switch
                {
                    EngineBasis.PerUnit => line.Quantity,
                    EngineBasis.PercentOfProfit => line.Quantity * line.UnitSalePrice - line.DiscountAmount - line.Quantity * line.UnitCost,
                    _ => (set.SaleBaseAfterDiscountDefault ? line.Quantity * line.UnitSalePrice - line.DiscountAmount : line.Quantity * line.UnitSalePrice)
                };
                await _commissions.AddAsync(SalesCommissionEntry.Create(companyId, line.Id, sellerPartyId,
                    (DomainBasis)(int)rule.Basis, baseAmount, rule.Rate, amount, ym), ct);
            }

            // U-PARTY-BAL (هم‌راستا با فروش/خریدِ هسته) — فروشِ نسیه بدهیِ جدید را به Party.Balanceِ
            // مشتری اضافه می‌کند؛ فروشِ نقدی/کارتی چیزی به مشتری بدهکار نمی‌کند، پس Balance دست‌نخورده می‌ماند.
            if (isCredit && req.CustomerPartyId is > 0)
            {
                var customer = await _parties.GetByIdAsync(req.CustomerPartyId.Value, ct);
                if (customer != null) customer.UpdateBalance(customer.Balance + netToCustomer);
            }

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(sale.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Fail(ex.GetBaseException().Message);
        }
    }

    private static Result<int> Fail(string m) => Result<int>.Failure(m);

    /// <summary>«YYYY/MM/DD» → «YYYYMM».</summary>
    internal static string PersianYearMonth(string date)
    {
        var p = (date ?? "").Replace('-', '/').Split('/');
        if (p.Length < 2) return "000000";
        return $"{p[0]}{p[1].PadLeft(2, '0')}";
    }

    /// <summary>آیا تاریخِ شمسیِ فروش در بازهٔ اعتبارِ قاعده است؟ (مقایسهٔ رشته‌ایِ YYYY/MM/DD معتبر است.)</summary>
    internal static bool DateInRange(string date, string from, string? to) =>
        string.Compare(date, from, StringComparison.Ordinal) >= 0
        && (string.IsNullOrWhiteSpace(to) || string.Compare(date, to, StringComparison.Ordinal) <= 0);
}
