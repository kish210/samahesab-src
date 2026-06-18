using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>دفترِ حساب‌ها — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class AccountListViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _totalAccounts;

    public ObservableCollection<AccountItem> Accounts { get; } = new();

    public AccountListViewModel(IMediator mediator, ApiClient api,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator;
        _api = api;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Accounts.Clear();
            var term = SearchText?.Trim();
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                foreach (var a in await _api.GetAccountsAsync())
                    if (string.IsNullOrEmpty(term) || a.Name.Contains(term) || a.Code.Contains(term))
                        Accounts.Add(new AccountItem(a.Id, a.Code, a.Name, a.Level, a.Nature, a.IsActive));
            }
            else
            {
                foreach (var a in await _mediator.Send(new GetAccountsQuery()))
                    if (string.IsNullOrEmpty(term) || a.Name.Contains(term) || a.Code.Contains(term))
                        Accounts.Add(new AccountItem(a.Id, a.Code, a.Name, a.Level, a.Nature, a.IsActive));
            }
            TotalAccounts = Accounts.Count;
        }, "در حال بارگذاری حساب‌ها...");
    }

    [RelayCommand] private async Task SearchAsync() => await LoadAsync();
    [RelayCommand] private void NewAccount() => _navigationService.NavigateTo("AccountEdit");
    [RelayCommand] private async Task DeleteAsync() => await _dialogService.ShowInfoAsync("برای حذف حساب، ابتدا تراکنش‌های آن را بررسی کنید.");
}

public record AccountItem(int Id, string Code, string Name, int Level, string Nature, bool IsActive);
