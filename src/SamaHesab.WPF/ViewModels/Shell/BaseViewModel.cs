using CommunityToolkit.Mvvm.ComponentModel;
using SamaHesab.WPF.Services;

namespace SamaHesab.WPF.ViewModels.Shell;

public abstract partial class BaseViewModel : ObservableObject
{
    protected readonly IDialogService _dialogService;
    protected readonly INavigationService _navigationService;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _loadingMessage = "در حال بارگذاری...";

    protected BaseViewModel(IDialogService dialogService, INavigationService navigationService)
    { _dialogService = dialogService; _navigationService = navigationService; }

    public virtual Task LoadAsync() => Task.CompletedTask;

    protected async Task ExecuteAsync(Func<Task> action, string loadingMsg = "در حال پردازش...")
    {
        IsLoading = true; LoadingMessage = loadingMsg;
        try { await action(); }
        catch (Exception ex)
        {
            // U-UX-ACCT-1: پیش‌تر خطا اینجا مدیریت نمی‌شد و تا DispatcherUnhandledException بالا
            // می‌رفت — یعنی هر خطایِ معمولیِ کوئری/شبکه به‌عنوانِ باگِ فاجعه‌بار نشان داده می‌شد.
            await _dialogService.ShowErrorAsync(ex.Message);
        }
        finally { IsLoading = false; }
    }
}
