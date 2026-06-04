using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SamaHesab.WPF.Services;

public record ApiProduct(int Id, string Code, string Name, string? Barcode, decimal SalePrice, decimal TaxRate);

/// <summary>
/// HTTP client used by the POS / restaurant kiosk apps to talk to the central
/// SamaHesab Web API (never the database directly). Configured from ApiSettings.
/// </summary>
public class ApiClient
{
    private readonly HttpClient _http = new();
    private string? _accessToken;
    private string? _refreshToken;

    public string BaseUrl { get; private set; } = "";
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    public void Configure(string baseUrl)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        _http.BaseAddress = new Uri(BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    private sealed class TokenResponse { public string? accessToken { get; set; } public string? refreshToken { get; set; } }
    private sealed class PosResult { public int invoiceId { get; set; } }
    private sealed class ErrorResult { public string? message { get; set; } }

    public async Task<(bool ok, string? error)> LoginAsync(string username, string password, int companyId = 1)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/auth/login", new { username, password, companyId });
            if (!resp.IsSuccessStatusCode)
                return (false, "نام کاربری/رمز یا آدرس سرور نادرست است.");
            var t = await resp.Content.ReadFromJsonAsync<TokenResponse>();
            _accessToken = t?.accessToken; _refreshToken = t?.refreshToken;
            if (string.IsNullOrEmpty(_accessToken)) return (false, "پاسخ نامعتبر از سرور.");
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            return (true, null);
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    public async Task<List<ApiProduct>> SearchProductsAsync(string? q = null)
    {
        var url = string.IsNullOrWhiteSpace(q) ? "/api/products" : $"/api/products?q={Uri.EscapeDataString(q)}";
        var list = await _http.GetFromJsonAsync<List<ApiProduct>>(url);
        return list ?? new List<ApiProduct>();
    }

    public async Task<(bool ok, int invoiceId, string? error)> CreatePosSaleAsync(
        IEnumerable<(int productId, decimal qty, decimal unitPrice, decimal discountPct, decimal taxPct)> items,
        decimal paid, string paymentMethod, int customerId, int warehouseId, decimal discount = 0)
    {
        try
        {
            var body = new
            {
                items = items.Select(i => new
                {
                    productId = i.productId, quantity = i.qty, unitPrice = i.unitPrice,
                    discountPct = i.discountPct, taxPct = i.taxPct
                }).ToArray(),
                paid, paymentMethod, customerId, warehouseId, discount
            };
            var resp = await _http.PostAsJsonAsync("/api/sales/pos", body);
            if (resp.IsSuccessStatusCode)
            {
                var r = await resp.Content.ReadFromJsonAsync<PosResult>();
                return (true, r?.invoiceId ?? 0, null);
            }
            var err = await resp.Content.ReadFromJsonAsync<ErrorResult>();
            return (false, 0, err?.message ?? $"خطای سرور ({(int)resp.StatusCode})");
        }
        catch (Exception ex) { return (false, 0, ex.GetBaseException().Message); }
    }
}
