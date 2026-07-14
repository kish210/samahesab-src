using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Application.Sales.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Sales;

/// <summary>
/// لیستِ فاکتورهای فروش — 🏛️ الگوی API-only: کلاینتِ شبکه‌ای از API (ApiClient.GetSalesInvoices)،
/// دسکتاپِ آفلاین از لایهٔ Application (GetSalesInvoicesQuery). بدونِ ریپازیتوریِ مستقیم.
/// </summary>
public partial class SalesInvoiceListViewModel : BaseViewModel, ISupportsNew
{
    /// <summary>F2-GLOBAL: «جدید» → فاکتورِ فروشِ جدید.</summary>
    public void RequestNew() => NewInvoice();

    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;
    private readonly ApiClient _api;
    private readonly ModuleService _modules;

    /// <summary>آیا دکمهٔ «ارسال به مودیان» نشان داده شود؟ (ماژولِ اختیاریِ TaxInvoicing).</summary>
    public bool TaxInvoicingEnabled => _modules.IsEnabled(ModuleService.TaxInvoicing);

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatus = "همه";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalAmount;

    public ObservableCollection<SalesInvoiceListItem> Invoices { get; } = new();
    public List<string> StatusList { get; } = new() { "همه", "پیش‌نویس", "قطعی", "لغو شده" };
    [ObservableProperty] private SalesInvoiceListItem? _selectedInvoice;

    /// <summary>UX-SALES-VIEW — بازکردنِ فاکتورِ انتخاب‌شده در حالتِ مشاهده (راست‌کلیک/دابل‌کلیک).</summary>
    [RelayCommand]
    private void OpenInvoice(SalesInvoiceListItem? i)
    {
        var item = i ?? SelectedInvoice;
        if (item != null) _navigationService.NavigateTo("SalesInvoice", item.Id);
    }

    public SalesInvoiceListViewModel(IPersianCalendarService calendar,
        IMediator mediator, ApiClient api, ModuleService modules,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _calendar = calendar;
        _mediator = mediator;
        _api = api;
        _modules = modules;
    }

    public override async Task LoadAsync()
    {
        var persianCal = new System.Globalization.PersianCalendar();
        var now = DateTime.Now;
        FromDate = $"{persianCal.GetYear(now)}/{persianCal.GetMonth(now):D2}/01";
        ToDate = _calendar.GetCurrentPersianDate();
        await SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await ExecuteAsync(async () =>
        {
            Invoices.Clear();
            var status = SelectedStatus == "همه" ? null : SelectedStatus;
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;

            // 🏛️ مسیرِ داده: کلاینتِ شبکه‌ای → API؛ دسکتاپِ آفلاین → Application.
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                foreach (var r in await _api.GetSalesInvoicesAsync(FromDate, ToDate, status, search))
                    Invoices.Add(new SalesInvoiceListItem(r.Id, r.Number, r.Date, r.CustomerName, r.Total, r.Paid, r.Remain, r.Status));
            }
            else
            {
                foreach (var r in await _mediator.Send(new GetSalesInvoicesQuery(FromDate, ToDate, status, search)))
                    Invoices.Add(new SalesInvoiceListItem(r.Id, r.Number, r.Date, r.CustomerName, r.Total, r.Paid, r.Remain, r.Status));
            }
            TotalCount = Invoices.Count;
            TotalAmount = Invoices.Sum(i => i.Total);
        });
    }

    [RelayCommand] private void NewInvoice() => _navigationService.NavigateTo("SalesInvoice");

    /// <summary>دکمهٔ ردیفیِ «ارسال به مودیان» — صف/ارسالِ همان فاکتور به سامانهٔ مودیان (ماژولِ اختیاری).</summary>
    [RelayCommand]
    private async Task SendToTaxAuthorityAsync(SalesInvoiceListItem? row)
    {
        if (row is null) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(
                new SamaHesab.Modules.TaxInvoicing.Application.Commands.SendInvoiceToTaxAuthorityCommand(row.Id));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync($"فاکتورِ {row.Number} به سامانهٔ مودیان ارسال شد.");
        }, "در حال ارسال به سامانهٔ مودیان...");
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Invoices.Count == 0) { await _dialogService.ShowWarningAsync("فهرستی برای چاپ نیست."); return; }
        try { OpenFile(SaveTo("html", ReportExporter.ToHtml(BuildTable()))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (Invoices.Count == 0) { await _dialogService.ShowWarningAsync("فهرستی برای خروجی نیست."); return; }
        try { OpenFile(SaveTo("csv", ReportExporter.ToCsv(BuildTable()))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        return new ReportTable($"لیست فاکتورهای فروش ({FromDate} تا {ToDate})",
            new[] { "شماره", "تاریخ", "مشتری", "مبلغ کل", "پرداختی", "مانده", "وضعیت" },
            Invoices.Select(i => new[] { i.Number, i.Date, i.CustomerName, N(i.Total), N(i.Paid), N(i.Remain), i.Status }).ToList());
    }

    private static string SaveTo(string ext, string content)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"لیست_فروش_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}

public record SalesInvoiceListItem(int Id, string Number, string Date, string CustomerName,
    decimal Total, decimal Paid, decimal Remain, string Status);
