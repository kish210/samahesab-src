using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Modules.Tourism.Application;
using SamaHesab.Modules.Tourism.Application.Commands;
using DomainBasis = SamaHesab.Modules.Tourism.Domain.CommissionBasis;
using EngineBasis = SamaHesab.Modules.Tourism.Application.CommissionBasis;

namespace SamaHesab.Modules.Tourism.Application.Itinerary;

// ───────────────────────── کوئریِ مشاهدهٔ برنامه (پنلِ مهمان) ─────────────────────────

/// <summary>
/// دریافتِ برنامهٔ اقامتیِ مهمان با توکنِ یکتا (لینکِ پنلِ مهمان). بدونِ نیاز به احراز هویت:
/// توکنِ GUID خودش کلیدِ دسترسی است (در درخواستِ ناشناس فیلترِ multi-tenant غیرفعال است، پس
/// جستجو بر توکن کار می‌کند؛ چون توکن غیرقابلِ‌حدس است، مهمان فقط برنامهٔ خودش را می‌بیند).
/// </summary>
public record GetGuestItineraryQuery(string Token) : IRequest<Result<GuestItineraryDto>>;

public record GuestStopDto(
    int StopId, int Day, int SortOrder, int ProductId, string ProductName,
    int SessionId, string SessionLabel, int StartMinute, int EndMinute, decimal SalePrice);

public record GuestItineraryDto(
    string Token, string GuestName, int Days, string Status, string CreatedDate,
    string? Notes, decimal TotalSale, IReadOnlyList<GuestStopDto> Stops);

public class GetGuestItineraryQueryHandler : IRequestHandler<GetGuestItineraryQuery, Result<GuestItineraryDto>>
{
    private readonly IRepository<GuestItinerary> _itineraries;
    private readonly IRepository<ItineraryStop> _stops;
    private readonly IRepository<TourismProduct> _products;
    private readonly IRepository<ProductSession> _sessions;

    public GetGuestItineraryQueryHandler(IRepository<GuestItinerary> itineraries, IRepository<ItineraryStop> stops,
        IRepository<TourismProduct> products, IRepository<ProductSession> sessions)
    { _itineraries = itineraries; _stops = stops; _products = products; _sessions = sessions; }

    public async Task<Result<GuestItineraryDto>> Handle(GetGuestItineraryQuery req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Token)) return Result<GuestItineraryDto>.Failure("توکن نامعتبر است.");

        var it = await _itineraries.FindSingleAsync(g => g.Token == req.Token, ct);
        if (it is null) return Result<GuestItineraryDto>.Failure("برنامه‌ای با این لینک یافت نشد.");

        var stops = await _stops.FindAsync(s => s.ItineraryId == it.Id, ct);
        var productIds = stops.Select(s => s.ProductId).ToHashSet();
        var sessionIds = stops.Select(s => s.SessionId).ToHashSet();
        var pNames = (await _products.FindAsync(p => productIds.Contains(p.Id), ct)).ToDictionary(p => p.Id, p => p.Name);
        var sLabels = (await _sessions.FindAsync(s => sessionIds.Contains(s.Id), ct)).ToDictionary(s => s.Id, s => s.Label);

        var dto = new GuestItineraryDto(
            it.Token, it.GuestName, it.Days, it.Status.ToString(), it.CreatedDate, it.Notes, it.TotalSale,
            stops.OrderBy(s => s.DayNumber).ThenBy(s => s.SortOrder)
                .Select(s => new GuestStopDto(
                    s.Id, s.DayNumber, s.SortOrder, s.ProductId, pNames.GetValueOrDefault(s.ProductId, $"#{s.ProductId}"),
                    s.SessionId, sLabels.GetValueOrDefault(s.SessionId, ""), s.StartMinute, s.EndMinute, s.SalePrice))
                .ToList());
        return Result<GuestItineraryDto>.Success(dto);
    }
}

// ───────────────────────── ویرایش/تأییدِ مهمان (پنلِ مهمان) ─────────────────────────

/// <summary>
/// ویرایش (حذفِ برخی اقلام) و/یا تأییدِ نهاییِ برنامه توسطِ مهمان — با توکنِ یکتا.
/// Confirm=true → وضعیت Confirmed؛ وگرنه GuestEdited (هنوز قابلِ تغییر). اقلامِ حذف‌شده پاک می‌شوند.
/// </summary>
public record SubmitGuestItineraryCommand(
    string Token, IReadOnlyList<int> RemovedStopIds, bool Confirm, string? Notes = null)
    : IRequest<Result>;

public class SubmitGuestItineraryCommandValidator : AbstractValidator<SubmitGuestItineraryCommand>
{
    public SubmitGuestItineraryCommandValidator()
        => RuleFor(x => x.Token).NotEmpty().WithMessage("توکن الزامی است.");
}

