using System.Globalization;

namespace SamaHesab.Application.Accounting;

/// <summary>
/// پارسر صورت‌حساب بانک از متن CSV — منطق خالص و تست‌پذیر.
/// هر خط: «تاریخ,مبلغ[,شرح]» — تاریخ شمسی yyyy/MM/dd.
/// خطِ سرستون (header) و خطوط خالی نادیده گرفته می‌شوند. ارقام فارسی پشتیبانی می‌شود.
/// </summary>
public static class BankStatementParser
{
    public static List<StatementLine> Parse(string csv)
    {
        var result = new List<StatementLine>();
        if (string.IsNullOrWhiteSpace(csv)) return result;

        foreach (var raw in csv.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var cols = line.Split(',');
            if (cols.Length < 2) continue;

            var date = NormalizeDigits(cols[0].Trim());
            var amountText = NormalizeDigits(cols[1].Trim()).Replace("،", "").Replace(",", "");

            // خطِ سرستون یا ردیف نامعتبر را رد کن
            if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                continue;

            var reference = cols.Length >= 3 ? cols[2].Trim() : null;
            if (string.IsNullOrWhiteSpace(reference)) reference = null;

            result.Add(new StatementLine(date, amount, reference));
        }

        return result;
    }

    /// <summary>تبدیل ارقام فارسی/عربی به لاتین.</summary>
    private static string NormalizeDigits(string input)
    {
        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c >= '۰' && c <= '۹') chars[i] = (char)('0' + (c - '۰'));        // فارسی
            else if (c >= '٠' && c <= '٩') chars[i] = (char)('0' + (c - '٠'));   // عربی
        }
        return new string(chars);
    }
}
