using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Queries;

/// <summary>فهرستِ فاکتورهای خرید (با نامِ تأمین‌کننده) — منبعِ واحد (API + دسکتاپ). الگوی API-only.</summary>
public record PurchaseInvoiceRowDto(int Id, string Number, string Date, string SupplierName,
    decimal Total, decimal Paid, decimal Remain, string Status);

public record GetPurchaseInvoicesQuery(string? FromDate = null, string? ToDate = null,
    string? Search = null) : IRequest<List<PurchaseInvoiceRowDto>>;

public class GetPurchaseInvoicesQueryHandler : IRequestHandler<GetPurchaseInvoicesQuery, List<PurchaseInvoiceRowDto>>
{
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly IRepository<Supplier> _suppliers;
    private readonly ICurrentUserService _currentUser;

    public GetPurchaseInvoicesQueryHandler(IRepository<PurchaseInvoice> invoices,
        IRepository<Supplier> suppliers, ICurrentUserService currentUser)
    { _invoices = invoices; _suppliers = suppliers; _currentUser = currentUser; }

    private static bool InRange(string date, string? from, string? to)
        => (string.IsNullOrEmpty(from) || string.CompareOrdinal(date, from) >= 0)
        && (string.IsNullOrEmpty(to) || string.CompareOrdinal(date, to) <= 0);

    public async Task<List<PurchaseInvoiceRowDto>> Handle(GetPurchaseInvoicesQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _invoices.FindAsync(i => i.CompanyId == companyId, ct);
        var suppliers = (await _suppliers.FindAsync(s => s.CompanyId == companyId, ct))
            .ToDictionary(s => s.Id, s => s.FullName);

        var term = req.Search?.Trim();
        var rows = new List<PurchaseInvoiceRowDto>();
        foreach (var inv in list.OrderByDescending(i => i.Id))
        {
            if (!InRange(inv.InvoiceDate, req.FromDate, req.ToDate)) continue;
            suppliers.TryGetValue(inv.SupplierId, out var sname);
            sname ??= $"#{inv.SupplierId}";
            if (!string.IsNullOrEmpty(term) && !(inv.InvoiceNumber.Contains(term) || sname.Contains(term))) continue;
            rows.Add(new PurchaseInvoiceRowDto(inv.Id, inv.InvoiceNumber, inv.InvoiceDate, sname,
                inv.GrandTotal, inv.PaidAmount, inv.RemainAmount, inv.StatusCode));
        }
        return rows;
    }
}
