using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.HRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.HRM;

// ─── Employee List ─────────────────────────────────────────────────────────────
public partial class EmployeeListViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _showInactive;
    [ObservableProperty] private EmployeeDto? _selectedEmployee;

    public ObservableCollection<EmployeeDto> Employees { get; } = new();

    public EmployeeListViewModel(IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService) { _mediator = mediator; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var list = await _mediator.Send(new GetEmployeesQuery(ShowInactive,
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim()));
            Employees.Clear();
            foreach (var e in list) Employees.Add(e);
        }, "در حال بارگذاری کارکنان...");
    }

    [RelayCommand] private void AddNew() => _navigationService.NavigateTo("EmployeeEdit");
    [RelayCommand] private void Edit(EmployeeDto? emp)
    { if (emp != null) _navigationService.NavigateTo("EmployeeEdit"); }

    [RelayCommand]
    private async Task DeleteAsync(EmployeeDto? emp)
    {
        if (emp == null) return;
        if (!await _dialogService.ConfirmAsync($"آیا کارمند {emp.FullName} حذف شود؟")) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new DeleteEmployeeCommand(emp.Id));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            Employees.Remove(emp);
        }, "در حال حذف...");
    }

    partial void OnSearchTextChanged(string value) => _ = LoadAsync();
    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync();
}

// ─── Employee Edit ────────────────────────────────────────────────────────────
public partial class EmployeeEditViewModel : BaseViewModel
{
    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;

    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _nationalCode = string.Empty;
    [ObservableProperty] private string _firstName = string.Empty;
    [ObservableProperty] private string _lastName = string.Empty;
    [ObservableProperty] private string? _fatherName;
    [ObservableProperty] private string? _birthDate;
    [ObservableProperty] private string? _gender;
    [ObservableProperty] private string? _maritalStatus;
    [ObservableProperty] private string? _education;
    [ObservableProperty] private string? _mobile;
    [ObservableProperty] private string? _phone;
    [ObservableProperty] private string? _email;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private int? _departmentId;
    [ObservableProperty] private int? _positionId;
    [ObservableProperty] private string _hireDate = string.Empty;
    [ObservableProperty] private string _contractType = "دائم";
    [ObservableProperty] private decimal _baseSalary;
    [ObservableProperty] private string? _bankName;
    [ObservableProperty] private string? _bankAccount;
    [ObservableProperty] private string? _shebaNumber;
    [ObservableProperty] private string? _insuranceNumber;
    [ObservableProperty] private string? _notes;

    public List<string> Genders { get; } = new() { "مرد", "زن" };
    public List<string> MaritalStatuses { get; } = new() { "مجرد", "متأهل", "مطلقه", "بیوه" };
    public List<string> Educations { get; } = new() { "زیر دیپلم", "دیپلم", "فوق دیپلم", "لیسانس", "فوق لیسانس", "دکتری" };
    public List<string> ContractTypes { get; } = new() { "دائم", "موقت", "پاره وقت", "پیمانی" };

    public EmployeeEditViewModel(IPersianCalendarService calendar, IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _calendar = calendar; _mediator = mediator; }

    public override async Task LoadAsync()
    {
        HireDate = _calendar.GetCurrentPersianDate();
        Code = "E" + DateTime.Now.ToString("yyMMddHH");
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(NationalCode)) { await _dialogService.ShowErrorAsync("کد ملی الزامی است."); return; }
        if (string.IsNullOrWhiteSpace(FirstName)) { await _dialogService.ShowErrorAsync("نام الزامی است."); return; }
        if (string.IsNullOrWhiteSpace(LastName)) { await _dialogService.ShowErrorAsync("نام خانوادگی الزامی است."); return; }
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SaveEmployeeCommand(
                0, Code, NationalCode, FirstName, LastName, HireDate, BaseSalary, ContractType,
                Mobile, Phone, Email, Address, FatherName, BirthDate, Gender, MaritalStatus,
                Education, DepartmentId, PositionId, BankName, BankAccount, ShebaNumber, InsuranceNumber, Notes));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("پرونده کارمند ذخیره شد.");
            _navigationService.NavigateTo("Employees");
        }, "در حال ذخیره...");
    }

    [RelayCommand] private void Cancel() => _navigationService.NavigateTo("Employees");
}

// ─── Salary ───────────────────────────────────────────────────────────────────
public partial class SalaryViewModel : BaseViewModel
{
    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;

    [ObservableProperty] private string _selectedYear = string.Empty;
    [ObservableProperty] private int _selectedMonth = 1;
    [ObservableProperty] private SalarySlipRow? _selectedSlip;
    [ObservableProperty] private decimal _totalGross;
    [ObservableProperty] private decimal _totalInsurance;
    [ObservableProperty] private decimal _totalTax;
    [ObservableProperty] private decimal _totalNet;

    public ObservableCollection<SalarySlipRow> SalarySlips { get; } = new();
    public List<string> Years { get; } = new() { "1402", "1403", "1404" };
    public List<MonthItem> Months { get; } = Enumerable.Range(1, 12)
        .Select(m => new MonthItem(m, new string[]{"فروردین","اردیبهشت","خرداد","تیر","مرداد","شهریور","مهر","آبان","آذر","دی","بهمن","اسفند"}[m-1]))
        .ToList();

