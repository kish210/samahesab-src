using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Modules.Tourism.Application;
using SamaHesab.Modules.Tourism.Application.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Tourism;

/// <summary>یک گزینهٔ گزارش در دراپ‌داون.</summary>
public record TourismReportPick(int Index, string Display);

/// <summary>
/// TUR-C2-5 — صفحهٔ گزارش‌های گردشگری. چهار گزارشِ <see cref="TourismReportBuilder"/> را از
/// <see cref="GetTourismReportsQuery"/> می‌گیرد، با ستون‌های پویا در گرید نشان می‌دهد و
/// به CSV/HTML/PDF خروجی می‌گیرد. فیلترِ دوره (ماهِ شمسی YYYYMM) فقط بر پورسانت/عملکرد اثر دارد.
/// </summary>
public partial class TourismReportsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;
    private ReportTable[] _tables = System.Array.Empty<ReportTable>();

    public ObservableCollection<TourismReportPick> Reports { get; } =
    [
        new(0, "ماندهٔ ودیعهٔ تأمین‌کنندگان"),
        new(1, "سودِ محصولات"),
        new(2, "پورسانتِ ماهانهٔ فروشندگان"),
        new(3, "عملکردِ فروشندگان"),
        new(4, "فهرستِ فروش (تاریخِ سفر/مسافر)"),
    ];

    [ObservableProperty] private int _selectedReportIndex;
    [ObservableProperty] private string _period = string.Empty;   // YYYYMM شمسی؛ خالی = همهٔ دوره‌ها
    [ObservableProperty] private DataView? _currentView;
    [ObservableProperty] private string _currentTitle = string.Empty;
    [ObservableProperty] private int _rowCount;
    [ObservableProperty] private int _voucherSaleId;              // شمارهٔ فروش برای چاپِ واچرِ مسافر

    /// <summary>چاپِ واچرِ مسافر/کارتِ سفر برای یک فروشِ گردشگری (GetTravelVoucherQuery → HTMLِ راست‌چین).</summary>
    [RelayCommand]
    private async Task PrintTravelVoucherAsync()
    {
        if (VoucherSaleId <= 0) { await _dialogService.ShowWarningAsync("شمارهٔ فروش را وارد کنید."); return; }
        try
        {
            var res = await _mediator.Send(new GetTravelVoucherQuery(VoucherSaleId));
            if (!res.Succeeded || res.Value is null) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            var g = AppSettingsStore.GetGeneral();
            var html = BuildVoucherHtml(res.Value, g.CompanyName);
            var path = SaveBytes("html", new System.Text.UTF8Encoding(true).GetBytes(html));
            OpenFile(path);
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    private static string N(decimal d) => Converters.NumberFormatConverter.ToPersian(d.ToString("#,0"));

    /// <summary>سندِ واچرِ سفر → HTMLِ راست‌چینِ قابلِ چاپ (سربرگ + خدمات + مسافران + جمع).</summary>
    private static string BuildVoucherHtml(TravelVoucher v, string? companyName)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<!doctype html><html dir='rtl' lang='fa'><head><meta charset='utf-8'><title>واچرِ سفر</title><style>");
        sb.Append("body{font-family:Tahoma,Vazirmatn,sans-serif;margin:24px;color:#222}");
        sb.Append("h1{font-size:20px;color:#2E5A9C;margin:0 0 4px}.muted{color:#666;font-size:12px}");
        sb.Append("table{width:100%;border-collapse:collapse;margin-top:12px}th,td{border:1px solid #bbb;padding:6px;font-size:13px;text-align:right}");
        sb.Append("th{background:#eef2f8}.tot{font-weight:bold;background:#f6f8fb}.box{border:1px solid #ddd;border-radius:8px;padding:10px;margin-top:10px}");
        sb.Append("@media print{button{display:none}}</style></head><body>");
        sb.Append($"<h1>واچرِ سفر / کارتِ مسافر</h1><div class='muted'>{System.Net.WebUtility.HtmlEncode(companyName ?? "")}</div>");
        var h = v.Header;
        sb.Append("<div class='box'>");
        sb.Append($"شمارهٔ واچر: <b>{System.Net.WebUtility.HtmlEncode(h.VoucherNo)}</b> &nbsp;|&nbsp; تاریخِ صدور: <b>{Converters.NumberFormatConverter.ToPersian(h.IssueDate)}</b><br>");
        sb.Append($"مشتری: <b>{System.Net.WebUtility.HtmlEncode(h.CustomerName)}</b> &nbsp;|&nbsp; فروشنده: <b>{System.Net.WebUtility.HtmlEncode(h.SalespersonName)}</b> &nbsp;|&nbsp; پرداخت: {System.Net.WebUtility.HtmlEncode(h.PaymentMethod)}");
        sb.Append("</div>");

        sb.Append("<table><thead><tr><th>خدمت</th><th>تأمین‌کننده</th><th>تاریخِ سفر</th><th>تعداد</th><th>قیمتِ واحد</th><th>مسافران</th></tr></thead><tbody>");
        foreach (var l in v.Lines)
        {
            var pax = string.Join("، ", l.Passengers.Select(p =>
                System.Net.WebUtility.HtmlEncode(p.FullName) + (string.IsNullOrWhiteSpace(p.NationalIdOrPassport) ? "" : $" ({System.Net.WebUtility.HtmlEncode(p.NationalIdOrPassport!)})")));
            sb.Append($"<tr><td>{System.Net.WebUtility.HtmlEncode(l.ProductName)}</td><td>{System.Net.WebUtility.HtmlEncode(l.SupplierName)}</td>");
            sb.Append($"<td>{Converters.NumberFormatConverter.ToPersian(l.TravelDate ?? "—")}</td><td>{N(l.Quantity)}</td><td>{N(l.UnitSalePrice)}</td><td>{pax}</td></tr>");
        }
        sb.Append("</tbody></table>");
        sb.Append("<table style='margin-top:8px'><tbody>");
        sb.Append($"<tr><td>تعدادِ مسافران</td><td class='tot'>{N(v.PassengerCount)}</td></tr>");
        sb.Append($"<tr><td>جمعِ فروش</td><td class='tot'>{N(v.TotalSale)} ریال</td></tr>");
        sb.Append($"<tr><td>تخفیف</td><td>{N(v.TotalDiscount)} ریال</td></tr>");
        sb.Append($"<tr><td>قابلِ پرداخت</td><td class='tot'>{N(v.NetPayable)} ریال</td></tr>");
        sb.Append("</tbody></table>");
        sb.Append("<p style='margin-top:16px'><button onclick='window.print()'>چاپ</button></p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public TourismReportsViewModel(IMediator mediator, IPdfService pdf,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _pdf = pdf; }

    public override Task LoadAsync() => RunAsync();

    partial void OnSelectedReportIndexChanged(int value) => ShowSelected();

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var period = string.IsNullOrWhiteSpace(Period) ? null : Period.Trim();
            var dto = await _mediator.Send(new GetTourismReportsQuery(period));
            // ✈️ رودمپ — فهرستِ فروش با تاریخِ سفر/مسافر (GetTourismSalesQuery، همهٔ دوره‌ها مثلِ بقیهٔ گزارش‌ها).
            var sales = await _mediator.Send(new GetTourismSalesQuery());
            _tables = [dto.SupplierDeposits, dto.ProductProfit, dto.MonthlyCommission, dto.SellerPerformance,
                       BuildSalesTable(sales)];
            ShowSelected();
        }, "در حال تهیهٔ گزارش...");
    }

    private void ShowSelected()
    {
        if (_tables.Length == 0) { CurrentView = null; CurrentTitle = string.Empty; RowCount = 0; return; }
        var t = _tables[System.Math.Clamp(SelectedReportIndex, 0, _tables.Length - 1)];
        CurrentView = ToDataTable(t).DefaultView;
        CurrentTitle = t.Title;
        RowCount = t.Rows.Count;
    }

    /// <summary>فهرستِ فروشِ گردشگری → ReportTable (همان زیرساختِ گرید/خروجی/چاپ).</summary>
    private static ReportTable BuildSalesTable(System.Collections.Generic.List<TourismSaleRowDto> rows)
    {
        static string M(decimal v) => v.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture);
        var headers = new[] { "تاریخِ ثبت", "مشتری", "فروشنده", "نزدیک‌ترین سفر", "مسافر",
                              "خالصِ فروش", "سود", "پرداخت", "وضعیت" };
        var data = rows.Select(r => new[]
        {
            r.Date, r.CustomerName, r.SalespersonName, r.NearestTravelDate ?? "—",
            r.PassengerCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            M(r.NetSale), M(r.Profit), r.PaymentMethod, r.IsPosted ? "ثبت‌شده" : "پیش‌نویس",
        }).ToList();
        return new ReportTable("فهرستِ فروش (تاریخِ سفر/مسافر)", headers, data);
    }

    private static DataTable ToDataTable(ReportTable t)
    {
        var dt = new DataTable();
        foreach (var h in t.Headers) dt.Columns.Add(h, typeof(string));
        // ارقامِ فارسی + جداکنندهٔ هزارگانِ فارسی برای نمایش (ux-prompt §0/§4)؛ خروجیِ CSV/PDF لاتین می‌ماند.
        foreach (var r in t.Rows)
            dt.Rows.Add(r.Select(c => (object)Converters.NumberFormatConverter.ToPersian(c)).ToArray());
        return dt;
    }

    private ReportTable? Selected
        => _tables.Length == 0 ? null : _tables[System.Math.Clamp(SelectedReportIndex, 0, _tables.Length - 1)];

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (Selected is null) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try { OpenFile(SaveBytes("csv", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToCsv(Selected)))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Selected is null) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try { OpenFile(SaveBytes("html", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToHtml(Selected)))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (Selected is null) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try
        {
            var g = AppSettingsStore.GetGeneral();
            var meta = new PdfMeta(g.CompanyName, null, System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
            OpenFile(SaveBytes("pdf", _pdf.RenderTable(Selected, meta)));
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    private static string SaveBytes(string ext, byte[] content)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"گزارش_گردشگری_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
