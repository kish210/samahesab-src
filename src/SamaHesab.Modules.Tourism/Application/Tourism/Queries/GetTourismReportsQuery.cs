using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Tourism.Application.Queries;

/// <summary>
/// TUR-C2-5 — گزارش‌های گردشگری. داده را از جدول‌های <c>Tur.*</c> می‌خواند و با
/// <see cref="TourismReportBuilder"/> به چهار <see cref="ReportTable"/>ِ آماده تبدیل می‌کند
/// (ماندهٔ ودیعهٔ تأمین‌کننده · سودِ محصول · پورسانتِ ماهانهٔ فروشنده · عملکردِ فروشنده).
/// خروجی مستقیماً در صفحهٔ گزارش نمایش و با <see cref="ReportExporter"/> به CSV/HTML صادر می‌شود.
/// </summary>
/// <param name="Period">ماهِ شمسی «YYYYMM» برای فیلترِ پورسانت؛ خالی = همهٔ دوره‌ها.</param>
public record GetTourismReportsQuery(string? Period = null) : IRequest<TourismReportsDto>;

public record TourismReportsDto(
    ReportTable SupplierDeposits,
    ReportTable ProductProfit,
    ReportTable MonthlyCommission,
    ReportTable SellerPerformance);

public class GetTourismReportsQueryHandler : IRequestHandler<GetTourismReportsQuery, TourismReportsDto>
{
    private readonly IRepository<TourismSale> _sales;
    private readonly IRepository<TourismSaleLine> _lines;
    private readonly IRepository<SalesCommissionEntry> _commissions;
    private readonly IRepository<SupplierDeposit> _deposits;
    private readonly IRepository<TourismProduct> _products;
    private readonly IRepository<Party> _parties;
    private readonly ICurrentUserService _user;

    public GetTourismReportsQueryHandler(IRepository<TourismSale> sales, IRepository<TourismSaleLine> lines,
        IRepository<SalesCommissionEntry> commissions, IRepository<SupplierDeposit> deposits,
        IRepository<TourismProduct> products, IRepository<Party> parties, ICurrentUserService user)
    {
        _sales = sales; _lines = lines; _commissions = commissions; _deposits = deposits;
        _products = products; _parties = parties; _user = user;
    }

    public async Task<TourismReportsDto> Handle(GetTourismReportsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;

        // نام‌های طرف‌حساب و محصولِ همین شرکت (Tenant-scoped) — برای جلوگیری از نشتِ چندمستأجری.
        var partyNames = (await _parties.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id, p => p.FullName);
        var productNames = (await _products.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id, p => p.Name);

        var sales = await _sales.FindAsync(s => s.CompanyId == companyId, ct);
        var saleIds = sales.Select(s => s.Id).ToHashSet();
        var lines = (await _lines.FindAsync(l => saleIds.Contains(l.SaleId), ct)).ToList();

        // ── ۱) ماندهٔ ودیعهٔ تأمین‌کنندگان: شارژ − برداشت(بهای خطوط) ──
        var charged = (await _deposits.FindAsync(d => d.CompanyId == companyId, ct))
            .GroupBy(d => d.SupplierPartyId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Amount));
        var drawn = lines.GroupBy(l => l.SupplierPartyId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity * l.UnitCost));
        var depositRows = charged.Keys.Union(drawn.Keys)
            .Select(id => new SupplierDepositRow(
                partyNames.GetValueOrDefault(id, $"#{id}"),
                charged.GetValueOrDefault(id), drawn.GetValueOrDefault(id)))
            .OrderBy(r => r.Charged - r.Drawn)
            .ToList();

        // ── ۲) سودِ محصولات ──
        var profitRows = lines.GroupBy(l => l.ProductId)
            .Select(g => new ProductProfitRow(
                productNames.GetValueOrDefault(g.Key, $"#{g.Key}"),
                g.Sum(l => l.Quantity),
                g.Sum(l => l.Quantity * l.UnitSalePrice - l.DiscountAmount),
                g.Sum(l => l.Quantity * l.UnitCost),
                g.Sum(l => l.LineProfit)))
            .OrderByDescending(r => r.Profit)
            .ToList();

        // ── ۳) پورسانتِ ماهانهٔ فروشنده (فیلترِ دوره) ──
        var lineNet = lines.ToDictionary(l => l.Id, l => l.Quantity * l.UnitSalePrice - l.DiscountAmount);
        var commEntries = (await _commissions.FindAsync(
                c => c.CompanyId == companyId && (req.Period == null || c.PersianYearMonth == req.Period), ct))
            .ToList();
        var commissionRows = commEntries.GroupBy(c => c.SalespersonPartyId)
            .Select(g => new SellerCommissionRow(
                partyNames.GetValueOrDefault(g.Key, $"#{g.Key}"),
                g.Select(c => c.SaleLineId).Distinct().Count(),
                g.Sum(c => lineNet.GetValueOrDefault(c.SaleLineId)),
                g.Sum(c => c.CommissionAmount)))
            .OrderByDescending(r => r.Commission)
            .ToList();

        // ── ۴) عملکردِ فروشندگان (تعداد فروش / خالص / سود + پورسانتِ هم‌دوره) ──
        var commBySeller = commissionRows.ToDictionary(r => r.SellerName, r => r.Commission);
        var perfRows = sales.GroupBy(s => s.SalespersonPartyId)
            .Select(g =>
            {
                var name = partyNames.GetValueOrDefault(g.Key, $"#{g.Key}");
                return new SellerPerformanceRow(
                    name, g.Count(),
                    g.Sum(s => s.TotalSale - s.TotalDiscount),
                    g.Sum(s => s.TotalProfit),
                    commBySeller.GetValueOrDefault(name));
            })
            .OrderByDescending(r => r.NetSale)
            .ToList();

        var period = string.IsNullOrWhiteSpace(req.Period) ? "همهٔ دوره‌ها" : req.Period!;
        return new TourismReportsDto(
            TourismReportBuilder.SupplierDepositBalance(depositRows),
            TourismReportBuilder.ProductProfit(profitRows),
            TourismReportBuilder.MonthlyCommission(period, commissionRows),
            TourismReportBuilder.SellerPerformance(perfRows));
    }
}
