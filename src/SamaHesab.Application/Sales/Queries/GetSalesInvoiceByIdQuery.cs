using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Queries;

/// <summary>
/// UX-SALES-VIEW — بارگذاریِ یک فاکتورِ فروش با اقلامش برای «حالتِ مشاهده» در فرمِ فاکتور.
/// (فقط خواندن؛ فرم اجازهٔ ثبتِ دوباره نمی‌دهد تا سندِ تکراری نسازد.)
/// </summary>
public record GetSalesInvoiceByIdQuery(int Id) : IRequest<SalesInvoiceDetailDto?>;

public record SalesInvoiceDetailItem(
    int ProductId, string Code, string Name, decimal Quantity, decimal UnitPrice,
    decimal DiscountPct, decimal TaxPct, string? Description);

public record SalesInvoiceDetailDto(
    int Id, string Number, string Date, int CustomerId, int WarehouseId, string PriceLevel,
    decimal Shipping, decimal OtherCosts, decimal GrandTotal, decimal PaidAmount, decimal RemainAmount,
    string? DueDate, string? Description, string? Reference, string? Title,
    string InvoiceType, IReadOnlyList<SalesInvoiceDetailItem> Items, decimal InvoiceDiscount = 0);

public class GetSalesInvoiceByIdQueryHandler : IRequestHandler<GetSalesInvoiceByIdQuery, SalesInvoiceDetailDto?>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceItem> _items;
    private readonly IRepository<Product> _products;
    public GetSalesInvoiceByIdQueryHandler(IRepository<SalesInvoice> invoices,
        IRepository<SalesInvoiceItem> items, IRepository<Product> products)
    { _invoices = invoices; _items = items; _products = products; }

    private static string TypeFa(InvoiceType t) => t switch
    {
        InvoiceType.SaleReturn => "برگشت از فروش",
        InvoiceType.Quotation => "پیش‌فاکتور",
        _ => "فروش"
    };

    public async Task<SalesInvoiceDetailDto?> Handle(GetSalesInvoiceByIdQuery req, CancellationToken ct)
    {
        var inv = await _invoices.GetByIdAsync(req.Id, ct);
        if (inv == null) return null;

        var lines = (await _items.FindAsync(it => it.InvoiceId == req.Id, ct))
            .OrderBy(it => it.RowNumber).ToList();
        var pids = lines.Select(l => l.ProductId).ToHashSet();
        var prods = (await _products.FindAsync(p => pids.Contains(p.Id), ct))
            .ToDictionary(p => p.Id);

        var items = lines.Select(l =>
        {
            prods.TryGetValue(l.ProductId, out var p);
            return new SalesInvoiceDetailItem(l.ProductId, p?.Code ?? "", p?.Name ?? $"#{l.ProductId}",
                l.Quantity, l.UnitPrice, l.DiscountPct, l.TaxPct, l.Description);
        }).ToList();

        return new SalesInvoiceDetailDto(
            inv.Id, inv.InvoiceNumber, inv.InvoiceDate, inv.CustomerId, inv.WarehouseId, inv.PriceLevel,
            inv.Shipping, inv.OtherCosts, inv.GrandTotal, inv.PaidAmount, inv.RemainAmount,
            inv.DueDate, inv.Description, inv.Reference, inv.Title, TypeFa(inv.InvoiceType), items,
            inv.InvoiceDiscount);
    }
}
