using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Tourism;

/// <summary>
/// TUR-C1-1 — مسافرِ یک خطِ فروش (برای محصولاتی که RequiresPassengerList دارند: تور/گشتِ جزیره).
/// در لیستِ مسافرِ گزارشِ روزانهٔ تأمین‌کننده می‌آید.
/// </summary>
public class SalePassenger : BaseEntity
{
    public int SaleLineId { get; private set; }
    public string FullName { get; private set; } = default!;
    public string? NationalIdOrPassport { get; private set; }
    public string? Phone { get; private set; }

    private SalePassenger() { }

    public static SalePassenger Create(string fullName, string? nationalIdOrPassport = null, string? phone = null)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("نامِ مسافر الزامی است.");
        return new SalePassenger { FullName = fullName, NationalIdOrPassport = nationalIdOrPassport, Phone = phone };
    }
}
