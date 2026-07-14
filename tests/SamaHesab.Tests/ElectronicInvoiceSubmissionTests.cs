using SamaHesab.Modules.TaxInvoicing.Domain;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-ACCT-2 — رفتارِ خالصِ store-and-forwardِ رکوردِ ارسالِ صورتحسابِ الکترونیکی.</summary>
public class ElectronicInvoiceSubmissionTests
{
    [Fact]
    public void Create_Starts_As_Pending()
    {
        var sub = ElectronicInvoiceSubmission.Create(companyId: 1, salesInvoiceId: 42);

        Assert.Equal(SubmissionStatus.Pending, sub.Status);
        Assert.Equal(42, sub.SalesInvoiceId);
        Assert.Equal(0, sub.RetryCount);
        Assert.Null(sub.UniqueTaxId);
    }

    [Fact]
    public void Create_Rejects_NonPositive_SalesInvoiceId()
    {
        Assert.Throws<System.ArgumentException>(() => ElectronicInvoiceSubmission.Create(1, 0));
    }

    [Fact]
    public void MarkSent_Then_MarkAccepted_Sets_UniqueTaxId_And_Clears_Error()
    {
        var sub = ElectronicInvoiceSubmission.Create(1, 42);
        sub.MarkError("خطای شبکه");   // یک شکستِ قبلی برای اثباتِ پاک‌شدنِ خطا
        Assert.Equal(1, sub.RetryCount);

        sub.MarkSent("REF-123");
        Assert.Equal(SubmissionStatus.Sent, sub.Status);
        Assert.Equal("REF-123", sub.ReferenceNumber);
        Assert.Null(sub.ErrorMessage);
        Assert.NotNull(sub.SentAt);

        sub.MarkAccepted("1234567890123456789012");
        Assert.Equal(SubmissionStatus.Accepted, sub.Status);
        Assert.Equal("1234567890123456789012", sub.UniqueTaxId);
    }

    [Fact]
    public void MarkError_Increments_RetryCount_Each_Time()
    {
        var sub = ElectronicInvoiceSubmission.Create(1, 42);

        sub.MarkError("خطای اول");
        sub.MarkError("خطای دوم");

        Assert.Equal(SubmissionStatus.Error, sub.Status);
        Assert.Equal(2, sub.RetryCount);
        Assert.Equal("خطای دوم", sub.ErrorMessage);
    }

    [Fact]
    public void ResetToPending_Allows_Retry_After_Error()
    {
        var sub = ElectronicInvoiceSubmission.Create(1, 42);
        sub.MarkError("خطای موقت");

        sub.ResetToPending();

        Assert.Equal(SubmissionStatus.Pending, sub.Status);
    }
}

/// <summary>U-ACCT-2 — نگاشتِ کالا→کدِ رسمیِ سامانهٔ مودیان.</summary>
public class TaxItemCodeTests
{
    [Fact]
    public void Create_Trims_And_Stores_Fields()
    {
        var code = TaxItemCode.Create(1, productId: 7, itemId: " 123456 ", measurementUnitCode: " عدد ");

        Assert.Equal(7, code.ProductId);
        Assert.Equal("123456", code.ItemId);
        Assert.Equal("عدد", code.MeasurementUnitCode);
    }

    [Fact]
    public void Create_Rejects_Empty_ItemId()
    {
        Assert.Throws<System.ArgumentException>(() => TaxItemCode.Create(1, 7, "", "عدد"));
    }

    [Fact]
    public void Update_Overwrites_ItemId_And_Unit()
    {
        var code = TaxItemCode.Create(1, 7, "111", "عدد");

        code.Update("222", "کیلوگرم");

        Assert.Equal("222", code.ItemId);
        Assert.Equal("کیلوگرم", code.MeasurementUnitCode);
    }
}
