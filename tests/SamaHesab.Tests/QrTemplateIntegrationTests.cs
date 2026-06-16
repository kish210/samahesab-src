using SamaHesab.Application.Documents;
using SamaHesab.Infrastructure.Services.Reporting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// L6 (هماهنگیِ C1↔C2) — قراردادِ «QR در قالب‌ها»: تصویرِ QRِ BarcodeService (C1) از طریقِ
/// توکنِ <c>{QrImage}</c> توسطِ موتورِ قالبِ C2 به‌صورتِ HTMLِ خام (نه escape‌شده) رندر می‌شود.
/// قالب‌های فروش/خرید/POS/رستوران اکنون به‌جای جعبهٔ متنیِ {QrData} این توکن را دارند.
/// </summary>
public class QrTemplateIntegrationTests
{
    [Fact]
    public void Engine_Renders_QrImage_Token_As_Raw_Html()
    {
        var data = DocumentData.Of(new Dictionary<string, string?>
        {
            ["QrImage"] = "<img src=\"data:image/png;base64,AAAA\" width=\"60\" height=\"60\" alt=\"QR\" />",
        });
        var html = DocumentTemplateEngine.Render("<div class='qr'>{QrImage}</div>", data);

        Assert.Contains("<img src=\"data:image/png;base64,", html);   // خام درج شد، نه escape
        Assert.DoesNotContain("{QrImage}", html);                    // توکن نشت نکرد
    }

    [Fact]
    public void Barcode_Image_Flows_Through_Engine_End_To_End()
    {
        var bc = new BarcodeService();
        var qr = bc.QrImageHtml("FA-1404-001", 60);  // payload = شمارهٔ فاکتور (هم‌رفتارِ VMها)

        var html = DocumentTemplateEngine.Render(
            "<div class='qr'>{QrImage}</div>",
            DocumentData.Of(new Dictionary<string, string?> { ["QrImage"] = qr }));

        Assert.Contains("data:image/png;base64,", html);
        Assert.Contains("width=\"60\"", html);
    }

    [Fact]
    public void Empty_Payload_Leaves_Qr_Box_Blank_Not_Leaking_Token()
    {
        var bc = new BarcodeService();
        var html = DocumentTemplateEngine.Render(
            "<div class='qr'>{QrImage}</div>",
            DocumentData.Of(new Dictionary<string, string?> { ["QrImage"] = bc.QrImageHtml(null, 60) }));

        Assert.Equal("<div class='qr'></div>", html);   // payload خالی → جعبهٔ خالی، نه «{QrImage}»
    }
}
