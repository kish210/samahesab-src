using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

public record StatementRow(string Date, string DocType, string DocNumber, string Description,
    decimal Debit, decimal Credit, decimal Balance);

public record CustomerStatement(int CustomerId, string CustomerName,
    decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance, IReadOnlyList<StatementRow> Rows);

public record GetCustomerStatementQuery(int CustomerId, string? FromDate = null, string? ToDate = null)
    : IRequest<Result<CustomerStatement>>;

public class GetCustomerStatementQueryHandler
    : IRequestHandler<GetCustomerStatementQuery, Result<CustomerStatement>>
{
    private readonly IRepository<SalesInvoice> _sales;
    private readonly IRepository<Party> _customers;
    private readonly ICurrentUserService _currentUser;

    public GetCustomerStatementQueryHandler(IRepository<SalesInvoice> sales,
        IRepository<Party> customers, ICurrentUserService currentUser)
    { _sales = sales; _customers = customers; _currentUser = currentUser; }

    public async Task<Result<CustomerStatement>> Handle(GetCustomerStatementQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var customer = await _customers.GetByIdAsync(req.CustomerId, ct);
        if (customer == null) return Result<CustomerStatement>.Failure("مشتری یافت نشد.");

        var invoices = (await _sales.FindAsync(
            i => i.CompanyId == companyId && i.CustomerId == req.CustomerId, ct))
            .Where(i => string.IsNullOrEmpty(req.FromDate) || string.Compare(i.InvoiceDate, req.FromDate, StringComparison.Ordinal) >= 0)
            .Where(i => string.IsNullOrEmpty(req.ToDate)   || string.Compare(i.InvoiceDate, req.ToDate, StringComparison.Ordinal) <= 0)
            .OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id)
            .ToList();

        var rows = new List<StatementRow>();
        decimal balance = 0, totalDebit = 0, totalCredit = 0;
        foreach (var inv in invoices)
        {
            // Invoice increases what the customer owes us (debit).
            balance += inv.GrandTotal; totalDebit += inv.GrandTotal;
            rows.Add(new StatementRow(inv.InvoiceDate, "فاکتور فروش", inv.InvoiceNumber,
                $"فاکتور فروش {inv.InvoiceNumber}", inv.GrandTotal, 0, balance));

            // Amount paid at invoice time reduces the balance (credit).
            if (inv.PaidAmount > 0)
            {
                balance -= inv.PaidAmount; totalCredit += inv.PaidAmount;
                rows.Add(new StatementRow(inv.InvoiceDate, "دریافت", inv.InvoiceNumber,
                    $"دریافت بابت فاکتور {inv.InvoiceNumber}", 0, inv.PaidAmount, balance));
            }
        }

        return Result<CustomerStatement>.Success(new CustomerStatement(
            customer.Id, customer.FullName, totalDebit, totalCredit, balance, rows));
    }
}
