using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

public partial class BankAccountViewModel : BaseViewModel
{
    private readonly IRepository<BankAccount> _bankRepo;
    private readonly ICurrentUserService _currentUser;

    [ObservableProperty] private int _totalCount;

    public ObservableCollection<BankAccountRow> Accounts { get; } = new();

    public BankAccountViewModel(IRepository<BankAccount> bankRepo, ICurrentUserService currentUser,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _bankRepo = bankRepo; _currentUser = currentUser; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var companyId = _currentUser.CompanyId ?? 1;
            var list = await _bankRepo.FindAsync(b => b.CompanyId == companyId);
            Accounts.Clear();
            foreach (var b in list)
                Accounts.Add(new BankAccountRow(b.Id, b.BankName, b.AccountNumber,
                    b.ShebaNumber ?? "", b.CardNumber ?? "", b.BranchName ?? "", b.OpeningBalance, b.IsActive));
            TotalCount = Accounts.Count;
        }, "در حال بارگذاری حساب‌های بانکی...");
    }

    [RelayCommand] private async Task RefreshAsync() => await LoadAsync();
}

public record BankAccountRow(int Id, string BankName, string AccountNumber, string Sheba,
    string CardNumber, string BranchName, decimal OpeningBalance, bool IsActive);
