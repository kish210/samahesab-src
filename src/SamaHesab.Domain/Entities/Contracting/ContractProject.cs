using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Contracting;

/// <summary>نوعِ قرارداد پیمان.</summary>
public enum ContractType { UnitPrice = 0, FixedPrice = 1, CostPlus = 2 }   // فهرست‌بها / مقطوع / امانی

/// <summary>وضعیتِ پیمان.</summary>
public enum ContractProjectStatus { Active = 0, Suspended = 1, Closed = 2 }

/// <summary>
/// CON-C1-1 — پیمان (Project/پیمانکاری). پیمانکار به کارفرما (EmployerPartyId) از طریقِ صورت‌وضعیت فاکتور می‌دهد.
/// درصدهای کسر اینجا پیش‌فرضِ پروژه‌اند (override بر تنظیماتِ سراسری). ProjectDimensionId → Acc.Project برای سود/زیان.
/// </summary>
public class ContractProject : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Title { get; private set; } = default!;
    public int EmployerPartyId { get; private set; }
    public ContractType ContractType { get; private set; }
    public decimal ContractAmount { get; private set; }
    public string StartDate { get; private set; } = default!;   // شمسی
    public int DurationDays { get; private set; }

    // درصدهای کسر (override پروژه؛ اگر صفر، پیش‌فرضِ تنظیماتِ سراسری به‌کار می‌رود)
    public decimal AdvancePercent { get; private set; }
    public decimal RetentionPercent { get; private set; }
    public decimal InsuranceWithholdPercent { get; private set; }
    public decimal TaxWithholdPercent { get; private set; }
    public bool AdjustmentEnabled { get; private set; }

    public int? ProjectDimensionId { get; private set; }        // → Acc.Project.Id (بُعدِ GL)
    public ContractProjectStatus Status { get; private set; } = ContractProjectStatus.Active;

    private ContractProject() { }

    public static ContractProject Create(int companyId, string code, string title, int employerPartyId,
        ContractType contractType, decimal contractAmount, string startDate, int durationDays = 0,
        decimal advancePercent = 0, decimal retentionPercent = 0, decimal insuranceWithholdPercent = 0,
        decimal taxWithholdPercent = 0, bool adjustmentEnabled = false, int? projectDimensionId = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کدِ پیمان الزامی است.");
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("عنوانِ پیمان الزامی است.");
        if (employerPartyId <= 0) throw new ArgumentException("کارفرما الزامی است.");
        if (contractAmount < 0) throw new ArgumentException("مبلغِ پیمان نمی‌تواند منفی باشد.");
        return new ContractProject
        {
            CompanyId = companyId, Code = code, Title = title, EmployerPartyId = employerPartyId,
            ContractType = contractType, ContractAmount = contractAmount, StartDate = startDate,
            DurationDays = durationDays < 0 ? 0 : durationDays,
            AdvancePercent = Nn(advancePercent), RetentionPercent = Nn(retentionPercent),
            InsuranceWithholdPercent = Nn(insuranceWithholdPercent), TaxWithholdPercent = Nn(taxWithholdPercent),
            AdjustmentEnabled = adjustmentEnabled, ProjectDimensionId = projectDimensionId
        };
    }

    public void Update(string title, ContractType contractType, decimal contractAmount, string startDate,
        int durationDays, decimal advancePercent, decimal retentionPercent, decimal insuranceWithholdPercent,
        decimal taxWithholdPercent, bool adjustmentEnabled, int? projectDimensionId)
    {
        if (!string.IsNullOrWhiteSpace(title)) Title = title;
        ContractType = contractType; ContractAmount = contractAmount < 0 ? 0 : contractAmount;
        if (!string.IsNullOrWhiteSpace(startDate)) StartDate = startDate;
        DurationDays = durationDays < 0 ? 0 : durationDays;
        AdvancePercent = Nn(advancePercent); RetentionPercent = Nn(retentionPercent);
        InsuranceWithholdPercent = Nn(insuranceWithholdPercent); TaxWithholdPercent = Nn(taxWithholdPercent);
        AdjustmentEnabled = adjustmentEnabled; ProjectDimensionId = projectDimensionId;
        SetAudit(null);
    }

    public void SetStatus(ContractProjectStatus status) { Status = status; SetAudit(null); }

    private static decimal Nn(decimal v) => v < 0 ? 0 : v;
}
