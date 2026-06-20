using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>
/// درآمد/سود (مدلِ کسب‌وکار، طبقِ فرمولِ کاربر):
///   سود = فروش − خرید − هزینه − مالیات
/// «فروش/خرید» خالصِ بدونِ مالیات‌اند؛ «هزینه» از حساب‌های گروهِ ۷/۸/۹ (AccountClassifier)؛
/// «مالیات» مالیاتِ فروش. ردیف‌های ماهانه برای «لیست درآمدها».
/// </summary>
public record IncomeRow(string Period, decimal Sales, decimal Purchase, decimal Expense, decimal Tax)
{
    public decimal Profit => Sales - Purchase - Expense - Tax;
}

public record IncomeSummaryDto(
    decimal Sales, decimal Purchase, decimal Expense, decimal Tax, decimal Profit,
    List<IncomeRow> Months);

public record GetIncomeSummaryQuery(string FromDate, string ToDate, int? BranchId = null)
    : IRequest<IncomeSummaryDto>;

public class GetIncomeSummaryQueryHandler : IRequestHandler<GetIncomeSummaryQuery, IncomeSummaryDto>
{
    private readonly IRepository<SalesInvoice> _sales;
    private readonly IRepository<PurchaseInvoice> _purchases;
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public GetIncomeSummaryQueryHandler(IRepository<SalesInvoice> sales, IRepository<PurchaseInvoice> purchases,
        IVoucherRepository vouchers, IAccountRepository accounts, ICurrentUserService currentUser)
    { _sales = sales; _purchases = purchases; _vouchers = vouchers; _accounts = accounts; _currentUser = currentUser; }

    private static bool InRange(string date, string from, string to)
        => string.CompareOrdinal(date, from) >= 0 && string.CompareOrdinal(date, to) <= 0;

    private static string Month(string date) => date.Length >= 7 ? date.Substring(0, 7) : date;

    public async Task<IncomeSummaryDto> Handle(GetIncomeSummaryQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;

        // فروشِ قطعی‌شده در بازه (خالصِ بدونِ مالیات + مالیاتِ فروش)
        // قطعی = Posted (دارای سند) یا Confirmed (نهایی بدونِ سند، وقتی چارتِ حساب تنظیم نشده) — هر دو فروشِ واقعی‌اند.
        var sales = (await _sales.FindAsync(i => i.CompanyId == companyId
                        && (i.Status == InvoiceStatus.Posted || i.Status == InvoiceStatus.Confirmed), ct))
            .Where(i => (req.BranchId == null || i.BranchId == req.BranchId) && InRange(i.InvoiceDate, req.FromDate, req.ToDate))
            .ToList();

        // خریدِ قطعی‌شده در بازه (خالصِ بدونِ مالیات)
        var purchases = (await _purchases.FindAsync(p => p.CompanyId == companyId && p.StatusCode == "قطعی", ct))
            .Where(p => InRange(p.InvoiceDate, req.FromDate, req.ToDate))
            .ToList();

        // هزینه‌ها از اسنادِ حسابداری (گروه‌های ۷/۸/۹)
        var accounts = (await _accounts.GetByCompanyAsync(companyId, ct)).ToDictionary(a => a.Id, a => a.Code);
        var vouchers = (await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct))
            .Where(v => req.BranchId == null || v.BranchId == req.BranchId)
            .ToList();

        decimal ExpenseOfMonth(string? month)
        {
            decimal sum = 0;
            foreach (var v in vouchers)
            {
                if (month != null && Month(v.VoucherDate) != month) continue;
                foreach (var it in v.Items)
                {
                    if (!accounts.TryGetValue(it.AccountId, out var code)) continue;
                    if (AccountClassifier.Classify(code) == AccountCategory.Expense)
                        sum += it.Debit - it.Credit;
                }
            }
            return sum;
        }

        decimal SalesNet(SalesInvoice i) => i.SubTotal - i.TotalDiscount;
        decimal PurchNet(PurchaseInvoice p) => p.SubTotal - p.TotalDiscount;

        // ردیف‌های ماهانه
        var months = sales.Select(i => Month(i.InvoiceDate))
            .Concat(purchases.Select(p => Month(p.InvoiceDate)))
            .Concat(vouchers.Select(v => Month(v.VoucherDate)))
            .Where(m => !string.IsNullOrEmpty(m))
            .Distinct().OrderBy(m => m, StringComparer.Ordinal).ToList();

        var rows = months.Select(m => new IncomeRow(
            m,
            sales.Where(i => Month(i.InvoiceDate) == m).Sum(SalesNet),
            purchases.Where(p => Month(p.InvoiceDate) == m).Sum(PurchNet),
            ExpenseOfMonth(m),
            sales.Where(i => Month(i.InvoiceDate) == m).Sum(i => i.TotalTax)
        )).ToList();

        var totalSales = sales.Sum(SalesNet);
        var totalPurchase = purchases.Sum(PurchNet);
        var totalExpense = ExpenseOfMonth(null);
        var totalTax = sales.Sum(i => i.TotalTax);
        var profit = totalSales - totalPurchase - totalExpense - totalTax;

        return new IncomeSummaryDto(totalSales, totalPurchase, totalExpense, totalTax, profit, rows);
    }
}
