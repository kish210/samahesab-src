using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Treasury;

/// <summary>
/// تقویمِ سررسیدِ چک (رودمپ-خزانه، بک‌اندِ pc: GetChequeDueCalendarQuery/ChequeDueCalendar).
/// چک‌های در جریان را به سطل‌های زمانی (سررسیدگذشته/امروز/۷روز/۳۰روز/بعدتر) و روزهای مجزا
/// جمع می‌بندد؛ هر سطل جمعِ دریافتی/پرداختی و خالصِ نقدینگی دارد.
/// </summary>
public partial class ChequeDueCalendarViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<ChequeDueBucket> Buckets { get; } = new();
    public ObservableCollection<ChequeDueDay> Days { get; } = new();

    [ObservableProperty] private decimal _totalReceived;
    [ObservableProperty] private decimal _totalPaid;
    [ObservableProperty] private decimal _net;

    public ChequeDueCalendarViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new GetChequeDueCalendarQuery(_calendar.GetCurrentPersianDate()));
            Buckets.Clear(); foreach (var b in res.Buckets) Buckets.Add(b);
            Days.Clear(); foreach (var d in res.Days) Days.Add(d);
            TotalReceived = res.TotalReceived; TotalPaid = res.TotalPaid; Net = res.Net;
        }, "در حال تهیهٔ تقویمِ سررسید...");
    }
}
