using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

public partial class VoucherListViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IVoucherRepository _voucherRepo;
    private readonly IAccountRepository _accountRepo;
    private Dictionary<int, string> _accountNames = new();

    // پنل پیش‌نمایش: ردیف‌های حساب سند انتخاب‌شده (طبق accounting-docs.html)
    public ObservableCollection<VoucherPreviewLine> PreviewLines { get; } = new();
    [ObservableProperty] private decimal _previewDebit;
    [ObservableProperty] private decimal _previewCredit;
    [ObservableProperty] private bool _previewBalanced;

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private int _fiscalYearId = 1;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int? _selectedStatus;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalDebit;
    [ObservableProperty] private decimal _totalCredit;

    public ObservableCollection<VoucherListDto> Vouchers { get; } = new();

    [ObservableProperty] private VoucherListDto? _selectedVoucher;

    public List<StatusItem> StatusOptions { get; } = new()
    {
        new(null, "همه"),
        new(1, "پیش‌نویس"),
        new(2, "قطعی"),
        new(3, "دائمی")
    };

    public VoucherListViewModel(
        IMediator mediator,
        ICurrentUserService currentUser,
        IDialogService dialogService,
        INavigationService navigationService,
        IPersianCalendarService calendar,
        IVoucherRepository voucherRepo,
        IAccountRepository accountRepo) : base(dialogService, navigationService)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _calendar = calendar;
        _voucherRepo = voucherRepo;
        _accountRepo = accountRepo;
    }

    /// <summary>با انتخاب سند، ردیف‌های حساب آن را برای پنل پیش‌نمایش بارگذاری می‌کند.</summary>
    partial void OnSelectedVoucherChanged(VoucherListDto? value)
    {
        if (value == null) { PreviewLines.Clear(); PreviewDebit = PreviewCredit = 0; PreviewBalanced = false; return; }
        _ = LoadPreviewAsync(value.Id);
    }

    private async Task LoadPreviewAsync(int voucherId)
    {
        try
        {
            if (_accountNames.Count == 0)
            {
                var accs = await _accountRepo.GetLeafAccountsAsync(_currentUser.CompanyId ?? 1);
                _accountNames = accs.ToDictionary(a => a.Id, a => a.Name);
            }
            var v = await _voucherRepo.GetWithItemsAsync(voucherId);
            PreviewLines.Clear();
            if (v == null) return;
            foreach (var it in v.Items.OrderBy(i => i.RowNumber))
                PreviewLines.Add(new VoucherPreviewLine(
                    _accountNames.TryGetValue(it.AccountId, out var n) ? n : $"حساب {it.AccountId}",
                    it.Debit, it.Credit));
            PreviewDebit = PreviewLines.Sum(l => l.Debit);
            PreviewCredit = PreviewLines.Sum(l => l.Credit);
            PreviewBalanced = Math.Abs(PreviewDebit - PreviewCredit) < 0.01m && PreviewLines.Count > 0;
        }
        catch { PreviewLines.Clear(); }
    }

    public override async Task LoadAsync()
    {
        var now = DateTime.Now;
        var persianCal = new System.Globalization.PersianCalendar();
        var year = persianCal.GetYear(now);
        var month = persianCal.GetMonth(now);
        FromDate = $"{year}/{month:D2}/01";
        ToDate = _calendar.GetCurrentPersianDate();
        await SearchAsync();
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await ExecuteAsync(async () =>
        {
            var query = new GetVouchersQuery(
                FiscalYearId: FiscalYearId,
                FromDate: FromDate,
                ToDate: ToDate,
                Status: SelectedStatus,
                SearchText: SearchText);

            var result = await _mediator.Send(query);
            Vouchers.Clear();
            foreach (var v in result.Items)
                Vouchers.Add(v);

            TotalCount = result.TotalCount;
            TotalDebit = Vouchers.Sum(v => v.TotalDebit);
            TotalCredit = Vouchers.Sum(v => v.TotalCredit);
            // master-detail: همیشه یک سند انتخاب باشد تا پنل پیش‌نمایش پر بماند
            SelectedVoucher = Vouchers.FirstOrDefault();
        }, "در حال جستجو...");
    }

    [RelayCommand]
    private void NewVoucher() => _navigationService.NavigateTo("VoucherEdit");

    [RelayCommand] private void Ledger() => _navigationService.NavigateTo("FinancialReports");
    [RelayCommand] private void TrialBalance() => _navigationService.NavigateTo("FinancialReports");
    [RelayCommand] private async Task ExportAsync() => await _dialogService.ShowInfoAsync("خروجی اکسلِ اسناد فیلترشده در حال آماده‌سازی…");

    [RelayCommand]
    private void EditVoucher()
    {
        if (SelectedVoucher == null) return;
        _navigationService.NavigateTo("VoucherEdit", SelectedVoucher.Id);
    }

    [RelayCommand]
    private async Task PostVoucherAsync()
    {
        if (SelectedVoucher == null) return;
        if (SelectedVoucher.StatusName != "پیش‌نویس")
        {
            await _dialogService.ShowErrorAsync("فقط اسناد پیش‌نویس قابل قطعی کردن هستند.");
            return;
        }

        var confirm = await _dialogService.ConfirmAsync("آیا سند را قطعی می‌کنید؟", "قطعی کردن سند");
        if (!confirm) return;

        await ExecuteAsync(async () =>
        {
            var result = await _mediator.Send(
                new Application.Accounting.Commands.PostVoucherCommand(SelectedVoucher.Id));
            if (result.Succeeded)
            {
                await _dialogService.ShowSuccessAsync("سند با موفقیت قطعی شد.");
                await SearchAsync();
            }
            else
                await _dialogService.ShowErrorAsync(result.ErrorMessage);
        });
    }

    [RelayCommand]
    private async Task PrintVoucherAsync()
    {
        if (SelectedVoucher == null) return;
        await _dialogService.ShowInfoAsync("در حال آماده‌سازی چاپ سند...");
    }
}

public record StatusItem(int? Id, string Name);
public record VoucherPreviewLine(string Account, decimal Debit, decimal Credit);
