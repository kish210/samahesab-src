using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Contracting.Application;
using SamaHesab.Modules.Contracting.Application.Commands;
using SamaHesab.Modules.Contracting.Application.Queries;
using SamaHesab.Modules.Contracting.Domain;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Contracting;

/// <summary>CON-C2-2 — ثبتِ صورت‌وضعیتِ پیمان با پیش‌نمایشِ زندهٔ آبشار (StatementWaterfallEngine) + Save/Post.</summary>
public partial class ContractingStatementViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _user;

    [ObservableProperty] private ContractProjectListDto? _selectedProject;
    [ObservableProperty] private int _number = 1;
    [ObservableProperty] private bool _isFinal;          // false=موقت، true=قطعی
    [ObservableProperty] private string _date = string.Empty;
    [ObservableProperty] private decimal _cumulativeGrossWork;
    [ObservableProperty] private decimal _previousCumulative;
    [ObservableProperty] private decimal _adjustmentAmount;
    [ObservableProperty] private decimal _materialDiffAmount;
    [ObservableProperty] private decimal _penalty;
    [ObservableProperty] private decimal _other;
    [ObservableProperty] private int _selectedFiscalYearId;
    [ObservableProperty] private int _savedStatementId;

    // پیش‌نمایشِ زندهٔ آبشار
    [ObservableProperty] private decimal _periodWork;
    [ObservableProperty] private decimal _grossThisPeriod;
    [ObservableProperty] private decimal _advanceRecovery;
    [ObservableProperty] private decimal _retention;
    [ObservableProperty] private decimal _insurance;
    [ObservableProperty] private decimal _tax;
    [ObservableProperty] private decimal _netPayable;

    public ObservableCollection<ContractProjectListDto> Projects { get; } = new();
    public ObservableCollection<FiscalYearDto> FiscalYears { get; } = new();
    public bool CanPost => SavedStatementId > 0;

    public ContractingStatementViewModel(IMediator mediator, IPersianCalendarService calendar,
        ICurrentUserService user, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; _user = user; }

    public override async Task LoadAsync()
    {
        Date = _calendar.GetCurrentPersianDate();
        await ExecuteAsync(async () =>
        {
            Projects.Clear();
            foreach (var p in await _mediator.Send(new GetContractProjectsQuery())) Projects.Add(p);
            FiscalYears.Clear();
            foreach (var f in await _mediator.Send(new GetFiscalYearsQuery())) FiscalYears.Add(f);
            if (SelectedFiscalYearId == 0)
                SelectedFiscalYearId = FiscalYears.FirstOrDefault(f => f.IsActive)?.Id ?? FiscalYears.FirstOrDefault()?.Id ?? 0;
        }, "در حال بارگذاری...");
    }

    partial void OnSelectedProjectChanged(ContractProjectListDto? value) => Recompute();
    partial void OnCumulativeGrossWorkChanged(decimal value) => Recompute();
    partial void OnPreviousCumulativeChanged(decimal value) => Recompute();
    partial void OnAdjustmentAmountChanged(decimal value) => Recompute();
    partial void OnMaterialDiffAmountChanged(decimal value) => Recompute();
    partial void OnPenaltyChanged(decimal value) => Recompute();
    partial void OnOtherChanged(decimal value) => Recompute();
    partial void OnSavedStatementIdChanged(int value) => OnPropertyChanged(nameof(CanPost));

    /// <summary>پیش‌نمایشِ زنده — موتورِ خالصِ آبشار را با نرخ‌های پیمان مستقیم صدا می‌زند.</summary>
    private void Recompute()
    {
        var p = SelectedProject;
        if (p is null) { PeriodWork = GrossThisPeriod = AdvanceRecovery = Retention = Insurance = Tax = NetPayable = 0; return; }
        var r = StatementWaterfallEngine.Compute(new WaterfallInput(
            CumulativeGrossWork, PreviousCumulative, AdjustmentAmount, MaterialDiffAmount,
            p.AdvancePercent, p.RetentionPercent, p.InsurancePercent, p.TaxPercent,
            Penalty, Other, p.AdvanceOutstanding));
        PeriodWork = r.PeriodWork; GrossThisPeriod = r.GrossThisPeriod; AdvanceRecovery = r.AdvanceRecovery;
        Retention = r.Retention; Insurance = r.Insurance; Tax = r.Tax; NetPayable = r.NetPayable;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (SelectedProject is null) { await _dialogService.ShowWarningAsync("پیمان را انتخاب کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SaveProgressStatementCommand(
                SelectedProject.Id, Number, IsFinal ? StatementType.Final : StatementType.Interim, Date,
                CumulativeGrossWork, PreviousCumulative, AdjustmentAmount, MaterialDiffAmount, Penalty, Other));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            SavedStatementId = res.Value;
            await _dialogService.ShowSuccessAsync($"صورت‌وضعیتِ شمارهٔ {Number} ذخیره شد (پیش‌نویس). خالصِ پرداختی {NetPayable:N0} ریال. اکنون می‌توانید «قطعی/سند» بزنید.");
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task PostAsync()
    {
        if (SavedStatementId <= 0) { await _dialogService.ShowWarningAsync("ابتدا صورت‌وضعیت را ذخیره کنید."); return; }
        if (SelectedFiscalYearId <= 0) { await _dialogService.ShowWarningAsync("سالِ مالی را انتخاب کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new PostProgressStatementCommand(SavedStatementId, _user.BranchId ?? 1, SelectedFiscalYearId));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync($"صورت‌وضعیت قطعی شد و سندِ حسابداریِ متوازن صادر گردید (سند #{res.Value}).");
            SavedStatementId = 0;
        }, "در حال صدورِ سند...");
    }
}

/// <summary>CON-C2-2 — داشبوردِ پیمان (KPIهای مالی از GetProjectDashboardQuery).</summary>
public partial class ContractingDashboardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private ContractProjectListDto? _selectedProject;
    [ObservableProperty] private ProjectDashboardDto? _dashboard;

    public ObservableCollection<ContractProjectListDto> Projects { get; } = new();

    public ContractingDashboardViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            Projects.Clear();
            foreach (var p in await _mediator.Send(new GetContractProjectsQuery())) Projects.Add(p);
            if (SelectedProject is null) SelectedProject = Projects.FirstOrDefault();
        }, "در حال بارگذاری...");
    }

    partial void OnSelectedProjectChanged(ContractProjectListDto? value) => _ = LoadDashboardAsync();

    [RelayCommand]
    private async Task LoadDashboardAsync()
    {
        if (SelectedProject is null) { Dashboard = null; return; }
        await ExecuteAsync(async () =>
        {
            Dashboard = await _mediator.Send(new GetProjectDashboardQuery(SelectedProject.Id));
        }, "در حال محاسبه...");
    }
}
