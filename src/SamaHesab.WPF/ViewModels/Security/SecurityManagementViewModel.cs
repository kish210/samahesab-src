using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Security.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Security;

/// <summary>
/// مدیریت امنیت/RBAC (هستهٔ ERP): نقش‌ها + تخصیص مجوز، و تخصیص نقش به کاربران.
/// </summary>
public partial class SecurityManagementViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public ObservableCollection<RoleDto> Roles { get; } = new();
    public ObservableCollection<PermCheck> Permissions { get; } = new();
    public ObservableCollection<SecurityUserDto> Users { get; } = new();
    public ObservableCollection<RoleCheck> UserRoleChecks { get; } = new();

    [ObservableProperty] private RoleDto? _selectedRole;
    [ObservableProperty] private SecurityUserDto? _selectedUser;

    // جست‌وجوی کاربر (نام کاربری/نام) — روی کشِ کاملِ کاربران اعمال می‌شود.
    private readonly List<SecurityUserDto> _allUsers = new();
    [ObservableProperty] private string _userFilter = string.Empty;
    partial void OnUserFilterChanged(string value) => ApplyUserFilter();
    private void ApplyUserFilter()
    {
        var t = (UserFilter ?? string.Empty).Trim();
        Users.Clear();
        foreach (var u in _allUsers.Where(u => t.Length == 0
                 || (u.Username?.Contains(t, System.StringComparison.OrdinalIgnoreCase) ?? false)
                 || (u.FullName?.Contains(t, System.StringComparison.OrdinalIgnoreCase) ?? false)))
            Users.Add(u);
    }

    [ObservableProperty] private string _newRoleCode = string.Empty;
    [ObservableProperty] private string _newRoleName = string.Empty;

    public SecurityManagementViewModel(IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            Roles.Clear();
            foreach (var r in await _mediator.Send(new GetRolesQuery())) Roles.Add(r);
            _allUsers.Clear();
            foreach (var u in await _mediator.Send(new GetSecurityUsersQuery())) _allUsers.Add(u);
            ApplyUserFilter();
            SelectedRole ??= Roles.FirstOrDefault();
        }, "در حال بارگذاری امنیت...");
    }

    // ════════ نقش‌ها ════════
    partial void OnSelectedRoleChanged(RoleDto? value) => _ = LoadPermissionsAsync(value);

    private async Task LoadPermissionsAsync(RoleDto? role)
    {
        var catalog = await _mediator.Send(new GetPermissionCatalogQuery());
        var granted = new HashSet<string>(role?.Permissions ?? Array.Empty<string>());
        var isAdmin = granted.Contains("*");
        Permissions.Clear();
        foreach (var p in catalog)
            Permissions.Add(new PermCheck(p.Code, p.Module, p.Label) { IsChecked = isAdmin || granted.Contains(p.Code) });
    }

    [RelayCommand]
    private void NewRole() { NewRoleCode = string.Empty; NewRoleName = string.Empty; }

    [RelayCommand]
    private async Task SaveRoleAsync()
    {
        if (string.IsNullOrWhiteSpace(NewRoleCode) || string.IsNullOrWhiteSpace(NewRoleName))
        { await _dialogService.ShowWarningAsync("کد و نام نقش را وارد کنید."); return; }
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SaveRoleCommand(0, NewRoleCode.Trim(), NewRoleName.Trim()));
            if (r.Succeeded) { NewRole(); await RefreshAsync(); await _dialogService.ShowSuccessAsync("نقش ساخته شد."); }
            else await _dialogService.ShowErrorAsync(r.ErrorMessage);
        }, "در حال ذخیره...");
    }

    [RelayCommand]
    private async Task SaveRolePermissionsAsync()
    {
        if (SelectedRole is null) { await _dialogService.ShowWarningAsync("یک نقش را انتخاب کنید."); return; }
        var codes = Permissions.Where(p => p.IsChecked).Select(p => p.Code).ToArray();
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SetRolePermissionsCommand(SelectedRole.Id, codes));
            if (r.Succeeded) { await RefreshAsync(); await _dialogService.ShowSuccessAsync("مجوزهای نقش ذخیره شد."); }
            else await _dialogService.ShowErrorAsync(r.ErrorMessage);
        }, "در حال ذخیرهٔ مجوزها...");
    }

    // ════════ کاربران ════════
    partial void OnSelectedUserChanged(SecurityUserDto? value)
    {
        UserRoleChecks.Clear();
        if (value is null) return;
        var assigned = new HashSet<int>(value.RoleIds);
        foreach (var r in Roles)
            UserRoleChecks.Add(new RoleCheck(r.Id, $"{r.Code} — {r.Name}") { IsChecked = assigned.Contains(r.Id) });
    }

    [RelayCommand]
    private async Task SaveUserRolesAsync()
    {
        if (SelectedUser is null) { await _dialogService.ShowWarningAsync("یک کاربر را انتخاب کنید."); return; }
        var roleIds = UserRoleChecks.Where(r => r.IsChecked).Select(r => r.Id).ToArray();
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SetUserRolesCommand(SelectedUser.Id, roleIds));
            if (r.Succeeded) { await RefreshAsync(); await _dialogService.ShowSuccessAsync("نقش‌های کاربر ذخیره شد."); }
            else await _dialogService.ShowErrorAsync(r.ErrorMessage);
        }, "در حال ذخیرهٔ نقش‌ها...");
    }
}

public partial class PermCheck : ObservableObject
{
    public string Code { get; }
    public string Module { get; }
    public string Label { get; }
    [ObservableProperty] private bool _isChecked;
    public PermCheck(string code, string module, string label) { Code = code; Module = module; Label = label; }
}

public partial class RoleCheck : ObservableObject
{
    public int Id { get; }
    public string Title { get; }
    [ObservableProperty] private bool _isChecked;
    public RoleCheck(int id, string title) { Id = id; Title = title; }
}
