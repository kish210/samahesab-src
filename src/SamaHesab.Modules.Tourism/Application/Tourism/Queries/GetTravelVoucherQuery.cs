using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Tourism.Application.Queries;

/// <summary>مونتاژِ واچرِ سفر (کارتِ مسافر/بلیت) برای یک فروشِ گردشگری — برای چاپ/تحویل به مشتری.</summary>
public record GetTravelVoucherQuery(int SaleId) : IRequest<Result<TravelVoucher>>;

public class GetTravelVoucherQueryHandler : IRequestHandler<GetTravelVoucherQuery, Result<TravelVoucher>>
{
    private readonly IRepository<TourismSale> _sales;
    private readonly IRepository<TourismSaleLine> _lines;
    private readonly IRepository<SalePassenger> _passengers;
    private readonly IRepository<TourismProduct> _products;
    private readonly IRepository<Party> _parties;
    private readonly ICurrentUserService _user;

    public GetTravelVoucherQueryHandler(IRepository<TourismSale> sales, IRepository<TourismSaleLine> lines,
        IRepository<SalePassenger> passengers, IRepository<TourismProduct> products,
        IRepository<Party> parties, ICurrentUserService user)
    { _sales = sales; _lines = lines; _passengers = passengers; _products = products; _parties = parties; _user = user; }

    public async Task<Result<TravelVoucher>> Handle(GetTravelVoucherQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var sale = await _sales.FindSingleAsync(s => s.Id == req.SaleId && s.CompanyId == companyId, ct);
        if (sale is null) return Result<TravelVoucher>.Failure("فروشِ گردشگری یافت نشد.");

        var lines = (await _lines.FindAsync(l => l.SaleId == sale.Id, ct)).ToList();
        var lineIds = lines.Select(l => l.Id).ToHashSet();
        var pax = (await _passengers.FindAsync(p => lineIds.Contains(p.SaleLineId), ct))
            .GroupBy(p => p.SaleLineId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var names = (await _parties.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id, p => p.FullName);
        var products = (await _products.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id, p => p.Name);

        var serviceLines = lines.Select(l => new VoucherServiceLine(
            ProductName: products.GetValueOrDefault(l.ProductId, $"#{l.ProductId}"),
            SupplierName: names.GetValueOrDefault(l.SupplierPartyId, $"#{l.SupplierPartyId}"),
            TravelDate: l.TravelDate,
            Quantity: l.Quantity,
            UnitSalePrice: l.UnitSalePrice,
            Passengers: (pax.GetValueOrDefault(l.Id) ?? new List<SalePassenger>())
                .Select(p => new VoucherPassenger(p.FullName, p.NationalIdOrPassport, p.Phone)).ToList()))
            .ToList();

        var header = new VoucherHeader(
            SaleId: sale.Id,
            VoucherNo: sale.VoucherId is int vid ? $"TUR-{vid}" : $"S-{sale.Id}",
            IssueDate: sale.Date,
            CustomerName: sale.CustomerPartyId is int cid ? names.GetValueOrDefault(cid, "—") : "مشتریِ نقدی",
            SalespersonName: names.GetValueOrDefault(sale.SalespersonPartyId, $"#{sale.SalespersonPartyId}"),
            PaymentMethod: sale.PaymentMethod);

        return Result<TravelVoucher>.Success(TravelVoucherBuilder.Build(header, serviceLines, sale.TotalDiscount));
    }
}
