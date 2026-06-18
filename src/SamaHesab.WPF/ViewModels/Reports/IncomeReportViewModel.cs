using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Reports;

/// <summary>
/// درآمد/سود (طبقِ فرمولِ کاربر): سود = فروش − خرید − هزینه − مالیات.
/// خلاصه + تفکیکِ ماهانه («لیست درآمدها»). روی GetIncomeSummaryQuery.
/// </summary>
public partial class IncomeReportViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _fromDate = string.Empty;
    [ObservableProperty] private string _toDate = string.Empty;

    [ObservableProperty] private decimal _sales;
    [ObservableProperty] private decimal _purchase;
    [ObservableProperty] private decimal _expense;
    [ObservableProperty] private decimal _tax;
    [ObservableProperty] private decimal _profit;

    public ObservableCollection<IncomeRow> Months { get; } = new();

    public IncomeReportViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        var today = _calendar.GetCurrentPersianDate();           // yyyy/MM/dd
        ToDate = today;
        FromDate = today.Length >= 7 ? today[..5] + "01/01" : today;   // ابتدای سالِ جاری
        await RunAsync();
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var dto = await _mediator.Send(new GetIncomeSummaryQuery(FromDate, ToDate));
            Sales = dto.Sales; Purchase = dto.Purchase; Expense = dto.Expense; Tax = dto.Tax; Profit = dto.Profit;
            Months.Clear();
            foreach (var m in dto.Months) Months.Add(m);
        }, "در حال محاسبهٔ درآمد/سود...");
    }
}
