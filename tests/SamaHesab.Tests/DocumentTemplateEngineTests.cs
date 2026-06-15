using System.Collections.Generic;
using SamaHesab.Application.Documents;
using SamaHesab.Domain.Entities.Documents;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// فاز ۱۰ — DT-1: راستی‌آزماییِ موتورِ خالصِ قالب (توکنِ اسکالر + بلوکِ تکرارِ ردیف + توکنِ ناشناخته).
/// </summary>
public class DocumentTemplateEngineTests
{
    private static DocumentData Sample() => DocumentData.Of(
        new Dictionary<string, string?>
        {
            ["InvoiceNumber"] = "F-1001",
            ["CustomerName"] = "شرکت آلفا",
            ["TotalAmount"] = "12,500,000",
        },
        new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["ProductName"] = "کالا الف", ["Quantity"] = "2", ["UnitPrice"] = "1,000" },
            new Dictionary<string, string?> { ["ProductName"] = "کالا ب",  ["Quantity"] = "5", ["UnitPrice"] = "2,000" },
        });

    [Fact]
    public void Resolves_Scalar_Tokens()
    {
        var html = "<h1>فاکتور {InvoiceNumber} — {CustomerName}</h1>";
        Assert.Equal("<h1>فاکتور F-1001 — شرکت آلفا</h1>", DocumentTemplateEngine.Render(html, Sample()));
    }

    [Fact]
    public void Unknown_Token_Becomes_Empty()
    {
        Assert.Equal("x:", DocumentTemplateEngine.Render("x:{DoesNotExist}", Sample()));
    }

    [Fact]
    public void Expands_Row_Block_With_Index()
    {
        var html = "[[ROWS]]<tr><td>{#}</td><td>{ProductName}</td><td>{Quantity}</td></tr>[[/ROWS]]";
        var result = DocumentTemplateEngine.Render(html, Sample());
        Assert.Equal(
            "<tr><td>1</td><td>کالا الف</td><td>2</td></tr><tr><td>2</td><td>کالا ب</td><td>5</td></tr>",
            result);
    }

    [Fact]
    public void Row_Block_Falls_Back_To_Header_Fields()
    {
        // توکنی که در ردیف نیست ولی در سرسند هست → از سرسند پر می‌شود.
        var html = "[[ROWS]]{ProductName}@{CustomerName};[[/ROWS]]";
        Assert.Equal("کالا الف@شرکت آلفا;کالا ب@شرکت آلفا;", DocumentTemplateEngine.Render(html, Sample()));
    }

    [Fact]
    public void RenderTemplate_Composes_Header_Body_Footer()
    {
        var t = DocumentTemplate.Create(1, "SalesInvoice", "رسمی",
            bodyHtml: "<body>{CustomerName}</body>", headerHtml: "<h>{InvoiceNumber}</h>", footerHtml: "<f>{TotalAmount}</f>");
        Assert.Equal("<h>F-1001</h><body>شرکت آلفا</body><f>12,500,000</f>",
            DocumentTemplateEngine.RenderTemplate(t, Sample()));
    }

    [Fact]
    public void ExtractTokens_Lists_Unique_Tokens()
    {
        var tokens = DocumentTemplateEngine.ExtractTokens("{A}{B}{A}[[ROWS]]{C}{#}[[/ROWS]]");
        Assert.Contains("A", tokens); Assert.Contains("B", tokens); Assert.Contains("C", tokens);
        Assert.DoesNotContain("#", tokens);
        Assert.Equal(3, tokens.Count);
    }
}
