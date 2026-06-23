using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Hotel;

/// <summary>PMS-C1-1 — اجرای ممیزیِ شبانه (Night Audit): بستنِ روزِ کاری + سندِ درآمدِ اتاق/عوارض. idempotent بر BusinessDate.</summary>
public class NightAuditRun : AuditableEntity
{
    public string BusinessDate { get; private set; } = default!;   // شمسی — کلیدِ یکتاییِ روز
    public string StartedAt { get; private set; } = default!;
    public string? FinishedAt { get; private set; }
    public int RunByUserId { get; private set; }
    public decimal RoomRevenue { get; private set; }
    public decimal LevyTotal { get; private set; }
    public int FoliosProcessed { get; private set; }
    public int? VoucherId { get; private set; }

    private NightAuditRun() { }

    public static NightAuditRun Start(int companyId, string businessDate, string startedAt, int runByUserId)
    {
        if (string.IsNullOrWhiteSpace(businessDate)) throw new ArgumentException("روزِ کاری الزامی است.");
        return new NightAuditRun { CompanyId = companyId, BusinessDate = businessDate, StartedAt = startedAt, RunByUserId = runByUserId };
    }

    public void Finish(string finishedAt, decimal roomRevenue, decimal levyTotal, int foliosProcessed, int? voucherId)
    {
        FinishedAt = finishedAt; RoomRevenue = roomRevenue; LevyTotal = levyTotal;
        FoliosProcessed = foliosProcessed; VoucherId = voucherId; SetAudit(null);
    }
}

/// <summary>PMS-C1-1 — تنظیماتِ PMS: نگاشتِ حساب‌ها/مراکزِهزینه + نرخِ عوارض + برشِ روزِ کاری. هیچ کدِ حسابی hardcode نیست.</summary>
public class PmsSettings : AuditableEntity
{
    // نگاشتِ حساب‌ها (از نمودارِ حساب‌ها انتخاب می‌شوند)
    public int RoomRevenueAccountId { get; private set; }
    public int LevyPayableAccountId { get; private set; }
    public int FolioReceivableAccountId { get; private set; }
    public int DepositLiabilityAccountId { get; private set; }
    public int InterDeptFbReceivableAccountId { get; private set; }
    public int CompanyReceivableAccountId { get; private set; }
    public int BankAccountId { get; private set; }
    public int FbRevenueCostCenterId { get; private set; }
    public int RoomRevenueCostCenterId { get; private set; }
    public decimal LevyPercent { get; private set; }
    public string BusinessDayCutoff { get; private set; } = "06:00";
    public bool NoShowChargeFirstNight { get; private set; } = true;

    private PmsSettings() { }

    public static PmsSettings Create(int companyId) => new PmsSettings { CompanyId = companyId };

    public void Update(int roomRevenueAccountId, int levyPayableAccountId, int folioReceivableAccountId,
        int depositLiabilityAccountId, int interDeptFbReceivableAccountId, int companyReceivableAccountId,
        int bankAccountId, int fbRevenueCostCenterId, int roomRevenueCostCenterId,
        decimal levyPercent, string businessDayCutoff, bool noShowChargeFirstNight)
    {
        RoomRevenueAccountId = roomRevenueAccountId; LevyPayableAccountId = levyPayableAccountId;
        FolioReceivableAccountId = folioReceivableAccountId; DepositLiabilityAccountId = depositLiabilityAccountId;
        InterDeptFbReceivableAccountId = interDeptFbReceivableAccountId; CompanyReceivableAccountId = companyReceivableAccountId;
        BankAccountId = bankAccountId; FbRevenueCostCenterId = fbRevenueCostCenterId; RoomRevenueCostCenterId = roomRevenueCostCenterId;
        LevyPercent = levyPercent < 0 ? 0 : levyPercent;
        if (!string.IsNullOrWhiteSpace(businessDayCutoff)) BusinessDayCutoff = businessDayCutoff;
        NoShowChargeFirstNight = noShowChargeFirstNight; SetAudit(null);
    }
}
