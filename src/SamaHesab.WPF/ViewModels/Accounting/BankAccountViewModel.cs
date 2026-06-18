using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>حساب‌های بانکی — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application. بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class BankAccountViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private int _totalCount;

    public ObservableCollection<BankAccountRow> Accounts { get; } = new();

    public BankAccountViewModel(IMediator mediator, ApiClient api,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _api = api; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Accounts.Clear();
            if (!string.IsNullOrWhiteSpace(_api.BaseUrl))
                foreach (var b in await _api.GetBankAccountsAsync())
                    Accounts.Add(new BankAccountRow(b.Id, b.BankName, b.AccountNumber, b.Sheba, b.CardNumber, b.BranchName, b.OpeningBalance, b.IsActive));
            else
                foreach (var b in await _mediator.Send(new GetBankAccountsQuery()))
                    Accounts.Add(new BankAccountRow(b.Id, b.BankName, b.AccountNumber, b.Sheba, b.CardNumber, b.BranchName, b.OpeningBalance, b.IsActive));
            TotalCount = Accounts.Count;
        }, "در حال بارگذاری حساب‌های بانکی...");
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
}

public record BankAccountRow(int Id, string BankName, string AccountNumber, string Sheba,
    string CardNumber, string BranchName, decimal OpeningBalance, bool IsActive);
