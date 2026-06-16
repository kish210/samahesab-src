using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SamaHesab.Application.Licensing;

/// <summary>
/// فاز ۱۲ P-G7 — محتوای امضاشدهٔ لایسنس (payload). امضای RSA روی <see cref="Canonical"/> زده می‌شود؛
/// تغییرِ هر فیلد، امضا را باطل می‌کند (tamper detection).
/// </summary>
public sealed record LicenseInfo(
    string CompanyName,
    string? NationalId,
    string MachineFingerprint,
    LicenseTier Tier,
    DateTime IssuedAtUtc,
    DateTime ExpiresUtc,
    int MaxBranches,
    int MaxUsers)
{
    /// <summary>رشتهٔ متعارف (deterministic) برای امضا/تأیید — ترتیب و قالبِ ثابت.</summary>
    [JsonIgnore]
    public string Canonical =>
        string.Join("|",
            "v1",
            CompanyName.Trim(),
            (NationalId ?? "").Trim(),
            MachineFingerprint.Trim().ToUpperInvariant(),
            ((int)Tier).ToString(),
            IssuedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ExpiresUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            MaxBranches.ToString(),
            MaxUsers.ToString());

    public byte[] CanonicalBytes() => Encoding.UTF8.GetBytes(Canonical);
}

/// <summary>فایل/سندِ لایسنس = payload + امضای Base64. این چیزی است که به مشتری داده می‌شود.</summary>
public sealed record LicenseDocument(LicenseInfo License, string Signature)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOpts);

    public static LicenseDocument? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<LicenseDocument>(json, JsonOpts); }
        catch { return null; }
    }
}
