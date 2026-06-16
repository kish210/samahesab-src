using SamaHesab.Application.Reports.Queries;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ (RC) — خلاصهٔ مالیاتِ ارزش‌افزوده: مالیاتِ خالص = خروجی − ورودی.</summary>
public class VatSummaryTests
{
    [Fact]
    public void NetPayable_is_output_minus_input()
    {
        var dto = new VatSummaryDto(SalesCount: 3, SalesBase: 10_000_000, OutputVat: 900_000,
                                    PurchaseCount: 2, PurchaseBase: 4_000_000, InputVat: 360_000);
        Assert.Equal(540_000, dto.NetPayable);
    }

    [Fact]
    public void NetPayable_can_be_credit_when_input_exceeds_output()
    {
        var dto = new VatSummaryDto(1, 1_000_000, 90_000, 5, 8_000_000, 720_000);
        Assert.Equal(-630_000, dto.NetPayable);   // طلبکار/قابلِ استرداد
    }
}
