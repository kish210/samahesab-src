using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace SamaHesab.WPF.Services;

public record ApiProduct(int Id, string Code, string Name, string? Barcode, decimal SalePrice, decimal TaxRate, int? GroupId = null);
public record ApiPerson(int Id, string Code, string Name, string Mobile, decimal Balance,
    string Role, bool IsCustomer, bool IsSupplier, bool IsActive);
public record ApiProductRow(int Id, string Code, string Barcode, string Name,
    decimal SalePrice, decimal PurchasePrice, decimal WholesalePrice,
    decimal MinStock, bool IsActive, bool IsLowStock,
    decimal ConsumerPrice = 0, decimal TaxRate = 0);
public record ApiSalesInvoiceRow(int Id, string Number, string Date, string CustomerName,
    decimal Total, decimal Paid, decimal Remain, string Status);
public record ApiSupplierRow(int Id, string Code, string Name, string Mobile, string City, decimal Balance, bool IsActive);
public record ApiAccountRow(int Id, string Code, string Name, int Level, string Nature, bool IsActive,
    string? AccountType = null, int? ParentId = null, bool IsLeaf = false);
public record ApiCustomerRow(int Id, string Code, string Name, string Mobile, decimal Balance, string PriceLevel, bool IsActive);
public record ApiCustomerCard(int Id, string Name, string Code, string CustomerType, string PriceLevel,
    string? Mobile, string? Phone, string? NationalCode, string? EconomicCode,
    string? ContactPerson, string? Visitor, string? Province, string? City, string? Address,
    int LoyaltyPoints, int CreditDays, bool IsActive, decimal Balance, decimal CreditLimit, decimal ChequeInProgress);
public record ApiPurchaseInvoiceRow(int Id, string Number, string Date, string SupplierName,
    decimal Total, decimal Paid, decimal Remain, string Status);
public record ApiWarehouseRow(int Id, string Code, string Name, string Manager, string Address, bool IsDefault, bool IsActive);
public record ApiBankAccount(int Id, string BankName, string AccountNumber, string Sheba,
    string CardNumber, string BranchName, decimal OpeningBalance, bool IsActive);
public record ApiVoucherPreviewLine(string AccountName, decimal Debit, decimal Credit);
public record ApiProductCardStock(string WarehouseName, decimal Quantity, bool IsLow);
public record ApiProductCard(int Id, string Code, string Name, string? Barcode, bool IsActive,
    decimal PurchasePrice, decimal SalePrice, decimal WholesalePrice, decimal ConsumerPrice, decimal TaxRate,
    decimal MinStock, decimal? MaxStock, decimal? ReorderPoint, string Tracking,
    decimal TotalStock, List<ApiProductCardStock> WarehouseStocks);
public record ApiGroup(int Id, string Name);
public record ApiMe(int UserId, int CompanyId, int BranchId, string Username, string FullName,
    string[] Roles, string[]? Permissions = null);

// ── Restaurant (v2) DTOs — match the Application-layer query DTOs ──
public record ApiHall(int Id, string Name, int DisplayOrder, List<ApiTable> Tables);
public record ApiTable(int Id, int HallId, string Name, int Capacity, string Status,
    int StatusCode, int? CurrentOrderId, int PositionX, int PositionY, DateTime? OccupiedSince = null);
public record ApiOrder(int Id, string OrderNumber, string OrderType, string Status, int? TableId,
    int GuestCount, decimal SubTotal, decimal Discount, decimal ServiceCharge, decimal Tax,
    decimal Tip, decimal GrandTotal, decimal PaidAmount, List<ApiOrderItem> Items);
public record ApiOrderItem(int Id, int ProductId, string ProductName, decimal Quantity,
    decimal UnitPrice, decimal DiscountAmount, decimal LineTotal, string Status, int StatusCode, string? Notes);
public record ApiKitchenTicket(int Id, string TicketNumber, int OrderId, string? TableName,
    string Status, int StatusCode, DateTime CreatedAt, List<ApiKitchenItem> Items);
public record ApiKitchenItem(int Id, string ProductName, decimal Quantity, string Status, string? Notes);

// ── Waiter (U7) DTO ──
public record ApiWaiter(int Id, string Name);

// ── Unified barcode (#27) DTO ──
public record ApiBarcodeHit(int ProductId, string Code, string Name, decimal SalePrice,
    decimal TaxRate, int? GroupId, bool Weighted, decimal? EmbeddedValue);

