using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Modules.TaxInvoicing.Application.Commands;
using SamaHesab.Modules.TaxInvoicing.Application.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.TaxInvoicing;

/// <summary>
/// نگاشتِ کالا→شناسهٔ کالایِ رسمی/کدِ واحدِ سامانهٔ مودیان — بدونِ این نگاشت، ردیفِ فاکتورِ آن کالا
/// در payloadِ ارسالی itemId/unit ندارد (<see cref="SendElectronicInvoiceCommand"/>). صفحهٔ ساده‌ای
/// برایِ پر کردنِ همین جدولِ کناری (Core Product دست‌نخورده می‌ماند).
/// </summary>
public partial class TaxItemCodesViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public partial class Row : ObservableObject
    {
        public int ProductId { get; }
        public string ProductCode { get; }
        public string ProductName { get; }
        [ObservableProperty] private string _itemId;
        [ObservableProperty] private string _measurementUnitCode;

        public Row(TaxItemCodeRowDto d)
        {
            ProductId = d.ProductId; ProductCode = d.ProductCode; ProductName = d.ProductName;
            _itemId = d.ItemId ?? ""; _measurementUnitCode = d.MeasurementUnitCode ?? "";
        }
    }

    public ObservableCollection<Row> Rows { get; } = new();
    [ObservableProperty] private string _searchText = string.Empty;

    public TaxItemCodesViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => SearchAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        await ExecuteAsync(async () =>
        {
            var list = await _mediator.Send(new GetTaxItemCodesQuery(
                string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim()));
            Rows.Clear();
            foreach (var d in list) Rows.Add(new Row(d));
        }, "در حال بارگیریِ کالاها...");
    }

    [RelayCommand]
    private async Task SaveRowAsync(Row? row)
    {
        if (row is null) return;
        var res = await _mediator.Send(new SaveTaxItemCodeCommand(row.ProductId, row.ItemId, row.MeasurementUnitCode));
        if (!res.Succeeded) await _dialogService.ShowErrorAsync(res.ErrorMessage);
    }
}
