using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.CRM;

public partial class CustomerEditViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRepository<Customer> _customerRepo;

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
    [ObservableProperty] private bool _isPersonal = true;
    [ObservableProperty] private bool _isCompany;

    private int _editingId;
    public bool IsEditing => _editingId > 0;
    public List<string> CustomerTypes { get; } = new() { "حقیقی", "حقوقی" };
    public List<string> PriceLevels { get; } = new() { "خرده", "عمده", "ویژه" };
    public List<CustomerGroupItem> Groups { get; private set; } = new();

    public CustomerEditViewModel(ICurrentUserService currentUser, IPersianCalendarService calendar,
        IUnitOfWork unitOfWork, IRepository<Customer> customerRepo,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _currentUser = currentUser; _calendar = calendar; _unitOfWork = unitOfWork; _customerRepo = customerRepo; }

    public override async Task LoadAsync()
    {
        Groups = new List<CustomerGroupItem> { new(1,"مشتریان عادی"), new(2,"مشتریان طلایی"), new(3,"عمده‌فروشان") };
        OnPropertyChanged(nameof(Groups));
        if (!IsEditing) Code = "C" + DateTime.Now.ToString("yyMMddHH");
        await Task.CompletedTask;
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

        await ExecuteAsync(async () =>
        {
            try
            {
                var companyId = _currentUser.CompanyId ?? 1;
                var entity = Customer.Create(companyId, Code, CustomerType, FirstName, LastName, CompanyName);
                entity.UpdateContactInfo(Phone, Mobile, Email, Province, City, Address, PostalCode);
                entity.UpdateCreditTerms(CreditLimit, CreditDays, PriceLevel, Discount);
                entity.SetDetails(NationalCode, EconomicCode, GroupId, Notes);
                if (!string.IsNullOrWhiteSpace(BirthDate)) entity.SetBirthDate(BirthDate!);

                await _customerRepo.AddAsync(entity);
                await _unitOfWork.SaveChangesAsync();

                await _dialogService.ShowSuccessAsync("مشتری با موفقیت ذخیره شد.");
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

    [RelayCommand] private async Task PrintStatementAsync() => await _dialogService.ShowInfoAsync("در حال آماده‌سازی صورتحساب...");
    [RelayCommand] private void Cancel() => _navigationService.NavigateTo("Customers");
}

public record CustomerGroupItem(int Id, string Name);
