namespace SamaHesab.Application.Accounting;

/// <summary>یک ردیف از صورت‌حساب بانک (وارد‌شده از فایل).</summary>
public record StatementLine(string Date, decimal Amount, string? Reference = null);

/// <summary>یک ردیف از دفتر بانکِ سیستم (آرتیکل سند روی حساب بانک).</summary>
public record LedgerLine(int VoucherItemId, string Date, decimal Amount);

/// <summary>یک تطبیق بین ردیف صورت‌حساب و ردیف دفتر.</summary>
public record ReconMatch(LedgerLine Ledger, StatementLine Statement);

/// <summary>نتیجه‌ی مغایرت‌گیری: منطبق‌ها + نامنطبق‌های هر طرف.</summary>
public record ReconResult(
    IReadOnlyList<ReconMatch> Matched,
    IReadOnlyList<LedgerLine> UnmatchedLedger,
    IReadOnlyList<StatementLine> UnmatchedStatement);

/// <summary>
/// موتور تطبیق خودکار صورت‌حساب بانک با دفتر — منطق خالص و تست‌پذیر.
/// تطبیق بر اساس برابری «مبلغ» و «تاریخ» (یک‌به‌یک، حریصانه)؛ باقی‌مانده‌ها دستی بررسی می‌شوند.
/// </summary>
public static class BankReconciliation
{
    public static ReconResult AutoMatch(
        IEnumerable<LedgerLine> ledger,
        IEnumerable<StatementLine> statement)
    {
        var ledgerList = ledger.ToList();
        var matched = new List<ReconMatch>();
        var usedLedger = new HashSet<int>();      // index در ledgerList
        var unmatchedStatement = new List<StatementLine>();

        foreach (var st in statement)
        {
            var idx = -1;
            for (int i = 0; i < ledgerList.Count; i++)
            {
                if (usedLedger.Contains(i)) continue;
                if (ledgerList[i].Amount == st.Amount && ledgerList[i].Date == st.Date)
                {
                    idx = i;
                    break;
                }
            }

            if (idx >= 0)
            {
                usedLedger.Add(idx);
                matched.Add(new ReconMatch(ledgerList[idx], st));
            }
            else
            {
                unmatchedStatement.Add(st);
            }
        }

        var unmatchedLedger = ledgerList
            .Where((_, i) => !usedLedger.Contains(i))
            .ToList();

        return new ReconResult(matched, unmatchedLedger, unmatchedStatement);
    }
}
