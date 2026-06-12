using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>
/// مدیریت ابعاد حسابداری (هستهٔ ERP): سال مالی · مرکز هزینه · پروژه.
/// سه بخش CRUD مستقل روی Command/Queryهای Accounting/Dimensions.
/// </summary>
public partial class AccountingDimensionsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<FiscalYearDto> FiscalYears { get; } = new();
    public ObservableCollection<CostCenterDto> CostCenters { get; } = new();
    public ObservableCollection<ProjectDto> Projects { get; } = new();

    // ── فرم سال مالی ──
    [ObservableProperty] private int _fyId;
    [ObservableProperty] private string _fyTitle = string.Empty;
    [ObservableProperty] private string _fyStart = string.Empty;
    [ObservableProperty] private string _fyEnd = string.Empty;
    [ObservableProperty] private FiscalYearDto? _selectedFiscalYear;

    // ── فرم مرکز هزینه ──
    [ObservableProperty] private int _ccId;
    [ObservableProperty] private string _ccCode = string.Empty;
    [ObservableProperty] private string _ccName = string.Empty;
    [ObservableProperty] private CostCenterDto? _selectedCostCenter;

    // ── فرم پروژه ──
    [ObservableProperty] private int _prId;
    [ObservableProperty] private string _prCode = string.Empty;
    [ObservableProperty] private string _prName = string.Empty;
    [ObservableProperty] private string _prStart = string.Empty;
    [ObservableProperty] private string _prEnd = string.Empty;
    [ObservableProperty] private decimal _prBudget;
    [ObservableProperty] private ProjectDto? _selectedProject;

    public AccountingDimensionsViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            FiscalYears.Clear();
            foreach (var f in await _mediator.Send(new GetFiscalYearsQuery())) FiscalYears.Add(f);
            CostCenters.Clear();
            foreach (var c in await _mediator.Send(new GetCostCentersQuery())) CostCenters.Add(c);
            Projects.Clear();
            foreach (var p in await _mediator.Send(new GetProjectsQuery())) Projects.Add(p);
        }, "در حال بارگذاری ابعاد...");
    }

    // ════════ سال مالی ════════
    partial void OnSelectedFiscalYearChanged(FiscalYearDto? value)
    {
        if (value is null) return;
        FyId = value.Id; FyTitle = value.Title; FyStart = value.StartDate; FyEnd = value.EndDate;
    }

    [RelayCommand]
    private void NewFiscalYear()
    {
        FyId = 0; FyTitle = string.Empty;
        var cal = new System.Globalization.PersianCalendar();
        var y = cal.GetYear(DateTime.Now);
        FyStart = $"{y}/01/01"; FyEnd = $"{y}/12/29";
    }

    [RelayCommand]
    private async Task SaveFiscalYearAsync()
    {
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SaveFiscalYearCommand(FyId, FyTitle, FyStart, FyEnd));
            await Report(r.Succeeded, r.ErrorMessage, "سال مالی ذخیره شد.");
            if (r.Succeeded) { NewFiscalYear(); await RefreshAsync(); }
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task ToggleFiscalYearClosedAsync(FiscalYearDto? fy)
    {
        if (fy is null) return;
        if (!await _dialogService.ConfirmAsync(fy.IsClosed
                ? $"سال مالی «{fy.Title}» بازگشایی شود؟"
                : $"سال مالی «{fy.Title}» بسته شود؟ پس از بستن، ثبت سند در آن مجاز نیست."))
            return;
        var r = await _mediator.Send(new SetFiscalYearClosedCommand(fy.Id, !fy.IsClosed));
        await Report(r.Succeeded, r.ErrorMessage, "انجام شد.");
        await RefreshAsync();
    }

    // ════════ مرکز هزینه ════════
    partial void OnSelectedCostCenterChanged(CostCenterDto? value)
    {
        if (value is null) return;
        CcId = value.Id; CcCode = value.Code; CcName = value.Name;
    }

    [RelayCommand]
    private void NewCostCenter() { CcId = 0; CcCode = string.Empty; CcName = string.Empty; }

    [RelayCommand]
    private async Task SaveCostCenterAsync()
    {
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SaveCostCenterCommand(CcId, CcCode, CcName, null));
            await Report(r.Succeeded, r.ErrorMessage, "مرکز هزینه ذخیره شد.");
            if (r.Succeeded) { NewCostCenter(); await RefreshAsync(); }
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task ToggleCostCenterActiveAsync(CostCenterDto? cc)
    {
        if (cc is null) return;
        var r = await _mediator.Send(new SetCostCenterActiveCommand(cc.Id, !cc.IsActive));
        await Report(r.Succeeded, r.ErrorMessage, "انجام شد.");
        await RefreshAsync();
    }

    // ════════ پروژه ════════
    partial void OnSelectedProjectChanged(ProjectDto? value)
    {
        if (value is null) return;
        PrId = value.Id; PrCode = value.Code; PrName = value.Name;
        PrStart = value.StartDate ?? string.Empty; PrEnd = value.EndDate ?? string.Empty; PrBudget = value.Budget;
    }

    [RelayCommand]
    private void NewProject()
    { PrId = 0; PrCode = string.Empty; PrName = string.Empty; PrStart = string.Empty; PrEnd = string.Empty; PrBudget = 0; }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SaveProjectCommand(PrId, PrCode, PrName,
                string.IsNullOrWhiteSpace(PrStart) ? null : PrStart,
                string.IsNullOrWhiteSpace(PrEnd) ? null : PrEnd, PrBudget));
            await Report(r.Succeeded, r.ErrorMessage, "پروژه ذخیره شد.");
            if (r.Succeeded) { NewProject(); await RefreshAsync(); }
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task ToggleProjectClosedAsync(ProjectDto? p)
    {
        if (p is null) return;
        var r = await _mediator.Send(new SetProjectClosedCommand(p.Id, !p.IsClosed));
        await Report(r.Succeeded, r.ErrorMessage, "انجام شد.");
        await RefreshAsync();
    }

    private async Task Report(bool ok, string? error, string success)
    {
        if (ok) await _dialogService.ShowSuccessAsync(success);
        else await _dialogService.ShowErrorAsync(string.IsNullOrEmpty(error) ? "عملیات ناموفق بود." : error);
    }
}
