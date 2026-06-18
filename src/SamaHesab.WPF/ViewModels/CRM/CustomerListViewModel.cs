using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Reports.Export;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.CRM;

/// <summary>مشتریان — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class CustomerListViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalBalance;

    public ObservableCollection<CustomerListItem> Customers { get; } = new();

    public CustomerListViewModel(IMediator mediator, ApiClient api,
        IDialogService d, INavigationService n)
        : base(d, n) { _mediator = mediator; _api = api; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Customers.Clear();
            var search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText;
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                foreach (var c in await _api.GetCustomersAsync(search))
                    Customers.Add(new CustomerListItem(c.Id, c.Code, c.Name, c.Mobile, c.Balance, c.PriceLevel, c.IsActive));
            }
            else
            {
                foreach (var c in await _mediator.Send(new GetCustomersQuery(search)))
                    Customers.Add(new CustomerListItem(c.Id, c.Code, c.Name, c.Mobile, c.Balance, c.PriceLevel, c.IsActive));
            }
            TotalCount = Customers.Count;
            TotalBalance = Customers.Sum(x => x.Balance);
        }, "در حال بارگذاری مشتریان...");
    }

    [RelayCommand] private async Task SearchAsync() => await LoadAsync();
    [RelayCommand] private void NewCustomer() => _navigationService.NavigateTo("CustomerEdit");
    [RelayCommand] private void EditCustomer() { }
    [RelayCommand] private async Task SendSmsAsync() => await _dialogService.ShowInfoAsync("ارسال پیامک...");
    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Customers.Count == 0) { await _dialogService.ShowWarningAsync("فهرستی برای خروجی نیست."); return; }
        try
        {
            string N(decimal d) => d.ToString("N0");
            var table = new ReportTable("لیست مشتریان",
                new[] { "کد", "نام", "موبایل", "سطح قیمت", "مانده", "وضعیت" },
                Customers.Select(c => new[] { c.Code, c.Name, c.Mobile, c.PriceLevel, N(c.Balance), c.IsActive ? "فعال" : "غیرفعال" }).ToList());
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"لیست_مشتریان_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            System.IO.File.WriteAllText(path, ReportExporter.ToCsv(table), new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}

public record CustomerListItem(int Id, string Code, string Name, string Mobile, decimal Balance, string PriceLevel, bool IsActive);
