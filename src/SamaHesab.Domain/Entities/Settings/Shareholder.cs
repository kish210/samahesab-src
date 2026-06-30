using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Settings;

/// <summary>سهامدارِ شرکت — دفترِ سهام: نام، درصدِ سهم و آوردهٔ سرمایه. (دادهٔ پایهٔ شرکتی، schema Cfg)</summary>
public class Shareholder : BaseEntity
{
    public int CompanyId { get; private set; }
    public string FullName { get; private set; } = default!;
    public string? NationalCode { get; private set; }
    public decimal SharePercent { get; private set; }   // درصدِ سهم (۰..۱۰۰)
    public decimal CapitalAmount { get; private set; }   // آوردهٔ سرمایه (ریال)
    public string? Phone { get; private set; }
    public string? Notes { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; private set; }

    private Shareholder() { }

    public static Shareholder Create(int companyId, string fullName, decimal sharePercent, decimal capitalAmount)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("نامِ سهامدار الزامی است.");
        return new Shareholder
        {
            CompanyId = companyId,
            FullName = fullName.Trim(),
            SharePercent = sharePercent < 0 ? 0 : sharePercent,
            CapitalAmount = capitalAmount < 0 ? 0 : capitalAmount,
        };
    }

    public void Update(string fullName, string? nationalCode, decimal sharePercent, decimal capitalAmount,
        string? phone, string? notes)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("نامِ سهامدار الزامی است.");
        FullName = fullName.Trim();
        NationalCode = nationalCode;
        SharePercent = sharePercent < 0 ? 0 : sharePercent;
        CapitalAmount = capitalAmount < 0 ? 0 : capitalAmount;
        Phone = phone;
        Notes = notes;
        UpdatedAt = DateTime.Now;
    }

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.Now; }
}
