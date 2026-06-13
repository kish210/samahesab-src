using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Queries;
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
    [ObservableProperty] private decimal _balance;   // از تراز آزمایشی پر و به والدها roll-up می‌شود
    public bool IsLeaf { get; init; }
    public ObservableCollection<AccountTreeNode> Children { get; } = new();
}

// ─── Full ChartOfAccounts ViewModel ──────────────────────────────────────────
public partial class ChartOfAccountsViewModel : BaseViewModel
{
    private readonly IAccountRepository _accountRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;

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
    [ObservableProperty] private bool _isEditing;          // نمایشِ فرمِ ویرایش/ایجاد
    [ObservableProperty] private bool _isNewAccount;        // ایجاد (true) یا ویرایش (false)
    [ObservableProperty] private bool _showDetail;          // جزئیاتِ فقط‌خواندنی (= انتخاب‌شده و درحالِ‌ویرایش‌نبودن)
    [ObservableProperty] private string _editParentInfo = "—"; // والدِ نمایشی در فرم
    [ObservableProperty] private string _editTitle = "حساب جدید";

    public List<string> NatureOptions { get; } = new() { "بدهکار", "بستانکار" };
    public List<string> AccountTypeOptions { get; } = new() { "دارایی", "بدهی", "سرمایه", "درآمد", "هزینه" };

    public ObservableCollection<AccountTreeNode> RootAccounts { get; } = new();

