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

        // P1 — آشتیِ صورت‌حساب با ماندهٔ مرجعِ مشتری (`Party.Balance`).
        //   `Party.Balance` همهٔ رویدادها را در خود دارد: فاکتورها، دریافت/پرداختِ مستقلِ خزانه
        //   (که FIFO به PaidAmountِ فاکتورها هم می‌خورد)، پیش‌پرداخت/مازاد، ماندهٔ اولیه و تعدیل‌های دستی.
        //   صورت‌حسابِ فاکتورمحورِ قبلی فقط فعالیتِ فاکتورها را می‌دید و از ماندهٔ مرجع واگرا می‌شد.
        //   راهکار: «ماندهٔ اول دوره» = ماندهٔ مرجع منهای فعالیتِ فاکتورهای **از FromDate به بعد** →
        //   مانده در پایان دقیقاً به ماندهٔ مرجع (به‌تاریخِ ToDate) می‌رسد و با کارت/اعتبار مشتری یکی می‌شود.
        var allInvoices = (await _sales.FindAsync(
            i => i.CompanyId == companyId && i.CustomerId == req.CustomerId, ct)).ToList();

        decimal Net(SalesInvoice i) => i.GrandTotal - i.PaidAmount;

        // فعالیتِ خالصِ فاکتورهای از ابتدای بازه به بعد (شاملِ پس از ToDate) — برای محاسبهٔ ماندهٔ اول دوره.
        decimal netFromRangeStart = allInvoices
            .Where(i => string.IsNullOrEmpty(req.FromDate) || string.Compare(i.InvoiceDate, req.FromDate, StringComparison.Ordinal) >= 0)
            .Sum(Net);
        decimal openingBalance = customer.Balance - netFromRangeStart;

        var invoices = allInvoices
            .Where(i => string.IsNullOrEmpty(req.FromDate) || string.Compare(i.InvoiceDate, req.FromDate, StringComparison.Ordinal) >= 0)
            .Where(i => string.IsNullOrEmpty(req.ToDate)   || string.Compare(i.InvoiceDate, req.ToDate, StringComparison.Ordinal) <= 0)
            .OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id)
            .ToList();

        var rows = new List<StatementRow>();
        decimal balance = openingBalance, totalDebit = 0, totalCredit = 0;

        // ردیفِ ماندهٔ اول دوره (ماندهٔ پیش از بازه + پیش‌پرداخت/ماندهٔ اولیه/تعدیل‌های غیرفاکتوری).
        if (openingBalance != 0)
            rows.Add(new StatementRow(req.FromDate ?? "", "مانده اول دوره", "", "مانده اول دوره",
                openingBalance > 0 ? openingBalance : 0, openingBalance < 0 ? -openingBalance : 0, balance));

        foreach (var inv in invoices)
        {
            // Invoice increases what the customer owes us (debit).
            balance += inv.GrandTotal; totalDebit += inv.GrandTotal;
            rows.Add(new StatementRow(inv.InvoiceDate, "فاکتور فروش", inv.InvoiceNumber,
                $"فاکتور فروش {inv.InvoiceNumber}", inv.GrandTotal, 0, balance));

            // Amount paid (شاملِ تخصیصِ FIFOِ دریافت‌های مستقلِ خزانه) reduces the balance (credit).
            if (inv.PaidAmount > 0)
            {
                balance -= inv.PaidAmount; totalCredit += inv.PaidAmount;
                rows.Add(new StatementRow(inv.InvoiceDate, "دریافت", inv.InvoiceNumber,
                    $"دریافت بابت فاکتور {inv.InvoiceNumber}", 0, inv.PaidAmount, balance));
            }
        }

        // مانده در پایان = ماندهٔ مرجعِ مشتری (به‌تاریخِ ToDate) — آشتی‌شده با کارت/اعتبار.
        return Result<CustomerStatement>.Success(new CustomerStatement(
            customer.Id, customer.FullName, totalDebit, totalCredit, balance, rows));
    }
}
