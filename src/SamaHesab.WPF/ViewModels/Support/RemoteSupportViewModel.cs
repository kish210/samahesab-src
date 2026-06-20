using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Support.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>
/// 🆘 HC-6b — پشتیبانیِ ریموت با RustDesk:
/// مشتری «کدِ پشتیبانی» + «شناسهٔ RustDesk» را می‌سازد؛ نشست به سرورِ vendor (وردپرس) ثبت می‌شود
/// تا کارشناس در داشبورد ببیند. سپس کارشناس با RustDesk به شناسهٔ مشتری وصل می‌شود (کد = تأییدِ هویت).
/// کنترلِ صفحه با خودِ RustDesk (متن‌باز، قابلِ self-host) انجام می‌شود — نه بازسازیِ آن.
/// </summary>
public partial class RemoteSupportViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly DiagnosticsCollector _collector;

    [ObservableProperty] private string? _currentCode;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private string _rustDeskId = string.Empty;   // شناسهٔ RustDeskِ مشتری
    [ObservableProperty] private RemoteSessionDto? _selected;
    [ObservableProperty] private string? _infoMessage;

    public bool HasCode => !string.IsNullOrEmpty(CurrentCode);
    partial void OnCurrentCodeChanged(string? value) => OnPropertyChanged(nameof(HasCode));

    public ObservableCollection<RemoteSessionDto> Sessions { get; } = new();

    public RemoteSupportViewModel(IMediator mediator, DiagnosticsCollector collector,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService) { _mediator = mediator; _collector = collector; }

    public override Task LoadAsync()
    {
        TryDetectRustDeskId();   // اگر RustDesk نصب است، شناسه را خودکار پر کن
        return RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var list = await _mediator.Send(new GetRemoteSessionsQuery());
        Sessions.Clear();
        foreach (var s in list) Sessions.Add(s);
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        await ExecuteAsync(async () =>
        {
            // عکسِ تشخیصی (فقط دادهٔ فنی) برای کارشناس همراهِ درخواست.
            string? diagJson = null;
            try { diagJson = JsonSerializer.Serialize(await _collector.CollectAsync()); } catch { }

            var res = await _mediator.Send(new GenerateSupportCodeCommand(
                string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
                string.IsNullOrWhiteSpace(RustDeskId) ? null : RustDeskId.Trim(),
                60, diagJson));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }

            CurrentCode = res.Value!.Code;
            InfoMessage = res.Value.Message + $"  (معتبر تا {res.Value.ExpiresAt:HH:mm})";

            // لاگِ نشست را هم محلی ذخیره می‌کنیم (شاملِ شناسهٔ RustDesk).
            try
            {
                var info = await _collector.CollectAsync();
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "پشتیبانی");
                Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(Path.Combine(dir, $"session_{res.Value.Code}.txt"),
                    $"نشستِ پشتیبانیِ {res.Value.Code}\nشروع: {DateTime.Now:yyyy-MM-dd HH:mm}\nشناسهٔ RustDesk: {RustDeskId}\nیادداشت: {Note}\n\n{info.ToReportText()}",
                    new System.Text.UTF8Encoding(true));
            }
            catch { /* best-effort */ }

            Note = string.Empty;
            await RefreshAsync();
        }, "در حال ساختِ درخواستِ پشتیبانی...");
    }

    [RelayCommand]
    private void CopyCode()
    {
        if (string.IsNullOrEmpty(CurrentCode)) return;
        try { System.Windows.Clipboard.SetText(CurrentCode); } catch { }
    }

    /// <summary>بازکردنِ RustDesk (اگر نصب است) وگرنه صفحهٔ دانلود.</summary>
    [RelayCommand]
    private void OpenRustDesk()
    {
        var exe = FindRustDesk();
        try
        {
            if (exe is not null) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            else Process.Start(new ProcessStartInfo("https://rustdesk.com/") { UseShellExecute = true });
        }
        catch { }
        TryDetectRustDeskId();
    }

    [RelayCommand]
    private async Task EndSessionAsync(RemoteSessionDto? session)
    {
        session ??= Selected;
        if (session is null) return;
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "SamaHesab", "پشتیبانی", $"session_{session.Code}.txt");
        await _mediator.Send(new EndRemoteSessionCommand(session.Id, File.Exists(logPath) ? logPath : null));
        if (CurrentCode == session.Code) CurrentCode = null;
        await RefreshAsync();
    }

    // ── RustDesk helpers ──
    private static string? FindRustDesk()
    {
        foreach (var p in new[]
        {
            @"C:\Program Files\RustDesk\rustdesk.exe",
            @"C:\Program Files (x86)\RustDesk\rustdesk.exe",
        })
            if (File.Exists(p)) return p;
        return null;
    }

    /// <summary>تلاش برای خواندنِ خودکارِ شناسهٔ RustDeskِ این دستگاه (`rustdesk --get-id`).</summary>
    private void TryDetectRustDeskId()
    {
        if (!string.IsNullOrWhiteSpace(RustDeskId)) return;
        var exe = FindRustDesk();
        if (exe is null) return;
        try
        {
            var psi = new ProcessStartInfo(exe, "--get-id")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            if (p is null) return;
            var id = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(3000);
            if (!string.IsNullOrWhiteSpace(id) && id.All(char.IsDigit)) RustDeskId = id;
        }
        catch { /* best-effort */ }
    }
}
