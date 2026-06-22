using System.Globalization;
using SamaHesab.Application.Reports.Export;

namespace SamaHesab.Application.Contracting;

// ── ورودی‌های گزارش (DTOهای ساده؛ C1 از جداولِ Con پر می‌کند) ──

/// <summary>دادهٔ چاپِ رسمیِ یک صورت‌وضعیت.</summary>
public record StatementPrintoutData(
    string ProjectTitle, string ProjectCode, string EmployerName, int Number, string TypeLabel, string Date,
    decimal CumulativeGrossWork, decimal PreviousCumulative, decimal PeriodWork,
    decimal AdjustmentAmount, decimal MaterialDiffAmount, decimal GrossThisPeriod,
    decimal AdvanceRecovery, decimal Retention, decimal Insurance, decimal Tax, decimal Penalty, decimal Other,
    decimal NetPayable);

/// <summary>یک ردیفِ خلاصهٔ مالیِ پروژه.</summary>
public record ProjectSummaryRow(string Code, string Title, decimal ContractAmount, decimal CumulativeBilled,
    decimal RetentionHeld, decimal InsuranceHeld, decimal AdvanceOutstanding, decimal NetReceivable);

/// <summary>یک ردیفِ سپردهٔ نگه‌داشته per پروژه.</summary>
public record DepositRow(string ProjectTitle, decimal RetentionHeld, decimal InsuranceHeld);

/// <summary>یک ردیفِ دفترِ ضمانت‌نامه.</summary>
public record GuaranteeRow(string ProjectTitle, string TypeLabel, string Bank, decimal Amount,
    string ExpiryDate, int? DaysToExpiry, string StatusLabel);

/// <summary>
/// سازنده‌های گزارشِ ماژولِ پیمانکاری — منطقِ خالص و تست‌پذیر، بدونِ DB.
/// خروجی `ReportTable` (با `ReportExporter` به CSV/HTMLِ راست‌چین). هم‌الگوی TourismReportBuilder.
/// </summary>
public static class ContractingReportBuilder
{
    /// <summary>چاپِ رسمیِ صورت‌وضعیت (ناخالص/قبلی/دوره/تعدیل/کسورات/خالص) — چیدمانِ ایرانی.</summary>
    public static ReportTable StatementPrintout(StatementPrintoutData s)
    {
        var rows = new List<string[]>
        {
            new[] { "کارفرما", s.EmployerName },
            new[] { "نوع", s.TypeLabel },
            new[] { "تاریخ", s.Date },
            new[] { "کارکردِ تجمعی", N(s.CumulativeGrossWork) },
            new[] { "کسرِ کارکردِ قبلی", N(s.PreviousCumulative) },
            new[] { "کارکردِ این دوره", N(s.PeriodWork) },
            new[] { "تعدیل", N(s.AdjustmentAmount) },
            new[] { "مابه‌التفاوتِ مصالح", N(s.MaterialDiffAmount) },
            new[] { "ناخالصِ این دوره", N(s.GrossThisPeriod) },
            new[] { "کسر: بازیافتِ پیش‌پرداخت", N(s.AdvanceRecovery) },
            new[] { "کسر: حسن‌انجام‌کار", N(s.Retention) },
            new[] { "کسر: بیمه", N(s.Insurance) },
            new[] { "کسر: مالیات", N(s.Tax) },
            new[] { "کسر: جریمه", N(s.Penalty) },
            new[] { "کسر: سایر", N(s.Other) },
            new[] { "خالصِ قابلِ پرداخت", N(s.NetPayable) },
        };
        return new ReportTable(
            $"صورت‌وضعیتِ {s.TypeLabel} شمارهٔ {s.Number} — پیمانِ {s.ProjectCode} ({s.ProjectTitle})",
            new[] { "شرح", "مبلغ (ریال)" }, rows);
    }

    /// <summary>خلاصهٔ مالیِ پروژه‌ها: مبلغِ پیمان، کارکرد، سپرده‌ها، ماندهٔ پیش‌پرداخت، خالصِ دریافتنی.</summary>
    public static ReportTable ProjectFinancialSummary(IEnumerable<ProjectSummaryRow> rows)
    {
        var list = rows?.ToList() ?? new();
        var body = list.Select(r => new[]
        {
            r.Code, r.Title, N(r.ContractAmount), N(r.CumulativeBilled),
            Pct(r.CumulativeBilled, r.ContractAmount), N(r.RetentionHeld), N(r.InsuranceHeld),
            N(r.AdvanceOutstanding), N(r.NetReceivable)
        }).ToList();
        body.Add(new[] { "", "جمع", N(list.Sum(r => r.ContractAmount)), N(list.Sum(r => r.CumulativeBilled)), "",
            N(list.Sum(r => r.RetentionHeld)), N(list.Sum(r => r.InsuranceHeld)),
            N(list.Sum(r => r.AdvanceOutstanding)), N(list.Sum(r => r.NetReceivable)) });
        return new ReportTable("خلاصهٔ مالیِ پیمان‌ها",
            new[] { "کد", "عنوان", "مبلغِ پیمان", "کارکردِ تجمعی", "پیشرفت", "حسن‌انجام", "بیمه", "ماندهٔ پیش‌پرداخت", "خالصِ دریافتنی" },
            body);
    }

    /// <summary>سپرده‌های نگه‌داشته (حسن‌انجام‌کار + بیمه) per پروژه با ردیفِ جمع.</summary>
    public static ReportTable DepositsHeld(IEnumerable<DepositRow> rows)
    {
        var list = rows?.ToList() ?? new();
        var body = list.Select(r => new[]
        {
            r.ProjectTitle, N(r.RetentionHeld), N(r.InsuranceHeld), N(r.RetentionHeld + r.InsuranceHeld)
        }).ToList();
        body.Add(new[] { "جمع", N(list.Sum(r => r.RetentionHeld)), N(list.Sum(r => r.InsuranceHeld)),
            N(list.Sum(r => r.RetentionHeld + r.InsuranceHeld)) });
        return new ReportTable("سپرده‌های نگه‌داشته (حسن‌انجام‌کار و بیمه)",
            new[] { "پیمان", "حسن‌انجام‌کار", "بیمه", "جمعِ سپرده" }, body);
    }

    /// <summary>دفترِ ضمانت‌نامه‌ها با روزهای ماندهٔ انقضا.</summary>
    public static ReportTable GuaranteeRegister(IEnumerable<GuaranteeRow> rows)
    {
        var list = rows?.ToList() ?? new();
        var body = list.Select(r => new[]
        {
            r.ProjectTitle, r.TypeLabel, r.Bank, N(r.Amount), r.ExpiryDate,
            r.DaysToExpiry is int d ? d.ToString(CultureInfo.InvariantCulture) : "—", r.StatusLabel
        }).ToList();
        return new ReportTable("دفترِ ضمانت‌نامه‌ها",
            new[] { "پیمان", "نوع", "بانک", "مبلغ", "تاریخِ انقضا", "روزِ ماندهٔ انقضا", "وضعیت" }, body);
    }

    private static string N(decimal v) =>
        Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture);

    private static string Pct(decimal part, decimal whole) =>
        whole == 0 ? "—" : (part / whole * 100m).ToString("0.#", CultureInfo.InvariantCulture) + "٪";
}
