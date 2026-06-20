namespace SamaHesab.Application.Payments;

/// <summary>💳 CR-1 — درخواستِ پرداخت به ترمینالِ بانکی (کارت‌خوان).</summary>
public sealed record CardPaymentRequest(decimal Amount, string? InvoiceNumber = null, string? Description = null);

/// <summary>💳 CR-1 — نتیجهٔ پرداخت از ترمینال (شمارهٔ پیگیریِ مرجع/RRN، PANِ ماسک‌شده، …).</summary>
public sealed record CardPaymentResult(
    bool Approved, decimal Amount, string? Rrn, string? MaskedPan, string? TerminalId,
    string? TraceNo, string? CardType, DateTime At, string Message)
{
    public static CardPaymentResult Declined(string message, decimal amount = 0) =>
        new(false, amount, null, null, null, null, null, DateTime.Now, message);
}

/// <summary>
/// 💳 CR-1 — انتزاعِ «ترمینالِ پرداختِ بانکی» (کارت‌خوانِ POS). درایورِ هر PSP/بانک این را پیاده می‌کند.
/// مستقل از سخت‌افزار و UI: کلاینت مبلغ را می‌فرستد، نتیجه (تأیید + RRN) را روی پرداختِ فاکتور ثبت می‌کند.
/// <para>درایورِ پیش‌فرض = شبیه‌ساز (<see cref="SimulatedPaymentTerminal"/>) تا بدونِ سخت‌افزار هم کار کند؛
/// درایورِ واقعیِ هر PSP (Behpardakht/Sep/Parsian/…) بعداً همین قرارداد را پیاده می‌کند.</para>
/// </summary>
public interface IPaymentTerminalService
{
    /// <summary>نامِ درایور/PSP (برای نمایش به کاربر).</summary>
    string ProviderName { get; }

    /// <summary>آیا ترمینال پیکربندی/آماده است؟</summary>
    bool IsReady { get; }

    /// <summary>ارسالِ مبلغ به ترمینال و دریافتِ نتیجهٔ تراکنش.</summary>
    Task<CardPaymentResult> PayAsync(CardPaymentRequest request, CancellationToken ct = default);

    /// <summary>برگشتِ وجهِ یک تراکنش (اگر PSP پشتیبانی نکند، Declined برمی‌گرداند).</summary>
    Task<CardPaymentResult> RefundAsync(string rrn, decimal amount, CancellationToken ct = default);
}
