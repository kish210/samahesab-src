using SamaHesab.Application.Documents;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۱ — P3/DT-8: اعتبارسنجی/نسخه‌بندیِ بسته‌های `.shtpl`.</summary>
public class DocumentTemplatePackageValidatorTests
{
    private static DocumentTemplatePackage Valid() => new(
        DocumentTemplatePackage.CurrentFormat, "SalesInvoice", "قالبِ نمونه", "A4P",
        "<h1>{InvoiceNumber}</h1>", "<div>{CustomerName}</div>", "<small>پابرگ</small>");

    [Fact]
    public void Valid_package_passes()
    {
        Assert.True(DocumentTemplatePackageValidator.Validate(Valid()).Ok);
    }

    [Fact]
    public void Null_package_fails()
    {
        Assert.False(DocumentTemplatePackageValidator.Validate(null).Ok);
    }

    [Fact]
    public void Wrong_format_version_fails()
    {
        var pkg = Valid() with { Format = "shtpl-v2" };
        var r = DocumentTemplatePackageValidator.Validate(pkg);
        Assert.False(r.Ok);
        Assert.Contains("shtpl-v1", r.Error);
    }

    [Fact]
    public void Empty_document_type_fails()
    {
        var pkg = Valid() with { DocumentType = "" };
        Assert.False(DocumentTemplatePackageValidator.Validate(pkg).Ok);
    }

    [Fact]
    public void Unknown_but_nonempty_document_type_is_accepted()
    {
        // کاتالوگِ نوع‌ها توسطِ C2 گسترش می‌یابد؛ نباید نوعِ تازه را بی‌صدا رد کنیم.
        var pkg = Valid() with { DocumentType = "SomeFutureDocType" };
        Assert.True(DocumentTemplatePackageValidator.Validate(pkg).Ok);
    }

    [Theory]
    [InlineData("Voucher")]
    [InlineData("JournalVoucher")]
    [InlineData("BalanceSheet")]
    [InlineData("PosReceipt")]
    public void Existing_gallery_types_pass(string docType)
    {
        var pkg = Valid() with { DocumentType = docType };
        Assert.True(DocumentTemplatePackageValidator.Validate(pkg).Ok);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_body_fails(string body)
    {
        var pkg = Valid() with { BodyHtml = body };
        Assert.False(DocumentTemplatePackageValidator.Validate(pkg).Ok);
    }

    [Fact]
    public void Empty_name_fails()
    {
        var pkg = Valid() with { Name = "" };
        Assert.False(DocumentTemplatePackageValidator.Validate(pkg).Ok);
    }
}
