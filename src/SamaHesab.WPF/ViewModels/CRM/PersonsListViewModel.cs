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

/// <summary>
/// اشخاص (طرف‌حساب) — نمای یکپارچهٔ مشتری + تأمین‌کننده در یک فهرست (سبکِ ERP ایرانی).
/// 🏛️ الگوی مرجعِ معماریِ API-only: کلاینتِ شبکه‌ای از API می‌خواند (ApiClient.GetPersons)،
/// دسکتاپِ آفلاین از لایهٔ Application (GetPersonsQuery) — هیچ ریپازیتوریِ مستقیمی در ViewModel نیست.
/// </summary>
public partial class PersonsListViewModel : BaseViewModel, SamaHesab.WPF.Services.ISupportsNew
{
    /// <summary>F2-GLOBAL: «جدید» → ساختِ مشتری/شخصِ جدید.</summary>
    public void RequestNew() => NewCustomer();

    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _roleFilter;   // 0=همه، 1=مشتری، 2=تأمین‌کننده
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalBalance;
    [ObservableProperty] private PersonListItem? _selectedPerson;

    private readonly List<PersonListItem> _all = new();
    public ObservableCollection<PersonListItem> Persons { get; } = new();

    public PersonsListViewModel(IMediator mediator, ApiClient api, IDialogService d, INavigationService n)
        : base(d, n) { _mediator = mediator; _api = api; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            _all.Clear();
            // 🏛️ مسیرِ داده: اگر کلاینتِ شبکه‌ای است (BaseUrl ست) → API؛ وگرنه دسکتاپِ آفلاین → Application.
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                foreach (var p in await _api.GetPersonsAsync())
                    _all.Add(new PersonListItem(p.Id, p.Code, p.Name, p.Mobile, p.Balance, p.Role, p.IsCustomer, p.IsSupplier, p.IsActive, p.IsEmployee, p.IsSalesperson));
            }
            else
            {
                foreach (var p in await _mediator.Send(new GetPersonsQuery()))
                    _all.Add(new PersonListItem(p.Id, p.Code, p.Name, p.Mobile, p.Balance, p.Role, p.IsCustomer, p.IsSupplier, p.IsActive, p.IsEmployee, p.IsSalesperson));
            }
            ApplyFilter();
        }, "در حال بارگذاری اشخاص...");
    }

    partial void OnRoleFilterChanged(int value) => ApplyFilter();

    [RelayCommand] private void SetRole(string? mode)
    { RoleFilter = mode switch { "customer" => 1, "supplier" => 2, "employee" => 3, "salesperson" => 4, _ => 0 }; }

    private void ApplyFilter()
    {
        var term = SearchText?.Trim() ?? string.Empty;
        IEnumerable<PersonListItem> q = _all;
        if (RoleFilter == 1) q = q.Where(p => p.IsCustomer);
        else if (RoleFilter == 2) q = q.Where(p => p.IsSupplier);
        else if (RoleFilter == 3) q = q.Where(p => p.IsEmployee);
        else if (RoleFilter == 4) q = q.Where(p => p.IsSalesperson);
        if (term.Length > 0)
            q = q.Where(p => p.Name.Contains(term) || p.Code.Contains(term) || p.Mobile.Contains(term));

        Persons.Clear();
        foreach (var p in q.OrderBy(p => p.Name)) Persons.Add(p);
        TotalCount = Persons.Count;
        TotalBalance = Persons.Sum(p => p.Balance);
    }

    [RelayCommand] private async Task SearchAsync() { ApplyFilter(); await Task.CompletedTask; }
    [RelayCommand] private void NewCustomer() => _navigationService.NavigateTo("CustomerEdit");
    // UX-CRM-SUPPLIER-1: پیش‌تر این‌جا فقط لیستِ تأمین‌کنندگان (که خودش راهِ ساخت نداشت) دوباره
    // باز می‌شد — یعنی این دکمه عملاً کار نمی‌کرد. حالا مستقیم فرمِ ساختِ تأمین‌کننده باز می‌شود.
    [RelayCommand] private void NewSupplier() => _navigationService.NavigateTo("CustomerEdit", "supplier");

    /// <summary>CORE-UX-NAV (PERSON-CARD): کلیک/دوبارکلیک روی نامِ شخص → کارت حساب (کارت ۳۶۰°).
    /// همهٔ اشخاص رکوردِ Customer هستند، پس کارت برای مشتری/تأمین‌کننده/کارمند یکسان باز می‌شود.</summary>
    [RelayCommand]
    private void OpenCard(PersonListItem? person)
    {
        var p = person ?? SelectedPerson;
        if (p is null || p.Id <= 0) return;
        _navigationService.NavigateTo("CustomerCard", p.Id);
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Persons.Count == 0) { await _dialogService.ShowWarningAsync("فهرستی برای خروجی نیست."); return; }
        try
        {
            string N(decimal d) => d.ToString("N0");
            var table = new ReportTable("لیست اشخاص",
                new[] { "کد", "نام", "نوع", "موبایل", "مانده", "وضعیت" },
                Persons.Select(p => new[] { p.Code, p.Name, p.Role, p.Mobile, N(p.Balance), p.IsActive ? "فعال" : "غیرفعال" }).ToList());
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"لیست_اشخاص_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            System.IO.File.WriteAllText(path, ReportExporter.ToCsv(table), new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}

public record PersonListItem(int Id, string Code, string Name, string Mobile, decimal Balance,
    string Role, bool IsCustomer, bool IsSupplier, bool IsActive, bool IsEmployee = false, bool IsSalesperson = false);
