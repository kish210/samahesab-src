namespace SamaHesab.Application.Accounting;

/// <summary>یک ردیفِ خامِ معین (سند مرجع + بدهکار/بستانکار).</summary>
public record AccountLedgerRawLine(int VoucherId, string VoucherNumber, string Date,
    decimal Debit, decimal Credit, string Description);

/// <summary>ردیفِ معین با ماندهٔ تجمعی — هر ردیف به سندِ مرجع (drill-down) اشاره می‌کند.</summary>
public record AccountLedgerRow(int VoucherId, string VoucherNumber, string Date, string Description,
    decimal Debit, decimal Credit, decimal Balance);

public record AccountLedgerResult(decimal OpeningBalance, IReadOnlyList<AccountLedgerRow> Rows,
    decimal TotalDebit, decimal TotalCredit, decimal ClosingBalance);

/// <summary>
/// دفترِ معینِ یک حساب با ماندهٔ تجمعی — منطقِ خالص و تست‌پذیر. رودمپ-گزارش‌ها:
/// «Drill-down یکپارچه: گزارش ← سندِ مرجع». از ماندهٔ ابتدای دوره + ردیف‌های سندِ همان حساب،
/// ماندهٔ روان می‌سازد. هر ردیف VoucherId/شمارهٔ سند را دارد تا UI سند را باز کند.
/// </summary>
public static class AccountLedger
{
    public static AccountLedgerResult Build(decimal openingBalance, IEnumerable<AccountLedgerRawLine> lines)
    {
        var ordered = lines
            .OrderBy(l => l.Date, StringComparer.Ordinal)
            .ThenBy(l => l.VoucherNumber, StringComparer.Ordinal)
            .ToList();

        var rows = new List<AccountLedgerRow>(ordered.Count);
        decimal running = openingBalance, totDebit = 0, totCredit = 0;

        foreach (var l in ordered)
        {
            running += l.Debit - l.Credit;
            totDebit += l.Debit;
            totCredit += l.Credit;
            rows.Add(new AccountLedgerRow(l.VoucherId, l.VoucherNumber, l.Date, l.Description,
                l.Debit, l.Credit, running));
        }

        return new AccountLedgerResult(openingBalance, rows, totDebit, totCredit, running);
    }
}
