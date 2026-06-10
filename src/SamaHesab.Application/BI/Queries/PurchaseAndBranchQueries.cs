using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

public record TopSupplierDto(int SupplierId, string Name, decimal Total, int InvoiceCount);
public record BranchPerformanceDto(int BranchId, string Name, decimal Total, int InvoiceCount);

// ── Top Suppliers (by purchase value) ────────────────────────────────────────
public record GetTopSuppliersQuery(string FromDate, string ToDate, int Take = 10)
    : IRequest<List<TopSupplierDto>>;

public class GetTopSuppliersQueryHandler : IRequestHandler<GetTopSuppliersQuery, List<TopSupplierDto>>
{
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly IRepository<Supplier> _suppliers;
    private readonly ICurrentUserService _currentUser;

    public GetTopSuppliersQueryHandler(IRepository<PurchaseInvoice> invoices,
        IRepository<Supplier> suppliers, ICurrentUserService currentUser)
    { _invoices = invoices; _suppliers = suppliers; _currentUser = currentUser; }

    public async Task<List<TopSupplierDto>> Handle(GetTopSuppliersQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var all = await _invoices.FindAsync(
            i => i.CompanyId == companyId && i.StatusCode == "قطعی", ct);
        var inRange = all.Where(i => string.CompareOrdinal(i.InvoiceDate, req.FromDate) >= 0
                                  && string.CompareOrdinal(i.InvoiceDate, req.ToDate) <= 0);

        var ranked = SalesAnalytics.TopParties(
            inRange.Select(i => new SalesRecord(i.InvoiceDate, i.SupplierId, i.GrandTotal)), req.Take);

        var names = (await _suppliers.FindAsync(s => s.CompanyId == companyId, ct))
            .ToDictionary(s => s.Id, s => s.FullName);

        return ranked
            .Select(r => new TopSupplierDto(r.Id,
                names.TryGetValue(r.Id, out var n) ? n : $"#{r.Id}", r.Total, r.Count))
            .ToList();
    }
}

// ── Branch Performance (sales by branch) ─────────────────────────────────────
public record GetBranchPerformanceQuery(string FromDate, string ToDate)
    : IRequest<List<BranchPerformanceDto>>;

public class GetBranchPerformanceQueryHandler
    : IRequestHandler<GetBranchPerformanceQuery, List<BranchPerformanceDto>>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Branch> _branches;
    private readonly ICurrentUserService _currentUser;

    public GetBranchPerformanceQueryHandler(IRepository<SalesInvoice> invoices,
        IRepository<Branch> branches, ICurrentUserService currentUser)
    { _invoices = invoices; _branches = branches; _currentUser = currentUser; }

    public async Task<List<BranchPerformanceDto>> Handle(GetBranchPerformanceQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var sales = await SalesQueryHelper.LoadSalesAsync(_invoices, companyId, req.FromDate, req.ToDate, ct);

        var ranked = SalesAnalytics.TopParties(
            sales.Select(i => new SalesRecord(i.InvoiceDate, i.BranchId, i.GrandTotal)), int.MaxValue);

        var names = (await _branches.FindAsync(b => b.CompanyId == companyId, ct))
            .ToDictionary(b => b.Id, b => b.Name);

        return ranked
            .Select(r => new BranchPerformanceDto(r.Id,
                names.TryGetValue(r.Id, out var n) ? n : $"شعبه #{r.Id}", r.Total, r.Count))
            .ToList();
    }
}
