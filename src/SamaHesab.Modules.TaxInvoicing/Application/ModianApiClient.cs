using System.Text;
using System.Text.Json;
using SamaHesab.Application.Common.Models;

namespace SamaHesab.Modules.TaxInvoicing.Application;

public sealed record ModianSubmitResult(string ReferenceNumber);
public sealed record ModianInquiryResult(string Status, string? UniqueTaxId, string? Description);

/// <summary>
/// کلاینتِ HTTPِ سامانهٔ مودیان — الگویِ try/catch→Result، عیناً <c>ISupportApiClient</c>.
/// ⚠️ آدرس‌ها/مسیرهایِ زیر از یک راهنمایِ فنیِ فارسیِ **غیررسمی** (bodjex.ir) آمده‌اند، نه سندِ
/// رسمیِ سازمان — پیش از استفادهٔ واقعی حتماً در برابرِ مستنداتِ tp.tax.gov.ir بازبینی شوند
/// (به‌محضِ دریافتِ اعتبارنامهٔ Sandbox/واقعی).
/// </summary>
public interface IModianApiClient
{
    Task<Result<string>> GetNonceAsync(bool useSandbox, CancellationToken ct = default);
    Task<Result<string>> GetServerPublicKeyPemAsync(bool useSandbox, CancellationToken ct = default);
    Task<Result<ModianSubmitResult>> SubmitInvoiceAsync(string jwe, bool useSandbox, CancellationToken ct = default);
    Task<Result<ModianInquiryResult>> InquiryByReferenceNumberAsync(string referenceNumber, bool useSandbox, CancellationToken ct = default);
}

public sealed class ModianApiClient : IModianApiClient
{
    private const string ProdBaseUrl = "https://tp.tax.gov.ir";
    private const string SandboxBaseUrl = "https://sandboxrc.tax.gov.ir";

    private readonly HttpClient _http;
    public ModianApiClient(HttpClient http) => _http = http;

    private static string BaseUrl(bool useSandbox) => useSandbox ? SandboxBaseUrl : ProdBaseUrl;

    public async Task<Result<string>> GetNonceAsync(bool useSandbox, CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync($"{BaseUrl(useSandbox)}/requestsmanager/api/v2/nonce", ct);
            if (!res.IsSuccessStatusCode)
                return Result<string>.Failure($"خطایِ سرور در دریافتِ nonce: {(int)res.StatusCode}");

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var nonce = doc.RootElement.TryGetProperty("nonce", out var n) ? n.GetString() : null;
            return string.IsNullOrEmpty(nonce)
                ? Result<string>.Failure("پاسخِ سرور فاقدِ فیلدِ nonce بود.")
                : Result<string>.Success(nonce);
        }
        catch (Exception ex) { return Result<string>.Failure(ex.GetBaseException().Message); }
    }

    public async Task<Result<string>> GetServerPublicKeyPemAsync(bool useSandbox, CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync($"{BaseUrl(useSandbox)}/server-information", ct);
            if (!res.IsSuccessStatusCode)
                return Result<string>.Failure($"خطایِ سرور در دریافتِ کلیدِ عمومی: {(int)res.StatusCode}");

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var pem = doc.RootElement.TryGetProperty("publicKey", out var k) ? k.GetString() : null;
            return string.IsNullOrEmpty(pem)
                ? Result<string>.Failure("پاسخِ سرور فاقدِ کلیدِ عمومی بود.")
                : Result<string>.Success(pem);
        }
        catch (Exception ex) { return Result<string>.Failure(ex.GetBaseException().Message); }
    }

    public async Task<Result<ModianSubmitResult>> SubmitInvoiceAsync(string jwe, bool useSandbox, CancellationToken ct = default)
    {
        try
        {
            using var content = new StringContent(jwe, Encoding.UTF8, "application/jose");
            using var res = await _http.PostAsync($"{BaseUrl(useSandbox)}/requestsmanager/api/v2/invoice", content, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return Result<ModianSubmitResult>.Failure($"سازمان درخواست را رد کرد ({(int)res.StatusCode}): {body}");

            using var doc = JsonDocument.Parse(body);
            var refNum = doc.RootElement.TryGetProperty("referenceNumber", out var r) ? r.GetString() : null;
            return string.IsNullOrEmpty(refNum)
                ? Result<ModianSubmitResult>.Failure("پاسخِ سرور فاقدِ referenceNumber بود.")
                : Result<ModianSubmitResult>.Success(new ModianSubmitResult(refNum));
        }
        catch (Exception ex) { return Result<ModianSubmitResult>.Failure(ex.GetBaseException().Message); }
    }

    public async Task<Result<ModianInquiryResult>> InquiryByReferenceNumberAsync(string referenceNumber, bool useSandbox, CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync(
                $"{BaseUrl(useSandbox)}/requestsmanager/api/v2/inquiry-by-reference-id?referenceNumber={Uri.EscapeDataString(referenceNumber)}", ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return Result<ModianInquiryResult>.Failure($"خطایِ سرور در استعلام: {(int)res.StatusCode}: {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "Unknown" : "Unknown";
            var uid = root.TryGetProperty("uid", out var u) ? u.GetString() : null;
            var desc = root.TryGetProperty("description", out var d) ? d.GetString() : null;
            return Result<ModianInquiryResult>.Success(new ModianInquiryResult(status, uid, desc));
        }
        catch (Exception ex) { return Result<ModianInquiryResult>.Failure(ex.GetBaseException().Message); }
    }
}
