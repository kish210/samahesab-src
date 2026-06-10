using SamaHesab.Domain.Entities.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>الگوی سند (Voucher Templates) — منطق دامنه.</summary>
public class VoucherTemplateTests
{
    private static VoucherTemplate Template()
    {
        var t = VoucherTemplate.Create(companyId: 1, branchId: 1, name: "اجاره ماهانه", voucherTypeId: 1);
        t.AddLine(VoucherTemplateLine.Create(0, 1, accountId: 10, debit: 3_000_000, credit: 0, "هزینه اجاره"));
        t.AddLine(VoucherTemplateLine.Create(0, 2, accountId: 20, debit: 0, credit: 3_000_000, "پرداخت"));
        return t;
    }

    [Fact]
    public void Template_Requires_Name()
        => Assert.Throws<ArgumentException>(() => VoucherTemplate.Create(1, 1, "  "));

    [Fact]
    public void Template_Aggregates_Default_Totals()
    {
        var t = Template();
        Assert.Equal(2, t.Lines.Count);
        Assert.Equal(3_000_000, t.TotalDebit);
        Assert.Equal(3_000_000, t.TotalCredit);
    }

    [Fact]
    public void TemplateLine_Rejects_Both_Debit_And_Credit()
        => Assert.Throws<ArgumentException>(() => VoucherTemplateLine.Create(0, 1, 10, debit: 100, credit: 100));

    [Fact]
    public void Voucher_Built_From_Template_Lines_Is_Balanced()
    {
        var t = Template();
        // شبیه‌سازی CreateVoucherFromTemplate: انتقال ردیف‌های الگو به سند
        var v = Voucher.Create(1, 1, 1, "1", "1404/01/01", t.VoucherTypeId, t.Name);
        int row = 1;
        foreach (var l in t.Lines)
            v.AddItem(VoucherItem.Create(0, row++, l.AccountId, l.Debit, l.Credit, l.Description));

        Assert.True(v.IsBalanced());
        Assert.Equal(t.TotalDebit, v.TotalDebit);
        Assert.Equal(t.TotalCredit, v.TotalCredit);
    }
}
