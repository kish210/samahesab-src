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

    /// <summary>PAY-C2-4 — محاسبهٔ دسته‌ایِ حقوقِ ماه و ذخیرهٔ فیش‌ها (RunMonthlyPayrollCommand).</summary>
    [RelayCommand]
    private async Task RunBatchAsync()
    {
        var ok = await _dialogService.ConfirmAsync(
            $"حقوقِ همهٔ کارکنانِ فعال برای {SelectedMonth}/{SelectedYear} محاسبه و فیش‌ها ذخیره شود؟\n" +
            "(فیش‌های موجودِ این ماه دوباره محاسبه و جایگزین می‌شوند.)");
        if (!ok) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new RunMonthlyPayrollCommand(SelectedYear, (byte)SelectedMonth, Overwrite: true));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            var r = res.Value!;
            await LoadSlipsAsync();
            await _dialogService.ShowSuccessAsync(
                $"محاسبه انجام شد: {r.Created} فیش صادر شد.\n" +
                $"جمعِ ناخالص {r.TotalGross:N0} · خالص {r.TotalNet:N0} ریال\n" +
                $"بیمهٔ کارفرما {r.TotalEmployerInsurance:N0} · مالیات {r.TotalTax:N0} ریال");
        }, "در حال محاسبهٔ دسته‌ای...");
    }

    /// <summary>PAY-C2-4 — تولید و ذخیرهٔ فایل‌های خروجی (بیمه/مالیات/بانک) از فیش‌های ماه.</summary>
    [RelayCommand]
    private async Task ExportFilesAsync()
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new GetPayrollExportQuery(SelectedYear, (byte)SelectedMonth));
            if (res.EmployeeCount == 0)
            {
                await _dialogService.ShowWarningAsync("برای این ماه فیشی ذخیره نشده. ابتدا «محاسبهٔ دسته‌ای» را بزنید.");
                return;
            }
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                "SamaHesab", "حقوق", $"{SelectedYear}-{SelectedMonth:00}");
            System.IO.Directory.CreateDirectory(dir);
            var enc = new System.Text.UTF8Encoding(true);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "لیست_بیمه.csv"), res.InsuranceListCsv, enc);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "لیست_مالیات.csv"), res.TaxListCsv, enc);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "فایل_بانک.csv"), res.BankFileCsv, enc);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
            await _dialogService.ShowSuccessAsync(
                $"۳ فایلِ خروجی برای {res.EmployeeCount} نفر ذخیره شد:\n{dir}");
        }, "در حال تولیدِ فایل‌های خروجی...");
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

    /// <summary>PAY-C2-4 — چاپِ فیشِ حقوقی: HTMLِ راست‌چین (PayslipHtmlBuilder) در مرورگرِ پیش‌فرض.</summary>
    [RelayCommand] private async Task PrintSlipAsync(SalarySlipRow? row)
    {
        var item = row ?? SelectedSlip;
        if (item == null) { await _dialogService.ShowWarningAsync("ابتدا یک فیش را انتخاب کنید."); return; }
        try
        {
            // بازسازیِ نتیجهٔ حقوق از مقادیرِ ردیف (سهمِ کارفرما۲۳٪ از مأخذِ بیمه برای نمایش).
            var insurableBase = item.GrossSalary;
            var employer = System.Math.Round(insurableBase * 0.23m, 0);
            var result = new FullPayrollResult(
                OvertimePay: item.OvertimePay, NightPay: 0, HolidayPay: 0, ChildAllowance: 0,
                Gross: item.GrossSalary, InsurableBase: insurableBase,
                EmployeeInsurance: item.InsuranceDeduct, EmployerInsurance: employer,
                Tax: item.TaxDeduct, TotalDeductions: item.InsuranceDeduct + item.TaxDeduct,
                Net: item.NetSalary);
            var header = new PayslipHeader(
                CompanyName: AppSettingsStore.GetGeneral().CompanyName ?? "سما حساب",
                EmployeeName: item.EmployeeName, PersonnelCode: item.EmployeeId.ToString(),
                NationalCode: "", Year: int.TryParse(SelectedYear, out var y) ? y : 0, Month: SelectedMonth);
            var html = PayslipHtmlBuilder.Build(header, result);

            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"payslip_{item.EmployeeId}_{SelectedYear}{SelectedMonth:00}.html");
            System.IO.File.WriteAllText(path, html, new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}

// ─── مدیریتِ مرخصی (ATTP-C2-2) ──────────────────────────────────────────────────
public partial class LeaveManagementViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private bool _pendingOnly = true;
    [ObservableProperty] private LeaveRequestDto? _selectedRequest;
    // فرمِ درخواستِ نو
    [ObservableProperty] private int _newEmployeeId;
    [ObservableProperty] private string _newLeaveType = "استحقاقی";
    [ObservableProperty] private string _newStartDate = string.Empty;
    [ObservableProperty] private string _newEndDate = string.Empty;
    [ObservableProperty] private decimal _newDays = 1;
    [ObservableProperty] private decimal _newHours;
    [ObservableProperty] private string? _newReason;

    public ObservableCollection<LeaveRequestDto> Requests { get; } = new();
    public ObservableCollection<EmployeeDto> Employees { get; } = new();
    public string[] LeaveTypes { get; } = { "استحقاقی", "استعلاجی", "بدونِ‌حقوق", "ساعتی" };

    public LeaveManagementViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        NewStartDate = _calendar.GetCurrentPersianDate();
        NewEndDate = NewStartDate;
        await ExecuteAsync(async () =>
        {
            Employees.Clear();
            foreach (var emp in await _mediator.Send(new GetEmployeesQuery())) Employees.Add(emp);
            await ReloadAsync();
        }, "در حال بارگذاری...");
    }

    [RelayCommand] private async Task RefreshAsync() => await ReloadAsync();
    partial void OnPendingOnlyChanged(bool value) => _ = ReloadAsync();

    private async Task ReloadAsync()
    {
        Requests.Clear();
        foreach (var r in await _mediator.Send(new GetLeaveRequestsQuery(PendingOnly))) Requests.Add(r);
    }

    [RelayCommand]
    private async Task ApproveAsync(LeaveRequestDto? row) => await DecideAsync(row, true);
    [RelayCommand]
    private async Task RejectAsync(LeaveRequestDto? row) => await DecideAsync(row, false);

    private async Task DecideAsync(LeaveRequestDto? row, bool approve)
    {
        var r = row ?? SelectedRequest;
        if (r == null) { await _dialogService.ShowWarningAsync("یک درخواست را انتخاب کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new DecideLeaveCommand(r.Id, approve, _calendar.GetCurrentPersianDate()));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await ReloadAsync();
        }, approve ? "در حال تأیید..." : "در حال رد...");
    }

    [RelayCommand]
    private async Task SubmitRequestAsync()
    {
        if (NewEmployeeId <= 0) { await _dialogService.ShowWarningAsync("کارمند را انتخاب کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new RequestLeaveCommand(
                NewEmployeeId, NewLeaveType, NewStartDate, NewEndDate, NewDays, NewHours, NewReason));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("درخواستِ مرخصی ثبت شد.");
            NewReason = null; NewDays = 1; NewHours = 0;
            await ReloadAsync();
        }, "در حال ثبتِ درخواست...");
    }
}

