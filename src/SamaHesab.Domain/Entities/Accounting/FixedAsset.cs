using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// داراییِ ثابت (U-FIXED-ASSET) — هم‌راستا با «نرم‌افزار دارایی ثابت»یِ راهکاران:
/// ثبتِ بهایِ تمام‌شده، عمرِ مفید، ارزشِ اسقاط و محاسبهٔ استهلاکِ ماهانه (خط مستقیم/نزولی)
/// بدونِ نیاز به ثبتِ دستی. چارتِ حساب از قبل حساب‌های «دارایی‌های ثابت» (2-01..2-07) و
/// «استهلاک» (8-03) را دارد؛ سندِ استهلاک به‌صورتِ تجمیعی توسطِ DepreciateFixedAssetsCommand
/// (بدهکارِ 8-03 / بستانکارِ 2-06) زده می‌شود.
/// </summary>
public class FixedAsset : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    /// <summary>تاریخِ خرید/بهره‌برداری به‌صورتِ شمسیِ «yyyy/MM/dd».</summary>
    public string PurchaseDate { get; private set; } = default!;
    public decimal PurchaseCost { get; private set; }
    public decimal SalvageValue { get; private set; }
    /// <summary>عمرِ مفید بر حسبِ ماه.</summary>
    public int UsefulLifeMonths { get; private set; }
    public DepreciationMethod Method { get; private set; } = DepreciationMethod.StraightLine;
    public bool IsActive { get; private set; } = true;
    /// <summary>استهلاکِ انباشته — کشِ سریع‌خوانی که با هر اجرایِ استهلاک به‌روز می‌شود.</summary>
    public decimal AccumulatedDepreciation { get; private set; }
    /// <summary>آخرین ماهِ استهلاک‌شده به‌صورتِ «yyyy/MM» — نقطهٔ شروعِ اجرایِ بعدی.</summary>
    public string? LastDepreciatedMonth { get; private set; }

    private FixedAsset() { }

    public static FixedAsset Create(int companyId, string code, string name, string purchaseDate,
        decimal purchaseCost, decimal salvageValue, int usefulLifeMonths,
        DepreciationMethod method = DepreciationMethod.StraightLine, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کدِ دارایی الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ دارایی الزامی است.");
        if (purchaseCost < 0) throw new ArgumentException("بهایِ تمام‌شده نمی‌تواند منفی باشد.");
        if (salvageValue < 0) throw new ArgumentException("ارزشِ اسقاط نمی‌تواند منفی باشد.");
        if (usefulLifeMonths <= 0) throw new ArgumentException("عمرِ مفید باید بزرگ‌تر از صفر باشد.");

        return new FixedAsset
        {
            CompanyId = companyId,
            Code = code,
            Name = name,
            PurchaseDate = purchaseDate,
            PurchaseCost = purchaseCost,
            SalvageValue = salvageValue,
            UsefulLifeMonths = usefulLifeMonths,
            Method = method,
            Description = description
        };
    }

    public void Update(string name, string purchaseDate, decimal purchaseCost, decimal salvageValue,
        int usefulLifeMonths, DepreciationMethod method, string? description)
    {
        Name = name;
        PurchaseDate = purchaseDate;
        PurchaseCost = purchaseCost;
        SalvageValue = salvageValue;
        UsefulLifeMonths = usefulLifeMonths;
        Method = method;
        Description = description;
        UpdatedAt = DateTime.Now;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.Now; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.Now; }

    /// <summary>اعمالِ استهلاکِ محاسبه‌شده برایِ یک دوره — فقط مبلغِ مثبت.</summary>
    public void ApplyDepreciation(decimal amount, string periodMonth)
    {
        if (amount <= 0) return;
        AccumulatedDepreciation += amount;
        LastDepreciatedMonth = periodMonth;
        UpdatedAt = DateTime.Now;
    }

    public decimal BookValue => PurchaseCost - AccumulatedDepreciation;
    public bool IsFullyDepreciated => BookValue <= SalvageValue + 0.01m;
    /// <summary>مبلغِ قابلِ‌استهلاکِ باقی‌مانده (بهایِ تمام‌شده منهایِ انباشته و اسقاط).</summary>
    public decimal RemainingDepreciable => Math.Max(0, BookValue - SalvageValue);
}