    public SalaryViewModel(IPersianCalendarService calendar, IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _calendar = calendar; _mediator = mediator; }

    public override async Task LoadAsync()
    {
        var now = DateTime.Now;
        SelectedYear = _calendar.GetPersianYear(now).ToString();
        SelectedMonth = _calendar.GetPersianMonth(now);
        await LoadSlipsAsync();
    }

    [RelayCommand]
    private async Task LoadSlipsAsync()
    {
        await ExecuteAsync(async () =>
        {
            SalarySlips.Clear();
            var slips = await _mediator.Send(new GetSalarySlipsQuery(SelectedYear, SelectedMonth));
            foreach (var s in slips)
                SalarySlips.Add(new SalarySlipRow(s.EmployeeId, s.EmployeeName, s.Department,
                    s.BaseSalary, s.Overtime, s.Allowances, s.Insurance, s.Tax, s.Net));

            TotalGross = SalarySlips.Sum(s => s.GrossSalary);
            TotalInsurance = SalarySlips.Sum(s => s.InsuranceDeduct);
            TotalTax = SalarySlips.Sum(s => s.TaxDeduct);
            TotalNet = SalarySlips.Sum(s => s.NetSalary);
            await Task.CompletedTask;
        }, "در حال محاسبه حقوق...");
    }

    [RelayCommand]
    private async Task PostAllAsync()
    {
        if (SalarySlips.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا فیش‌ها را محاسبه کنید."); return; }
        var ok = await _dialogService.ConfirmAsync($"حقوق {SalarySlips.Count} نفر به مبلغ کل {TotalNet:N0} ریال پرداخت شود؟");
        if (!ok) return;
        await ExecuteAsync(async () =>
        {
            var date = _calendar.GetCurrentPersianDate();
            var res = await _mediator.Send(new PostSalaryVoucherCommand(date));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync($"حقوق ثبت و سندِ حسابداری صادر شد (سند #{res.Value!.VoucherId}).");
        }, "در حال ثبت حقوق...");
    }

    [RelayCommand] private async Task PrintSlipAsync(SalarySlipRow? row)
    {
        if (row == null) return;
        await _dialogService.ShowInfoAsync($"در حال چاپ فیش حقوقی {row.EmployeeName}...");
    }
}

// ─── Attendance ───────────────────────────────────────────────────────────────
public partial class AttendanceViewModel : BaseViewModel
{
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _selectedDate = string.Empty;
    [ObservableProperty] private int _presentCount;
    [ObservableProperty] private int _absentCount;
    [ObservableProperty] private int _leaveCount;

    public ObservableCollection<AttendanceRow> Records { get; } = new();

    public AttendanceViewModel(IPersianCalendarService calendar, IDialogService dialogService,
        INavigationService navigationService) : base(dialogService, navigationService)
    { _calendar = calendar; }

    public override async Task LoadAsync()
    {
        SelectedDate = _calendar.GetCurrentPersianDate();
        await LoadRecordsAsync();
    }

    [RelayCommand]
    private async Task LoadRecordsAsync()
    {
        await ExecuteAsync(async () =>
        {
            Records.Clear();
            Records.Add(new AttendanceRow(1, "علی احمدی", "08:05", "17:10", 8.08m, 0.17m, "حاضر"));
            Records.Add(new AttendanceRow(2, "مریم رضایی", "", "", 0, 0, "مرخصی"));
            Records.Add(new AttendanceRow(3, "رضا محمدی", "08:30", "17:00", 7.5m, 0, "حاضر"));
            PresentCount = Records.Count(r => r.Status == "حاضر");
            AbsentCount  = Records.Count(r => r.Status == "غایب");
            LeaveCount   = Records.Count(r => r.Status == "مرخصی");
            await Task.CompletedTask;
        }, "در حال بارگذاری...");
    }

    [RelayCommand] private async Task SaveAsync()
    {
        await ExecuteAsync(async () => await _dialogService.ShowSuccessAsync("حضور و غیاب ذخیره شد."));
    }
}

// ─── Models ───────────────────────────────────────────────────────────────────
public partial class SalarySlipRow : ObservableObject
{
    public int EmployeeId { get; }
    public string EmployeeName { get; }
    public string Department { get; }
    [ObservableProperty] private decimal _baseSalary;
    [ObservableProperty] private decimal _overtimePay;
    [ObservableProperty] private decimal _allowances;
    [ObservableProperty] private decimal _insuranceDeduct;
    [ObservableProperty] private decimal _taxDeduct;
    [ObservableProperty] private decimal _netSalary;
    public decimal GrossSalary => BaseSalary + OvertimePay + Allowances;

    public SalarySlipRow(int id, string name, string dept, decimal base_, decimal over, decimal allow,
        decimal ins, decimal tax, decimal net)
    {
        EmployeeId = id; EmployeeName = name; Department = dept;
        _baseSalary = base_; _overtimePay = over; _allowances = allow;
        _insuranceDeduct = ins; _taxDeduct = tax; _netSalary = net;
    }
}

public record AttendanceRow(int EmployeeId, string EmployeeName,
    string CheckIn, string CheckOut, decimal WorkHours, decimal OvertimeHours, string Status);

public record MonthItem(int Number, string Name);