public class SubmitGuestItineraryCommandHandler : IRequestHandler<SubmitGuestItineraryCommand, Result>
{
    private readonly IRepository<GuestItinerary> _itineraries;
    private readonly IRepository<ItineraryStop> _stops;
    private readonly IRepository<TourismSetting> _settings;
    private readonly IRepository<TourismProduct> _products;
    private readonly IRepository<TourismSale> _sales;
    private readonly IRepository<CommissionRule> _rules;
    private readonly IRepository<SalesCommissionEntry> _commissions;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IRepository<Party> _parties;
    private readonly IPersianCalendarService _calendar;
    private readonly IUnitOfWork _uow;

    public SubmitGuestItineraryCommandHandler(IRepository<GuestItinerary> itineraries,
        IRepository<ItineraryStop> stops, IRepository<TourismSetting> settings, IRepository<TourismProduct> products,
        IRepository<TourismSale> sales, IRepository<CommissionRule> rules, IRepository<SalesCommissionEntry> commissions,
        IVoucherRepository vouchers, IRepository<FiscalYear> fiscalYears, IRepository<Party> parties,
        IPersianCalendarService calendar, IUnitOfWork uow)
    {
        _itineraries = itineraries; _stops = stops; _settings = settings; _products = products;
        _sales = sales; _rules = rules; _commissions = commissions;
        _vouchers = vouchers; _fiscalYears = fiscalYears; _parties = parties; _calendar = calendar; _uow = uow;
    }

