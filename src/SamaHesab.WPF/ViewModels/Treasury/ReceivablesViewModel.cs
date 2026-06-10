using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Treasury.Commands;
using SamaHesab.Application.Treasury.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Globalization;

namespace SamaHesab.WPF.ViewModels.Treasury;

/// <summary>
/// کار #۲۰ — فهرست دریافتنی‌ها/پرداختنی‌ها با «وصول/پرداخت سریع».
/// گردش‌کار وصول مطالبات: بدهکاران مرتب بر اساس مبلغ؛ یک کلیک = ثبت دریافت کامل،
/// یا مبلغ دلخواه با ورودی. همه از طریق `CreateReceiptCommand`/`CreatePaymentCommand` (سند خودکار).
/// </summary>
public partial class ReceivablesViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<ReceivableDto> Receivables { get; } = new();
    public ObservableCollection<PayableDto> Payables { get; } = new();

    [ObservableProperty] private decimal _totalReceivable;
    [ObservableProperty] private decimal _totalPayable;

    public ReceivablesViewModel(IMediator mediator, ICurrentUserService user,
        IPersianCalendarService calendar, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _user = user; _calendar = calendar;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var recv = await _mediator.Send(new GetReceivablesQuery());
            var pay = await _mediator.Send(new GetPayablesQuery());
            Receivables.Clear(); foreach (var r in recv) Receivables.Add(r);
            Payables.Clear(); foreach (var p in pay) Payables.Add(p);
            TotalReceivable = recv.Sum(r => r.Balance);
            TotalPayable = pay.Sum(p => p.Balance);
        }, "در حال بارگیری مطالبات...");
    }

    [RelayCommand]
    private Task ReceiveFullAsync(ReceivableDto? r)
        => ReceiveAsync(r, r?.Balance ?? 0);

    [RelayCommand]
    private async Task ReceiveCustomAsync(ReceivableDto? r)
    {
        if (r is null) return;
        var input = await _dialogService.ShowInputAsync($"مبلغ دریافت از «{r.Name}» (مانده: {r.Balance:#,##0}):", "ثبت دریافت");
        if (TryAmount(input, out var amount)) await ReceiveAsync(r, amount);
    }

    private async Task ReceiveAsync(ReceivableDto? r, decimal amount)
    {
        if (r is null || amount <= 0) return;
        if (!await _dialogService.ConfirmAsync($"ثبت دریافت {amount:#,##0} ریال از «{r.Name}»؟")) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new CreateReceiptCommand(
                _user.BranchId ?? 1, 1, _calendar.GetCurrentPersianDate(), r.CustomerId, amount,
                "نقدی", $"وصول از فهرست دریافتنی‌ها"));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync($"دریافت ثبت شد (سند #{res.Value}).");
            await LoadAsync();
        }, "در حال ثبت دریافت...");
    }

    [RelayCommand]
    private Task PayFullAsync(PayableDto? p) => PayAsync(p, p?.Balance ?? 0);

    [RelayCommand]
    private async Task PayCustomAsync(PayableDto? p)
    {
        if (p is null) return;
        var input = await _dialogService.ShowInputAsync($"مبلغ پرداخت به «{p.Name}» (مانده: {p.Balance:#,##0}):", "ثبت پرداخت");
        if (TryAmount(input, out var amount)) await PayAsync(p, amount);
    }

    private async Task PayAsync(PayableDto? p, decimal amount)
    {
        if (p is null || amount <= 0) return;
        if (!await _dialogService.ConfirmAsync($"ثبت پرداخت {amount:#,##0} ریال به «{p.Name}»؟")) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new CreatePaymentCommand(
                _user.BranchId ?? 1, 1, _calendar.GetCurrentPersianDate(), p.SupplierId, amount,
                "نقدی", $"پرداخت از فهرست پرداختنی‌ها"));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync($"پرداخت ثبت شد (سند #{res.Value}).");
            await LoadAsync();
        }, "در حال ثبت پرداخت...");
    }

    private static bool TryAmount(string? s, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return decimal.TryParse(s.Replace(",", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out amount) && amount > 0;
    }
}
