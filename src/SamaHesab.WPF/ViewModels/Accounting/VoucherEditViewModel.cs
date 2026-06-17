using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Favorites;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

public partial class VoucherEditViewModel : BaseViewModel, SamaHesab.WPF.Services.INavigationAware
{
    private readonly IMediator _mediator;
    private readonly IAccountRepository _accountRepo;
    private readonly IVoucherRepository _voucherRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IBarcodeService _barcode;

    // Header
    [ObservableProperty] private string _voucherNumber = "--- خودکار ---";
    [ObservableProperty] private string _voucherDate = string.Empty;
    [ObservableProperty] private int _selectedVoucherTypeId = 9;
    [ObservableProperty] private int? _selectedCostCenterId;
    [ObservableProperty] private int? _selectedProjectId;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _reference;

    private int _fiscalYearId = 1;   // از سال مالیِ فعال در LoadAsync پر می‌شود

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
    public List<CostCenterItem> Projects { get; private set; } = new();

    /// <summary>چیپ‌های دسترسیِ سریع به حساب: پرکاربرد (سنجاق‌شده) + اخیر — کیبوردمحور/تک‌کلیک (Recent/Favorite Accounts).</summary>
    public ObservableCollection<AccountChip> QuickAccounts { get; } = new();

    // ماندهٔ هر حساب (از تراز آزمایشی) برای نمایش کنار چیپ‌های دسترسیِ سریع — OPT-1.
    private readonly Dictionary<int, decimal> _accountBalances = new();

    public VoucherEditViewModel(IMediator mediator, IAccountRepository accountRepo,
        IVoucherRepository voucherRepo, ICurrentUserService currentUser,
        IPersianCalendarService calendar, IBarcodeService barcode, IDialogService dialogService,
        INavigationService navigationService) : base(dialogService, navigationService)
    {
        _mediator = mediator; _accountRepo = accountRepo; _voucherRepo = voucherRepo;
        _currentUser = currentUser; _calendar = calendar; _barcode = barcode;
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

        // مراکز هزینه و پروژه‌های واقعی (هستهٔ ERP — جایگزین لیست هاردکد)
        var ccs = await _mediator.Send(new GetCostCentersQuery(ActiveOnly: true));
        CostCenters = ccs.Select(c => new CostCenterItem(c.Id, $"{c.Code} - {c.Name}")).ToList();
        OnPropertyChanged(nameof(CostCenters));

        var prs = await _mediator.Send(new GetProjectsQuery(ActiveOnly: true));
        Projects = prs.Select(p => new CostCenterItem(p.Id, $"{p.Code} - {p.Name}")).ToList();
        OnPropertyChanged(nameof(Projects));

        // سال مالیِ فعال (برای قفل دوره). اگر تعریف نشده باشد، ۱ می‌ماند.
        var years = await _mediator.Send(new GetFiscalYearsQuery());
        var active = years.FirstOrDefault(y => y.IsActive && !y.IsClosed) ?? years.FirstOrDefault(y => !y.IsClosed);
        if (active is not null) _fiscalYearId = active.Id;

        var accounts = await _accountRepo.GetLeafAccountsAsync(_currentUser.CompanyId ?? 1);
        LeafAccounts = accounts.Select(a => new VoucherAccountItem(a.Id, a.Code, a.Name)).ToList();
        OnPropertyChanged(nameof(LeafAccounts));

        // ماندهٔ حساب‌ها برای نمایش کنار چیپِ دسترسیِ سریع (OPT-1) — از تراز آزمایشی (Code→مانده).
        _accountBalances.Clear();
        try
        {
            var tb = await _mediator.Send(new GetTrialBalanceQuery("1400/01/01", "1410/12/29"));
            var balByCode = tb.GroupBy(r => r.Code).ToDictionary(g => g.Key, g => g.Sum(r => r.Balance));
            foreach (var a in accounts)
                if (balByCode.TryGetValue(a.Code, out var b)) _accountBalances[a.Id] = b;
        }
        catch { /* مانده اختیاری است */ }

        // U13 — type-ahead حساب با نمایشِ مانده در فهرستِ پیشنهاد (DESIGN LAW ۲-ب).
        foreach (var it in LeafAccounts)
            if (_accountBalances.TryGetValue(it.Id, out var bal)) it.Balance = bal;
        OnPropertyChanged(nameof(LeafAccounts));

        await LoadQuickAccountsAsync();
        await LoadPrintTemplatesAsync();
    }

