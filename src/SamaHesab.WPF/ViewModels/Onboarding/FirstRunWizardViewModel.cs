using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Security.Commands;
using SamaHesab.Application.Settings.Commands;
using SamaHesab.Application.Inventory.Commands;
using SamaHesab.Application.CRM.Commands;
using SamaHesab.Domain.Enums;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Onboarding;

/// <summary>
/// فاز ۱۲ G3 — ویزاردِ راه‌اندازیِ اولیه (First-Run):
/// اطلاعاتِ شرکت/لوگو + سالِ مالی + اجبارِ تغییرِ رمزِ پیش‌فرضِ admin. یک‌بار در اولین اجرا.
/// از commandهای موجود استفاده می‌کند (SaveFiscalYear / ChangeUserPassword) + AppSettingsStore برای شرکت.
/// </summary>
public partial class FirstRunWizardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;
    private readonly IUnitLookup _units;
    private readonly ModuleService _modules;

    /// <summary>ماژول‌های اختیاریِ قابلِ انتخاب در راه‌اندازیِ اولیه (POS/رستوران/گردشگری/HR/…)</summary>
    public ObservableCollection<WizModule> Modules { get; } = new();

    // شرکت
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string? _companyPhone;
    [ObservableProperty] private string? _companyNationalId;
    [ObservableProperty] private string? _companyEconomicCode;
    [ObservableProperty] private string? _companyAddress;
    [ObservableProperty] private string? _logoPath;

    // سالِ مالی
    [ObservableProperty] private string _fiscalTitle = string.Empty;
    [ObservableProperty] private string _fiscalStart = string.Empty;
    [ObservableProperty] private string _fiscalEnd = string.Empty;

    // رمزِ ادمین
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;

    /// <summary>تیکِ «ورودِ داده‌های نمونه/دمو» — برای آشنایی/آزمایش (مشتری/کالا/فاکتورِ نمونه).</summary>
    [ObservableProperty] private bool _loadDemoData;

    // ── ویزاردِ مرحله‌به‌مرحله ──
    public const int StepCount = 5;
    [ObservableProperty] private int _step;   // ۰..۴
    public bool IsFirstStep => Step == 0;
    public bool IsLastStep => Step >= StepCount - 1;

    /// <summary>
    /// U-MULTI-COMPANY-1 — حالتِ «ساختِ شرکتِ جدید» (از دکمهٔ «افزودنِ شرکتِ جدید» در صفحهٔ
    /// ورود، نه راه‌اندازیِ اولیهٔ برنامه). گام‌هایِ ماژول‌ها (۱) و دادهٔ پایه (۳) بی‌معنا هستند
    /// (متعلق به سشنِ شرکتِ فعلی‌اند، نه شرکتِ تازه) — پس Next/Back آن‌ها را رد می‌کنند.
    /// </summary>
    [ObservableProperty] private bool _isNewCompanyMode;
    partial void OnIsNewCompanyModeChanged(bool value)
    {
        OnPropertyChanged(nameof(StepTitle));
        Step = 0;
    }

    /// <summary>پس از ساختِ موفقِ شرکتِ نو (فقط در IsNewCompanyMode) — برایِ انتخابِ خودکار در صفحهٔ ورود.</summary>
    public int? CreatedCompanyId { get; private set; }

    public string StepTitle => IsNewCompanyMode
        ? Step switch
        {
            0 => "گام ۱ از ۳ — اطلاعاتِ شرکتِ جدید",
            2 => "گام ۲ از ۳ — سالِ مالی",
            _ => "گام ۳ از ۳ — رمزِ مدیرِ شرکتِ جدید",
        }
        : Step switch
        {
            0 => "گام ۱ از ۵ — اطلاعاتِ شرکت و صنف",
            1 => "گام ۲ از ۵ — ماژول‌های موردِ استفاده",
            2 => "گام ۳ از ۵ — سالِ مالی",
            3 => "گام ۴ از ۵ — دادهٔ پایه و دموی متناسب",
            _ => "گام ۵ از ۵ — رمزِ مدیر و اتمام",
        };
    partial void OnStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(StepTitle));
    }

    [RelayCommand]
    private void Back()
    {
        if (Step <= 0) return;
        Step = IsNewCompanyMode && Step == 4 ? 2 : IsNewCompanyMode && Step == 2 ? 0 : Step - 1;
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        if (Step == 0 && (CompanyName?.Trim().Length ?? 0) < 2)
        { await _dialogService.ShowWarningAsync("نامِ معتبرِ شرکت را وارد کنید (دستِ‌کم ۲ نویسه)."); return; }
        if (Step >= StepCount - 1) return;
        Step = IsNewCompanyMode && Step == 0 ? 2 : IsNewCompanyMode && Step == 2 ? 4 : Step + 1;
    }

    // ── صنف/شغلِ شرکت + پیش‌پُرِ کالاهای نمونهٔ متناسب ──
    [ObservableProperty] private string? _businessType;
    public List<string> BusinessTypes { get; } = new()
    {
        "فروشگاه / سوپرمارکت", "رستوران / کافه / فست‌فود", "پوشاک و البسه", "خدمات / مشاوره",
        "پخش و بازرگانی", "تولیدی / کارگاه", "آرایشی و بهداشتی", "طلا و جواهر", "داروخانه",
        "لوازم خانگی / دیجیتال", "نمایشگاه خودرو", "سایر"
    };

    /// <summary>پیش‌پُر کردنِ کالاهای نمونه بر اساسِ صنفِ انتخاب‌شده (کاربر می‌تواند ویرایش/حذف کند).</summary>
    [RelayCommand]
    private async Task ApplyBusinessPresetAsync()
    {
        var samples = DemoPreset(BusinessType);
        if (samples.Count == 0)
        { await _dialogService.ShowInfoAsync("برای این صنف نمونهٔ آماده‌ای نیست؛ کالاها را دستی وارد کنید."); return; }
        Products.Clear();
        foreach (var (name, isService, sale) in samples)
            Products.Add(new WizProduct { Name = name, IsService = isService, SalePrice = sale });
    }

    private static List<(string Name, bool IsService, decimal Sale)> DemoPreset(string? type) => type switch
    {
        "رستوران / کافه / فست‌فود" => new() { ("چلوکباب کوبیده", false, 1850000), ("جوجه‌کباب", false, 1650000), ("نوشابه", false, 250000), ("چای", false, 150000), ("قهوه", false, 450000), ("سالاد فصل", false, 350000) },
        "فروشگاه / سوپرمارکت" => new() { ("برنج ایرانی (کیلو)", false, 1200000), ("روغن مایع", false, 850000), ("شکر (کیلو)", false, 380000), ("نوشابه خانواده", false, 320000), ("ماکارونی", false, 280000) },
        "پوشاک و البسه" => new() { ("پیراهن مردانه", false, 1850000), ("شلوار جین", false, 2400000), ("مانتو", false, 3200000), ("تی‌شرت", false, 950000) },
        "خدمات / مشاوره" => new() { ("مشاورهٔ ساعتی", true, 2500000), ("پشتیبانیِ ماهانه", true, 5000000), ("نصب و راه‌اندازی", true, 3500000) },
        "آرایشی و بهداشتی" => new() { ("شامپو", false, 480000), ("کرم مرطوب‌کننده", false, 650000), ("عطر", false, 2800000), ("لوازم آرایش", false, 1200000) },
        "داروخانه" => new() { ("استامینوفن", false, 85000), ("ویتامین C", false, 220000), ("ماسک", false, 35000), ("شربت سرماخوردگی", false, 180000) },
        "لوازم خانگی / دیجیتال" => new() { ("گوشی موبایل", false, 95000000), ("هندزفری", false, 1800000), ("شارژر", false, 850000), ("کابل USB", false, 250000) },
        "طلا و جواهر" => new() { ("انگشتر طلا (گرم)", false, 0), ("سرویس طلا", false, 0), ("سکه تمام", false, 0) },
        _ => new()
    };

    // ── مراحلِ دادهٔ پایه (اختیاری): انبارها / کالاها و خدمات / مشتری‌ها ──
    public ObservableCollection<WizWarehouse> Warehouses { get; } = new();
    public ObservableCollection<WizProduct> Products { get; } = new();
    public ObservableCollection<WizCustomer> Customers { get; } = new();

    [RelayCommand] private void AddWarehouse() => Warehouses.Add(new WizWarehouse());
    [RelayCommand] private void AddProduct() => Products.Add(new WizProduct());
    [RelayCommand] private void AddCustomer() => Customers.Add(new WizCustomer());
    [RelayCommand] private void RemoveWarehouse(WizWarehouse w) { if (w != null) Warehouses.Remove(w); }
    [RelayCommand] private void RemoveProduct(WizProduct p) { if (p != null) Products.Remove(p); }
    [RelayCommand] private void RemoveCustomer(WizCustomer c) { if (c != null) Customers.Remove(c); }

    /// <summary>پنجره با این رویداد خود را می‌بندد (اتمام یا «بعداً»).</summary>
    public event System.Action? Finished;

    public FirstRunWizardViewModel(IMediator mediator, ICurrentUserService user, IUnitLookup units,
        ModuleService modules,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _user = user; _units = units; _modules = modules;

        // ماژول‌های اختیاری — وضعیتِ فعلی پیش‌انتخاب می‌شود (پیش‌فرض: خاموش، مگر قبلاً روشن).
        foreach (var m in _modules.OptionalModules)
            Modules.Add(new WizModule(m.Key, m.Name) { IsSelected = _modules.IsEnabled(m.Key) });

        // پیش‌پُر از تنظیماتِ موجود + پیشنهادِ سالِ مالیِ جاری.
        var g = AppSettingsStore.GetGeneral();
        CompanyName = g.CompanyName ?? string.Empty;
        CompanyPhone = g.CompanyPhone; CompanyNationalId = g.CompanyNationalId;
        CompanyEconomicCode = g.CompanyEconomicCode; CompanyAddress = g.CompanyAddress;
        LogoPath = g.CompanyLogoPath;
        BusinessType = g.BusinessType;

        var today = calendar.GetCurrentPersianDate();                 // "1405/03/26"
        var year = today.Length >= 4 ? today[..4] : "1405";
        FiscalTitle = $"سالِ مالی {year}";
        FiscalStart = g.FiscalYearStart ?? $"{year}/01/01";
        FiscalEnd = g.FiscalYearEnd ?? $"{year}/12/29";

        // ردیف‌های اولیهٔ خالی برای ورودِ سریعِ دادهٔ پایه (انبارِ پیش‌فرض پیشنهاد می‌شود).
        Warehouses.Add(new WizWarehouse { Name = "انبار مرکزی" });
        Products.Add(new WizProduct()); Products.Add(new WizProduct());
        Customers.Add(new WizCustomer()); Customers.Add(new WizCustomer());
    }

    public override Task LoadAsync() => Task.CompletedTask;

    /// <summary>«بعداً» — بدونِ علامت‌گذاریِ تکمیل؛ ویزارد در اجرای بعدی دوباره می‌آید.</summary>
    [RelayCommand]
    private void Skip() => Finished?.Invoke();

    [RelayCommand]
    private async Task FinishAsync()
    {
        if ((CompanyName?.Trim().Length ?? 0) < 2)
        { await _dialogService.ShowWarningAsync("نامِ معتبرِ شرکت را وارد کنید (دستِ‌کم ۲ نویسه)."); return; }
        CompanyName = CompanyName!.Trim();

        if (IsNewCompanyMode) { await FinishNewCompanyAsync(); return; }

        // رمز: اختیاری ولی اگر وارد شد باید تأیید بخورد.
        var wantsPassword = !string.IsNullOrWhiteSpace(NewPassword);
        if (wantsPassword && NewPassword != ConfirmPassword)
        { await _dialogService.ShowWarningAsync("رمزِ عبور و تکرارِ آن یکسان نیستند."); return; }
        if (!wantsPassword &&
            !await _dialogService.ConfirmAsync(
                "رمزِ پیش‌فرضِ admin تغییر نکرده — ادامه می‌دهید؟ (توصیه: تغییر دهید)" + Environment.NewLine +
                "در این صورت برایِ ورود از «admin» / «admin123» استفاده کنید."))
            return;

        await ExecuteAsync(async () =>
        {
            // ۱) شرکت → تنظیماتِ محلی (merge با موجود)
            var g = AppSettingsStore.GetGeneral();
            g.CompanyName = CompanyName; g.CompanyPhone = CompanyPhone;
            g.CompanyNationalId = CompanyNationalId; g.CompanyEconomicCode = CompanyEconomicCode;
            g.CompanyAddress = CompanyAddress; g.CompanyLogoPath = LogoPath;
            g.BusinessType = BusinessType;
            g.FiscalYearStart = FiscalStart; g.FiscalYearEnd = FiscalEnd;

            // ۱.۵) U-MULTI-COMPANY-1 — شرکت → ردیفِ واقعیِ Cfg.Companies، نه فقط تنظیماتِ محلی.
            // پیش‌تر این‌جا اصلاً به‌روز نمی‌شد؛ صفحهٔ ورود همیشه نامِ seedِ اولیه («شرکت نمونه») را
            // نشان می‌داد، نه چیزی که کاربر همین‌جا وارد کرده بود.
            if (_user.CompanyId is int existingCompanyId)
                await _mediator.Send(new UpdateCompanyCommand(
                    existingCompanyId, CompanyName, CompanyNationalId, CompanyEconomicCode, CompanyPhone, CompanyAddress));

            // ۲) سالِ مالی → DB (command موجود)
            var fy = await _mediator.Send(new SaveFiscalYearCommand(0, FiscalTitle, FiscalStart, FiscalEnd));
            if (!fy.Succeeded)
            { await _dialogService.ShowErrorAsync(fy.ErrorMessage ?? "خطا در ثبتِ سالِ مالی."); return; }

            // ۳) رمزِ ادمین (در صورتِ ورود) + کدِ بازیابی (U-SEC-RECOVERY، درخواستِ کاربر)
            if (wantsPassword && _user.UserId is int uid)
            {
                var pr = await _mediator.Send(new ChangeUserPasswordCommand(uid, NewPassword));
                if (!pr.Succeeded)
                { await _dialogService.ShowErrorAsync(pr.ErrorMessage ?? "خطا در تغییرِ رمز."); return; }

                // اگر رمز عوض شد ولی بعداً فراموش شود، تنها راهِ بازیابی (بدونِ ایمیل/پیامک) همین
                // کد است — پس باید همین‌جا ساخته و به کاربر نشان داده شود، نه اختیاری/بعداً.
                var recoveryCode = Services.RecoveryCodeGenerator.Generate();
                var rc = await _mediator.Send(new SetRecoveryCodeCommand(uid, recoveryCode));
                if (rc.Succeeded)
                    new Views.Onboarding.RecoveryCodeWindow(recoveryCode) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
            }

            // ۴) دادهٔ پایه: انبارها / کالاها و خدمات / مشتری‌ها (فقط ردیف‌های پرشده)
            int whN = 0, prN = 0, cuN = 0;

            foreach (var w in Warehouses.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
                if ((await _mediator.Send(new CreateWarehouseCommand(w.Name!.Trim()))).Succeeded) whN++;

            int kseq = 1001;
            var defaultUnit = _units.DefaultUnitId() ?? 1;
            foreach (var p in Products.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                var code = string.IsNullOrWhiteSpace(p.Code) ? $"K{kseq++}" : p.Code!.Trim();
                var r = await _mediator.Send(new CreateProductCommand(
                    Code: code, Barcode: null, Name: p.Name!.Trim(), NameEn: null, GroupId: null, BrandId: null,
                    UnitId: defaultUnit, ProductType: p.IsService ? ProductType.Service : ProductType.Product,
                    PurchasePrice: p.PurchasePrice, SalePrice: p.SalePrice,
                    WholesalePrice: p.SalePrice, ConsumerPrice: p.SalePrice,
                    MinStock: 0, MaxStock: null, HasSerial: false, HasBatch: false, HasExpiry: false,
                    ValuationMethod: ValuationMethod.WeightedAverage, TaxRate: 0, Description: null));
                if (r.Succeeded) prN++;
            }

            int mseq = 1001;
            foreach (var c in Customers.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                var nm = c.Name!.Trim();
                var r = await _mediator.Send(new CreateCustomerCommand(
                    Code: $"M{mseq++}", CustomerType: c.IsCompany ? "حقوقی" : "حقیقی",
                    FirstName: c.IsCompany ? null : nm, LastName: null, CompanyName: c.IsCompany ? nm : null,
                    Phone: null, Mobile: c.Mobile, Email: null, Province: null, City: null, Address: null, PostalCode: null,
                    CreditLimit: 0, CreditDays: 0, PriceLevel: "خرده", Discount: 0,
                    NationalCode: null, EconomicCode: null, GroupId: null, Notes: null,
                    ContactPerson: null, Visitor: null, BirthDate: null));
                if (r.Succeeded) cuN++;
            }

            // ۵) ورودِ داده‌های دمو (در صورتِ تیک) — اختیاری، برای آشنایی/آزمایش.
            if (LoadDemoData)
            {
                try { await SamaHesab.Infrastructure.Data.DatabaseMigrator.RunDemoDataAsync(AppSettingsStore.GetConnectionString()); }
                catch (System.Exception ex) { await _dialogService.ShowWarningAsync("ورودِ داده‌های دمو کامل نشد: " + ex.Message); }
            }

            // ۶) ماژول‌های اختیاریِ انتخاب‌شده را فعال/غیرفعال کن — اول خاموش‌ها، سپس روشن‌ها با کنترلِ تداخل.
            foreach (var m in Modules.Where(m => !m.IsSelected))
                _modules.SetEnabled(m.Key, false);
            var blockedModules = new List<string>();
            foreach (var m in Modules.Where(m => m.IsSelected))
                if (!_modules.TrySetEnabled(m.Key, true, out var err) && err is not null)
                    blockedModules.Add(err);

            // ۷) علامتِ تکمیل و ذخیره
            g.SetupCompleted = true;
            AppSettingsStore.SaveGeneral(g);

            var conflictNote = blockedModules.Count > 0
                ? "\n\nبرخی ماژول‌ها به‌خاطرِ تداخل فعال نشدند:\n" + string.Join("\n", blockedModules)
                : string.Empty;
            await _dialogService.ShowSuccessAsync(
                $"راه‌اندازیِ اولیه کامل شد. ({whN} انبار، {prN} کالا/خدمت، {cuN} مشتری ثبت شد) خوش آمدید!" + conflictNote);
            Finished?.Invoke();
        }, "در حال ذخیرهٔ راه‌اندازی...");
    }

    /// <summary>
    /// U-MULTI-COMPANY-1 — ساختِ شرکتِ دوم/سوم/… در همان DBِ مشترک (از دکمهٔ «افزودنِ شرکتِ
    /// جدید» در صفحهٔ ورود). برخلافِ FinishAsyncِ عادی: رمز اجباری است (نه اختیاری — این اولین
    /// رمزِ ادمینِ این شرکت است، «پیش‌فرضِ شناخته‌شده»ای برایِ برگشت وجود ندارد)، ماژول‌ها/دادهٔ
    /// پایه/دمو نادیده گرفته می‌شوند (متعلق به سشنِ شرکتِ *فعلی*اند، نه شرکتِ تازه).
    /// </summary>
    private async Task FinishNewCompanyAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
        { await _dialogService.ShowWarningAsync("برایِ شرکتِ جدید تعیینِ رمزِ مدیر (admin) الزامی است."); return; }
        if (NewPassword != ConfirmPassword)
        { await _dialogService.ShowWarningAsync("رمزِ عبور و تکرارِ آن یکسان نیستند."); return; }

        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new CreateCompanyCommand(
                CompanyName!, CompanyNationalId, CompanyEconomicCode, CompanyPhone, CompanyAddress,
                FiscalTitle, FiscalStart, FiscalEnd, NewPassword));
            if (!r.Succeeded || r.Value is null)
            { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در ساختِ شرکتِ جدید."); return; }

            CreatedCompanyId = r.Value.CompanyId;

            // کدِ بازیابیِ ادمینِ شرکتِ نو (هم‌راستا با U-SEC-RECOVERY — تنها راهِ بازیابیِ رمز، آفلاین).
            var recoveryCode = Services.RecoveryCodeGenerator.Generate();
            var rc = await _mediator.Send(new SetRecoveryCodeCommand(r.Value.AdminUserId, recoveryCode));
            if (rc.Succeeded)
                new Views.Onboarding.RecoveryCodeWindow(recoveryCode) { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();

            await _dialogService.ShowSuccessAsync(
                $"شرکتِ «{r.Value.Name}» (کدِ {r.Value.Code}) ساخته شد.\nبرایِ ورود، آن را از کمبویِ «شرکت» در صفحهٔ ورود انتخاب کنید و با «admin» و رمزی که الان تعیین کردید وارد شوید.");
            Finished?.Invoke();
        }, "در حالِ ساختِ شرکتِ جدید...");
    }
}

// ── ردیف‌های ورودِ سریعِ دادهٔ پایه در ویزارد ──
public partial class WizWarehouse : ObservableObject
{
    [ObservableProperty] private string? _name;
}

public partial class WizProduct : ObservableObject
{
    [ObservableProperty] private string? _code;
    [ObservableProperty] private string? _name;
    [ObservableProperty] private bool _isService;
    [ObservableProperty] private decimal _salePrice;
    [ObservableProperty] private decimal _purchasePrice;
}

public partial class WizCustomer : ObservableObject
{
    [ObservableProperty] private string? _name;
    [ObservableProperty] private string? _mobile;
    [ObservableProperty] private bool _isCompany;
}

/// <summary>ماژولِ اختیاریِ قابلِ انتخاب در ویزارد.</summary>
public partial class WizModule : ObservableObject
{
    public string Key { get; }
    public string Name { get; }
    [ObservableProperty] private bool _isSelected;
    public WizModule(string key, string name) { Key = key; Name = name; }
}
