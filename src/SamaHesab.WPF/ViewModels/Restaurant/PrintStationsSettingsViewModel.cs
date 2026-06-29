using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.CRM.Queries;          // (استفاده نمی‌شود مستقیم؛ نگه‌داشته شد برای سازگاری)
using SamaHesab.Application.Inventory.Queries;     // GetProductsQuery
using SamaHesab.Modules.Restaurant.Application.Commands;
using SamaHesab.Modules.Restaurant.Application.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Restaurant;

/// <summary>ردیفِ کالا برای نگاشت به ایستگاه (در گریدِ نگاشت).</summary>
public partial class ProductStationRow : ObservableObject
{
    public int ProductId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    [ObservableProperty] private int _stationId;   // 0 = ایستگاهِ پیش‌فرض
}

/// <summary>
/// REST-PRINT-STATIONS — تنظیماتِ ایستگاه‌های چاپ/فیش‌پرینترِ رستوران.
/// (۱) تعریفِ ایستگاه‌ها (نام + پرینترِ ویندوز + پیش‌فرض). (۲) نگاشتِ هر کالا(آیتمِ منو) به یک ایستگاه.
/// هنگامِ ارسالِ سفارش، تیکتِ هر ایستگاه به پرینترِ خودش چاپ می‌شود (در RestaurantPosViewModel).
/// </summary>
public partial class PrintStationsSettingsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<PrintStationDto> Stations { get; } = new();
    public ObservableCollection<string> Printers { get; } = new();
    public ObservableCollection<ProductStationRow> Products { get; } = new();
    /// <summary>گزینه‌های ایستگاه برای کمبوی نگاشت (شاملِ «پیش‌فرض» با Id=0).</summary>
    public ObservableCollection<PrintStationDto> StationOptions { get; } = new();

    [ObservableProperty] private PrintStationDto? _selectedStation;
    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _selectedPrinter = string.Empty;
    [ObservableProperty] private bool _isDefault;
    [ObservableProperty] private bool _active = true;
    [ObservableProperty] private string _productFilter = string.Empty;

    public PrintStationsSettingsViewModel(IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            // پرینترهای نصب‌شدهٔ ویندوز.
            Printers.Clear();
            Printers.Add("");   // خالی = پرینترِ پیش‌فرضِ سیستم
            try
            {
                using var server = new System.Printing.LocalPrintServer();
                foreach (var q in server.GetPrintQueues().Select(q => q.Name).OrderBy(n => n))
                    Printers.Add(q);
            }
            catch { /* فهرستِ پرینتر در دسترس نیست */ }

            await ReloadStationsAsync();
            await ReloadProductsAsync();
        }, "در حال بارگذاری تنظیماتِ چاپ...");
    }

    private async Task ReloadStationsAsync()
    {
        Stations.Clear();
        foreach (var s in await _mediator.Send(new GetPrintStationsQuery())) Stations.Add(s);
        StationOptions.Clear();
        StationOptions.Add(new PrintStationDto(0, "— پیش‌فرض —", "", false, true));
        foreach (var s in Stations) StationOptions.Add(s);
    }

    private async Task ReloadProductsAsync()
    {
        var map = await _mediator.Send(new GetProductStationMapQuery());
        var rows = await _mediator.Send(new GetProductsQuery(ProductFilter));
        Products.Clear();
        foreach (var p in rows)
            Products.Add(new ProductStationRow
            { ProductId = p.Id, Code = p.Code, Name = p.Name, StationId = map.GetValueOrDefault(p.Id, 0) });
    }

    partial void OnSelectedStationChanged(PrintStationDto? value)
    {
        if (value is null) return;
        EditId = value.Id; Name = value.Name; SelectedPrinter = value.PrinterName;
        IsDefault = value.IsDefault; Active = value.Active;
    }

    [RelayCommand]
    private void NewStation()
    { SelectedStation = null; EditId = 0; Name = string.Empty; SelectedPrinter = string.Empty; IsDefault = false; Active = true; }

    [RelayCommand]
    private async Task SaveStationAsync()
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new SavePrintStationCommand(EditId, Name, SelectedPrinter, IsDefault, Active));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("ایستگاه ذخیره شد.");
            await ReloadStationsAsync();
            SelectedStation = Stations.FirstOrDefault(s => s.Id == res.Value);
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task DeleteStationAsync()
    {
        if (SelectedStation is not { } s) { await _dialogService.ShowErrorAsync("ابتدا یک ایستگاه را انتخاب کنید."); return; }
        if (!await _dialogService.ConfirmAsync($"حذفِ ایستگاهِ «{s.Name}»؟ (نگاشت‌های کالاهای آن هم پاک می‌شود)")) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new DeletePrintStationCommand(s.Id));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await ReloadStationsAsync(); await ReloadProductsAsync(); NewStation();
        }, "در حال حذف...");
    }

    [RelayCommand] private Task SearchProductsAsync() => ExecuteAsync(ReloadProductsAsync, "در حال جست‌وجو...");

    /// <summary>ذخیرهٔ نگاشتِ همهٔ کالاها به ایستگاه (StationId=0 → ایستگاهِ پیش‌فرض).</summary>
    [RelayCommand]
    private async Task SaveAssignmentsAsync()
    {
        await ExecuteAsync(async () =>
        {
            int n = 0;
            foreach (var p in Products)
            {
                var res = await _mediator.Send(new SetProductStationCommand(p.ProductId, p.StationId));
                if (res.Succeeded) n++;
            }
            await _dialogService.ShowSuccessAsync($"نگاشتِ {n} کالا ذخیره شد.");
        }, "در حال ذخیرهٔ نگاشت‌ها...");
    }
}