    /// <summary>بارگذاریِ حساب‌های پرکاربرد (سنجاق‌شده) + اخیر برای نوارِ دسترسیِ سریع.
    /// قابلیتِ اختیاری است؛ اگر جدولِ Favorites نبود/خطا داد، صفحهٔ سند نباید کرش کند.</summary>
    private async Task LoadQuickAccountsAsync()
    {
        QuickAccounts.Clear();
        try
        {
            var pinned = await _mediator.Send(new GetPinnedItemsQuery("account"));
            var recent = await _mediator.Send(new GetRecentItemsQuery("account", 8));
            var seen = new HashSet<int>();
            foreach (var p in pinned)
                if (seen.Add(p.EntityId)) QuickAccounts.Add(new AccountChip(p.EntityId, p.Label, true) { Balance = Bal(p.EntityId) });
            foreach (var r in recent)
                if (seen.Add(r.EntityId)) QuickAccounts.Add(new AccountChip(r.EntityId, r.Label, false) { Balance = Bal(r.EntityId) });
        }
        catch { /* دسترسیِ سریع اختیاری است — نبودِ داده/جدول نباید فرمِ سند را از کار بیندازد */ }
    }

    private decimal Bal(int accountId) => _accountBalances.TryGetValue(accountId, out var b) ? b : 0m;

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

        // ثبتِ حساب در «اخیر» (برای دسترسیِ سریعِ دفعهٔ بعد) — بدون انتظار.
        _ = TouchAccountAsync(row.AccountId, $"{row.AccountCode} — {row.AccountName}");

        // Reset input
        NewRowNumber++;
        NewAccountId = null; NewDescription = null; NewDebit = 0; NewCredit = 0;

