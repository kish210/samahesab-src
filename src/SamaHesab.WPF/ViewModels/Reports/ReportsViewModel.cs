using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.IO;

namespace SamaHesab.WPF.ViewModels.Reports;

public partial class ReportsViewModel : BaseViewModel
{
    private readonly IReportService _reportService;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;
    private readonly ModuleService _modules;

    [ObservableProperty] private ReportCategory? _selectedCategory;
    [ObservableProperty] private ReportItem? _selectedReport;
    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;
    [ObservableProperty] private bool _isReportReady;

    public ObservableCollection<ReportCategory> Categories { get; } = new();
    public ObservableCollection<ReportItem> Reports { get; } = new();
    public ObservableCollection<ReportResultRow> Results { get; } = new();

    public ReportsViewModel(IReportService reportService, IPersianCalendarService calendar,
        ICurrentUserService currentUser, IMediator mediator, ModuleService modules,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _reportService = reportService; _calendar = calendar; _currentUser = currentUser; _mediator = mediator; _modules = modules; }

    // پیش‌تنظیم‌های بازهٔ تاریخ (پارامترِ پیش‌فرضِ سریع) — رشته‌محورِ شمسی، سازگار با فیلترِ گزارش‌ها.
    [RelayCommand] private void RangeToday()     { var t = _calendar.GetCurrentPersianDate(); FromDate = t; ToDate = t; }
    [RelayCommand] private void RangeThisMonth() { var t = _calendar.GetCurrentPersianDate(); FromDate = t.Substring(0, 8) + "01"; ToDate = t; }
    [RelayCommand] private void RangeThisYear()  { var t = _calendar.GetCurrentPersianDate(); FromDate = t.Substring(0, 4) + "/01/01"; ToDate = t; }
    [RelayCommand]
    private void RangePrevMonth()
    {
        var p = _calendar.GetCurrentPersianDate().Split('/');
        int y = int.Parse(p[0]), m = int.Parse(p[1]);
        m--; if (m < 1) { m = 12; y--; }
        FromDate = $"{y:0000}/{m:00}/01";
        ToDate = $"{y:0000}/{m:00}/31";   // کرانِ بالا (مقایسهٔ رشته‌ای؛ روزِ ناموجود بی‌اثر)
    }

    public override async Task LoadAsync()
    {
        var now = DateTime.Now;
        FromDate = _calendar.ToPersianDate(new DateTime(now.Year, now.Month, 1));
        ToDate = _calendar.GetCurrentPersianDate();

        Categories.Clear();
        Categories.Add(new ReportCategory("حسابداری", new[]
        {
            new ReportItem("TrialBalance","تراز آزمایشی"),
            new ReportItem("GeneralLedger","دفتر کل"),
            new ReportItem("DetailLedger","دفتر معین"),
            new ReportItem("BalanceSheet","ترازنامه"),
            new ReportItem("ProfitLoss","سود و زیان"),
            new ReportItem("CashFlow","جریان وجوه نقد"),
        }));
        Categories.Add(new ReportCategory("فروش", new[]
        {
            new ReportItem("SalesSummary","خلاصه فروش"),
            new ReportItem("SalesByCustomer","فروش به تفکیک مشتری"),
            new ReportItem("SalesByProduct","فروش به تفکیک کالا"),
            new ReportItem("CustomerBalance","مانده مشتریان"),
        }));
        Categories.Add(new ReportCategory("خرید", new[]
        {
            new ReportItem("PurchaseSummary","خلاصه خرید"),
            new ReportItem("SupplierBalance","مانده تأمین‌کنندگان"),
        }));
        Categories.Add(new ReportCategory("انبار", new[]
        {
            new ReportItem("StockStatus","وضعیت موجودی"),
            new ReportItem("StockMovement","گردش کالا"),
            new ReportItem("LowStock","کمبود موجودی"),
            new ReportItem("StockValuation","ارزیابی موجودی"),
        }));
        Categories.Add(new ReportCategory("چک", new[]
        {
            new ReportItem("ChequesInProcess","چک‌های در جریان"),
            new ReportItem("ChequesDue","چک‌های سررسید"),
            new ReportItem("ChequesReturned","چک‌های برگشتی"),
        }));
        Categories.Add(new ReportCategory("منابع انسانی", new[]
        {
            new ReportItem("EmployeeList","لیست کارکنان"),
            new ReportItem("SalaryReport","گزارش حقوق"),
            new ReportItem("AttendanceSummary","خلاصه حضور و غیاب"),
        }));

        SelectedCategory = Categories.First();
        await Task.CompletedTask;
    }

    partial void OnSelectedCategoryChanged(ReportCategory? value)
    { Reports.Clear(); if (value != null) foreach (var r in value.Items) Reports.Add(r); }

    [RelayCommand]
    private void SelectReport(ReportItem? item)
    { if (item == null) return; SelectedReport = item; IsReportReady = false; Results.Clear(); }

