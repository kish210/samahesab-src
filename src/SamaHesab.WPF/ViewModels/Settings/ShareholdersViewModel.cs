using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Settings.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Settings;

/// <summary>سهامداران (دفترِ سهامِ شرکت): فهرست + ایجاد/ویرایش/حذف + جمعِ درصد و سرمایه. F2 = جدید.</summary>
public partial class ShareholdersViewModel : BaseViewModel, SamaHesab.WPF.Services.ISupportsNew
{
    private readonly IMediator _mediator;

    public ObservableCollection<ShareholderDto> Shareholders { get; } = new();

    [ObservableProperty] private ShareholderDto? _selectedShareholder;
    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _nationalCode = string.Empty;
    [ObservableProperty] private decimal _sharePercent;
    [ObservableProperty] private decimal _capitalAmount;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;

    [ObservableProperty] private decimal _totalShare;
    [ObservableProperty] private decimal _totalCapital;

    public ShareholdersViewModel(IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            Shareholders.Clear();
            foreach (var s in await _mediator.Send(new GetShareholdersQuery())) Shareholders.Add(s);
            TotalShare = Shareholders.Sum(s => s.SharePercent);
            TotalCapital = Shareholders.Sum(s => s.CapitalAmount);
        }, "در حال بارگذاری سهامداران...");
    }

    partial void OnSelectedShareholderChanged(ShareholderDto? v)
    {
        if (v is null) return;
        EditId = v.Id; FullName = v.FullName; NationalCode = v.NationalCode ?? "";
        SharePercent = v.SharePercent; CapitalAmount = v.CapitalAmount;
        Phone = v.Phone ?? ""; Notes = v.Notes ?? "";
    }

    [RelayCommand]
    private void New()
    {
        EditId = 0; FullName = ""; NationalCode = ""; SharePercent = 0; CapitalAmount = 0; Phone = ""; Notes = "";
        SelectedShareholder = null;
    }

    public void RequestNew() => New();   // F2-GLOBAL

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(FullName)) { await _dialogService.ShowWarningAsync("نامِ سهامدار را وارد کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SaveShareholderCommand(EditId, FullName.Trim(),
                string.IsNullOrWhiteSpace(NationalCode) ? null : NationalCode.Trim(),
                SharePercent, CapitalAmount,
                string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim(),
                string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()));
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("سهامدار ذخیره شد.");
            New();
            await RefreshAsync();
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditId == 0) { await _dialogService.ShowWarningAsync("سهامداری برای حذف انتخاب نشده."); return; }
        if (!await _dialogService.ConfirmAsync($"سهامدار «{FullName}» حذف شود؟", "حذف سهامدار")) return;
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new DeleteShareholderCommand(EditId));
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync("سهامدار حذف شد.");
            New();
            await RefreshAsync();
        }, "در حال حذف...");
    }
}
