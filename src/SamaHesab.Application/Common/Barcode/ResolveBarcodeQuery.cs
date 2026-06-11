using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Common.Barcode;

/// <summary>کار #۲۷ — حل بارکد به کالا (مشترک: POS/رسید/حواله/انبارگردانی). از طریق Web API هم افشا می‌شود.</summary>
public record ResolveBarcodeQuery(string Code) : IRequest<BarcodeHit?>;

/// <summary>نتیجه‌ی حل بارکد. <c>EmbeddedQuantity</c> فقط برای بارکدهای وزنی مقدار دارد.</summary>
public record BarcodeHit(int ProductId, string Code, string Name, decimal SalePrice,
    decimal TaxRate, int? GroupId, bool Weighted, decimal? EmbeddedValue);

public class ResolveBarcodeQueryHandler : IRequestHandler<ResolveBarcodeQuery, BarcodeHit?>
{
    private readonly IProductRepository _products;
    private readonly ICurrentUserService _user;

    public ResolveBarcodeQueryHandler(IProductRepository products, ICurrentUserService user)
    { _products = products; _user = user; }

    public async Task<BarcodeHit?> Handle(ResolveBarcodeQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var code = BarcodeParser.Normalize(req.Code);
        if (code.Length == 0) return null;

        // ۱) تطبیق کامل بارکد
        var product = await _products.GetByBarcodeAsync(companyId, code, ct);

        // ۲) بارکد وزنی/قیمت‌دار: کد کالای جاسازی‌شده را استخراج و جستجو کن
        bool weighted = false; decimal? embedded = null;
        if (product is null && BarcodeParser.TryParseWeighted(code, out var itemCode, out var value))
        {
            weighted = true; embedded = value;
            product = await _products.GetByBarcodeAsync(companyId, itemCode, ct)
                      ?? await _products.GetByCodeAsync(companyId, itemCode, ct);
        }

        // ۳) تطبیق با کد کالا
        product ??= await _products.GetByCodeAsync(companyId, code, ct);

        if (product is null) return null;
        return new BarcodeHit(product.Id, product.Code, product.Name, product.SalePrice,
            product.TaxRate, product.GroupId, weighted, embedded);
    }
}