    [RelayCommand]
    private async Task RunReportAsync()
    {
        if (SelectedReport == null) { await _dialogService.ShowErrorAsync("یک گزارش انتخاب کنید."); return; }

        // U-REPORTS-CENTER — پیش‌تر این صفحه برایِ هر ۲۲ گزارش داده‌یِ ساختگیِ یکسان نشان می‌داد.
        // حالا هر گزارش را به کوئریِ واقعیِ متناظرش وصل می‌کند (RunReportQuery برایِ گزارش‌هایِ هسته؛
        // SalaryReport/AttendanceSummary چون متعلق به ماژول‌هایِ اختیاریِ HR/حضوروغیاب‌اند، مستقیم
        // این‌جا صدا زده می‌شوند تا هسته به این ماژول‌ها وابسته نشود).
        Results.Clear();
        IsReportReady = false;

        await ExecuteAsync(async () =>
        {
            var code = SelectedReport.Code;

            if (code is "SalaryReport" or "AttendanceSummary")
            {
                if (!_modules.IsEnabled(ModuleService.Hr))
                {
                    await _dialogService.ShowWarningAsync("این گزارش به ماژولِ «منابعِ انسانی» تعلق دارد که غیرفعال است.");
                    return;
                }
                var parts = (string.IsNullOrWhiteSpace(ToDate) ? _calendar.GetCurrentPersianDate() : ToDate).Split('/');
                var year = parts[0]; var month = int.Parse(parts[1]);

                if (code == "SalaryReport")
                {
                    var slips = await _mediator.Send(new SamaHesab.Application.HRM.GetSalarySlipsQuery(year, month));
                    foreach (var s in slips)
                        Results.Add(new ReportResultRow(s.EmployeeId.ToString(), $"{s.EmployeeName} ({s.Department})",
                            s.BaseSalary + s.Overtime + s.Allowances, s.Insurance + s.Tax, s.Net));
                }
                else
                {
                    var employees = await _mediator.Send(new SamaHesab.Application.HRM.GetEmployeesQuery());
                    foreach (var e in employees)
                    {
                        var a = await _mediator.Send(new SamaHesab.Application.HRM.GetMonthlyAttendanceQuery(e.Id, year, (byte)month));
                        Results.Add(new ReportResultRow(e.Code, e.FullName,
                            a.Summary.PresentDays, a.Summary.AbsentDays + a.Summary.LeaveDays, a.Summary.WorkedHours));
                    }
                }
                IsReportReady = Results.Count > 0;
                if (!IsReportReady) await _dialogService.ShowInfoAsync($"گزارشِ «{SelectedReport.Name}» داده‌ای برایِ این بازه ندارد.");
                return;
            }

            var result = await _mediator.Send(new SamaHesab.Application.Reports.Queries.RunReportQuery(code, FromDate, ToDate));
            if (result.Rows is null)
            {
                await _dialogService.ShowInfoAsync(result.RedirectMessage ?? $"گزارشِ «{SelectedReport.Name}» هنوز پیاده‌سازی نشده است.");
                return;
            }
            foreach (var r in result.Rows)
                Results.Add(new ReportResultRow(r.Code, r.Name, r.Debit, r.Credit, r.Balance));
            IsReportReady = Results.Count > 0;
            if (!IsReportReady) await _dialogService.ShowInfoAsync($"گزارشِ «{SelectedReport.Name}» داده‌ای برایِ این بازه ندارد.");
        }, "در حال اجرایِ گزارش...");
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (!IsReportReady) { await _dialogService.ShowErrorAsync("ابتدا گزارش را اجرا کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var bytes = await _reportService.GeneratePdfAsync(SelectedReport?.Name ?? "Report", Results);
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"{SelectedReport?.Code}_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            await File.WriteAllBytesAsync(path, bytes);
            await _dialogService.ShowSuccessAsync($"PDF ذخیره شد:\n{path}");
        }, "در حال تولید PDF...");
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (!IsReportReady) { await _dialogService.ShowErrorAsync("ابتدا گزارش را اجرا کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var bytes = await _reportService.GenerateExcelAsync(SelectedReport?.Name ?? "Report", Results);
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"{SelectedReport?.Code}_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            await File.WriteAllBytesAsync(path, bytes);
            await _dialogService.ShowSuccessAsync($"Excel ذخیره شد:\n{path}");
        }, "در حال تولید Excel...");
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (!IsReportReady) { await _dialogService.ShowErrorAsync("ابتدا گزارش را اجرا کنید."); return; }
        await _reportService.PrintAsync(SelectedReport?.Name ?? "Report", Results);
    }
}

public record ReportCategory(string Name, ReportItem[] Items);
public record ReportItem(string Code, string Name);
public record ReportResultRow(string Code, string Name, decimal Debit, decimal Credit, decimal Balance);
