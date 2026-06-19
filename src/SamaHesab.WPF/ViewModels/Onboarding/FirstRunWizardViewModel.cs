using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Security.Commands;
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

    public FirstRunWizardViewModel(IMediator mediator, ICurrentUserService user,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _user = user;

        // پیش‌پُر از تنظیماتِ موجود + پیشنهادِ سالِ مالیِ جاری.
        var g = AppSettingsStore.GetGeneral();
        CompanyName = g.CompanyName ?? string.Empty;
        CompanyPhone = g.CompanyPhone; CompanyNationalId = g.CompanyNationalId;
        CompanyEconomicCode = g.CompanyEconomicCode; CompanyAddress = g.CompanyAddress;
        LogoPath = g.CompanyLogoPath;

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
        if (string.IsNullOrWhiteSpace(CompanyName))
        { await _dialogService.ShowWarningAsync("نامِ شرکت الزامی است."); return; }

        // رمز: اختیاری ولی اگر وارد شد باید تأیید بخورد.
        var wantsPassword = !string.IsNullOrWhiteSpace(NewPassword);
        if (wantsPassword && NewPassword != ConfirmPassword)
        { await _dialogService.ShowWarningAsync("رمزِ عبور و تکرارِ آن یکسان نیستند."); return; }
        if (!wantsPassword &&
            !await _dialogService.ConfirmAsync("رمزِ پیش‌فرضِ admin تغییر نکرده — ادامه می‌دهید؟ (توصیه: تغییر دهید)"))
            return;

        await ExecuteAsync(async () =>
        {
            // ۱) شرکت → تنظیماتِ محلی (merge با موجود)
            var g = AppSettingsStore.GetGeneral();
            g.CompanyName = CompanyName; g.CompanyPhone = CompanyPhone;
            g.CompanyNationalId = CompanyNationalId; g.CompanyEconomicCode = CompanyEconomicCode;
            g.CompanyAddress = CompanyAddress; g.CompanyLogoPath = LogoPath;
            g.FiscalYearStart = FiscalStart; g.FiscalYearEnd = FiscalEnd;

            // ۲) سالِ مالی → DB (command موجود)
            var fy = await _mediator.Send(new SaveFiscalYearCommand(0, FiscalTitle, FiscalStart, FiscalEnd));
            if (!fy.Succeeded)
            { await _dialogService.ShowErrorAsync(fy.ErrorMessage ?? "خطا در ثبتِ سالِ مالی."); return; }

            // ۳) رمزِ ادمین (در صورتِ ورود)
            if (wantsPassword && _user.UserId is int uid)
            {
                var pr = await _mediator.Send(new ChangeUserPasswordCommand(uid, NewPassword));
                if (!pr.Succeeded)
                { await _dialogService.ShowErrorAsync(pr.ErrorMessage ?? "خطا در تغییرِ رمز."); return; }
            }

            // ۴) دادهٔ پایه: انبارها / کالاها و خدمات / مشتری‌ها (فقط ردیف‌های پرشده)
            int whN = 0, prN = 0, cuN = 0;

            foreach (var w in Warehouses.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
                if ((await _mediator.Send(new CreateWarehouseCommand(w.Name!.Trim()))).Succeeded) whN++;

            int kseq = 1001;
            foreach (var p in Products.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                var code = string.IsNullOrWhiteSpace(p.Code) ? $"K{kseq++}" : p.Code!.Trim();
                var r = await _mediator.Send(new CreateProductCommand(
                    Code: code, Barcode: null, Name: p.Name!.Trim(), NameEn: null, GroupId: null, BrandId: null,
                    UnitId: 1, ProductType: p.IsService ? ProductType.Service : ProductType.Product,
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

            // ۶) علامتِ تکمیل و ذخیره
            g.SetupCompleted = true;
            AppSettingsStore.SaveGeneral(g);

            await _dialogService.ShowSuccessAsync(
                $"راه‌اندازیِ اولیه کامل شد. ({whN} انبار، {prN} کالا/خدمت، {cuN} مشتری ثبت شد) خوش آمدید!");
            Finished?.Invoke();
        }, "در حال ذخیرهٔ راه‌اندازی...");
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
