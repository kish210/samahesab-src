using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>یک ردیف الگوی سند: حساب + مبلغ پیش‌فرض (می‌تواند صفر باشد) + شرح.</summary>
public class VoucherTemplateLine : BaseEntity
{
    public int TemplateId { get; private set; }
    public int RowNumber { get; private set; }
    public int AccountId { get; private set; }
    public decimal Debit { get; private set; }
    public decimal Credit { get; private set; }
    public string? Description { get; private set; }

    private VoucherTemplateLine() { }

    public static VoucherTemplateLine Create(int templateId, int rowNumber, int accountId,
        decimal debit, decimal credit, string? description = null)
    {
        if (debit < 0 || credit < 0)
            throw new ArgumentException("مبلغ بدهکار و بستانکار نمی‌تواند منفی باشد.");
        if (debit > 0 && credit > 0)
            throw new ArgumentException("یک ردیف نمی‌تواند هم بدهکار و هم بستانکار باشد.");
        return new VoucherTemplateLine
        {
            TemplateId = templateId,
            RowNumber = rowNumber,
            AccountId = accountId,
            Debit = debit,
            Credit = credit,
            Description = description
        };
    }
}
