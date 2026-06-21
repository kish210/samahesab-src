using System.Text;

namespace SamaHesab.Application.Accounting;

/// <summary>یک ردیفِ خام از چسباندنِ اکسل — حساب هنوز تطبیق داده نشده (توکنِ کد یا نام).</summary>
public record VoucherPasteRow(string AccountToken, string? Description, decimal Debit, decimal Credit);

/// <summary>
/// بهره‌وریِ ثبتِ سند — تجزیهٔ متنِ چسبانده‌شده از اکسل (TSV) به ردیف‌های سند.
/// ستون‌ها: «حساب ⭾ شرح ⭾ بدهکار ⭾ بستانکار» (۴ ستون) یا «حساب ⭾ بدهکار ⭾ بستانکار» (۳ ستون).
/// منطقِ خالص/تست‌پذیر (بدونِ UI/EF). تطبیقِ واقعیِ حساب در ViewModel انجام می‌شود.
/// </summary>
public static class VoucherPasteParser
{
    public static IReadOnlyList<VoucherPasteRow> Parse(string? clipboardText)
    {
        var rows = new List<VoucherPasteRow>();
        if (string.IsNullOrWhiteSpace(clipboardText)) return rows;

        foreach (var raw in clipboardText.Replace("\r", "").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var c = raw.Split('\t');
            if (c.Length < 3) continue;   // حداقل: حساب + بدهکار + بستانکار

            var token = c[0].Trim();
            string? desc; decimal debit, credit;
            if (c.Length >= 4) { desc = c[1].Trim(); debit = Num(c[2]); credit = Num(c[3]); }
            else { desc = null; debit = Num(c[1]); credit = Num(c[2]); }

            if (token.Length == 0) continue;
            if (debit == 0 && credit == 0) continue;        // ردیفِ بی‌مبلغ
            if (debit > 0 && credit > 0) continue;            // نمی‌تواند هم بدهکار هم بستانکار باشد

            rows.Add(new VoucherPasteRow(token, string.IsNullOrWhiteSpace(desc) ? null : desc, debit, credit));
        }
        return rows;
    }

    /// <summary>تبدیلِ عددِ فارسی/عربی + حذفِ جداکننده‌های هزارگان → decimal (۰ در صورتِ ناموفق).</summary>
    public static decimal Num(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        var sb = new StringBuilder();
        foreach (var ch in s.Trim())
        {
            var c = ch;
            if (c >= '۰' && c <= '۹') c = (char)('0' + (c - '۰'));       // ارقامِ فارسی
            else if (c >= '٠' && c <= '٩') c = (char)('0' + (c - '٠'));   // ارقامِ عربی-هندی
            if (char.IsDigit(c) || c == '.') sb.Append(c);
            // جداکننده‌ها (، ٬ , فاصله ﷼ ریال …) نادیده گرفته می‌شوند
        }
        return decimal.TryParse(sb.ToString(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 0;
    }
}