// ─── ایمپورتِ ترددِ دستگاه/اکسل (ATTP-C2-2) ──────────────────────────────────────
public partial class AttendanceImportViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private string _resultText = string.Empty;

    public AttendanceImportViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => Task.CompletedTask;

    [RelayCommand]
    private void PickFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "فایلِ تردد (CSV/TXT)|*.csv;*.txt|همه|*.*" };
        if (dlg.ShowDialog() == true) FilePath = dlg.FileName;
    }

    [RelayCommand]
    private async Task ImportAsync()
    {
        if (string.IsNullOrWhiteSpace(FilePath) || !System.IO.File.Exists(FilePath))
        { await _dialogService.ShowWarningAsync("ابتدا فایلِ تردد را انتخاب کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var csv = await System.IO.File.ReadAllTextAsync(FilePath);
            var res = await _mediator.Send(new ImportAttendanceCommand(csv));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            var v = res.Value!;
            ResultText = $"درج‌شده: {v.Imported} · ردشده: {v.Skipped}" +
                         (v.Errors.Count > 0 ? "\nخطاها:\n• " + string.Join("\n• ", v.Errors.Take(20)) : "");
        }, "در حال ایمپورت...");
    }
}

// ─── کارگاهِ مستقلِ حضور و غیاب (ATTP-C2-1) ──────────────────────────────────────
public partial class AttendanceWorkspaceViewModel : BaseViewModel
{
    public AttendanceViewModel Daily { get; }
    public AttendanceMonthlyViewModel Monthly { get; }
    public LeaveManagementViewModel Leaves { get; }
    public AttendanceImportViewModel Import { get; }

    public AttendanceWorkspaceViewModel(AttendanceViewModel daily, AttendanceMonthlyViewModel monthly,
        LeaveManagementViewModel leaves, AttendanceImportViewModel import,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { Daily = daily; Monthly = monthly; Leaves = leaves; Import = import; }

    public override async Task LoadAsync()
    {
        await Daily.LoadAsync();
        await Monthly.LoadAsync();
        await Leaves.LoadAsync();
    }
}

// ─── کارکردِ ماهانه (ATTP-C2-2) ─────────────────────────────────────────────────
public partial class AttendanceMonthlyViewModel : BaseViewModel
{
    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;

    [ObservableProperty] private string _selectedYear = string.Empty;
    [ObservableProperty] private int _selectedMonth = 1;

