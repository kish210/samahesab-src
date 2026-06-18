using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Queries;

/// <summary>
/// فهرستِ فاکتورهای فروش (با نامِ مشتری و وضعیتِ فارسی). منبعِ واحدِ منطق —
/// هم API (SalesController) و هم دسکتاپ از همین کوئری می‌خوانند (الگوی API-only).
/// </summary>
public record SalesInvoiceRowDto(int Id, string Number, string Date, string CustomerName,
    decimal Total, decimal Paid, decimal Remain, string Status);

public record GetSalesInvoicesQuery(string? FromDate = null, string? ToDate = null,
    string? Status = null, string? Search = null) : IRequest<List<SalesInvoiceRowDto>>;

public class GetSalesInvoicesQueryHandler : IRequestHandler<GetSalesInvoicesQuery, List<SalesInvoiceRowDto>>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Customer> _customers;
    private readonly ICurrentUserService _currentUser;

    public GetSalesInvoicesQueryHandler(IRepository<SalesInvoice> invoices,
        IRepository<Customer> customers, ICurrentUserService currentUser)
    { _invoices = invoices; _customers = customers; _currentUser = currentUser; }

    private static string StatusFa(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft => "پیش‌نویس",
        InvoiceStatus.Confirmed => "قطعی",
        InvoiceStatus.Posted => "قطعی",
        InvoiceStatus.Cancelled => "لغو شده",
        _ => s.ToString()
    };

    private static bool InRange(string date, string? from, string? to)
        => (string.IsNullOrEmpty(from) || string.CompareOrdinal(date, from) >= 0)
        && (string.IsNullOrEmpty(to) || string.CompareOrdinal(date, to) <= 0);

    public async Task<List<SalesInvoiceRowDto>> Handle(GetSalesInvoicesQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _invoices.FindAsync(i => i.CompanyId == companyId, ct);
        var customers = (await _customers.FindAsync(c => c.CompanyId == companyId, ct))
            .ToDictionary(c => c.Id, c => c.FullName);

        var term = req.Search?.Trim();
        var rows = new List<SalesInvoiceRowDto>();
        foreach (var inv in list.OrderByDescending(i => i.Id))
        {
            if (!InRange(inv.InvoiceDate, req.FromDate, req.ToDate)) continue;
            var statusFa = StatusFa(inv.Status);
            if (!string.IsNullOrEmpty(req.Status) && req.Status != "همه" && statusFa != req.Status) continue;
            customers.TryGetValue(inv.CustomerId, out var cname);
            cname ??= $"#{inv.CustomerId}";
            if (!string.IsNullOrEmpty(term) && !(inv.InvoiceNumber.Contains(term) || cname.Contains(term))) continue;
            rows.Add(new SalesInvoiceRowDto(inv.Id, inv.InvoiceNumber, inv.InvoiceDate, cname,
                inv.GrandTotal, inv.PaidAmount, inv.RemainAmount, statusFa));
        }
        return rows;
    }
}
