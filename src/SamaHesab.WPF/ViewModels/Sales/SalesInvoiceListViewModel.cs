using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Sales;

public partial class SalesInvoiceListViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IRepository<SalesInvoice> _invoiceRepository;
    private readonly IRepository<Customer> _customerRepository;

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatus = "همه";
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalAmount;

    public ObservableCollection<SalesInvoiceListItem> Invoices { get; } = new();
    public List<string> StatusList { get; } = new() { "همه", "پیش‌نویس", "قطعی", "لغو شده" };

    public SalesInvoiceListViewModel(ICurrentUserService currentUser, IPersianCalendarService calendar,
        IRepository<SalesInvoice> invoiceRepository, IRepository<Customer> customerRepository,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _currentUser = currentUser;
        _calendar = calendar;
        _invoiceRepository = invoiceRepository;
        _customerRepository = customerRepository;
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
            var companyId = _currentUser.CompanyId ?? 1;
            var list = await _invoiceRepository.FindAsync(i => i.CompanyId == companyId);
            var customers = (await _customerRepository.GetAllAsync()).ToDictionary(c => c.Id, c => c.FullName);

            foreach (var inv in list.OrderByDescending(i => i.Id))
            {
                var statusFa = StatusToPersian(inv.Status);
                if (SelectedStatus != "همه" && statusFa != SelectedStatus) continue;
                customers.TryGetValue(inv.CustomerId, out var cname);
                Invoices.Add(new SalesInvoiceListItem(
                    inv.Id, inv.InvoiceNumber, inv.InvoiceDate, cname ?? $"#{inv.CustomerId}",
                    inv.GrandTotal, inv.PaidAmount, inv.RemainAmount, statusFa));
            }
            TotalCount = Invoices.Count;
            TotalAmount = Invoices.Sum(i => i.Total);
        });
    }

    private static string StatusToPersian(SamaHesab.Domain.Enums.InvoiceStatus s) => s switch
    {
        SamaHesab.Domain.Enums.InvoiceStatus.Draft => "پیش‌نویس",
        SamaHesab.Domain.Enums.InvoiceStatus.Confirmed => "قطعی",
        SamaHesab.Domain.Enums.InvoiceStatus.Posted => "قطعی",
        SamaHesab.Domain.Enums.InvoiceStatus.Cancelled => "لغو شده",
        _ => s.ToString()
    };

    [RelayCommand] private void NewInvoice() => _navigationService.NavigateTo("SalesInvoice");

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
