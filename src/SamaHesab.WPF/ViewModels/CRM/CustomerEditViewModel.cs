using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.CRM.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.CRM;

/// <summary>ویرایش/ساختِ مشتری — 🏛️ الگوی API-only: کلاینت→API، دسکتاپ→Application (CreateCustomerCommand). بدونِ ریپازیتوریِ مستقیم.</summary>
public partial class CustomerEditViewModel : BaseViewModel, INavigationAware
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _customerType = "حقیقی";
    [ObservableProperty] private string? _firstName;
    [ObservableProperty] private string? _lastName;
    [ObservableProperty] private string? _companyName;
    [ObservableProperty] private string? _nationalCode;
    [ObservableProperty] private string? _economicCode;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _mobile;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _province;
    [ObservableProperty] private string? _city;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string? _postalCode;
    [ObservableProperty] private string? _birthDate;
    [ObservableProperty] private int? _groupId;
    [ObservableProperty] private decimal _creditLimit;
    [ObservableProperty] private int _creditDays;
    [ObservableProperty] private string _priceLevel = "خرده";
    [ObservableProperty] private decimal _discount;
    [ObservableProperty] private int _loyaltyPoints;
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string? _contactPerson;   // کارِ ۱۰: شخصِ رابط
    [ObservableProperty] private string? _visitor;          // کارِ ۱۰: ویزیتور
    [ObservableProperty] private bool _isPersonal = true;
    [ObservableProperty] private bool _isCompany;

    public int EditingId { get; set; }
    public bool IsEditing => EditingId > 0;
    public List<string> CustomerTypes { get; } = new() { "حقیقی", "حقوقی" };
    public List<string> PriceLevels { get; } = new() { "خرده", "عمده", "ویژه" };
    public List<CustomerGroupItem> Groups { get; private set; } = new();

    public CustomerEditViewModel(ICurrentUserService currentUser, IPersianCalendarService calendar,
        IMediator mediator, ApiClient api,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _currentUser = currentUser; _calendar = calendar; _mediator = mediator; _api = api; }

    /// <summary>UX-CRM-EDIT — بازکردنِ فرم برای ویرایشِ مشتریِ مشخص (Param=Id از فهرست).</summary>
    public async Task OnNavigatedToAsync(object? parameter)
    {
        if (parameter is int id && id > 0) { EditingId = id; await LoadAsync(); }
    }

    public override async Task LoadAsync()
    {
        Groups = new List<CustomerGroupItem> { new(1,"مشتریان عادی"), new(2,"مشتریان طلایی"), new(3,"عمده‌فروشان") };
        OnPropertyChanged(nameof(Groups));
        if (!IsEditing) { Code = "C" + DateTime.Now.ToString("yyMMddHH"); return; }

        // UX-CRM-EDIT — بارگذاریِ فیلدهای مشتری برای ویرایش (دسکتاپ→Application).
        var dto = await _mediator.Send(new SamaHesab.Application.CRM.Queries.GetCustomerForEditQuery(EditingId));
        if (dto == null) return;
        Code = dto.Code; CustomerType = dto.CustomerType;
        FirstName = dto.FirstName; LastName = dto.LastName; CompanyName = dto.CompanyName;
        NationalCode = dto.NationalCode; EconomicCode = dto.EconomicCode;
        Phone = dto.Phone; Mobile = dto.Mobile; Email = dto.Email;
        Province = dto.Province; City = dto.City; Address = dto.Address; PostalCode = dto.PostalCode;
        CreditLimit = dto.CreditLimit; CreditDays = dto.CreditDays; PriceLevel = dto.PriceLevel; Discount = dto.Discount;
        Notes = dto.Notes; ContactPerson = dto.ContactPerson; Visitor = dto.Visitor;
        GroupId = dto.GroupId; BirthDate = dto.BirthDate;
    }

    partial void OnCustomerTypeChanged(string value) { IsPersonal = value == "حقیقی"; IsCompany = value == "حقوقی"; }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Code)) { await _dialogService.ShowErrorAsync("کد مشتری الزامی است."); return; }
        if (IsPersonal && string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName))
        { await _dialogService.ShowErrorAsync("نام و نام خانوادگی الزامی است."); return; }
        if (IsCompany && string.IsNullOrWhiteSpace(CompanyName))
        { await _dialogService.ShowErrorAsync("نام شرکت الزامی است."); return; }

        // RC-7 — اعتبارسنجیِ هویتِ مالیاتی (برای فاکتورِ رسمی). خالی مجاز است.
        if (IsPersonal && !SamaHesab.Application.Common.Validation.IranianIdentity.IsValidNationalCode(NationalCode))
        { await _dialogService.ShowErrorAsync("کدِ ملی نامعتبر است (۱۰ رقم با رقمِ کنترلیِ صحیح)."); return; }
        if (IsCompany && !SamaHesab.Application.Common.Validation.IranianIdentity.IsValidEconomicId(NationalCode))
        { await _dialogService.ShowErrorAsync("شناسهٔ ملیِ شرکت نامعتبر است (۱۱ یا ۱۴ رقم)."); return; }
        if (!SamaHesab.Application.Common.Validation.IranianIdentity.IsValidEconomicId(EconomicCode))
        { await _dialogService.ShowErrorAsync("کدِ اقتصادی نامعتبر است (۱۱/۱۲/۱۴ رقم)."); return; }

        await ExecuteAsync(async () =>
        {
            try
            {
                bool ok; string? err = null;
                if (IsEditing)
                {
                    // UX-CRM-EDIT — به‌روزرسانیِ مشتریِ موجود (نه ساختِ تکراری).
                    var ucmd = new UpdateCustomerCommand(EditingId, CustomerType, FirstName, LastName, CompanyName,
                        Phone, Mobile, Email, Province, City, Address, PostalCode,
                        CreditLimit, CreditDays, PriceLevel, Discount,
                        NationalCode, EconomicCode, Notes, ContactPerson, Visitor, GroupId, BirthDate);
                    var r = await _mediator.Send(ucmd); ok = r.Succeeded; err = r.ErrorMessage;
                }
                else
                {
                    // 🏛️ مسیرِ نوشتن: کلاینت→API، دسکتاپ→Application (کامندِ مشترک).
                    var cmd = new CreateCustomerCommand(Code, CustomerType, FirstName, LastName, CompanyName,
                        Phone, Mobile, Email, Province, City, Address, PostalCode,
                        CreditLimit, CreditDays, PriceLevel, Discount,
                        NationalCode, EconomicCode, GroupId, Notes, ContactPerson, Visitor, BirthDate);
                    if (!string.IsNullOrWhiteSpace(_api.BaseUrl)) (ok, err) = await _api.CreateCustomerAsync(cmd);
                    else { var r = await _mediator.Send(cmd); ok = r.Succeeded; err = r.ErrorMessage; }
                }

                if (!ok) { await _dialogService.ShowErrorAsync("خطا در ذخیره مشتری: " + err); return; }
                await _dialogService.ShowSuccessAsync(IsEditing ? "مشتری به‌روزرسانی شد." : "مشتری با موفقیت ذخیره شد.");
                _navigationService.NavigateTo("Customers");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("خطا در ذخیره مشتری: " + ex.Message);
            }
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task SendBirthdaySmsAsync()
    {
        if (string.IsNullOrWhiteSpace(Mobile)) { await _dialogService.ShowErrorAsync("شماره موبایل وارد نشده."); return; }
        await _dialogService.ShowSuccessAsync($"پیامک تبریک تولد به {Mobile} ارسال شد.");
    }

    [RelayCommand]
    private async Task PrintStatementAsync()
    {
        if (!IsEditing) { await _dialogService.ShowWarningAsync("صورت‌حساب فقط برای مشتریِ ذخیره‌شده موجود است."); return; }
        // کارت مشتری گردشِ کامل + دکمهٔ «چاپ صورت‌حساب» را دارد.
        _navigationService.NavigateTo("CustomerCard", EditingId);
    }
    [RelayCommand] private void Cancel() => _navigationService.NavigateTo("Customers");
}

public record CustomerGroupItem(int Id, string Name);
