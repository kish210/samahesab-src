namespace SamaHesab.Application.Payments;

/// <summary>
/// 💳 CR-1 — درایورِ شبیه‌سازِ ترمینال (برای تست/دمو و تا پیش از اتصالِ سخت‌افزارِ واقعی).
/// همیشه تأیید می‌کند (مگر مبلغ ≤ ۰) و RRN/شمارهٔ پیگیری/PANِ ماسک‌شدهٔ ساختگی تولید می‌کند.
/// درایورِ واقعیِ هر PSP بعداً جایگزینش می‌شود (همین <see cref="IPaymentTerminalService"/> را پیاده می‌کند).
/// </summary>
public sealed class SimulatedPaymentTerminal : IPaymentTerminalService
{
    private readonly string _terminalId;

    public SimulatedPaymentTerminal(string? terminalId = null)
        => _terminalId = string.IsNullOrWhiteSpace(terminalId) ? "SIM-0001" : terminalId!.Trim();

    public string ProviderName => "شبیه‌ساز (تست)";
    public bool IsReady => true;

    public Task<CardPaymentResult> PayAsync(CardPaymentRequest request, CancellationToken ct = default)
    {
        if (request.Amount <= 0)
            return Task.FromResult(CardPaymentResult.Declined("مبلغِ نامعتبر برای کارت‌خوان.", request.Amount));

        var rng = Random.Shared;
        var rrn = rng.NextInt64(100_000_000_000, 999_999_999_999).ToString();   // ۱۲ رقم
        var trace = rng.Next(100_000, 999_999).ToString();
        var pan = "6037-99**-****-" + rng.Next(1000, 9999);

        return Task.FromResult(new CardPaymentResult(
            Approved: true, Amount: request.Amount, Rrn: rrn, MaskedPan: pan, TerminalId: _terminalId,
            TraceNo: trace, CardType: "شتاب", At: DateTime.Now,
            Message: "پرداخت با موفقیت انجام شد (شبیه‌ساز)."));
    }

    public Task<CardPaymentResult> RefundAsync(string rrn, decimal amount, CancellationToken ct = default)
    {
        var trace = Random.Shared.Next(100_000, 999_999).ToString();
        return Task.FromResult(new CardPaymentResult(
            Approved: true, Amount: amount, Rrn: rrn, MaskedPan: null, TerminalId: _terminalId,
            TraceNo: trace, CardType: null, At: DateTime.Now, Message: "برگشتِ وجه انجام شد (شبیه‌ساز)."));
    }
}
