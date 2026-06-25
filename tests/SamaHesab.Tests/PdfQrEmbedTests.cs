using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Infrastructure.Services.Reporting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>DT-7 — embedِ تصویرِ QR/بارکد در PDFِ بومی (نه فقط حذفِ تگِ img).</summary>
public class PdfQrEmbedTests
{
    private static readonly IBarcodeService Barcode = new BarcodeService();
    private static readonly IPdfService Pdf = new PdfService();

    [Fact]
    public void QrPng_Is_Valid_Png()
    {
        var png = Barcode.QrPng("SH-1404-TEST");
        // امضای PNG: 89 50 4E 47
        Assert.True(png.Length > 8);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png[..4]);
    }

    [Fact]
    public void RenderHtmlDocument_Produces_Valid_Pdf()
    {
        var bytes = Pdf.RenderHtmlDocument("<p>سند آزمایشی</p>");
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes[..4]));   // امضای PDF
    }

    [Fact]
    public void Embedded_Qr_Image_Enlarges_Pdf()
    {
        const string text = "<p>فاکتور شمارهٔ ۱۰۰</p><p>مبلغ: ۵٬۰۰۰٬۰۰۰</p>";
        var qrHtml = Barcode.QrImageHtml("https://kishwifi.com/verify/100", 120);
        Assert.Contains("data:image/png;base64,", qrHtml);   // تصویرِ واقعی تولید شد

        var withoutQr = Pdf.RenderHtmlDocument(text);
        var withQr    = Pdf.RenderHtmlDocument(text + qrHtml);

        // هر دو PDFِ معتبر؛ نسخهٔ دارای QR به‌خاطرِ embedِ تصویر آشکارا بزرگ‌تر است
        // (اگر img مثلِ قبل strip می‌شد، اندازه‌ها تقریباً برابر می‌ماند).
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(withQr[..4]));
        Assert.True(withQr.Length > withoutQr.Length + 200,
            $"PDFِ دارای QR باید بزرگ‌تر باشد: بدون={withoutQr.Length} با={withQr.Length}");
    }
}
