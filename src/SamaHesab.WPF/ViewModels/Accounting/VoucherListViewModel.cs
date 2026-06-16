using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
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

    // P-G10 — صفحه‌بندیِ سمتِ سرور (کوئری از قبل Skip/Take دارد؛ این‌جا فقط ناوبری اضافه می‌شود)
    private const int PageSizeConst = 50;
    [ObservableProperty] private int _pageNumber = 1;
    [ObservableProperty] private int _totalPages = 1;
    public bool CanPrevPage => PageNumber > 1;
    public bool CanNextPage => PageNumber < TotalPages;
    public string PageInfo => $"صفحهٔ {PageNumber} از {System.Math.Max(1, TotalPages)}";

    partial void OnPageNumberChanged(int value) => RaisePagingChanged();
    partial void OnTotalPagesChanged(int value) => RaisePagingChanged();
    private void RaisePagingChanged()
    {
        OnPropertyChanged(nameof(CanPrevPage));
        OnPropertyChanged(nameof(CanNextPage));
        OnPropertyChanged(nameof(PageInfo));
    }

    public ObservableCollection<VoucherListDto> Vouchers { get; } = new();

    [ObservableProperty] private VoucherListDto? _selectedVoucher;

    public List<StatusItem> StatusOptions { get; } = new()
    {
        new(null, "همه"),
        new(1, "پیش‌نویس"),
        new(2, "قطعی"),
        new(3, "دائمی")
    };

    // کمبوی «نوع سند» (مطابق accounting-docs.html)
    public List<StatusItem> TypeOptions { get; } = new()
    {
        new(null, "همه انواع"),
        new(9, "عمومی"),
        new(11, "دریافت"),
        new(10, "پرداخت"),
        new(3, "فروش"),
        new(4, "خرید"),
    };
    [ObservableProperty] private int? _selectedTypeId;

    partial void OnSelectedTypeIdChanged(int? value) => _ = SearchAsync();

    /// <summary>چیپ‌های وضعیت (همه/قطعی/پیش‌نویس) — null=همه.</summary>
    [RelayCommand]
    private async Task FilterStatusAsync(string? status)
    {
        SelectedStatus = status switch { "قطعی" => 2, "پیش‌نویس" => 1, _ => (int?)null };
        await SearchAsync();
    }

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

    /// <summary>اعمالِ فیلتر = بازگشت به صفحهٔ ۱ و بارگذاری.</summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        PageNumber = 1;
        await LoadPageAsync();
    }

    /// <summary>بارگذاریِ صفحهٔ جاری با فیلترهای فعلی (مشترکِ جستجو و ناوبریِ صفحه).</summary>
    private async Task LoadPageAsync()
    {
        await ExecuteAsync(async () =>
        {
            var query = new GetVouchersQuery(
                FiscalYearId: FiscalYearId,
                FromDate: FromDate,
                ToDate: ToDate,
                VoucherTypeId: SelectedTypeId,
                Status: SelectedStatus,
                SearchText: SearchText,
                PageNumber: PageNumber,
                PageSize: PageSizeConst);

            var result = await _mediator.Send(query);
            Vouchers.Clear();
            foreach (var v in result.Items)
                Vouchers.Add(v);

            TotalCount = result.TotalCount;
            TotalPages = System.Math.Max(1, (int)System.Math.Ceiling(TotalCount / (double)PageSizeConst));
            if (PageNumber > TotalPages) PageNumber = TotalPages;   // پس از تغییرِ فیلتر/حذف
            TotalDebit = Vouchers.Sum(v => v.TotalDebit);
            TotalCredit = Vouchers.Sum(v => v.TotalCredit);
            // master-detail: همیشه یک سند انتخاب باشد تا پنل پیش‌نمایش پر بماند
            SelectedVoucher = Vouchers.FirstOrDefault();
            RaisePagingChanged();
        }, "در حال جستجو...");
    }

    [RelayCommand] private Task FirstPageAsync() { if (PageNumber == 1) return Task.CompletedTask; PageNumber = 1; return LoadPageAsync(); }
    [RelayCommand] private Task PrevPageAsync()  { if (!CanPrevPage) return Task.CompletedTask; PageNumber--; return LoadPageAsync(); }
    [RelayCommand] private Task NextPageAsync()  { if (!CanNextPage) return Task.CompletedTask; PageNumber++; return LoadPageAsync(); }
    [RelayCommand] private Task LastPageAsync()  { if (PageNumber == TotalPages) return Task.CompletedTask; PageNumber = TotalPages; return LoadPageAsync(); }

    [RelayCommand]
    private void NewVoucher() => _navigationService.NavigateTo("VoucherEdit");

    [RelayCommand] private void Ledger() => _navigationService.NavigateTo("FinancialReports");
    [RelayCommand] private void TrialBalance() => _navigationService.NavigateTo("FinancialReports");

    /// <summary>T22 — ارسالِ سندِ انتخاب‌شده برای تأیید (وارد کردنِ آن به گردش‌کار).</summary>
    [RelayCommand]
    private async Task SubmitForApprovalAsync()
    {
        if (SelectedVoucher is null) { await _dialogService.ShowWarningAsync("یک سند را انتخاب کنید."); return; }
        if (!await _dialogService.ConfirmAsync($"ارسالِ سندِ شمارهٔ {SelectedVoucher.VoucherNumber} برای تأیید؟")) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SamaHesab.Application.Accounting.Commands.SubmitVoucherForApprovalCommand(SelectedVoucher.Id));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("سند برای تأیید ارسال شد (در «کارتابلِ تأیید» قابلِ پیگیری است).");
            await SearchAsync();
        }, "در حال ارسال برای تأیید...");
    }

    /// <summary>جدولِ گزارشِ لیستِ اسنادِ فیلترشده (مشترکِ خروجی اکسل/چاپ).</summary>
    private ReportTable BuildListTable()
    {
        var headers = new[] { "شماره", "تاریخ", "نوع", "وضعیت", "بدهکار", "بستانکار", "کاربر", "شرح" };
        var rows = Vouchers.Select(v => new[]
        {
            v.VoucherNumber, v.VoucherDate, v.VoucherTypeName, v.StatusName,
            v.TotalDebit.ToString("#,##0"), v.TotalCredit.ToString("#,##0"), v.UserName, v.Description ?? ""
        }).ToList();
        rows.Add(new[] { "", "", "", "جمع", TotalDebit.ToString("#,##0"), TotalCredit.ToString("#,##0"), "", "" });
        return new ReportTable($"فهرست اسناد حسابداری ({FromDate} تا {ToDate})", headers, rows);
    }

    private string SaveAndOpen(string ext, string content)
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"اسناد_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllText(path, content, new System.Text.UTF8Encoding(true));
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        return path;
    }

    /// <summary>خروجیِ اکسل (CSV) از فهرستِ اسنادِ فیلترشده.</summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Vouchers.Count == 0) { await _dialogService.ShowWarningAsync("سندی برای خروجی وجود ندارد."); return; }
        try { var p = SaveAndOpen("csv", ReportExporter.ToCsv(BuildListTable())); await _dialogService.ShowSuccessAsync($"خروجی اکسل ذخیره شد:\n{p}"); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

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

    /// <summary>چاپِ فهرستِ اسنادِ فیلترشده (HTML راست‌چینِ قابل‌چاپ).</summary>
    [RelayCommand]
    private async Task PrintVoucherAsync()
    {
        if (Vouchers.Count == 0) { await _dialogService.ShowWarningAsync("سندی برای چاپ وجود ندارد."); return; }
        try { var p = SaveAndOpen("html", ReportExporter.ToHtml(BuildListTable())); await _dialogService.ShowSuccessAsync($"فهرست برای چاپ آماده شد:\n{p}"); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}

public record StatusItem(int? Id, string Name);
public record VoucherPreviewLine(string Account, decimal Debit, decimal Credit);
