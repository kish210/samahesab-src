using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.CRM;

public partial class SupplierListViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<Supplier> _supplierRepo;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalBalance;

    public ObservableCollection<SupplierListItem> Suppliers { get; } = new();

    public SupplierListViewModel(ICurrentUserService currentUser, IRepository<Supplier> supplierRepo,
        IDialogService d, INavigationService n) : base(d, n)
    { _currentUser = currentUser; _supplierRepo = supplierRepo; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var companyId = _currentUser.CompanyId ?? 1;
            var all = await _supplierRepo.FindAsync(s => s.CompanyId == companyId);
            var term = SearchText?.Trim() ?? string.Empty;
            Suppliers.Clear();
            foreach (var s in all)
            {
                if (term.Length > 0 &&
                    !((s.FullName?.Contains(term) ?? false) ||
                      (s.Code?.Contains(term) ?? false) ||
                      (s.Mobile?.Contains(term) ?? false)))
                    continue;
                Suppliers.Add(new SupplierListItem(s.Id, s.Code, s.FullName, s.Mobile ?? "",
                    s.City ?? "", s.Balance, s.IsActive));
            }
            TotalCount = Suppliers.Count;
            TotalBalance = Suppliers.Sum(x => x.Balance);
        }, "در حال بارگذاری تأمین‌کنندگان...");
    }

    [RelayCommand] private async Task SearchAsync() => await LoadAsync();
    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
    [RelayCommand] private async Task ExportAsync() => await _dialogService.ShowInfoAsync("خروجی اکسل...");
}

public record SupplierListItem(int Id, string Code, string Name, string Mobile, string City, decimal Balance, bool IsActive);
