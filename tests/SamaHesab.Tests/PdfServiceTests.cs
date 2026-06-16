using System.Text;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Infrastructure.Services.Reporting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۱ — P2/DT-7: اطمینان از تولیدِ PDFِ معتبر (با محتوای فارسیِ راست‌چین) توسطِ QuestPDF.</summary>
public class PdfServiceTests
{
    private static readonly IPdfService Pdf = new PdfService();

    private static ReportTable SampleTable() => new(
        "گزارش موجودی و ارزش انبار",
        new[] { "کد", "نام کالا", "موجودی", "ارزش" },
        new List<string[]>
        {
            new[] { "1001", "آردِ سفید", "1,200", "36,000,000" },
            new[] { "1002", "روغنِ مایع", "350", "21,000,000" },
        });

    [Fact]
    public void RenderTable_produces_valid_pdf_bytes()
    {
        var meta = new PdfMeta("شرکتِ نمونه", "بازه: ۱۴۰۵/۰۱/۰۱ تا ۱۴۰۵/۰۳/۳۱", "۱۴۰۵/۰۳/۳۱ ۱۲:۰۰");

        var bytes = Pdf.RenderTable(SampleTable(), meta);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 1000, $"PDF خیلی کوچک است: {bytes.Length} بایت");
        // امضای فایلِ PDF
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void RenderTable_handles_empty_rows()
    {
        var empty = new ReportTable("بدونِ داده", new[] { "ستون" }, new List<string[]>());
        var bytes = Pdf.RenderTable(empty);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void RenderHtmlDocument_strips_tags_and_produces_pdf()
    {
        const string html = "<html><body><h1>رسیدِ دریافت</h1><p>مبلغ: 1,000,000 ریال</p></body></html>";
        var bytes = Pdf.RenderHtmlDocument(html, new PdfMeta("شرکتِ نمونه", "رسیدِ دریافت"));
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }

    [Fact]
    public void Barcode_QrPng_is_valid_png()
    {
        IBarcodeService bc = new BarcodeService();
        var png = bc.QrPng("VCH-1404-000123");
        Assert.True(png.Length > 100);
        // امضای PNG: 89 50 4E 47
        Assert.Equal(0x89, png[0]);
        Assert.Equal(0x50, png[1]);
        Assert.Equal(0x4E, png[2]);
        Assert.Equal(0x47, png[3]);
    }

    [Fact]
    public void Barcode_QrImageHtml_embeds_data_uri_and_is_empty_for_blank()
    {
        IBarcodeService bc = new BarcodeService();
        Assert.Contains("data:image/png;base64,", bc.QrImageHtml("12345"));
        Assert.Equal(string.Empty, bc.QrImageHtml(null));
        Assert.Equal(string.Empty, bc.QrImageHtml("   "));
    }
}
