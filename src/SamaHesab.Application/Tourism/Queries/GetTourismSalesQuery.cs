using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Tourism;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Tourism.Queries;

/// <summary>
/// لیستِ فروشِ گردشگری برای گریدِ فروش — هر ردیف با «نزدیک‌ترین تاریخِ سفر» و «تعدادِ مسافر»
/// (بک‌اندِ موردِ رودمپ: نمای رزرو/تاریخِ سفر در ردیف). فیلترِ بازهٔ تاریخِ اختیاری.
/// </summary>
public record GetTourismSalesQuery(string? FromDate = null, string? ToDate = null)
    : IRequest<List<TourismSaleRowDto>>;

public record TourismSaleRowDto(
    int SaleId, string Date, string CustomerName, string SalespersonName,
    string? NearestTravelDate, int PassengerCount,
    decimal NetSale, decimal Profit, string PaymentMethod, bool IsPosted);

public class GetTourismSalesQueryHandler : IRequestHandler<GetTourismSalesQuery, List<TourismSaleRowDto>>
{
    private readonly IRepository<TourismSale> _sales;
    private readonly IRepository<TourismSaleLine> _lines;
    private readonly IRepository<SalePassenger> _passengers;
    private readonly IRepository<Party> _parties;
    private readonly ICurrentUserService _user;

    public GetTourismSalesQueryHandler(IRepository<TourismSale> sales, IRepository<TourismSaleLine> lines,
        IRepository<SalePassenger> passengers, IRepository<Party> parties, ICurrentUserService user)
    { _sales = sales; _lines = lines; _passengers = passengers; _parties = parties; _user = user; }

    public async Task<List<TourismSaleRowDto>> Handle(GetTourismSalesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;

        var sales = (await _sales.FindAsync(s => s.CompanyId == companyId, ct))
            .Where(s => TourismSalesView.InRange(s.Date, req.FromDate, req.ToDate))
            .ToList();
        if (sales.Count == 0) return new();

        var saleIds = sales.Select(s => s.Id).ToHashSet();
        var lines = (await _lines.FindAsync(l => saleIds.Contains(l.SaleId), ct)).ToList();
        var lineIds = lines.Select(l => l.Id).ToHashSet();

        // نزدیک‌ترین تاریخِ سفرِ هر فروش = کوچک‌ترین تاریخِ شمسیِ ناتهیِ خطوط (مقایسهٔ رشته‌ای = زمانی).
        var nearestBySale = lines
            .GroupBy(l => l.SaleId)
            .ToDictionary(g => g.Key, g => TourismSalesView.NearestTravelDate(g.Select(l => l.TravelDate)));

        // تعدادِ مسافرِ هر فروش = جمعِ مسافرانِ خطوطِ آن.
        var lineToSale = lines.ToDictionary(l => l.Id, l => l.SaleId);
        var paxBySale = (await _passengers.FindAsync(p => lineIds.Contains(p.SaleLineId), ct))
            .GroupBy(p => lineToSale[p.SaleLineId])
            .ToDictionary(g => g.Key, g => g.Count());

        var names = (await _parties.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id, p => p.FullName);

        return sales
            .OrderByDescending(s => s.Date)
            .Select(s => new TourismSaleRowDto(
                s.Id, s.Date,
                s.CustomerPartyId is int cid ? names.GetValueOrDefault(cid, "—") : "مشتریِ نقدی",
                names.GetValueOrDefault(s.SalespersonPartyId, $"#{s.SalespersonPartyId}"),
                nearestBySale.GetValueOrDefault(s.Id),
                paxBySale.GetValueOrDefault(s.Id),
                s.TotalSale - s.TotalDiscount, s.TotalProfit, s.PaymentMethod,
                IsPosted: s.VoucherId is not null))
            .ToList();
    }
}
