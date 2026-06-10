using SamaHesab.Application.Reports;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کار #۲۶ — طبقه‌بندی حساب طبق نمودار واقعی (رفع باگ Seg0).</summary>
public class AccountClassifierTests
{
    [Theory]
    [InlineData("1-01-001", AccountCategory.Asset)]
    [InlineData("2-03", AccountCategory.Asset)]        // دارایی ثابت
    [InlineData("3-01-001", AccountCategory.Liability)]
    [InlineData("4-01", AccountCategory.Liability)]    // بدهی بلندمدت (قبلاً اشتباهاً درآمد)
    [InlineData("5-03", AccountCategory.Equity)]       // حقوق صاحبان سهام (قبلاً اشتباهاً هزینه)
    [InlineData("6-01-001", AccountCategory.Revenue)]
    [InlineData("7-01-001", AccountCategory.Expense)]  // بهای تمام‌شده
    [InlineData("8-01", AccountCategory.Expense)]
    [InlineData("9-02", AccountCategory.Expense)]      // سایر هزینه‌ها (قبلاً اصلاً دیده نمی‌شد)
    public void Classifies_By_Chart_Group(string code, AccountCategory expected)
        => Assert.Equal(expected, AccountClassifier.Classify(code));
}
