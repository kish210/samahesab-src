using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.CRM;

public partial class CustomerListViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<Customer> _customerRepo;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalBalance;

    public ObservableCollection<CustomerListItem> Customers { get; } = new();

    public CustomerListViewModel(ICurrentUserService currentUser, IRepository<Customer> customerRepo,
        IDialogService d, INavigationService n)
        : base(d, n) { _currentUser = currentUser; _customerRepo = customerRepo; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var companyId = _currentUser.CompanyId ?? 1;
            var all = await _customerRepo.FindAsync(c => c.CompanyId == companyId);
            var term = SearchText?.Trim() ?? string.Empty;
            Customers.Clear();
            foreach (var c in all)
            {
                if (term.Length > 0 &&
                    !((c.FullName?.Contains(term) ?? false) ||
                      (c.Code?.Contains(term) ?? false) ||
                      (c.Mobile?.Contains(term) ?? false)))
                    continue;
                Customers.Add(new CustomerListItem(c.Id, c.Code ?? "", c.FullName ?? "", c.Mobile ?? "",
                    c.Balance, c.PriceLevel, c.IsActive));
            }
            TotalCount = Customers.Count;
            TotalBalance = Customers.Sum(x => x.Balance);
        }, "در حال بارگذاری مشتریان...");
    }

    [RelayCommand] private async Task SearchAsync() => await LoadAsync();
    [RelayCommand] private void NewCustomer() => _navigationService.NavigateTo("CustomerEdit");
    [RelayCommand] private void EditCustomer() { }
    [RelayCommand] private async Task SendSmsAsync() => await _dialogService.ShowInfoAsync("ارسال پیامک...");
    [RelayCommand] private async Task ExportAsync() => await _dialogService.ShowInfoAsync("خروجی اکسل...");
}

public record CustomerListItem(int Id, string Code, string Name, string Mobile, decimal Balance, string PriceLevel, bool IsActive);
