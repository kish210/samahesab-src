using MediatR;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Queries;

/// <summary>
/// UX-PURCHASE-VIEW — بارگذاریِ یک فاکتورِ خرید با اقلامش برای «حالتِ مشاهده» در فرمِ فاکتور.
/// (فقط خواندن؛ فرم اجازهٔ ثبتِ دوباره نمی‌دهد تا سندِ تکراری نسازد.) قرینهٔ GetSalesInvoiceByIdQuery.
/// </summary>
public record GetPurchaseInvoiceByIdQuery(int Id) : IRequest<PurchaseInvoiceDetailDto?>;

public record PurchaseInvoiceDetailItem(
    int ProductId, string Code, string Name, decimal Quantity, decimal UnitPrice,
    decimal DiscountPct, decimal TaxPct, string? Description);

public record PurchaseInvoiceDetailDto(
    int Id, string Number, string Date, int SupplierId, int WarehouseId,
    decimal Shipping, decimal OtherCosts, decimal GrandTotal, decimal PaidAmount, decimal RemainAmount,
    string? DueDate, string? Description, string InvoiceType, string StatusCode,
    IReadOnlyList<PurchaseInvoiceDetailItem> Items, string? SupplierName = null);

public class GetPurchaseInvoiceByIdQueryHandler : IRequestHandler<GetPurchaseInvoiceByIdQuery, PurchaseInvoiceDetailDto?>
{
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly IRepository<PurchaseInvoiceItem> _items;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Party> _parties;
    public GetPurchaseInvoiceByIdQueryHandler(IRepository<PurchaseInvoice> invoices,
        IRepository<PurchaseInvoiceItem> items, IRepository<Product> products, IRepository<Party> parties)
    { _invoices = invoices; _items = items; _products = products; _parties = parties; }

    public async Task<PurchaseInvoiceDetailDto?> Handle(GetPurchaseInvoiceByIdQuery req, CancellationToken ct)
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
            return new PurchaseInvoiceDetailItem(l.ProductId, p?.Code ?? "", p?.Name ?? $"#{l.ProductId}",
                l.Quantity, l.UnitPrice, l.DiscountPct, l.TaxPct, l.Description);
        }).ToList();

        var supplier = await _parties.GetByIdAsync(inv.SupplierId, ct);

        return new PurchaseInvoiceDetailDto(
            inv.Id, inv.InvoiceNumber, inv.InvoiceDate, inv.SupplierId, inv.WarehouseId,
            inv.Shipping, inv.OtherCosts, inv.GrandTotal, inv.PaidAmount, inv.RemainAmount,
            inv.DueDate, inv.Description, inv.InvoiceType, inv.StatusCode, items, supplier?.FullName);
    }
}
