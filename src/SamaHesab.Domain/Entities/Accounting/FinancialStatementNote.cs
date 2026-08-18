using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// یادداشتِ توضیحیِ صورتِ مالی (U-FIN-NOTES) — متنِ تکمیلی که حسابدار کنارِ ترازنامه/سودوزیان/
/// جریانِ وجوهِ نقد می‌نویسد و در خروجیِ چاپی/اکسلِ همان صورت هم نمایش داده می‌شود.
/// </summary>
public class FinancialStatementNote : AuditableEntity
{
    public FinancialStatementType StatementType { get; private set; }
    public string Title { get; private set; } = default!;
    public string? Body { get; private set; }
    public int Order { get; private set; }

    private FinancialStatementNote() { }

    public static FinancialStatementNote Create(int companyId, FinancialStatementType type,
        string title, string? body, int order)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("عنوانِ یادداشت الزامی است.");

        return new FinancialStatementNote
        {
            CompanyId = companyId,
            StatementType = type,
            Title = title.Trim(),
            Body = body,
            Order = order
        };
    }

    public void Update(string title, string? body, int order)
    {
        Title = title.Trim();
        Body = body;
        Order = order;
        UpdatedAt = DateTime.Now;
    }
}
