using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.CRM.Queries; // StatementRow (مشترک با صورت‌حساب مشتری)
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Queries;

/// <summary>
/// کارِ ۶ — صورت‌حساب/ماندهٔ تأمین‌کننده (قرینهٔ صورت‌حساب مشتری).
/// مانده = آنچه ما به تأمین‌کننده بدهکاریم. فاکتور خرید بدهیِ ما را زیاد می‌کند (بستانکار)،
/// مبلغِ پرداخت‌شده هنگام فاکتور آن را کم می‌کند (بدهکار). ردیف‌ها با ماندهٔ غلتان برمی‌گردند.
/// </summary>
public record SupplierStatement(int SupplierId, string SupplierName,
    decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance, IReadOnlyList<StatementRow> Rows);

public record GetSupplierStatementQuery(int SupplierId, string? FromDate = null, string? ToDate = null)
    : IRequest<Result<SupplierStatement>>;

public class GetSupplierStatementQueryHandler
    : IRequestHandler<GetSupplierStatementQuery, Result<SupplierStatement>>
{
    private readonly IRepository<PurchaseInvoice> _purchases;
    private readonly IRepository<Party> _suppliers;
    private readonly ICurrentUserService _currentUser;

    public GetSupplierStatementQueryHandler(IRepository<PurchaseInvoice> purchases,
        IRepository<Party> suppliers, ICurrentUserService currentUser)
    { _purchases = purchases; _suppliers = suppliers; _currentUser = currentUser; }

    public async Task<Result<SupplierStatement>> Handle(GetSupplierStatementQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var supplier = await _suppliers.GetByIdAsync(req.SupplierId, ct);
        if (supplier == null) return Result<SupplierStatement>.Failure("تأمین‌کننده یافت نشد.");

        var invoices = (await _purchases.FindAsync(
            i => i.CompanyId == companyId && i.SupplierId == req.SupplierId, ct))
            .Where(i => string.IsNullOrEmpty(req.FromDate) || string.Compare(i.InvoiceDate, req.FromDate, StringComparison.Ordinal) >= 0)
            .Where(i => string.IsNullOrEmpty(req.ToDate)   || string.Compare(i.InvoiceDate, req.ToDate, StringComparison.Ordinal) <= 0)
            .OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id)
            .ToList();

        var rows = new List<StatementRow>();
        decimal balance = 0, totalDebit = 0, totalCredit = 0;
        foreach (var inv in invoices)
        {
            var isReturn = inv.InvoiceType == "برگشت خرید";
            if (isReturn)
            {
                // مرجوعی خرید بدهیِ ما به تأمین‌کننده را کم می‌کند (بدهکار).
                balance -= inv.GrandTotal; totalDebit += inv.GrandTotal;
                rows.Add(new StatementRow(inv.InvoiceDate, "مرجوعی خرید", inv.InvoiceNumber,
                    $"مرجوعی خرید {inv.InvoiceNumber}", inv.GrandTotal, 0, balance));
            }
            else
            {
                // فاکتور خرید بدهیِ ما را زیاد می‌کند (بستانکار).
                balance += inv.GrandTotal; totalCredit += inv.GrandTotal;
                rows.Add(new StatementRow(inv.InvoiceDate, "فاکتور خرید", inv.InvoiceNumber,
                    $"فاکتور خرید {inv.InvoiceNumber}", 0, inv.GrandTotal, balance));

                // مبلغِ پرداخت‌شده هنگام فاکتور بدهیِ ما را کم می‌کند (بدهکار).
                if (inv.PaidAmount > 0)
                {
                    balance -= inv.PaidAmount; totalDebit += inv.PaidAmount;
                    rows.Add(new StatementRow(inv.InvoiceDate, "پرداخت", inv.InvoiceNumber,
                        $"پرداخت بابت فاکتور {inv.InvoiceNumber}", inv.PaidAmount, 0, balance));
                }
            }
        }

        return Result<SupplierStatement>.Success(new SupplierStatement(
            supplier.Id, supplier.FullName, totalDebit, totalCredit, balance, rows));
    }
}
