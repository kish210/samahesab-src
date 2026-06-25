using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SamaHesab.SellerWeb.Services;

/// <summary>
/// SP-3 — کلاینتِ APIِ پنلِ فروشِ گردشگری. آدرسِ سرور + توکنِ JWT را نگه می‌دارد و
/// endpointهای TourismController/Auth را صدا می‌زند. فروشنده خودکار از همان JWT تعیین می‌شود.
/// </summary>
public class SellerApi
{
    private readonly HttpClient _http;
    public string BaseUrl { get; set; } = "";
    public string? Token { get; private set; }
    public TourismContext? Context { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    public SellerApi(HttpClient http) => _http = http;

    private HttpRequestMessage Req(HttpMethod m, string path)
    {
        var r = new HttpRequestMessage(m, BaseUrl.TrimEnd('/') + path);
        if (Token is not null) r.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        return r;
    }

    /// <summary>ورود → دریافتِ توکن + بارگذاریِ زمینهٔ پنل. خروجی: پیامِ خطا یا null در موفقیت.</summary>
    public async Task<string?> LoginAsync(string baseUrl, string username, string password, int companyId)
    {
        BaseUrl = baseUrl;
        try
        {
            var resp = await _http.PostAsJsonAsync(BaseUrl.TrimEnd('/') + "/api/auth/login",
                new { Username = username, Password = password, CompanyId = companyId });
            if (!resp.IsSuccessStatusCode)
                return resp.StatusCode == System.Net.HttpStatusCode.Unauthorized
                    ? "نام کاربری یا رمز نادرست است."
                    : $"خطای سرور ({(int)resp.StatusCode}).";
            var pair = await resp.Content.ReadFromJsonAsync<TokenPair>();
            Token = pair?.AccessToken;
            if (Token is null) return "پاسخِ نامعتبر از سرور.";
            Context = await GetJsonAsync<TourismContext>("/api/tourism/context");
            return null;
        }
        catch (Exception ex) { return "اتصال به سرور ناموفق بود: " + ex.Message; }
    }

    public void Logout() { Token = null; Context = null; }

    public Task<List<AvailabilityRow>?> GetAvailabilityAsync()
        => GetJsonAsync<List<AvailabilityRow>>("/api/tourism/availability");

    /// <summary>ثبتِ فروش. فروشنده/شعبه/سالِ مالی از زمینه؛ فروشنده=۰ (خودکار از JWT).</summary>
    public async Task<(bool ok, string? error, int saleId)> CreateSaleAsync(
        string paymentMethod, IReadOnlyList<SaleLine> lines, int? customerPartyId, string? note)
    {
        if (Context is null) return (false, "زمینهٔ پنل بارگذاری نشده.", 0);
        var body = new
        {
            BranchId = Context.BranchId,
            FiscalYearId = Context.FiscalYearId,
            Date = TodayJalali(),
            SalespersonPartyId = Context.SalespersonPartyId ?? 0,
            CustomerPartyId = customerPartyId,
            PaymentMethod = paymentMethod,
            Lines = lines,
            Note = note,
        };
        try
        {
            var req = Req(HttpMethod.Post, "/api/tourism/sales");
            req.Content = JsonContent.Create(body);
            var resp = await _http.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                var r = await resp.Content.ReadFromJsonAsync<SaleResult>();
                return (true, null, r?.SaleId ?? 0);
            }
            var err = await resp.Content.ReadFromJsonAsync<ErrorBody>();
            return (false, err?.Message ?? $"خطای سرور ({(int)resp.StatusCode}).", 0);
        }
        catch (Exception ex) { return (false, "اتصال ناموفق: " + ex.Message, 0); }
    }

    private async Task<T?> GetJsonAsync<T>(string path)
    {
        var resp = await _http.SendAsync(Req(HttpMethod.Get, path));
        return resp.IsSuccessStatusCode ? await resp.Content.ReadFromJsonAsync<T>() : default;
    }

    /// <summary>تاریخِ امروزِ شمسی (yyyy/MM/dd) — برای فیلدِ تاریخِ فروش.</summary>
    private static string TodayJalali()
    {
        var pc = new System.Globalization.PersianCalendar();
        var now = DateTime.Now;
        return $"{pc.GetYear(now):0000}/{pc.GetMonth(now):00}/{pc.GetDayOfMonth(now):00}";
    }

    // ── DTOها (هم‌راستا با API) ──
    public record TokenPair(string AccessToken, string RefreshToken, DateTime ExpiresAt);
    public record TourismContext(int BranchId, int FiscalYearId, string? FiscalYearTitle,
        int? SalespersonPartyId, string? FullName, bool IsSeller);
    public record AvailabilityRow(int ProductId, string Name, decimal SalePrice,
        int? Capacity, decimal Sold, decimal? Remaining, bool IsSoldOut);
    public record SaleLine(int ProductId, decimal Quantity, decimal UnitSalePrice, decimal DiscountAmount = 0);
    private record SaleResult(int SaleId);
    private record ErrorBody(string Message);
}
