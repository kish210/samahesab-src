using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.CRM.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Modules.Contracting.Application.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Contracting;

/// <summary>
/// قراردادهایِ خدماتیِ تکرارشونده (اینترنت/پشتیبانی/...) — جایگزینِ فیچرِ حذف‌شدهٔ
/// «فاکتورِ تکرارشونده». فهرستِ قراردادها + تعریفِ قراردادِ جدید + افزودنِ آیتمِ اضافیِ
/// همان‌دوره + تولیدِ پیش‌فاکتورهایِ سررسیدشده.
/// </summary>
public partial class ServiceContractListViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<ServiceContractRow> Items { get; } = new();
    public List<CustomerRowDto> Customers { get; private set; } = new();
    public List<WarehouseDto> Warehouses { get; private set; } = new();
    public List<ProductRowDto> Products { get; private set; } = new();
    public List<FreqOption> Frequencies { get; } = new()
        { new(0, "ماهانه"), new(2, "فصلی (هر ۳ ماه)"), new(3, "شش‌ماهه"), new(1, "سالانه") };

    private static string FreqFa(int f) => f switch { 1 => "سالانه", 2 => "فصلی", 3 => "شش‌ماهه", _ => "ماهانه" };

    // فرمِ تعریفِ قراردادِ جدید
    [ObservableProperty] private string _newTitle = string.Empty;
    [ObservableProperty] private int _newCustomerId;
    [ObservableProperty] private int _newWarehouseId;
    [ObservableProperty] private int _newServiceProductId;
    [ObservableProperty] private decimal _newMonthlyAmount;
    [ObservableProperty] private int _newFrequency;
    [ObservableProperty] private string _newNextDate = string.Empty;

    // افزودنِ آیتمِ اضافیِ همان‌دوره
    [ObservableProperty] private ServiceContractRow? _selectedContract;
    [ObservableProperty] private int _extraProductId;
    [ObservableProperty] private decimal _extraQty = 1;
    [ObservableProperty] private decimal _extraPrice;

    public ServiceContractListViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        NewNextDate = _calendar.GetCurrentPersianDate();

        Customers = await _mediator.Send(new GetCustomersQuery());
        OnPropertyChanged(nameof(Customers));

        Warehouses = await _mediator.Send(new GetWarehousesQuery());
        OnPropertyChanged(nameof(Warehouses));
        if (Warehouses.Any()) NewWarehouseId = Warehouses[0].Id;

        Products = await _mediator.Send(new GetProductsQuery());
        OnPropertyChanged(nameof(Products));

        await ReloadListAsync();
    }

    private async Task ReloadListAsync()
    {
        var names = Customers.ToDictionary(c => c.Id, c => c.Name);
        Items.Clear();
        foreach (var d in await _mediator.Send(new GetServiceContractsQuery()))
            Items.Add(new ServiceContractRow(d.Id, d.Title,
                names.TryGetValue(d.CustomerId, out var n) ? n : $"#{d.CustomerId}",
                FreqFa(d.Frequency), d.NextInvoiceDate, d.LastGeneratedDate ?? "—", d.IsActive,
                d.MonthlyAmount, d.PendingExtraItems));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTitle)) { await _dialogService.ShowWarningAsync("عنوانِ قرارداد الزامی است."); return; }
        if (NewCustomerId <= 0) { await _dialogService.ShowWarningAsync("مشتری را انتخاب کنید."); return; }
        if (NewServiceProductId <= 0) { await _dialogService.ShowWarningAsync("خدمتِ قرارداد را انتخاب کنید."); return; }
        if (NewMonthlyAmount <= 0) { await _dialogService.ShowWarningAsync("مبلغِ ماهانه را وارد کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var cmd = new SaveServiceContractCommand(NewTitle, NewCustomerId, NewWarehouseId, NewServiceProductId,
                NewMonthlyAmount, NewFrequency, NewNextDate);
            var r = await _mediator.Send(cmd);
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در ذخیرهٔ قرارداد."); return; }
            await _dialogService.ShowSuccessAsync($"قراردادِ «{NewTitle}» ذخیره شد.");
            NewTitle = string.Empty; NewCustomerId = 0; NewServiceProductId = 0; NewMonthlyAmount = 0;
            await ReloadListAsync();
        }, "در حال ذخیرهٔ قرارداد...");
    }

    [RelayCommand]
    private async Task AddExtraItemAsync()
    {
        if (SelectedContract == null) { await _dialogService.ShowWarningAsync("ابتدا یک قرارداد از فهرست انتخاب کنید."); return; }
        if (ExtraProductId <= 0 || ExtraQty <= 0) { await _dialogService.ShowWarningAsync("کالا و تعداد را مشخص کنید."); return; }

        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new AddServiceContractExtraItemCommand(
                SelectedContract.Id, ExtraProductId, ExtraQty, ExtraPrice));
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در افزودنِ آیتم."); return; }
            await _dialogService.ShowSuccessAsync("آیتمِ این‌دوره اضافه شد — در فاکتورِ سررسیدِ بعدیِ همین قرارداد می‌آید.");
            ExtraProductId = 0; ExtraQty = 1; ExtraPrice = 0;
            await ReloadListAsync();
        }, "در حال افزودنِ آیتم...");
    }

    [RelayCommand]
    private async Task TerminateAsync(ServiceContractRow? row)
    {
        if (row == null) return;
        if (!await _dialogService.ConfirmAsync($"قراردادِ «{row.Title}» فسخ شود؟ دیگر فاکتورِ جدیدی برایش تولید نمی‌شود."))
            return;
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new TerminateServiceContractCommand(row.Id));
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در فسخِ قرارداد."); return; }
            await ReloadListAsync();
        }, "در حال فسخِ قرارداد...");
    }

    [RelayCommand]
    private async Task GenerateDueAsync()
    {
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new GenerateDueServiceContractInvoicesCommand(_calendar.GetCurrentPersianDate()));
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در تولید."); return; }
            await _dialogService.ShowSuccessAsync($"{r.Value!.Generated} پیش‌فاکتورِ سررسیدشده تولید شد.");
            await ReloadListAsync();
        }, "در حال تولیدِ پیش‌فاکتورهای سررسیدشده...");
    }

    [RelayCommand] private async Task RefreshAsync() => await ReloadListAsync();
}

public record ServiceContractRow(int Id, string Title, string CustomerName, string Frequency,
    string NextInvoiceDate, string LastGenerated, bool IsActive, decimal MonthlyAmount, int PendingExtraItems);
public record FreqOption(int Id, string Name);
