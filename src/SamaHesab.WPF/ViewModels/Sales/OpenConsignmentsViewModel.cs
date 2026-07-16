using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Sales;

/// <summary>
/// U-CONSIGN-SETTLE — فهرستِ کنسینمنت‌هایِ بازِ تسویه‌نشده + دکمهٔ «تسویه» برایِ هر ردیف.
/// وقتی کنسینی واقعاً کالای امانی را به مشتریِ نهایی فروخت، از این‌جا تسویه می‌شود
/// (SettleConsignmentCommand: خروج از ۱-۰۵-۰۰۳ + سندِ واقعیِ درآمد/COGS/دریافتنی).
/// </summary>
public partial class OpenConsignmentsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<OpenConsignmentRow> Rows { get; } = new();

    public OpenConsignmentsViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            Rows.Clear();
            foreach (var r in await _mediator.Send(new GetOpenConsignmentsQuery()))
                Rows.Add(r);
        }, "در حال بارگذاریِ کنسینمنت‌هایِ باز...");
    }

    [RelayCommand]
    private async Task SettleAsync(OpenConsignmentRow? row)
    {
        if (row is null) return;
        var dlg = new Views.Sales.SettleConsignmentWindow(row.InvoiceId, row.Number, row.RemainAmount)
        { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
            await RefreshAsync();
    }
}
