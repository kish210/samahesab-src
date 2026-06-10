using SamaHesab.Application.Reports.Export;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>موتور خروجی‌گیری گزارش — CSV/HTML.</summary>
public class ReportExporterTests
{
    private static ReportTable Sample() => new(
        "گزارش تست",
        new[] { "کد", "نام", "مبلغ" },
        new List<string[]>
        {
            new[] { "1", "صندوق", "1000" },
            new[] { "2", "بانک, ملت", "2500" },   // شامل کاما → باید محصور شود
        });

    [Fact]
    public void ToCsv_Escapes_Commas()
    {
        var csv = ReportExporter.ToCsv(Sample());
        var lines = csv.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal("کد,نام,مبلغ", lines[0]);
        Assert.Equal("1,صندوق,1000", lines[1]);
        Assert.Equal("2,\"بانک, ملت\",2500", lines[2]);   // فیلد دارای کاما محصور شد
    }

    [Fact]
    public void ToHtml_Is_Rtl_And_Contains_Data()
    {
        var html = ReportExporter.ToHtml(Sample());
        Assert.Contains("dir=\"rtl\"", html);
        Assert.Contains("<h2>گزارش تست</h2>", html);
        Assert.Contains("<td>صندوق</td>", html);
    }

    [Fact]
    public void From_Builds_Rows_With_Formatting()
    {
        var data = new[] { (Id: 1, Name: "الف", Amount: 1234.5m) };
        var t = ReportExporter.ToCsv(ReportTable.From(
            "ت", new[] { "کد", "نام", "مبلغ" }, data, x => new object?[] { x.Id, x.Name, x.Amount }));
        Assert.Contains("1,الف,1234.5", t.Replace("\r\n", "\n"));
    }

    [Fact]
    public void ToHtml_Escapes_Markup()
        => Assert.Contains("&lt;b&gt;",
            ReportExporter.ToHtml(new ReportTable("t", new[] { "h" }, new List<string[]> { new[] { "<b>" } })));
}
