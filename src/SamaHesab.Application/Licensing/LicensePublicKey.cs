namespace SamaHesab.Application.Licensing;

/// <summary>
/// فاز ۱۲ P-G7 — کلیدِ عمومیِ وندور برای اعتبارسنجیِ آفلاینِ لایسنس.
/// ⚠️ فقط کلیدِ <b>عمومی</b> اینجاست؛ کلیدِ <b>خصوصی</b> هرگز در کلاینت/این مخزن نیست
/// (نزدِ وندور، توسطِ ابزارِ `tools/SamaHesab.LicenseTool` نگه داشته می‌شود).
/// با `LicenseTool keygen` تولید و این مقدار جای‌گذاری می‌شود.
/// </summary>
public static class LicensePublicKey
{
    public const string Pem = """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA29hvvdlEbk3eRttFgmjZ
        bSLRjzZ7nhEWiKrsYzfAh23Z/f7YvFxPmh1ZgiaYlHAh6H3z21s6+DGKqZe2XPzW
        hBIrqvozPTY96+vv3RWYouMpNesgU+HyYOl+ePLS33rtpJzCUQnax3qbSHakpuQy
        dsJlHcY4IACF2QqrlDfTB34LGeYhjA9IgdBpu5eI5q61qnVKrvcTsmjXpLkPoVBV
        97RHFpvDtehmJoASpMyGZXXlpJ4/bJ2JcuuHaw2FhQmQhh9wu4pZdl7/LofI3b/H
        RwXJKqUXsFGvogBT5kVYorNlK04CNMYoB6C8PRLyFAcOv7giI8hyLCabPnKdKnG+
        DQIDAQAB
        -----END PUBLIC KEY-----
        """;
}
