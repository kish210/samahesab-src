using SamaHesab.Application.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ورودِ انبوهِ سند — تجزیهٔ متنِ چسبانده‌شده از اکسل (TSV).</summary>
public class VoucherPasteParserTests
{
    [Fact]
    public void Parses_FourColumns_AccountDescDebitCredit()
    {
        var rows = VoucherPasteParser.Parse("1101\tدریافت نقد\t1500000\t0\n2101\tفروش\t0\t1500000");
        Assert.Equal(2, rows.Count);
        Assert.Equal("1101", rows[0].AccountToken);
        Assert.Equal("دریافت نقد", rows[0].Description);
        Assert.Equal(1500000m, rows[0].Debit);
        Assert.Equal(0m, rows[0].Credit);
        Assert.Equal(1500000m, rows[1].Credit);
    }

    [Fact]
    public void Parses_ThreeColumns_NoDescription()
    {
        var rows = VoucherPasteParser.Parse("صندوق\t2000000\t0");
        Assert.Single(rows);
        Assert.Equal("صندوق", rows[0].AccountToken);
        Assert.Null(rows[0].Description);
        Assert.Equal(2000000m, rows[0].Debit);
    }

    [Fact]
    public void Normalizes_PersianDigits_And_Separators()
        => Assert.Equal(1234567m, VoucherPasteParser.Num("۱٬۲۳۴٬۵۶۷ ریال"));

    [Fact]
    public void Skips_Rows_With_Both_Sides_Or_No_Amount()
    {
        var rows = VoucherPasteParser.Parse(
            "1101\tخوب\t100\t0\n" +
            "1102\tهردو\t50\t50\n" +     // هم بدهکار هم بستانکار → رد
            "1103\tبی‌مبلغ\t0\t0\n" +    // بی‌مبلغ → رد
            "\tبی‌حساب\t10\t0");          // توکنِ حساب خالی → رد
        Assert.Single(rows);
        Assert.Equal("1101", rows[0].AccountToken);
    }

    [Fact]
    public void Ignores_Blank_And_TooFewColumns()
        => Assert.Empty(VoucherPasteParser.Parse("\n  \nفقط‌یک‌ستون\nدو\tستون"));
}
