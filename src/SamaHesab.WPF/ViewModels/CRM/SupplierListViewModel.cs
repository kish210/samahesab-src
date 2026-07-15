using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Reports.Export;
using System.Linq;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.CRM;

/// <summary>تأمین‌کنندگان — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class SupplierListViewModel : BaseViewModel, SamaHesab.WPF.Services.ISupportsNew
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalBalance;

    public ObservableCollection<SupplierListItem> Suppliers { get; } = new();

    public SupplierListViewModel(IMediator mediator, ApiClient api,
        IDialogService d, INavigationService n) : base(d, n)
    { _mediator = mediator; _api = api; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Suppliers.Clear();
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                foreach (var s in await _api.GetSuppliersAsync(search))
                    Suppliers.Add(new SupplierListItem(s.Id, s.Code, s.Name, s.Mobile, s.City, s.Balance, s.IsActive));
            }
            else
            {
                foreach (var s in await _mediator.Send(new GetSuppliersQuery(search)))
                    Suppliers.Add(new SupplierListItem(s.Id, s.Code, s.Name, s.Mobile, s.City, s.Balance, s.IsActive));
            }
            TotalCount = Suppliers.Count;
            TotalBalance = Suppliers.Sum(x => x.Balance);
        }, "در حال بارگذاری تأمین‌کنندگان...");
    }

    [ObservableProperty] private SupplierListItem? _selectedSupplier;

    [RelayCommand] private async Task SearchAsync() => await LoadAsync();
    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();

    /// <summary>UX-CRM-SUPPLIER-1 — پیش‌تر این صفحه هیچ راهِ ساخت/ویرایشِ تأمین‌کننده نداشت (فقط
    /// صورت‌حساب) و دکمهٔ «+ تأمین‌کننده» در صفحهٔ اشخاص هم فقط همین لیست را دوباره باز می‌کرد —
    /// یعنی کاربر برایِ ساختِ تأمین‌کنندهٔ نو راهی نداشت جز رفتن به فرمِ عمومیِ «اشخاص».</summary>
    [RelayCommand] private void NewSupplier() => _navigationService.NavigateTo("CustomerEdit", "supplier");
    public void RequestNew() => NewSupplier();

    [RelayCommand]
    private void EditSupplier(SupplierListItem? s)
    {
        var item = s ?? SelectedSupplier;
        if (item != null) _navigationService.NavigateTo("CustomerEdit", item.Id);
    }

    /// <summary>UX-SUPPLIER-PARITY — صورت‌حسابِ تأمین‌کنندهٔ انتخاب‌شده (راست‌کلیک/دابل‌کلیک).</summary>
    [RelayCommand]
    private void OpenStatement(SupplierListItem? s)
    {
        var item = s ?? SelectedSupplier;
        if (item != null) _navigationService.NavigateTo("SupplierStatement", item.Id);
    }
    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Suppliers.Count == 0) { await _dialogService.ShowWarningAsync("فهرستی برای خروجی نیست."); return; }
        try
        {
            string N(decimal d) => d.ToString("N0");
            var table = new ReportTable("لیست تأمین‌کنندگان",
                new[] { "کد", "نام", "موبایل", "شهر", "مانده", "وضعیت" },
                Suppliers.Select(s => new[] { s.Code, s.Name, s.Mobile, s.City, N(s.Balance), s.IsActive ? "فعال" : "غیرفعال" }).ToList());
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"لیست_تأمین‌کنندگان_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            System.IO.File.WriteAllText(path, ReportExporter.ToCsv(table), new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}

public record SupplierListItem(int Id, string Code, string Name, string Mobile, string City, decimal Balance, bool IsActive);