    public ObservableCollection<AttendanceReportRow> Rows { get; } = new();
    public List<string> Years { get; } = new() { "1403", "1404", "1405", "1406" };
    public List<MonthItem> Months { get; } = Enumerable.Range(1, 12)
        .Select(m => new MonthItem(m, new[]{"فروردین","اردیبهشت","خرداد","تیر","مرداد","شهریور","مهر","آبان","آذر","دی","بهمن","اسفند"}[m-1])).ToList();

    public AttendanceMonthlyViewModel(IPersianCalendarService calendar, IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _calendar = calendar; _mediator = mediator; }

    public override async Task LoadAsync()
    {
        var now = System.DateTime.Now;
        SelectedYear = _calendar.GetPersianYear(now).ToString();
        SelectedMonth = _calendar.GetPersianMonth(now);
        await LoadReportAsync();
    }

    [RelayCommand]
    private async Task LoadReportAsync()
    {
        await ExecuteAsync(async () =>
        {
            Rows.Clear();
            foreach (var r in await _mediator.Send(new GetAttendanceReportQuery(SelectedYear, (byte)SelectedMonth)))
                Rows.Add(r);
        }, "در حال محاسبهٔ کارکردِ ماهانه...");
    }
}

// ─── Attendance ───────────────────────────────────────────────────────────────
public partial class AttendanceViewModel : BaseViewModel
{
    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;

    [ObservableProperty] private string _selectedDate = string.Empty;
    [ObservableProperty] private int _presentCount;
    [ObservableProperty] private int _absentCount;
    [ObservableProperty] private int _leaveCount;

    public ObservableCollection<AttendanceRow> Records { get; } = new();

    public AttendanceViewModel(IPersianCalendarService calendar, IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _calendar = calendar; _mediator = mediator; }

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
            var rows = await _mediator.Send(new GetAttendanceQuery(SelectedDate));
            foreach (var r in rows)
                Records.Add(new AttendanceRow(r.EmployeeId, r.EmployeeName, r.CheckIn, r.CheckOut,
                    r.WorkHours, r.OvertimeHours, r.Status));
            PresentCount = Records.Count(r => r.Status == "حاضر");
            AbsentCount  = Records.Count(r => r.Status == "غایب");
            LeaveCount   = Records.Count(r => r.Status == "مرخصی");
        }, "در حال بارگذاری...");
    }

    /// <summary>ATT-C2-3 — ثبت/ویرایشِ ورود و خروجِ یک کارمند (UpsertAttendanceCommand).</summary>
    [RelayCommand]
    private async Task SaveRowAsync(AttendanceRow? row)
    {
        if (row == null) return;
        await ExecuteAsync(async () =>
        {
            var ci = ParseTime(row.CheckIn);
            var co = ParseTime(row.CheckOut);
            var res = await _mediator.Send(new UpsertAttendanceCommand(
                row.EmployeeId, SelectedDate, ci, co, Status: "حاضر"));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await LoadRecordsAsync();
        }, "در حال ثبتِ تردد...");
    }

    /// <summary>ATT-C2-3 — علامت‌گذاریِ دسته‌ایِ وضعیت برای ردیف‌های انتخاب‌شده (MarkBatchAttendanceCommand).</summary>
    [RelayCommand]
    private async Task MarkBatchAsync(string status)
    {
        var ids = Records.Where(r => r.IsSelected).Select(r => r.EmployeeId).ToList();
        if (ids.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا کارمندانِ موردِ نظر را تیک بزنید."); return; }
        var leaveType = status == "مرخصی" ? "استحقاقی" : null;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new MarkBatchAttendanceCommand(ids, SelectedDate, status, leaveType));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await LoadRecordsAsync();
            await _dialogService.ShowSuccessAsync($"وضعیتِ «{status}» برای {res.Value} نفر ثبت شد.");
        }, "در حال علامت‌گذاری...");
    }

    /// <summary>تبدیلِ متنِ «HH:mm» (با ارقامِ فارسی/لاتین) به TimeOnly؛ خالی → null.</summary>
    private static TimeOnly? ParseTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var s = new string(text.Select(c => c >= '۰' && c <= '۹' ? (char)('0' + (c - '۰')) : c).ToArray()).Trim();
        return TimeOnly.TryParse(s, out var t) ? t : (TimeOnly?)null;
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

public partial class AttendanceRow : ObservableObject
{
    public int EmployeeId { get; }
    public string EmployeeName { get; }
    [ObservableProperty] private string _checkIn;
    [ObservableProperty] private string _checkOut;
    [ObservableProperty] private decimal _workHours;
    [ObservableProperty] private decimal _overtimeHours;
    [ObservableProperty] private string _status;
    [ObservableProperty] private bool _isSelected;   // برای علامت‌گذاریِ دسته‌ای

    public AttendanceRow(int employeeId, string employeeName, string checkIn, string checkOut,
        decimal workHours, decimal overtimeHours, string status)
    {
        EmployeeId = employeeId; EmployeeName = employeeName;
        _checkIn = checkIn; _checkOut = checkOut;
        _workHours = workHours; _overtimeHours = overtimeHours; _status = status;
    }
}

public record MonthItem(int Number, string Name);

