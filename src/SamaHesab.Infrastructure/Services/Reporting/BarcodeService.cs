using QRCoder;
using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.Infrastructure.Services.Reporting;

/// <summary>
/// فاز ۱۱ — P2/DT-7: تولیدِ QR با QRCoder.
/// از <see cref="PngByteQRCode"/> استفاده می‌کنیم که PNG را بدونِ وابستگی به System.Drawing
/// (سازگار با همهٔ سکوها) تولید می‌کند.
/// </summary>
public sealed class BarcodeService : IBarcodeService
{
    public byte[] QrPng(string payload, int pixelsPerModule = 6)
    {
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(payload ?? "", QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(Math.Clamp(pixelsPerModule, 2, 40));
    }

    public string QrImageHtml(string? payload, int sizePx = 120)
    {
        if (string.IsNullOrWhiteSpace(payload)) return string.Empty;
        var b64 = Convert.ToBase64String(QrPng(payload!));
        return $"<img src=\"data:image/png;base64,{b64}\" width=\"{sizePx}\" height=\"{sizePx}\" alt=\"QR\" />";
    }
}