// ── POS shift (#30/#31) DTO ──
public record ApiShiftSummary(int Id, decimal OpeningFloat, decimal CashSales, decimal CardSales,
    int SalesCount, decimal ExpectedCash, decimal CountedCash, decimal Variance, int? VarianceVoucherId = null);

// ── Favorites / بهره‌وری (آیتم‌های اخیر و سنجاق‌شده) DTO — match Application ItemRefDto ──
public record ApiItemRef(int EntityId, string Label, bool Pinned, int UseCount);

// ── Held sales / فاکتورهای معلق POS (#33) DTOs ──
public record ApiHeldSale(int Id, string Label, decimal Total, DateTime CreatedAt);
public record ApiHeldSaleDetail(int Id, string Label, string Payload, decimal Total);

// ── Warehouse (v2 — Phase 2) DTOs ──
public record ApiWarehouse(int Id, string Name);
public record ApiStockRow(int ProductId, string Code, string Name, decimal Quantity, decimal AverageCost, decimal Value);

// ── Batch (R8 — انتخابِ بچ هنگام حواله) DTO ──
public record ApiBatch(int Id, int ProductId, string BatchNumber, string? ProductionDate,
    string? ExpiryDate, decimal Quantity, decimal? PurchasePrice, string? Notes);

// ── Serial (L1 — انتخابِ سریالِ تک‌واحدی هنگام حواله) DTO ──
public record ApiSerial(int Id, int ProductId, int? WarehouseId, string SerialNumber,
    string Status, decimal? PurchasePrice, string? PurchaseDate, string? SaleDate);

// ── Stock count / انبارگردانی (#28) DTOs ──
public record ApiStockCount(int Id, int WarehouseId, string Date, string Status,
    int LineCount, int VarianceCount, List<ApiStockCountLine> Lines);