        // T10 — ردیفِ ورودِ سریع: پس از افزودن، View فوکوس را به فیلدِ حساب برمی‌گرداند.
        RowAdded?.Invoke();
    }

    /// <summary>پس از افزودنِ موفقِ یک ردیف رخ می‌دهد — View فوکوس را به نوارِ ورود برمی‌گرداند.</summary>
    public event Action? RowAdded;

    private async Task TouchAccountAsync(int accountId, string label)
    {
        try
        {
            await _mediator.Send(new TouchRecentItemCommand("account", accountId, label));
            await LoadQuickAccountsAsync();
        }
        catch { /* ثبتِ اخیر حیاتی نیست */ }
    }

    /// <summary>انتخابِ حساب از چیپِ دسترسیِ سریع → پر کردنِ ردیفِ ورودی (تک‌کلیک، بدونِ جست‌وجو).</summary>
    [RelayCommand]
    private void PickAccount(AccountChip? chip)
    {
        if (chip is null) return;
        NewAccountId = chip.AccountId;
    }

    /// <summary>سنجاق/برداشتنِ سنجاقِ حساب (پرکاربرد/Favorite).</summary>
    [RelayCommand]
    private async Task TogglePinAccount(AccountChip? chip)
    {
        if (chip is null) return;
        await _mediator.Send(new SetPinnedItemCommand("account", chip.AccountId, chip.Label, !chip.Pinned));
        await LoadQuickAccountsAsync();
    }

    /// <summary>
    /// توازن خودکار: سمت خالیِ ردیف جدید را با مابه‌التفاوت بدهکار/بستانکار پر می‌کند
    /// تا با افزودن آن، سند تراز شود. کلید میان‌بر: «=».
    /// </summary>
    [RelayCommand]
    private void FillBalance()
    {
        var (debit, credit) = VoucherBalance.BalancingEntry(TotalDebit, TotalCredit);
        if (debit == 0 && credit == 0) return; // سند از قبل تراز است
        NewDebit = debit;
        NewCredit = credit;
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
        // مبلغِ سند به حروف (کلاسیکِ اسنادِ ایرانی) — مبنای مبلغِ سند = جمعِ بدهکار.
        var amount = TotalDebit > 0 ? TotalDebit : TotalCredit;
        try { TotalInWords = amount > 0 ? "مبلغِ سند به حروف: " + _calendar.NumberToWords(amount) + " ریال" : ""; }
        catch { TotalInWords = ""; }
    }

    /// <summary>مبلغِ سند به حروف (نمایشِ زنده، به‌خواستِ کاربر — امکانِ ERPِ ایرانی).</summary>
    [ObservableProperty] private string _totalInWords = string.Empty;

    /// <summary>Open an existing voucher for viewing (passed from the voucher list).</summary>
    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is int id && id > 0)
            await LoadVoucherAsync(id);
    }

    private async Task LoadVoucherAsync(int id)
    {
        await ExecuteAsync(async () =>
        {
            var v = await _voucherRepo.GetWithItemsAsync(id);
            if (v == null) { await _dialogService.ShowErrorAsync("سند یافت نشد."); return; }

            _editingId = v.Id;
            VoucherNumber = v.VoucherNumber;
            VoucherDate = v.VoucherDate;
            SelectedVoucherTypeId = v.VoucherTypeId;
            Description = v.Description;

            Items.Clear();
            foreach (var it in v.Items.OrderBy(i => i.RowNumber))
            {
                var acc = LeafAccounts.FirstOrDefault(a => a.Id == it.AccountId);
                var row = new VoucherItemRow
                {
                    RowNumber = it.RowNumber,
                    AccountId = it.AccountId,
                    AccountCode = acc?.Code ?? "",
                    AccountName = acc?.Name ?? "",
                    Description = it.Description,
                    Debit = it.Debit,
                    Credit = it.Credit
                };
                row.PropertyChanged += (_, _) => Recalculate();
                Items.Add(row);
            }
            NewRowNumber = Items.Count + 1;
            Recalculate();
        }, "در حال بارگذاری سند...");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (_editingId > 0)
        {
            await _dialogService.ShowInfoAsync("این سند قبلاً ذخیره شده است. برای اصلاح، آن را «برگشت» بزنید یا سند جدید ثبت کنید.");
            return;
        }
        if (!Items.Any()) { await _dialogService.ShowErrorAsync("سند باید حداقل یک ردیف داشته باشد."); return; }
        await ExecuteAsync(async () =>
        {
            var cmd = new CreateVoucherCommand(
                BranchId: _currentUser.BranchId ?? 1,
                FiscalYearId: _fiscalYearId,
                VoucherDate: VoucherDate,
                VoucherTypeId: SelectedVoucherTypeId,
                Description: Description,
                Reference: Reference,
                CurrencyId: null,
                ExchangeRate: 1,
                // بُعدِ سرتیترِ سند (مرکز هزینه/پروژه) روی همهٔ ردیف‌ها اعمال می‌شود
                Items: Items.Select(r => new VoucherItemDto(
                    r.RowNumber, r.AccountId, r.Debit, r.Credit, r.Description,
                    SelectedCostCenterId, SelectedProjectId)).ToList()
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
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new ReverseVoucherCommand(
                _editingId, _calendar.GetCurrentPersianDate(), $"سند معکوسِ سند {VoucherNumber}"));
            if (res.Succeeded) await _dialogService.ShowSuccessAsync($"سند معکوس با موفقیت ثبت شد (شناسه {res.Value}).");
            else await _dialogService.ShowErrorAsync(res.ErrorMessage);
        }, "در حال صدور سند معکوس...");
    }

    /// <summary>چاپِ سند: تولیدِ خروجیِ HTMLِ راست‌چینِ قابل‌چاپ و بازکردنِ آن (از موتورِ `ReportExporter`).</summary>
    [RelayCommand]
    private async Task PrintAsync()
    {
        if (!Items.Any()) { await _dialogService.ShowErrorAsync("سندی برای چاپ وجود ندارد."); return; }
        try
        {
            var headers = new[] { "ردیف", "کد حساب", "نام حساب", "شرح", "بدهکار", "بستانکار" };
            var rows = Items.Select(r => new[]
            {
                r.RowNumber.ToString(), r.AccountCode, r.AccountName, r.Description ?? "",
                r.Debit.ToString("#,##0"), r.Credit.ToString("#,##0")
            }).ToList();
            rows.Add(new[] { "", "", "", "جمع", TotalDebit.ToString("#,##0"), TotalCredit.ToString("#,##0") });

            var title = $"سند حسابداری {VoucherNumber} — تاریخ {VoucherDate}"
                        + (string.IsNullOrWhiteSpace(Description) ? "" : $" — {Description}");
            var html = ReportExporter.ToHtml(new ReportTable(title, headers, rows));

            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "اسناد");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"سند_{VoucherNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            System.IO.File.WriteAllText(path, html, new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });

            await _dialogService.ShowSuccessAsync($"سند برای چاپ آماده شد:\n{path}");
        }
        catch (Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    /// <summary>P1/DT-6 — قالب‌های چاپِ سندِ حسابداری (دکمهٔ «چاپ ▼»، قالب‌های نوعِ Voucherِ C2).</summary>
    public ObservableCollection<Application.Documents.DocumentTemplateListDto> PrintTemplates { get; } = new();

    private async Task LoadPrintTemplatesAsync()
    {
        PrintTemplates.Clear();
        try { foreach (var t in await _mediator.Send(new Application.Documents.GetDocumentTemplatesQuery("Voucher"))) PrintTemplates.Add(t); }
        catch { /* قالب‌ها اختیاری‌اند؛ نبودشان نباید سند را خراب کند */ }
    }

    /// <summary>P1/DT-6 — چاپِ سند با قالبِ پویای انتخاب‌شده (همان موتورِ DocumentTemplateEngine).</summary>
    [RelayCommand]
    private async Task PrintWithTemplateAsync(Application.Documents.DocumentTemplateListDto? tpl)
    {
        if (tpl is null) return;
        if (!Items.Any()) { await _dialogService.ShowErrorAsync("سندی برای چاپ وجود ندارد."); return; }
        try
        {
            var full = await _mediator.Send(new Application.Documents.GetDocumentTemplateQuery(tpl.Id));
            if (full is null) { await _dialogService.ShowErrorAsync("قالب یافت نشد."); return; }

            var g = Services.AppSettingsStore.GetGeneral();
            string N(decimal d) => d.ToString("#,##0");
            var typeName = VoucherTypes.FirstOrDefault(t => t.Id == SelectedVoucherTypeId)?.Name ?? "";
            var fields = new Dictionary<string, string?>
            {
                ["VoucherNumber"] = VoucherNumber, ["DocNumber"] = VoucherNumber, ["QrData"] = VoucherNumber,
                ["QrImage"] = _barcode.QrImageHtml(VoucherNumber),
                ["VoucherDate"] = VoucherDate, ["Date"] = VoucherDate, ["VoucherType"] = typeName,
                ["Reference"] = Reference, ["Description"] = Description,
                ["CompanyName"] = g.CompanyName, ["CompanyAddress"] = g.CompanyAddress, ["CompanyPhone"] = g.CompanyPhone,
                ["EconomicCode"] = g.CompanyEconomicCode, ["NationalId"] = g.CompanyNationalId, ["BranchName"] = "",
                ["TotalDebit"] = N(TotalDebit), ["TotalCredit"] = N(TotalCredit),
                ["PrintDate"] = _calendar.GetCurrentPersianDate(), ["PrintTime"] = DateTime.Now.ToString("HH:mm"),
            };
            var rows = Items.Select(r => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>
            {
                ["AccountCode"] = r.AccountCode, ["AccountName"] = r.AccountName, ["DetailName"] = r.AccountName,
                ["LineDescription"] = r.Description, ["Description"] = r.Description,
                ["Debit"] = N(r.Debit), ["Credit"] = N(r.Credit),
            }).ToList();

            var data = Application.Documents.DocumentData.Of(fields, rows);
            var html = Application.Documents.DocumentTemplateEngine.Render(full.HeaderHtml, data)
                     + Application.Documents.DocumentTemplateEngine.Render(full.BodyHtml, data)
                     + Application.Documents.DocumentTemplateEngine.Render(full.FooterHtml, data);

            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "اسناد");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"سند_{VoucherNumber}_{tpl.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            System.IO.File.WriteAllText(path, html, new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private void Cancel() => _navigationService.NavigateTo("Vouchers");

    /// <summary>دسترسیِ مستقیم به الگوها/اسنادِ تکرارشونده (بهره‌وری سند) — OPT-1.</summary>
    [RelayCommand]
    private void Templates() => _navigationService.NavigateTo("VoucherTools");

    [RelayCommand]
    private void New()
    {
        _editingId = 0; VoucherNumber = "--- خودکار ---";
        VoucherDate = _calendar.GetCurrentPersianDate();
        Description = null; Reference = null; Items.Clear(); NewRowNumber = 1; Recalculate();
    }

    /// <summary>کپی سند: ردیف‌های فعلی را در یک سند پیش‌نویس تازه نگه می‌دارد (شماره/تاریخ جدید).</summary>
    [RelayCommand]
    private void Copy()
    {
        _editingId = 0;
        VoucherNumber = "--- خودکار ---";
        VoucherDate = _calendar.GetCurrentPersianDate();
        // ردیف‌ها حفظ می‌شوند تا فقط مبالغ/شرح ویرایش شود
        Recalculate();
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
public record VoucherAccountItem(int Id, string Code, string Name)
{
    /// <summary>متنِ جست‌وجوی هوشمند: شامل کد و نام تا تایپِ هرکدام (contains) فیلتر کند.</summary>
    public string Display => $"{Code} — {Name}";
    /// <summary>ماندهٔ جاریِ حساب (U13) — برای نمایش کنارِ پیشنهادِ type-ahead.</summary>
    public decimal Balance { get; set; }
}
public record CostCenterItem(int Id, string Name);

/// <summary>چیپِ دسترسیِ سریع به حساب (اخیر/پرکاربرد) — با ماندهٔ جاری برای دیدِ سریع.</summary>
public record AccountChip(int AccountId, string Label, bool Pinned)
{
    public decimal Balance { get; init; }
}