    public async Task<Result> Handle(SubmitGuestItineraryCommand req, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var it = await _itineraries.FindSingleAsync(g => g.Token == req.Token, ct);
            if (it is null) return await Rollback("برنامه‌ای با این لینک یافت نشد.");
            // ملاکِ «قفل» = سند خورده (IsBilled)، نه صرفاً Confirmed — تا برنامه‌های Confirmedِ بدونِ سند
            // (دادهٔ ساخته‌شده پیش از MOD-TIT-BILL) هم بتوانند با تأییدِ دوباره سند بخورند.
            if (it.IsBilled) return await Rollback("این برنامه قبلاً خرید (سند) شده است.");

            if (req.RemovedStopIds is { Count: > 0 })
            {
                var toRemove = await _stops.FindAsync(s => s.ItineraryId == it.Id && req.RemovedStopIds.Contains(s.Id), ct);
                if (toRemove.Count > 0) _stops.RemoveRange(toRemove);
            }

            if (req.Confirm)
            {
                // MOD-TIT-BILL — تأییدِ مهمان = خرید: سندِ فروشِ متوازن + دریافتنی از مهمان (نسیه) صادر می‌شود
                // و رکوردِ TourismSale ثبت می‌شود تا در گزارش‌های گردشگری دیده شود. (منطقِ حسابداری هم‌راستا با
                // CreateTourismSaleCommand، شاخهٔ «نسیه»: Dr دریافتنی / Cr درآمد ‖ Dr COGS / Cr ودیعهٔ تأمین‌کننده.)
                var billMsg = await BillOnConfirmAsync(it, ct);
                if (billMsg is not null) return await Rollback(billMsg);
                it.ConfirmByGuest(req.Notes);
            }
            else it.MarkGuestEdited();
            _itineraries.Update(it);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result.Success();
        }
        catch (System.Exception ex) { return await Rollback(ex.GetBaseException().Message); }
    }

    /// <summary>ساختِ سندِ فروش + دریافتنی هنگامِ تأیید. خروجی: پیامِ خطا یا null در موفقیت.</summary>
    private async Task<string?> BillOnConfirmAsync(GuestItinerary it, CancellationToken ct)
    {
        if (it.IsBilled) return null;   // قبلاً سند خورده (idempotent)

        var stops = (await _stops.FindAsync(s => s.ItineraryId == it.Id, ct)).ToList();
        if (stops.Count == 0) return null;   // برنامهٔ خالی → بدونِ سند تأیید می‌شود

        var companyId = it.CompanyId;
        var set = await _settings.FindSingleAsync(s => s.CompanyId == companyId, ct);
        if (set is null) return "تنظیماتِ گردشگری تنظیم نشده (نگاشتِ حساب‌ها).";
        if (set.ReceivableAccountId is null || set.RevenueAccountId is null
            || set.CogsAccountId is null || set.SupplierDepositAccountId is null)
            return "حساب‌های دریافتنی/درآمد/COGS/ودیعه در تنظیماتِ گردشگری کامل نیست.";

        var fy = await _fiscalYears.FindSingleAsync(f => f.CompanyId == companyId && f.IsActive && !f.IsClosed, ct)
                 ?? await _fiscalYears.FindSingleAsync(f => f.CompanyId == companyId && !f.IsClosed, ct);
        if (fy is null) return "سالِ مالیِ فعالی برای صدورِ سند یافت نشد.";
        var date = _calendar.GetCurrentPersianDate();

        // سرسند + خطوط (نسیه: مهمان بدهکار می‌شود → دریافتنی = «درخواستِ پول از مهمان»).
        var sale = TourismSale.Create(companyId, it.BranchId, date, it.SalespersonPartyId ?? 0,
            it.GuestPartyId, "نسیه", $"خریدِ اقامتیِ مهمان «{it.GuestName}»");
        var products = (await _products.FindAsync(p => p.CompanyId == companyId, ct)).ToDictionary(p => p.Id);
        var lineMeta = new List<(TourismSaleLine Line, int GroupId)>();
        foreach (var st in stops.OrderBy(s => s.DayNumber).ThenBy(s => s.SortOrder))
        {
            if (!products.TryGetValue(st.ProductId, out var prod)) return $"محصولِ #{st.ProductId} یافت نشد.";
            var line = TourismSaleLine.Create(prod.Id, prod.SupplierPartyId, 1, st.SalePrice, 0, prod.PurchasePrice, null);
            sale.AddLine(line);
            lineMeta.Add((line, prod.ProductGroupId ?? 0));
        }
        if (sale.TotalSale <= 0) return "مبلغِ برنامه صفر است؛ سندی صادر نشد.";

        // سندِ متوازن (نسیه).
        var number = await _vouchers.GetNextNumberAsync(companyId, ct);
        var v = Voucher.Create(companyId, it.BranchId, fy.Id, number, date, 9 /*عمومی*/,
            $"خریدِ اقامتیِ مهمان «{it.GuestName}» (برنامه {it.Id})", $"TIT-{number}");
        int row = 1;
        v.AddItem(VoucherItem.Create(0, row++, set.ReceivableAccountId.Value, sale.TotalSale, 0, "دریافتنی از مهمان"));
        v.AddItem(VoucherItem.Create(0, row++, set.RevenueAccountId.Value, 0, sale.TotalSale, "درآمدِ فروشِ خدماتِ اقامتی"));
        v.AddItem(VoucherItem.Create(0, row++, set.CogsAccountId.Value, sale.TotalCost, 0, "بهای تمام‌شدهٔ خدمات"));
        foreach (var g in sale.Lines.GroupBy(l => l.SupplierPartyId))
        {
            var cost = g.Sum(l => l.Quantity * l.UnitCost);
            if (cost > 0)
                v.AddItem(VoucherItem.Create(0, row++, set.SupplierDepositAccountId.Value, 0, cost,
                    $"برداشت از ودیعهٔ تأمین‌کننده {g.Key}"));
        }
        v.Post(it.CreatedByUserId ?? 1);
        await _vouchers.AddAsync(v, ct);
        await _uow.SaveChangesAsync(ct);

        sale.SetVoucher(v.Id);
        await _sales.AddAsync(sale, ct);
        await _uow.SaveChangesAsync(ct);   // تا Idِ خطوطِ فروش برای رکوردِ پورسانت موجود شود

        // پیگیریِ اختیاریِ MOD-TIT-BILL — پورسانتِ فروشنده per-line (هم‌راستا با CreateTourismSaleCommand)،
        // فقط اگر برنامه فروشنده داشته باشد (خرید از پنلِ مهمان می‌تواند بدونِ فروشنده هم باشد).
        if (it.SalespersonPartyId is > 0)
        {
            var sellerPartyId = it.SalespersonPartyId.Value;
            var rules = (await _rules.FindAsync(r => r.CompanyId == companyId
                && r.SalespersonPartyId == sellerPartyId && r.Active, ct))
                .Where(r => CreateTourismSaleCommandHandler.DateInRange(date, r.EffectiveFrom, r.EffectiveTo))
                .Select(r => new CommissionRuleSpec((EngineBasis)(int)r.Basis, r.Rate, r.ProductId, r.ProductGroupId))
                .ToList();
            var ym = CreateTourismSaleCommandHandler.PersianYearMonth(date);

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
            await _uow.SaveChangesAsync(ct);
        }

        // U-PARTY-BAL (هم‌راستا با فروش/خرید/CreateTourismSaleCommand) — تأییدِ مهمان همیشه نسیه است
        // (Dr دریافتنی از مهمان)، پس بدهیِ جدید باید به Party.Balanceِ مهمان اضافه شود.
        if (it.GuestPartyId is > 0)
        {
            var guest = await _parties.GetByIdAsync(it.GuestPartyId.Value, ct);
            if (guest != null) guest.UpdateBalance(guest.Balance + sale.TotalSale);
        }

        it.SetSale(sale.Id);
        return null;
    }

    private async Task<Result> Rollback(string msg)
    {
        try { await _uow.RollbackTransactionAsync(default); } catch { /* ignore */ }
        return Result.Failure(msg);
    }
}
