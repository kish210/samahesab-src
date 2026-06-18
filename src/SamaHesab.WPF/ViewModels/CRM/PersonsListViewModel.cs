using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.CRM;

/// <summary>
/// اشخاص (طرف‌حساب) — نمای یکپارچهٔ مشتری + تأمین‌کننده در یک فهرست (سبکِ ERP ایرانی).
/// مرحلهٔ ۱ از ادغامِ طرف‌حساب: داده‌های واردشده (هر دو نقش) همین‌جا دیده می‌شوند.
/// </summary>
public partial class PersonsListViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<Customer> _customerRepo;
    private readonly IRepository<Supplier> _supplierRepo;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _roleFilter;   // 0=همه، 1=مشتری، 2=تأمین‌کننده
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalBalance;

    private readonly List<PersonListItem> _all = new();
    public ObservableCollection<PersonListItem> Persons { get; } = new();

    public PersonsListViewModel(ICurrentUserService currentUser,
        IRepository<Customer> customerRepo, IRepository<Supplier> supplierRepo,
        IDialogService d, INavigationService n)
        : base(d, n) { _currentUser = currentUser; _customerRepo = customerRepo; _supplierRepo = supplierRepo; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var companyId = _currentUser.CompanyId ?? 1;
            _all.Clear();

            foreach (var c in await _customerRepo.FindAsync(x => x.CompanyId == companyId))
                _all.Add(new PersonListItem(c.Id, c.Code ?? "", c.FullName ?? "", c.Mobile ?? "",
                    c.Balance, "مشتری", true, false, c.IsActive));

            foreach (var s in await _supplierRepo.FindAsync(x => x.CompanyId == companyId))
                _all.Add(new PersonListItem(s.Id, s.Code ?? "", s.FullName ?? "", s.Mobile ?? "",
                    s.Balance, "تأمین‌کننده", false, true, s.IsActive));

            ApplyFilter();
        }, "در حال بارگذاری اشخاص...");
    }

    partial void OnRoleFilterChanged(int value) => ApplyFilter();

    [RelayCommand] private void SetRole(string? mode)
    { RoleFilter = mode switch { "customer" => 1, "supplier" => 2, _ => 0 }; }

    private void ApplyFilter()
    {
        var term = SearchText?.Trim() ?? string.Empty;
        IEnumerable<PersonListItem> q = _all;
        if (RoleFilter == 1) q = q.Where(p => p.IsCustomer);
        else if (RoleFilter == 2) q = q.Where(p => p.IsSupplier);
        if (term.Length > 0)
            q = q.Where(p => p.Name.Contains(term) || p.Code.Contains(term) || p.Mobile.Contains(term));

        Persons.Clear();
        foreach (var p in q.OrderBy(p => p.Name)) Persons.Add(p);
        TotalCount = Persons.Count;
        TotalBalance = Persons.Sum(p => p.Balance);
    }

    [RelayCommand] private async Task SearchAsync() { ApplyFilter(); await Task.CompletedTask; }
    [RelayCommand] private void NewCustomer() => _navigationService.NavigateTo("CustomerEdit");
    [RelayCommand] private void NewSupplier() => _navigationService.NavigateTo("Suppliers");

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
    string Role, bool IsCustomer, bool IsSupplier, bool IsActive);
