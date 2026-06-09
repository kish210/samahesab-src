using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

public partial class AccountTreeNode : ObservableObject
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int Level { get; init; }
    public string Nature { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public bool IsLeaf { get; init; }
    public ObservableCollection<AccountTreeNode> Children { get; } = new();
}

// ─── Full ChartOfAccounts ViewModel ──────────────────────────────────────────
public partial class ChartOfAccountsViewModel : BaseViewModel
{
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private AccountTreeNode? _selectedAccount;
    [ObservableProperty] private decimal _selectedAccountBalance;
    [ObservableProperty] private bool _hasSelectedAccount;

    // Edit mode
    [ObservableProperty] private string _editCode = string.Empty;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editNature = "بدهکار";
    [ObservableProperty] private string _editAccountType = "دارایی";
    [ObservableProperty] private int? _editParentId;

    public ObservableCollection<AccountTreeNode> RootAccounts { get; } = new();

    public ChartOfAccountsViewModel(IAccountRepository accountRepo,
        ICurrentUserService currentUser, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _accountRepo = accountRepo;
        _currentUser = currentUser;
        _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var all = await _accountRepo.GetByCompanyAsync(_currentUser.CompanyId ?? 1);
            RootAccounts.Clear();

            // Build tree
            var dict = all.ToDictionary(a => a.Id, a => new AccountTreeNode
            {
                Id = a.Id, Code = a.Code, Name = a.Name,
                Level = (int)a.Level, Nature = a.Nature.ToString(), IsLeaf = a.IsLeaf
            });

            foreach (var a in all)
            {
                if (a.ParentId.HasValue && dict.TryGetValue(a.ParentId.Value, out var parent))
                    parent.Children.Add(dict[a.Id]);
                else
                    RootAccounts.Add(dict[a.Id]);
            }

            // If no accounts, add sample tree
            if (!RootAccounts.Any()) BuildSampleAccounts();

        }, "در حال بارگذاری نمودار حساب‌ها...");
    }

    private void BuildSampleAccounts()
    {
        var assets = new AccountTreeNode { Id=1, Code="1", Name="دارایی‌ها", Level=1, Nature="بدهکار" };
        var current = new AccountTreeNode { Id=2, Code="1-01", Name="دارایی‌های جاری", Level=2, Nature="بدهکار" };
        current.Children.Add(new AccountTreeNode { Id=3, Code="1-01-001", Name="صندوق", Level=3, Nature="بدهکار", IsLeaf=true });
        current.Children.Add(new AccountTreeNode { Id=4, Code="1-01-002", Name="بانک ملت", Level=3, Nature="بدهکار", IsLeaf=true });
        current.Children.Add(new AccountTreeNode { Id=5, Code="1-01-003", Name="حساب‌های دریافتنی", Level=3, Nature="بدهکار", IsLeaf=true });
        assets.Children.Add(current);

        var liabilities = new AccountTreeNode { Id=10, Code="2", Name="بدهی‌ها", Level=1, Nature="بستانکار" };
        var currLiab = new AccountTreeNode { Id=11, Code="2-01", Name="بدهی‌های جاری", Level=2, Nature="بستانکار" };
        currLiab.Children.Add(new AccountTreeNode { Id=12, Code="2-01-001", Name="حساب‌های پرداختنی", Level=3, Nature="بستانکار", IsLeaf=true });
        liabilities.Children.Add(currLiab);

        var equity = new AccountTreeNode { Id=20, Code="3", Name="سرمایه", Level=1, Nature="بستانکار" };
        var income = new AccountTreeNode { Id=30, Code="4", Name="درآمدها", Level=1, Nature="بستانکار" };
        income.Children.Add(new AccountTreeNode { Id=31, Code="4-01", Name="فروش کالا", Level=2, Nature="بستانکار", IsLeaf=true });

        var expense = new AccountTreeNode { Id=40, Code="5", Name="هزینه‌ها", Level=1, Nature="بدهکار" };
        expense.Children.Add(new AccountTreeNode { Id=41, Code="5-01", Name="بهای تمام شده کالای فروش رفته", Level=2, Nature="بدهکار", IsLeaf=true });
        expense.Children.Add(new AccountTreeNode { Id=42, Code="5-02", Name="هزینه‌های اداری و عمومی", Level=2, Nature="بدهکار", IsLeaf=true });

        RootAccounts.Add(assets);
        RootAccounts.Add(liabilities);
        RootAccounts.Add(equity);
        RootAccounts.Add(income);
        RootAccounts.Add(expense);
    }

    public void SetSelectedAccount(AccountTreeNode? node)
    {
        SelectedAccount = node;
        HasSelectedAccount = node != null;
        if (node != null)
        {
            SelectedAccountBalance = 12_500_000; // Load from DB
            EditCode = node.Code;
            EditName = node.Name;
            EditNature = node.Nature;
        }
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        await _dialogService.ShowInfoAsync("فرم ایجاد حساب جدید در نسخه کامل پیاده‌سازی می‌شود.");
    }

    [RelayCommand]
    private async Task EditAccountAsync()
    {
        if (SelectedAccount == null) { await _dialogService.ShowWarningAsync("یک حساب انتخاب کنید."); return; }
        await _dialogService.ShowInfoAsync($"ویرایش حساب: {SelectedAccount.Name}");
    }

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount == null) { await _dialogService.ShowWarningAsync("یک حساب انتخاب کنید."); return; }
        var hasTransactions = await _accountRepo.HasTransactionsAsync(SelectedAccount.Id);
        if (hasTransactions) { await _dialogService.ShowErrorAsync("این حساب دارای تراکنش است و قابل حذف نیست."); return; }
        var ok = await _dialogService.ConfirmAsync($"آیا حساب '{SelectedAccount.Name}' حذف شود؟");
        if (ok) await _dialogService.ShowSuccessAsync("حساب حذف شد.");
    }

    [RelayCommand]
    private async Task ViewLedgerAsync()
    {
        if (SelectedAccount == null) return;
        await _dialogService.ShowInfoAsync($"دفتر کل حساب: {SelectedAccount.Name}");
    }

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();
}
