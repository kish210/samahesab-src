using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Purchase;

public partial class PurchaseInvoiceListViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IRepository<PurchaseInvoice> _invoiceRepository;
    private readonly IRepository<Supplier> _supplierRepository;

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalAmount;

    public ObservableCollection<PurchaseInvoiceListItem> Invoices { get; } = new();

    public PurchaseInvoiceListViewModel(ICurrentUserService currentUser, IPersianCalendarService calendar,
        IRepository<PurchaseInvoice> invoiceRepository, IRepository<Supplier> supplierRepository,
        IDialogService d, INavigationService n) : base(d, n)
    {
        _currentUser = currentUser;
        _calendar = calendar;
        _invoiceRepository = invoiceRepository;
        _supplierRepository = supplierRepository;
    }

    public override async Task LoadAsync() => await SearchAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        await ExecuteAsync(async () =>
        {
            Invoices.Clear();
            var companyId = _currentUser.CompanyId ?? 1;
            var list = await _invoiceRepository.FindAsync(i => i.CompanyId == companyId);
            var suppliers = (await _supplierRepository.GetAllAsync()).ToDictionary(s => s.Id, s => s.FullName);

            foreach (var inv in list.OrderByDescending(i => i.Id))
            {
                suppliers.TryGetValue(inv.SupplierId, out var sname);
                Invoices.Add(new PurchaseInvoiceListItem(
                    inv.Id, inv.InvoiceNumber, inv.InvoiceDate, sname ?? $"#{inv.SupplierId}",
                    inv.GrandTotal, inv.PaidAmount, inv.RemainAmount, inv.StatusCode));
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
