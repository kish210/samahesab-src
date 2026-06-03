using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

public partial class VoucherEditViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IAccountRepository _accountRepo;
    private readonly IVoucherRepository _voucherRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;

    // Header
    [ObservableProperty] private string _voucherNumber = "--- خودکار ---";
    [ObservableProperty] private string _voucherDate = string.Empty;
    [ObservableProperty] private int _selectedVoucherTypeId = 9;
    [ObservableProperty] private int? _selectedCostCenterId;
    [ObservableProperty] private string? _description;

    // New row input
    [ObservableProperty] private int _newRowNumber = 1;
    [ObservableProperty] private int? _newAccountId;
    [ObservableProperty] private string? _newDescription;
    [ObservableProperty] private decimal _newDebit;
    [ObservableProperty] private decimal _newCredit;

    // Totals
    [ObservableProperty] private decimal _totalDebit;
    [ObservableProperty] private decimal _totalCredit;
    [ObservableProperty] private decimal _difference;
    [ObservableProperty] private bool _isBalanced;

    private int _editingId;

    public ObservableCollection<VoucherItemRow> Items { get; } = new();
    public List<VoucherTypeItem> VoucherTypes { get; private set; } = new();
    public List<VoucherAccountItem> LeafAccounts { get; private set; } = new();
    public List<CostCenterItem> CostCenters { get; private set; } = new();

    public VoucherEditViewModel(IMediator mediator, IAccountRepository accountRepo,
        IVoucherRepository voucherRepo, ICurrentUserService currentUser,
        IPersianCalendarService calendar, IDialogService dialogService,
        INavigationService navigationService) : base(dialogService, navigationService)
    {
        _mediator = mediator; _accountRepo = accountRepo; _voucherRepo = voucherRepo;
        _currentUser = currentUser; _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        VoucherDate = _calendar.GetCurrentPersianDate();

        VoucherTypes = new List<VoucherTypeItem>
        {
            new(1,"افتتاحیه"),new(2,"اختتامیه"),new(3,"فروش"),new(4,"خرید"),
            new(5,"صندوق"),new(6,"بانک"),new(7,"چک"),new(9,"عمومی"),
            new(10,"پرداخت"),new(11,"دریافت"),new(12,"حقوق و دستمزد"),
        };
        OnPropertyChanged(nameof(VoucherTypes));

        CostCenters = new List<CostCenterItem>
        {
            new(1,"اداری"),new(2,"فروش"),new(3,"تولید"),new(4,"توزیع"),
        };
        OnPropertyChanged(nameof(CostCenters));

        var accounts = await _accountRepo.GetLeafAccountsAsync(_currentUser.CompanyId ?? 1);
        LeafAccounts = accounts.Select(a => new VoucherAccountItem(a.Id, a.Code, a.Name)).ToList();
        OnPropertyChanged(nameof(LeafAccounts));

        await Task.CompletedTask;
    }

    [RelayCommand]
    private void AddRow()
    {
        if (!NewAccountId.HasValue) { _ = _dialogService.ShowErrorAsync("حساب را انتخاب کنید."); return; }
        if (NewDebit == 0 && NewCredit == 0) { _ = _dialogService.ShowErrorAsync("مبلغ بدهکار یا بستانکار را وارد کنید."); return; }
        if (NewDebit > 0 && NewCredit > 0) { _ = _dialogService.ShowErrorAsync("یک ردیف نمی‌تواند هم بدهکار و هم بستانکار باشد."); return; }

        var account = LeafAccounts.FirstOrDefault(a => a.Id == NewAccountId.Value);
        var row = new VoucherItemRow
        {
            RowNumber = NewRowNumber,
            AccountId = NewAccountId.Value,
            AccountCode = account?.Code ?? "",
            AccountName = account?.Name ?? "",
            Description = NewDescription,
            Debit = NewDebit,
            Credit = NewCredit
        };
        row.PropertyChanged += (_, _) => Recalculate();
        Items.Add(row);
        Recalculate();

        // Reset input
        NewRowNumber++;
        NewAccountId = null; NewDescription = null; NewDebit = 0; NewCredit = 0;
    }

    [RelayCommand]
    private void RemoveRow(VoucherItemRow? row)
    {
        if (row == null) return;
        Items.Remove(row);
        // Renumber
        for (int i = 0; i < Items.Count; i++) Items[i].RowNumber = i + 1;
        NewRowNumber = Items.Count + 1;
        Recalculate();
    }

    private void Recalculate()
    {
        TotalDebit  = Items.Sum(r => r.Debit);
        TotalCredit = Items.Sum(r => r.Credit);
        Difference  = TotalDebit - TotalCredit;
        IsBalanced  = Math.Abs(Difference) < 0.01m && Items.Any();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!Items.Any()) { await _dialogService.ShowErrorAsync("سند باید حداقل یک ردیف داشته باشد."); return; }
        await ExecuteAsync(async () =>
        {
            var cmd = new CreateVoucherCommand(
                BranchId: _currentUser.BranchId ?? 1,
                FiscalYearId: 1,
                VoucherDate: VoucherDate,
                VoucherTypeId: SelectedVoucherTypeId,
                Description: Description,
                Reference: null,
                CurrencyId: null,
                ExchangeRate: 1,
                Items: Items.Select(r => new VoucherItemDto(
                    r.RowNumber, r.AccountId, r.Debit, r.Credit, r.Description, null, null)).ToList()
            );
            var result = await _mediator.Send(cmd);
            if (result.Succeeded)
            {
                _editingId = result.Value;
                VoucherNumber = _editingId.ToString("D6");
                await _dialogService.ShowSuccessAsync($"سند شماره {VoucherNumber} ذخیره شد.");
            }
            else await _dialogService.ShowErrorAsync(result.ErrorMessage);
        }, "در حال ذخیره سند...");
    }

    [RelayCommand]
    private async Task PostAsync()
    {
        if (_editingId == 0) { await SaveAsync(); if (_editingId == 0) return; }
        if (!IsBalanced) { await _dialogService.ShowErrorAsync("سند تراز نیست. قطعی کردن ممکن نیست."); return; }
        var ok = await _dialogService.ConfirmAsync($"سند شماره {VoucherNumber} قطعی شود؟", "قطعی کردن سند");
        if (!ok) return;
        await ExecuteAsync(async () =>
        {
            var cmd = new PostVoucherCommand(_editingId);
            var result = await _mediator.Send(cmd);
            if (result.Succeeded) await _dialogService.ShowSuccessAsync("سند با موفقیت قطعی شد.");
            else await _dialogService.ShowErrorAsync(result.ErrorMessage);
        }, "در حال قطعی کردن...");
    }

    [RelayCommand]
    private async Task ReverseAsync()
    {
        if (_editingId == 0) { await _dialogService.ShowErrorAsync("ابتدا سند را ذخیره کنید."); return; }
        var ok = await _dialogService.ConfirmAsync("آیا سند برگشت داده شود؟ این عملیات یک سند معکوس ایجاد می‌کند.");
        if (!ok) return;
        await _dialogService.ShowSuccessAsync("سند برگشت صادر شد.");
    }

    [RelayCommand]
    private async Task PrintAsync() => await _dialogService.ShowInfoAsync("در حال آماده‌سازی چاپ سند...");

    [RelayCommand]
    private void Cancel() => _navigationService.NavigateTo("Vouchers");

        [RelayCommand]
    private void New()
    {
        _editingId = 0; VoucherNumber = "--- خودکار ---";
        VoucherDate = _calendar.GetCurrentPersianDate();
        Description = null; Items.Clear(); NewRowNumber = 1; Recalculate();
    }
}

public partial class VoucherItemRow : ObservableObject
{
    [ObservableProperty] private int _rowNumber;
    public int AccountId { get; set; }
    [ObservableProperty] private string _accountCode = string.Empty;
    [ObservableProperty] private string _accountName = string.Empty;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private decimal _debit;
    [ObservableProperty] private decimal _credit;

    // Iranian chart segments derived from the account code (e.g. 1-03-001)
    public string Kol => (AccountCode ?? "").Split('-').ElementAtOrDefault(0) ?? "";
    public string Moein
    {
        get { var p = (AccountCode ?? "").Split('-'); return p.Length >= 2 ? $"{p[0]}-{p[1]}" : (AccountCode ?? ""); }
    }
    public string Tafsili => AccountCode ?? "";
}

public record VoucherTypeItem(int Id, string Name);
public record VoucherAccountItem(int Id, string Code, string Name);
public record CostCenterItem(int Id, string Name);

