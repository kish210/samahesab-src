using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>یک کاشیِ مرکزِ پشتیبانی. <see cref="Available"/>=false → «به‌زودی» (فازِ بعدی).</summary>
public sealed record HelpTile(string PageKey, string Title, string Subtitle, string Icon, bool Available);

/// <summary>
/// 🆘 HC-1 — داشبوردِ «مرکزِ پشتیبانی». نقطهٔ ورود به زیربخش‌ها (گزارشِ باگ/قابلیت/تیکت/دانشنامه/
/// یادداشتِ نسخه/عیب‌یابی/پشتیبانیِ ریموت/درخواست‌های من). در HC-1 فقط «عیب‌یابی» زنده است؛
/// بقیه در فازهای بعدی فعال می‌شوند.
/// </summary>
public partial class HelpCenterViewModel : BaseViewModel
{
    public ObservableCollection<HelpTile> Tiles { get; } = new()
    {
        new("Diagnostics",  "عیب‌یابیِ سیستم",  "وضعیتِ لایسنس، پایگاه‌داده، حافظه و ماژول‌ها", "Stethoscope",          true),
        new("BugReport",    "گزارشِ باگ",        "گزارشِ خطا با الصاقِ خودکارِ اطلاعاتِ سیستم",   "BugOutline",            true),
        new("FeatureRequest","درخواستِ قابلیت",  "پیشنهادِ قابلیتِ تازه برای محصول",            "LightbulbOnOutline",    true),
        new("SupportTicket", "تیکتِ پشتیبانی",   "ثبت و پیگیریِ درخواستِ پشتیبانی",             "TicketConfirmationOutline", true),
        new("KnowledgeBase", "دانشنامه",         "مقالات، پرسش‌های پرتکرار و راهنما",          "BookOpenPageVariantOutline", true),
        new("ReleaseNotes",  "یادداشت‌های نسخه", "تازه‌ها، رفعِ باگ‌ها و مشکلاتِ شناخته‌شده",    "Update",                true),
        new("RemoteSupport", "پشتیبانیِ ریموت",  "کدِ پشتیبانی برای دسترسیِ کارشناس",           "MonitorShare",          true),
        new("MyRequests",    "درخواست‌های من",   "وضعیتِ تیکت‌ها و گزارش‌های ارسالی",           "FormatListChecks",      true),
    };

    public HelpCenterViewModel(IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService) { }

    [RelayCommand]
    private async Task OpenAsync(HelpTile? tile)
    {
        if (tile is null) return;
        if (!tile.Available) { await _dialogService.ShowInfoAsync($"«{tile.Title}» در نسخهٔ بعدی فعال می‌شود."); return; }
        _navigationService.NavigateTo(tile.PageKey);
    }
}
