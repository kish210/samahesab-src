using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>
/// 🆘 HC-1 — صفحهٔ «عیب‌یابیِ سیستم». لایسنس/اثرانگشت/سلامتِ DB/اتصال/ماژول‌ها/حافظه/دیسک/نسخه/شعبه.
/// همه محلی؛ بدونِ هیچ دادهٔ کسب‌وکار.
/// </summary>
public partial class DiagnosticsViewModel : BaseViewModel
{
    private readonly DiagnosticsCollector _collector;

    [ObservableProperty] private DiagnosticsInfo? _info;
    [ObservableProperty] private bool _dbHealthy;

    public DiagnosticsViewModel(DiagnosticsCollector collector,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _collector = collector; }

    public override async Task LoadAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            Info = await _collector.CollectAsync();
            DbHealthy = Info.DatabaseStatus.StartsWith("متصل");
        }, "در حال جمع‌آوریِ اطلاعاتِ تشخیصی...");
    }

    /// <summary>کپیِ گزارشِ متنی در کلیپ‌بورد (برای الصاق در تیکت/ایمیل).</summary>
    [RelayCommand]
    private async Task CopyAsync()
    {
        if (Info is null) return;
        try { System.Windows.Clipboard.SetText(Info.ToReportText()); await _dialogService.ShowInfoAsync("گزارشِ عیب‌یابی در کلیپ‌بورد کپی شد."); }
        catch (Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    /// <summary>ذخیرهٔ گزارش در «My Documents\SamaHesab\عیب‌یابی» و بازکردنِ آن.</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Info is null) return;
        try
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "عیب‌یابی");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            await System.IO.File.WriteAllTextAsync(path, Info.ToReportText(), new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}
