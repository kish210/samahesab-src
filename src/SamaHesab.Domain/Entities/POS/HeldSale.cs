using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.POS;

/// <summary>
/// کار #۳۳ — فاکتور معلق (Hold/Recall): سبدِ نیمه‌کاره‌ی صندوق ذخیره می‌شود تا بعداً
/// فراخوان شود (مثلاً مشتری برای آوردن کالای دیگر می‌رود). Payload همان سبد به‌صورت JSON است
/// (مبهم برای دامنه؛ کلاینت آن را می‌سازد/می‌خواند).
/// </summary>
public class HeldSale : AuditableEntity
{
    public int BranchId { get; private set; }
    public int UserId { get; private set; }
    public string Label { get; private set; } = default!;
    public string Payload { get; private set; } = default!;   // JSON سبد
    public decimal Total { get; private set; }

    private HeldSale() { }

    public static HeldSale Create(int companyId, int branchId, int userId, string label, string payload, decimal total)
    {
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("برچسب فاکتور معلق الزامی است.");
        if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("سبد خالی قابل تعلیق نیست.");
        return new HeldSale
        {
            CompanyId = companyId, BranchId = branchId, UserId = userId,
            Label = label.Trim(), Payload = payload, Total = total
        };
    }
}
