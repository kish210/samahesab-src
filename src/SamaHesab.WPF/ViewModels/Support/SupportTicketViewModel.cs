using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Support.Commands;
using SamaHesab.Application.Support.Queries;
using SamaHesab.Domain.Enums;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>🆘 HC-4 — تیکتِ پشتیبانی: ثبتِ تیکتِ نو + فهرستِ تیکت‌ها + گفت‌وگو/پاسخ.</summary>
public partial class SupportTicketViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    // فرمِ تیکتِ جدید
    [ObservableProperty] private string _subject = string.Empty;
    [ObservableProperty] private string _body = string.Empty;
    [ObservableProperty] private EnumOption<SupportCategory> _selectedCategory;
    [ObservableProperty] private string? _resultMessage;
    [ObservableProperty] private bool _resultIsError;

    // فهرست/گفت‌وگو
    [ObservableProperty] private TicketDto? _selectedTicket;
    [ObservableProperty] private string _replyText = string.Empty;

    public bool HasResult => !string.IsNullOrEmpty(ResultMessage);
    partial void OnResultMessageChanged(string? value) => OnPropertyChanged(nameof(HasResult));

    public ObservableCollection<TicketDto> Tickets { get; } = new();

    public ObservableCollection<EnumOption<SupportCategory>> Categories { get; } = new()
    {
        new("حسابداری", SupportCategory.Accounting), new("خزانه‌داری", SupportCategory.Treasury),
        new("فروش", SupportCategory.Sales), new("خرید", SupportCategory.Purchase),
        new("انبار", SupportCategory.Inventory), new("گزارش‌ها", SupportCategory.Reports),
        new("امنیت", SupportCategory.Security), new("صندوق فروش (POS)", SupportCategory.Pos),
        new("رستوران", SupportCategory.Restaurant), new("گردشگری", SupportCategory.Tourism),
        new("سیستم", SupportCategory.System), new("سایر", SupportCategory.Other),
    };

    public SupportTicketViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator;
        _selectedCategory = Categories[10];
    }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var list = await _mediator.Send(new GetSupportTicketsQuery());
        var keepId = SelectedTicket?.Id;
        Tickets.Clear();
        foreach (var t in list) Tickets.Add(t);
        SelectedTicket = Tickets.FirstOrDefault(t => t.Id == keepId) ?? Tickets.FirstOrDefault();
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(Subject)) { await _dialogService.ShowWarningAsync("موضوعِ تیکت را وارد کنید."); return; }
        if (string.IsNullOrWhiteSpace(Body)) { await _dialogService.ShowWarningAsync("متنِ تیکت را وارد کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new CreateSupportTicketCommand(Subject.Trim(), Body.Trim(), SelectedCategory.Value));
            ResultIsError = !res.Succeeded;
            ResultMessage = res.Succeeded ? res.Value!.Message : res.ErrorMessage;
            if (res.Succeeded) { Subject = string.Empty; Body = string.Empty; await RefreshAsync(); }
        }, "در حال ثبتِ تیکت...");
    }

    [RelayCommand]
    private async Task SendReplyAsync()
    {
        if (SelectedTicket is null) return;
        if (string.IsNullOrWhiteSpace(ReplyText)) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new AddTicketMessageCommand(SelectedTicket.Id, ReplyText.Trim()));
            if (res.Succeeded) { ReplyText = string.Empty; await RefreshAsync(); }
            else await _dialogService.ShowErrorAsync(res.ErrorMessage);
        }, "در حال ارسالِ پاسخ...");
    }
}
