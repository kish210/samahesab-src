using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Accounting;

/// <summary>
/// کار #۱۸ — تابلوی چک‌های در جریان مرتب بر اساس سررسید (نزدیک‌ترین/سررسیدگذشته اول)،
/// با عملیات سریعِ «وصول» و «برگشت» روی هر ردیف (سند حسابداری خودکار).
/// </summary>
public partial class ChequeBoardViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<ChequeRow> Cheques { get; } = new();
    [ObservableProperty] private decimal _totalAmount;
    [ObservableProperty] private int _overdueCount;

    public ChequeBoardViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var today = _calendar.GetCurrentPersianDate();
            var board = await _mediator.Send(new GetChequeBoardQuery(today));
            Cheques.Clear();
            foreach (var c in board) Cheques.Add(ChequeRow.From(c));
            TotalAmount = board.Sum(c => c.Amount);
            OverdueCount = Cheques.Count(c => c.StateCode == 2);
        }, "در حال بارگیری تابلوی چک...");
    }

    [RelayCommand]
    private async Task ClearChequeAsync(ChequeRow? c)
    {
        if (c is null) return;
        if (!await _dialogService.ConfirmAsync($"وصول چک {c.ChequeNumber} به مبلغ {c.Amount:#,##0}؟")) return;
        await ChangeAsync(c.Id, ChequeAction.Clear, null, "وصول ثبت شد");
    }

    [RelayCommand]
    private async Task ReturnChequeAsync(ChequeRow? c)
    {
        if (c is null) return;
        var reason = await _dialogService.ShowInputAsync($"علت برگشت چک {c.ChequeNumber}:", "برگشت چک");
        if (string.IsNullOrWhiteSpace(reason)) return;
        await ChangeAsync(c.Id, ChequeAction.Return, reason, "برگشت ثبت شد");
    }

    private async Task ChangeAsync(int id, ChequeAction action, string? reason, string okMsg)
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new ChangeChequeStatusCommand(id, action, _calendar.GetCurrentPersianDate(), reason));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync(okMsg);
            await LoadAsync();
        }, "در حال ثبت...");
    }
}

public partial class ChequeRow : ObservableObject
{
    public int Id { get; init; }
    public string ChequeNumber { get; init; } = "";
    public string BankName { get; init; } = "";
    public decimal Amount { get; init; }
    public string DueDate { get; init; } = "";
    public string Type { get; init; } = "";
    public string StateFa { get; init; } = "";
    public int StateCode { get; init; }   // 0=آینده 1=امروز 2=سررسیدگذشته

    public static ChequeRow From(ChequeBoardDto d) => new()
    {
        Id = d.Id, ChequeNumber = d.ChequeNumber, BankName = d.BankName, Amount = d.Amount,
        DueDate = d.DueDate, Type = d.Type,
        StateFa = d.DueState switch { "Overdue" => "سررسید گذشته", "DueToday" => "امروز", _ => "آینده" },
        StateCode = d.DueState switch { "Overdue" => 2, "DueToday" => 1, _ => 0 }
    };
}