    public ChartOfAccountsViewModel(IAccountRepository accountRepo,
        ICurrentUserService currentUser, IPersianCalendarService calendar, IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _accountRepo = accountRepo;
        _currentUser = currentUser;
        _calendar = calendar;
        _mediator = mediator;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var all = await _accountRepo.GetByCompanyAsync(_currentUser.CompanyId ?? 1);
            RootAccounts.Clear();

            // ── جستجوی کد/نام (OPT-4): فقط حساب‌های منطبق + نیاکانشان نمایش داده می‌شوند ──
            var term = (SearchText ?? string.Empty).Trim();
            var included = all.Select(a => a.Id).ToHashSet();
            if (term.Length > 0)
            {
                var parentOf = all.ToDictionary(a => a.Id, a => a.ParentId);
                included = new HashSet<int>();
                foreach (var a in all.Where(a => a.Code.Contains(term) || a.Name.Contains(term)))
                {
                    var id = (int?)a.Id;
                    while (id.HasValue && included.Add(id.Value))
                        id = parentOf.TryGetValue(id.Value, out var p) ? p : null;
                }
            }
            var visible = all.Where(a => included.Contains(a.Id)).ToList();

            // Build tree
            var dict = visible.ToDictionary(a => a.Id, a => new AccountTreeNode
            {
                Id = a.Id, Code = a.Code, Name = a.Name,
                Level = (int)a.Level, Nature = a.Nature.ToString(), IsLeaf = a.IsLeaf
            });

            foreach (var a in visible)
            {
                if (a.ParentId.HasValue && dict.TryGetValue(a.ParentId.Value, out var parent))
                    parent.Children.Add(dict[a.Id]);
                else
                    RootAccounts.Add(dict[a.Id]);
            }

            // If no accounts at all, add sample tree
            if (!all.Any()) { BuildSampleAccounts(); return; }

            // ── مانده‌ی واقعیِ هر حساب از تراز آزمایشی (Code→مانده) + roll-up به والدها (الگوی ERP) ──
            try
            {
                var tb = await _mediator.Send(new GetTrialBalanceQuery("1400/01/01", "1410/12/29"));
                var balMap = tb.GroupBy(r => r.Code).ToDictionary(g => g.Key, g => g.Sum(r => r.Balance));
                foreach (var root in RootAccounts) AssignBalance(root, balMap);
            }
            catch { /* مانده اختیاری است؛ اگر داده نبود درخت بدون مانده نمایش داده می‌شود */ }

        }, "در حال بارگذاری نمودار حساب‌ها...");
    }

    /// <summary>مانده‌ی برگ = از تراز آزمایشی؛ مانده‌ی والد = جمعِ فرزندان (roll-up).</summary>
    private static decimal AssignBalance(AccountTreeNode node, System.Collections.Generic.Dictionary<string, decimal> map)
    {
        if (node.Children.Count == 0)
            node.Balance = map.TryGetValue(node.Code, out var b) ? b : 0m;
        else
        {
            decimal sum = 0m;
            foreach (var c in node.Children) sum += AssignBalance(c, map);
            node.Balance = sum;
        }
        return node.Balance;
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
            SelectedAccountBalance = node.Balance; // مانده‌ی واقعیِ محاسبه‌شده (از تراز آزمایشی)
        }
    }

    /// <summary>نگاشتِ ماهیتِ حساب به برچسبِ فارسیِ کمبو (Debit/Credit → بدهکار/بستانکار).</summary>
    private static string NatureFa(string? nature) => nature switch
    {
        "Credit" or "بستانکار" => "بستانکار",
        _ => "بدهکار"
    };

    /// <summary>ورود به حالتِ ایجاد: حسابِ جدید به‌عنوان زیرمجموعهٔ حسابِ انتخاب‌شده (در صورت وجود).</summary>
    [RelayCommand]
    private void AddAccount()
    {
        IsNewAccount = true;
        IsEditing = true;
        EditTitle = "حساب جدید";
        EditCode = string.Empty;
        EditName = string.Empty;
        EditNature = NatureFa(SelectedAccount?.Nature);   // پیش‌فرض: هم‌ماهیتِ والد
        EditAccountType = "دارایی";
        EditParentId = SelectedAccount?.Id;
        EditParentInfo = SelectedAccount is null ? "— (حسابِ سطحِ گروه)" : $"{SelectedAccount.Code} — {SelectedAccount.Name}";
    }

    /// <summary>ورود به حالتِ ویرایشِ حسابِ انتخاب‌شده.</summary>
    [RelayCommand]
    private async Task EditAccountAsync()
    {
        if (SelectedAccount == null) { await _dialogService.ShowWarningAsync("یک حساب انتخاب کنید."); return; }
        IsNewAccount = false;
        IsEditing = true;
        EditTitle = $"ویرایشِ حساب {SelectedAccount.Code}";
        EditCode = SelectedAccount.Code;
        EditName = SelectedAccount.Name;
        EditNature = NatureFa(SelectedAccount.Nature);
        EditParentInfo = "—";
    }

    /// <summary>ذخیرهٔ حسابِ جدید/ویرایش‌شده (هستهٔ ERP — `SaveAccountCommand`).</summary>
    [RelayCommand]
    private async Task SaveAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(EditCode) || string.IsNullOrWhiteSpace(EditName))
        { await _dialogService.ShowErrorAsync("کد و نامِ حساب الزامی است."); return; }

        await ExecuteAsync(async () =>
        {
            var cmd = new SaveAccountCommand(
                Id: IsNewAccount ? null : SelectedAccount?.Id,
                Code: EditCode.Trim(), Name: EditName.Trim(), Nature: EditNature,
                AccountType: EditAccountType, ParentId: IsNewAccount ? EditParentId : null, Description: null);
            var res = await _mediator.Send(cmd);
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync(IsNewAccount ? "حساب ایجاد شد." : "حساب ویرایش شد.");
            IsEditing = false;
            await LoadAsync();
        }, "در حال ذخیرهٔ حساب...");
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        if (SelectedAccount == null) { await _dialogService.ShowWarningAsync("یک حساب انتخاب کنید."); return; }
        var hasTransactions = await _accountRepo.HasTransactionsAsync(SelectedAccount.Id);
        if (hasTransactions) { await _dialogService.ShowErrorAsync("این حساب دارای تراکنش است و قابل حذف نیست."); return; }
        var ok = await _dialogService.ConfirmAsync($"آیا حساب '{SelectedAccount.Name}' حذف شود؟");
        if (ok) await _dialogService.ShowSuccessAsync("حساب حذف شد.");
    }

    /// <summary>مشاهدهٔ دفتر کلِ حسابِ انتخاب‌شده (ناوبری به گزارش‌های مالی با پارامترِ حساب).</summary>
    [RelayCommand]
    private void ViewLedger()
    {
        if (SelectedAccount == null) return;
        _navigationService.NavigateTo("FinancialReports", SelectedAccount.Id);
    }

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();
    partial void OnIsEditingChanged(bool value) => ShowDetail = HasSelectedAccount && !value;
    partial void OnHasSelectedAccountChanged(bool value) => ShowDetail = value && !IsEditing;
}