public record ApiStockCountLine(int ProductId, string ProductName, decimal SystemQty,
    decimal CountedQty, decimal Variance);

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

    /// <summary>اشخاص (طرف‌حساب) — مشتری+تأمین‌کننده از API (الگوی مرجعِ API-only).</summary>
    public async Task<List<ApiPerson>> GetPersonsAsync(string? search = null, int? role = null)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (role is > 0) qs.Add($"role={role}");
        var url = "/api/persons" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsync<List<ApiPerson>>(url) ?? new();
    }

    /// <summary>کالاها — فهرستِ کاملِ صفحهٔ مدیریت (با هشدارِ کسری) از API.</summary>
    public async Task<List<ApiProductRow>> GetProductListAsync(string? search = null)
    {
        var url = string.IsNullOrWhiteSpace(search) ? "/api/products/list" : $"/api/products/list?search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<List<ApiProductRow>>(url) ?? new();
    }

    /// <summary>غیرفعال‌سازیِ (حذفِ نرمِ) کالا از طریقِ API.</summary>
    public async Task<bool> DeactivateProductAsync(int id)
    {
        var resp = await _http.PostAsync($"/api/products/{id}/deactivate", null);
        return resp.IsSuccessStatusCode;
    }

    /// <summary>فهرستِ فاکتورهای فروش از API.</summary>
    public async Task<List<ApiSalesInvoiceRow>> GetSalesInvoicesAsync(string? from = null, string? to = null,
        string? status = null, string? search = null)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(from)) qs.Add($"from={Uri.EscapeDataString(from)}");
        if (!string.IsNullOrWhiteSpace(to)) qs.Add($"to={Uri.EscapeDataString(to)}");
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        var url = "/api/sales/invoices" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsync<List<ApiSalesInvoiceRow>>(url) ?? new();
    }

    /// <summary>فهرستِ تأمین‌کنندگان از API.</summary>
    public async Task<List<ApiSupplierRow>> GetSuppliersAsync(string? search = null)
    {
        var url = string.IsNullOrWhiteSpace(search) ? "/api/suppliers" : $"/api/suppliers?search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<List<ApiSupplierRow>>(url) ?? new();
    }

    /// <summary>فهرستِ حساب‌ها از API.</summary>
    public async Task<List<ApiAccountRow>> GetAccountsAsync()
        => await _http.GetFromJsonAsync<List<ApiAccountRow>>("/api/accounts") ?? new();

    /// <summary>فهرستِ مشتریان از API.</summary>
    public async Task<List<ApiCustomerRow>> GetCustomersAsync(string? search = null)
    {
        var url = string.IsNullOrWhiteSpace(search) ? "/api/customers" : $"/api/customers?search={Uri.EscapeDataString(search)}";
        return await _http.GetFromJsonAsync<List<ApiCustomerRow>>(url) ?? new();
    }

    /// <summary>کارت ۳۶۰° مشتری از API.</summary>
    public async Task<ApiCustomerCard?> GetCustomerCardAsync(int id)
        => await _http.GetFromJsonAsync<ApiCustomerCard?>($"/api/customers/{id}/card");

    /// <summary>ساختِ مشتری از طریقِ API (کامندِ مشترکِ لایهٔ Application).</summary>
    public async Task<(bool ok, string? error)> CreateCustomerAsync(object command)
    {
        var resp = await _http.PostAsJsonAsync("/api/customers", command);
        return resp.IsSuccessStatusCode ? (true, null) : (false, await resp.Content.ReadAsStringAsync());
    }

    /// <summary>داشبوردِ کاملِ عملیاتی از API (DTO مشترکِ لایهٔ Application).</summary>
    public async Task<SamaHesab.Application.BI.Queries.DashboardDto?> GetDashboardAsync(string today)
        => await _http.GetFromJsonAsync<SamaHesab.Application.BI.Queries.DashboardDto>($"/api/dashboard/full?today={Uri.EscapeDataString(today)}");

    /// <summary>فهرستِ چک‌ها از API (DTO مشترکِ Application).</summary>
    public async Task<List<SamaHesab.Application.Accounting.Queries.ChequeRowDto>> GetChequesAsync()
        => await _http.GetFromJsonAsync<List<SamaHesab.Application.Accounting.Queries.ChequeRowDto>>("/api/cheques") ?? new();

    /// <summary>فهرستِ کاملِ انبارها (صفحهٔ مدیریت) از API.</summary>
    public async Task<List<ApiWarehouseRow>> GetWarehouseListAsync()
        => await _http.GetFromJsonAsync<List<ApiWarehouseRow>>("/api/warehouse/list") ?? new();

    /// <summary>حساب‌های بانکی از API.</summary>
    public async Task<List<ApiBankAccount>> GetBankAccountsAsync(bool activeOnly = false)
        => await _http.GetFromJsonAsync<List<ApiBankAccount>>($"/api/bankaccounts?activeOnly={(activeOnly ? "true" : "false")}") ?? new();

    /// <summary>پیش‌نمایشِ ردیف‌های یک سند از API.</summary>
    public async Task<List<ApiVoucherPreviewLine>> GetVoucherPreviewAsync(int voucherId)
        => await _http.GetFromJsonAsync<List<ApiVoucherPreviewLine>>($"/api/vouchers/{voucherId}/preview") ?? new();

    /// <summary>بارگذاریِ سند برای ویرایش از API (DTO مشترکِ Application).</summary>
    public async Task<SamaHesab.Application.Accounting.Queries.VoucherEditDto?> GetVoucherForEditAsync(int id)
        => await _http.GetFromJsonAsync<SamaHesab.Application.Accounting.Queries.VoucherEditDto>($"/api/vouchers/{id}/edit");

    /// <summary>کارت کالا (۳۶۰°) از API.</summary>
    public async Task<ApiProductCard?> GetProductCardAsync(int id)
        => await _http.GetFromJsonAsync<ApiProductCard?>($"/api/products/{id}/card");

    /// <summary>فهرستِ فاکتورهای خرید از API.</summary>
    public async Task<List<ApiPurchaseInvoiceRow>> GetPurchaseInvoicesAsync(string? from = null, string? to = null, string? search = null)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(from)) qs.Add($"from={Uri.EscapeDataString(from)}");
        if (!string.IsNullOrWhiteSpace(to)) qs.Add($"to={Uri.EscapeDataString(to)}");
        if (!string.IsNullOrWhiteSpace(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        var url = "/api/purchase/invoices" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return await _http.GetFromJsonAsync<List<ApiPurchaseInvoiceRow>>(url) ?? new();
    }

    public async Task<List<ApiGroup>> GetGroupsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ApiGroup>>("/api/products/groups") ?? new(); }
        catch { return new(); }
    }

    /// <summary>Returns the authenticated principal (from JWT claims) — call after a successful login.</summary>
    public async Task<ApiMe?> GetMeAsync()
    {
        try { return await _http.GetFromJsonAsync<ApiMe>("/api/auth/me"); }
        catch { return null; }
    }

    public async Task<(bool ok, int invoiceId, string? error)> CreatePosSaleAsync(
        IEnumerable<(int productId, decimal qty, decimal unitPrice, decimal discountPct, decimal taxPct)> items,
        decimal paid, string paymentMethod, int customerId, int warehouseId, decimal discount = 0,
        decimal otherCosts = 0, string? description = null)
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
                paid, paymentMethod, customerId, warehouseId, discount, otherCosts, description
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

    // ── Favorites / بهره‌وری (#38 پشتیبان) — آیتم‌های اخیر/سنجاق‌شده‌ی هر نوع ──
    public async Task<List<ApiItemRef>> GetRecentItemsAsync(string entityType, int top = 10)
    {
        try { return await _http.GetFromJsonAsync<List<ApiItemRef>>($"/api/favorites/recent/{entityType}?top={top}") ?? new(); }
        catch { return new(); }
    }

    public async Task<List<ApiItemRef>> GetPinnedItemsAsync(string entityType)
    {
        try { return await _http.GetFromJsonAsync<List<ApiItemRef>>($"/api/favorites/pinned/{entityType}") ?? new(); }
        catch { return new(); }
    }

    /// <summary>ثبت استفاده از یک آیتم (به‌روزرسانی فهرست اخیر/شمارش استفاده). best-effort.</summary>
    public async Task TouchRecentAsync(string entityType, int entityId, string label)
    {
        try { await _http.PostAsJsonAsync("/api/favorites/touch", new { entityType, entityId, label }); }
        catch { /* بهره‌وری؛ شکستش نباید فروش را متوقف کند */ }
    }

    // ── Held sales / فاکتورهای معلق POS (#33) ──
    public async Task<List<ApiHeldSale>> GetHeldSalesAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ApiHeldSale>>("/api/heldsales") ?? new(); }
        catch { return new(); }
    }

    public async Task<ApiHeldSaleDetail?> GetHeldSaleAsync(int id)
    {
        try { return await _http.GetFromJsonAsync<ApiHeldSaleDetail>($"/api/heldsales/{id}"); }
        catch { return null; }
    }

    public async Task<(bool ok, int id, string? error)> HoldSaleAsync(string label, string payload, decimal total)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/heldsales", new { label, payload, total });
            if (!resp.IsSuccessStatusCode) return (false, 0, await ReadErrorAsync(resp) ?? "خطا در تعلیق فاکتور.");
            var doc = await resp.Content.ReadFromJsonAsync<Dictionary<string, int>>();
            return (true, doc != null && doc.TryGetValue("heldSaleId", out var id) ? id : 0, null);
        }
        catch (Exception ex) { return (false, 0, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, string? error)> DeleteHeldSaleAsync(int id)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/heldsales/{id}");
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در حذف فاکتور معلق.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Restaurant (v2) — waiter / table / kitchen workflow over the Web API
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<string?> ReadErrorAsync(HttpResponseMessage resp)
    {
        try { return (await resp.Content.ReadFromJsonAsync<ErrorResult>())?.message; }
        catch { return null; }
    }

    public async Task<List<ApiHall>> GetHallsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ApiHall>>("/api/restaurant/halls") ?? new(); }
        catch { return new(); }
    }

    public async Task<ApiOrder?> GetOrderAsync(int orderId)
    {
        try { return await _http.GetFromJsonAsync<ApiOrder>($"/api/restaurant/orders/{orderId}"); }
        catch { return null; }
    }

    public async Task<List<ApiKitchenTicket>> GetKitchenBoardAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ApiKitchenTicket>>("/api/restaurant/kitchen") ?? new(); }
        catch { return new(); }
    }

    /// <summary>status: 1=Preparing 2=Ready 3=Completed</summary>
    public async Task<(bool ok, string? error)> AdvanceKitchenTicketAsync(int ticketId, int status)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/restaurant/kitchen/{ticketId}/status/{status}", null);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در تغییر وضعیت.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    /// <summary>orderType: 0=DineIn 1=Takeaway 2=Delivery</summary>
    public async Task<(bool ok, int orderId, string? error)> OpenOrderAsync(
        int orderType, int? tableId, int guestCount = 1, int? waiterId = null, int? customerId = null)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/restaurant/orders/open",
                new { orderType, tableId, guestCount, waiterId, customerId });
            if (!resp.IsSuccessStatusCode) return (false, 0, await ReadErrorAsync(resp) ?? "خطا در باز کردن سفارش.");
            var doc = await resp.Content.ReadFromJsonAsync<Dictionary<string, int>>();
            return (true, doc != null && doc.TryGetValue("orderId", out var id) ? id : 0, null);
        }
        catch (Exception ex) { return (false, 0, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, string? error)> AddOrderItemAsync(
        int orderId, int productId, string productName, decimal quantity, decimal unitPrice,
        decimal discountAmount = 0, string? notes = null)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/restaurant/orders/{orderId}/items",
                new { orderId, productId, productName, quantity, unitPrice, discountAmount, notes });
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در افزودن آیتم.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, string? error)> ChangeOrderItemQtyAsync(int orderId, int itemId, decimal qty)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/restaurant/orders/{orderId}/items/{itemId}/qty/{qty}", null);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در تغییر تعداد.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, string? error)> RemoveOrderItemAsync(int orderId, int itemId)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/restaurant/orders/{orderId}/items/{itemId}");
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در حذف ردیف.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, string? error)> SetOrderItemNotesAsync(int orderId, int itemId, string? notes)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/restaurant/orders/{orderId}/items/{itemId}/notes", notes);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در ثبت یادداشت.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    // ── Stock count / انبارگردانی (#28) ──
    public async Task<(bool ok, int sessionId, string? error)> StartStockCountAsync(int warehouseId, string date)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/stockcount/start", new { warehouseId, date });
            if (!resp.IsSuccessStatusCode) return (false, 0, await ReadErrorAsync(resp) ?? "خطا در شروع انبارگردانی.");
            var body = await resp.Content.ReadFromJsonAsync<StartCountResponse>();
            return (true, body?.SessionId ?? 0, null);
        }
        catch (Exception ex) { return (false, 0, ex.GetBaseException().Message); }
    }
    private record StartCountResponse(int SessionId);

    public async Task<ApiStockCount?> GetStockCountAsync(int sessionId)
    {
        try { return await _http.GetFromJsonAsync<ApiStockCount>($"/api/stockcount/{sessionId}"); }
        catch { return null; }
    }

    public async Task<(bool ok, string? error)> SetStockCountLineAsync(int sessionId, int productId, decimal countedQty)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/stockcount/{sessionId}/count", new { productId, countedQty });
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در ثبت شمارش.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, int adjusted, decimal variance, string? error)> PostStockCountAsync(int sessionId)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/stockcount/{sessionId}/post", null);
            if (!resp.IsSuccessStatusCode) return (false, 0, 0, await ReadErrorAsync(resp) ?? "خطا در قطعی‌سازی.");
            var body = await resp.Content.ReadFromJsonAsync<PostCountResponse>();
            return (true, body?.AdjustedItems ?? 0, body?.TotalVariance ?? 0, null);
        }
        catch (Exception ex) { return (false, 0, 0, ex.GetBaseException().Message); }
    }
    private record PostCountResponse(int AdjustedItems, decimal TotalVariance);

    // ── Unified barcode (#27) ──
    public async Task<ApiBarcodeHit?> ResolveBarcodeAsync(string code)
    {
        try { return await _http.GetFromJsonAsync<ApiBarcodeHit>($"/api/barcode/resolve?code={Uri.EscapeDataString(code)}"); }
        catch { return null; }
    }

    // ── POS shift / صندوق (#30/#31) ──
    public async Task<ApiShiftSummary?> GetCurrentShiftAsync()
    {
        try { return await _http.GetFromJsonAsync<ApiShiftSummary>("/api/shifts/current"); }
        catch { return null; }
    }

    public async Task<(bool ok, string? error)> OpenShiftAsync(decimal openingFloat)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/shifts/open", new { openingFloat });
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در باز کردن صندوق.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, ApiShiftSummary? summary, string? error)> CloseShiftAsync(decimal countedCash, string? notes = null)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/shifts/close", new { countedCash, notes });
            if (!resp.IsSuccessStatusCode) return (false, null, await ReadErrorAsync(resp) ?? "خطا در بستن صندوق.");
            var dto = await resp.Content.ReadFromJsonAsync<ApiShiftSummary>();
            return (true, dto, null);
        }
        catch (Exception ex) { return (false, null, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, string? error)> MoveOrderTableAsync(int orderId, int newTableId)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/restaurant/orders/{orderId}/move-table/{newTableId}", null);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در انتقال میز.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    /// <summary>U7 — رزرو/لغوِ رزروِ میز.</summary>
    public async Task<(bool ok, string? error)> ReserveTableAsync(int tableId, bool reserved)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/restaurant/tables/{tableId}/reserve/{(reserved ? "true" : "false")}", null);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در رزرو میز.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    /// <summary>U7 — تخصیص/تغییرِ گارسونِ سفارش.</summary>
    public async Task<(bool ok, string? error)> AssignWaiterAsync(int orderId, int waiterId)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/restaurant/orders/{orderId}/waiter/{waiterId}", null);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در تخصیص گارسون.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    /// <summary>U7 — فهرستِ گارسون‌ها (کارمندانِ فعال).</summary>
    public async Task<List<ApiWaiter>> GetWaitersAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ApiWaiter>>("/api/restaurant/waiters") ?? new(); }
        catch { return new(); }
    }

    public async Task<(bool ok, string? error)> SendToKitchenAsync(int orderId)
    {
        try
        {
            var resp = await _http.PostAsync($"/api/restaurant/orders/{orderId}/send-to-kitchen", null);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در ارسال به آشپزخانه.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, string? error)> SettleOrderAsync(int orderId, decimal paidAmount,
        decimal discount = 0, decimal serviceCharge = 0, decimal tax = 0, decimal tip = 0)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync($"/api/restaurant/orders/{orderId}/settle",
                new { orderId, paidAmount, discount, serviceCharge, tax, tip });
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در تسویه.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Warehouse (v2 — Phase 2): receiving / issuing / adjustment over the Web API
    // ─────────────────────────────────────────────────────────────────────────
    public async Task<List<ApiWarehouse>> GetWarehousesAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ApiWarehouse>>("/api/warehouse") ?? new(); }
        catch { return new(); }
    }

    public async Task<List<ApiStockRow>> GetWarehouseStockAsync(int warehouseId, string? q = null)
    {
        try
        {
            var url = $"/api/warehouse/stock?warehouseId={warehouseId}";
            if (!string.IsNullOrWhiteSpace(q)) url += $"&q={Uri.EscapeDataString(q)}";
            return await _http.GetFromJsonAsync<List<ApiStockRow>>(url) ?? new();
        }
        catch { return new(); }
    }

    public async Task<(bool ok, string? error)> ReceiveStockAsync(int warehouseId, string date,
        string? description, IEnumerable<(int productId, decimal qty, decimal unitCost)> items)
    {
        try
        {
            var body = new { warehouseId, date, description,
                items = items.Select(i => new { productId = i.productId, quantity = i.qty, unitCost = i.unitCost }).ToArray() };
            var resp = await _http.PostAsJsonAsync("/api/warehouse/receive", body);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در ثبت رسید.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, string? error)> IssueStockAsync(int warehouseId, string date,
        string? description, IEnumerable<(int productId, decimal qty, int? batchId, int? serialId)> items)
    {
        try
        {
            var body = new { warehouseId, date, description,
                items = items.Select(i => new { productId = i.productId, quantity = i.qty, batchId = i.batchId, serialId = i.serialId }).ToArray() };
            var resp = await _http.PostAsJsonAsync("/api/warehouse/issue", body);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در ثبت حواله.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    /// <summary>R8 — بچ‌های موجودِ یک کالا (برای انتخاب هنگام حواله).</summary>
    public async Task<List<ApiBatch>> GetBatchesAsync(int productId)
    {
        try { return await _http.GetFromJsonAsync<List<ApiBatch>>($"/api/batchserial/batches?productId={productId}") ?? new(); }
        catch { return new(); }
    }

    /// <summary>L1 — سریال‌های موجودِ یک کالا (برای انتخابِ تک‌واحدی هنگام حواله).</summary>
    public async Task<List<ApiSerial>> GetSerialsAsync(int productId)
    {
        try { return await _http.GetFromJsonAsync<List<ApiSerial>>($"/api/batchserial/serials?productId={productId}") ?? new(); }
        catch { return new(); }
    }

    public async Task<(bool ok, string? error)> AdjustStockAsync(int warehouseId, int productId,
        decimal newQuantity, string date, string? description)
    {
        try
        {
            var body = new { warehouseId, productId, newQuantity, date, description };
            var resp = await _http.PostAsJsonAsync("/api/warehouse/adjust", body);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در تعدیل.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }

    public async Task<(bool ok, string? error)> TransferStockAsync(int fromWarehouseId, int toWarehouseId,
        int productId, decimal quantity, string date, string? description)
    {
        try
        {
            var body = new { fromWarehouseId, toWarehouseId, productId, quantity, date, description };
            var resp = await _http.PostAsJsonAsync("/api/warehouse/transfer", body);
            return resp.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(resp) ?? "خطا در انتقال.");
        }
        catch (Exception ex) { return (false, ex.GetBaseException().Message); }
    }
}
