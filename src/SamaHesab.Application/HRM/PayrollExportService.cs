using System.Globalization;
using System.Text;

namespace SamaHesab.Application.HRM;

/// <summary>
/// یک ردیفِ خروجیِ حقوق برای یک کارمند در یک ماه — ورودیِ خالصِ تولیدِ فایل‌ها.
/// مقادیر از فیشِ محاسبه‌شده (PAY-C2-1) + اطلاعاتِ کارمند (PAY-C1-1) پر می‌شوند.
/// </summary>
public record PayrollExportRow(
    string EmployeeName,          // نام و نامِ خانوادگی
    string NationalCode,          // کدِ ملی
    string InsuranceNo,           // شمارهٔ بیمه (تأمین اجتماعی)
    string Sheba,                 // شمارهٔ شبا/حساب (برای فایلِ بانک)
    int WorkDays,                 // روزهای کارکرد (۰..۳۱)
    decimal InsurableBase,        // مزدِ مشمولِ بیمه (روزانه+مزایا)
    decimal EmployeeInsurance,    // سهمِ بیمهٔ کارمند ۷٪
    decimal EmployerInsurance,    // سهمِ بیمهٔ کارفرما ۲۳٪
    decimal Tax,                  // مالیاتِ حقوق
    decimal TaxableIncome,        // درآمدِ مشمولِ مالیات
    decimal NetPay);              // خالصِ پرداختی (برای فایلِ بانک)

/// <summary>سرستونِ خلاصهٔ یک دورهٔ حقوق (ماه/سال + کارگاه).</summary>
public record PayrollPeriod(int Year, int Month, string WorkshopCode = "", string EmployerName = "");

/// <summary>
/// تولیدِ فایل‌های خروجیِ حقوق — منطقِ خالص و تست‌پذیر، بدونِ وابستگی به DB.
/// سه خروجی: لیستِ بیمه (تأمین اجتماعی) · لیستِ مالیاتِ حقوق · فایلِ پرداختِ بانک.
/// قالبِ CSV (سازگار با اکسل و قابلِ‌تبدیل به فرمتِ سازمان) با جداکنندهٔ قابلِ‌تنظیم.
/// </summary>
public static class PayrollExportService
{
    /// <summary>
    /// لیستِ بیمهٔ تأمین اجتماعی (معادلِ DSKWAGE): یک ردیف به‌ازای هر بیمه‌شده،
    /// شاملِ شمارهٔ بیمه/کدِ ملی/روزِ کارکرد/مزدِ مشمول/سهمِ کارمند/سهمِ کارفرما.
    /// </summary>
    public static string BuildInsuranceList(PayrollPeriod period, IEnumerable<PayrollExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", new[]
        {
            "ردیف", "شماره بیمه", "کد ملی", "نام بیمه‌شده", "روز کارکرد",
            "دستمزد مشمول", "سهم کارمند (۷٪)", "سهم کارفرما (۲۳٪)", "جمع حق بیمه"
        }.Select(Escape)));

        int i = 0;
        decimal totBase = 0, totEmp = 0, totEr = 0;
        foreach (var r in rows)
        {
            i++;
            totBase += r.InsurableBase; totEmp += r.EmployeeInsurance; totEr += r.EmployerInsurance;
            sb.AppendLine(string.Join(",", new[]
            {
                Num(i), Escape(r.InsuranceNo), Escape(r.NationalCode), Escape(r.EmployeeName),
                Num(r.WorkDays), Money(r.InsurableBase), Money(r.EmployeeInsurance),
                Money(r.EmployerInsurance), Money(r.EmployeeInsurance + r.EmployerInsurance)
            }));
        }
        sb.AppendLine(string.Join(",", new[]
        {
            Escape("جمع"), "", "", Escape($"کارگاه {period.WorkshopCode} — {period.Year}/{period.Month:00}"),
            "", Money(totBase), Money(totEmp), Money(totEr), Money(totEmp + totEr)
        }));
        return sb.ToString();
    }

    /// <summary>
    /// لیستِ مالیاتِ حقوق: یک ردیف به‌ازای هر کارمند با درآمدِ مشمول و مالیاتِ کسرشده.
    /// </summary>
    public static string BuildTaxList(PayrollPeriod period, IEnumerable<PayrollExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", new[]
        {
            "ردیف", "کد ملی", "نام", "درآمد مشمول مالیات", "مالیات"
        }.Select(Escape)));

        int i = 0;
        decimal totTaxable = 0, totTax = 0;
        foreach (var r in rows)
        {
            i++;
            totTaxable += r.TaxableIncome; totTax += r.Tax;
            sb.AppendLine(string.Join(",", new[]
            {
                Num(i), Escape(r.NationalCode), Escape(r.EmployeeName),
                Money(r.TaxableIncome), Money(r.Tax)
            }));
        }
        sb.AppendLine(string.Join(",", new[]
        {
            Escape("جمع"), "", Escape($"{period.Year}/{period.Month:00}"), Money(totTaxable), Money(totTax)
        }));
        return sb.ToString();
    }

    /// <summary>
    /// فایلِ پرداختِ بانکی (دسته‌ای): شبا/حساب + مبلغِ خالص + نام برای آپلود در درگاهِ بانک.
    /// ردیف‌های بدونِ شبا یا با خالصِ غیرمثبت حذف می‌شوند.
    /// </summary>
    public static string BuildBankFile(IEnumerable<PayrollExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", new[] { "ردیف", "شماره شبا/حساب", "مبلغ", "نام ذی‌نفع", "بابت" }.Select(Escape)));

        int i = 0;
        decimal total = 0;
        foreach (var r in rows)
        {
            if (string.IsNullOrWhiteSpace(r.Sheba) || r.NetPay <= 0) continue;
            i++;
            total += r.NetPay;
            sb.AppendLine(string.Join(",", new[]
            {
                Num(i), Escape(r.Sheba), Money(r.NetPay), Escape(r.EmployeeName), Escape("حقوق")
            }));
        }
        sb.AppendLine(string.Join(",", new[] { Escape("جمع"), "", Money(total), "", "" }));
        return sb.ToString();
    }

    private static string Money(decimal v) =>
        Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);

    private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

    /// <summary>قاعدهٔ CSV (RFC 4180): محصورسازی در صورتِ وجودِ ، یا " یا خط جدید.</summary>
    private static string Escape(string field)
    {
        field ??= "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }
}
