using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Settings.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Settings;

/// <summary>مدیریت شعب (چندشعبه — هستهٔ ERP): فهرست + ایجاد/ویرایش + فعال/غیرفعال.</summary>
public partial class BranchManagementViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<BranchDto> Branches { get; } = new();

    [ObservableProperty] private BranchDto? _selectedBranch;
    [ObservableProperty] private int _editId;
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _managerName = string.Empty;
    [ObservableProperty] private bool _isHQ;

    public BranchManagementViewModel(IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            Branches.Clear();
            foreach (var b in await _mediator.Send(new GetBranchesQuery())) Branches.Add(b);
        }, "در حال بارگذاری شعب...");
    }

    partial void OnSelectedBranchChanged(BranchDto? value)
    {
        if (value is null) return;
        EditId = value.Id; Code = value.Code; Name = value.Name;
        Address = value.Address ?? ""; Phone = value.Phone ?? ""; ManagerName = value.ManagerName ?? ""; IsHQ = value.IsHQ;
    }

    [RelayCommand]
    private void NewBranch()
    {
        EditId = 0; Code = ""; Name = ""; Address = ""; Phone = ""; ManagerName = ""; IsHQ = false;
        SelectedBranch = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Code) || string.IsNullOrWhiteSpace(Name))
        { await _dialogService.ShowWarningAsync("کد و نام شعبه را وارد کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SaveBranchCommand(EditId, Code.Trim(), Name.Trim(),
                NullIfEmpty(Address), NullIfEmpty(Phone), NullIfEmpty(ManagerName), IsHQ));
            if (r.Succeeded) { NewBranch(); await RefreshAsync(); await _dialogService.ShowSuccessAsync("شعبه ذخیره شد."); }
            else await _dialogService.ShowErrorAsync(r.ErrorMessage);
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task ToggleAsync(BranchDto? b)
    {
        if (b is null) return;
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new ToggleBranchCommand(b.Id, !b.IsActive));
            if (r.Succeeded) await RefreshAsync();
            else await _dialogService.ShowErrorAsync(r.ErrorMessage);
        }, "در حال تغییر وضعیت...");
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
