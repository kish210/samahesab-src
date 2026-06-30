using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Purchase.Queries;
using SamaHesab.Application.Reports.Export;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Purchase;

/// <summary>لیستِ فاکتورهای خرید — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class PurchaseInvoiceListViewModel : BaseViewModel, ISupportsNew
{
    /// <summary>F2-GLOBAL: «جدید» → فاکتورِ خریدِ جدید.</summary>
    public void RequestNew() => NewInvoice();

    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalAmount;
    [ObservableProperty] private PurchaseInvoiceListItem? _selectedInvoice;

    public ObservableCollection<PurchaseInvoiceListItem> Invoices { get; } = new();

    /// <summary>UX-PURCHASE-VIEW — بازکردنِ فاکتورِ خریدِ موجود در حالتِ مشاهده.</summary>
    [RelayCommand]
    private void OpenInvoice(PurchaseInvoiceListItem? item)
    {
        var inv = item ?? SelectedInvoice;
        if (inv != null) _navigationService.NavigateTo("PurchaseInvoice", inv.Id);
    }

    public PurchaseInvoiceListViewModel(IPersianCalendarService calendar,
        IMediator mediator, ApiClient api,
        IDialogService d, INavigationService n) : base(d, n)
    {
        _calendar = calendar;
        _mediator = mediator;
        _api = api;
    }

    public override async Task LoadAsync() => await SearchAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        await ExecuteAsync(async () =>
        {
            Invoices.Clear();
            // 🏛️ مسیرِ داده: کلاینتِ شبکه‌ای → API؛ دسکتاپِ آفلاین → Application.
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                foreach (var r in await _api.GetPurchaseInvoicesAsync(FromDate, ToDate))
                    Invoices.Add(new PurchaseInvoiceListItem(r.Id, r.Number, r.Date, r.SupplierName, r.Total, r.Paid, r.Remain, r.Status));
            }
            else
            {
                foreach (var r in await _mediator.Send(new GetPurchaseInvoicesQuery(FromDate, ToDate)))
                    Invoices.Add(new PurchaseInvoiceListItem(r.Id, r.Number, r.Date, r.SupplierName, r.Total, r.Paid, r.Remain, r.Status));
            }
            TotalCount = Invoices.Count;
            TotalAmount = Invoices.Sum(i => i.Total);
        });
    }

    [RelayCommand] private void NewInvoice() => _navigationService.NavigateTo("PurchaseInvoice");

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
        return new ReportTable($"لیست فاکتورهای خرید ({FromDate} تا {ToDate})",
            new[] { "شماره", "تاریخ", "تأمین‌کننده", "مبلغ کل", "پرداختی", "مانده", "وضعیت" },
            Invoices.Select(i => new[] { i.Number, i.Date, i.SupplierName, N(i.Total), N(i.Paid), N(i.Remain), i.Status }).ToList());
    }

    private static string SaveTo(string ext, string content)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"لیست_خرید_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}

public record PurchaseInvoiceListItem(int Id, string Number, string Date, string SupplierName,
    decimal Total, decimal Paid, decimal Remain, string Status);
