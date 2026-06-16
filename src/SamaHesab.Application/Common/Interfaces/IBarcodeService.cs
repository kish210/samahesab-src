namespace SamaHesab.Application.Common.Interfaces;

/// <summary>
/// تولیدِ تصویرِ QR برای اسناد (فاز ۱۱ — P2/DT-7، نیمهٔ دومِ DT-7).
/// خروجی PNG و نیز قطعهٔ <c>&lt;img&gt;</c> با data-URI تا در قالب‌های HTMLِ سند
/// از طریقِ توکنِ <c>{QrImage}</c> به‌صورتِ تصویرِ واقعی درج شود.
/// </summary>
public interface IBarcodeService
{
    /// <summary>بایت‌های PNGِ یک QR از محتوای داده‌شده.</summary>
    byte[] QrPng(string payload, int pixelsPerModule = 6);

    /// <summary>قطعهٔ <c>&lt;img&gt;</c> با data-URI (مناسبِ درج در قالبِ HTMLِ سند). برای محتوای خالی، رشتهٔ خالی.</summary>
    string QrImageHtml(string? payload, int sizePx = 120);
}
