using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Support.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>
/// 🆘 HC-6 — پشتیبانیِ ریموت: تولیدِ «کدِ پشتیبانیِ» یک‌بارمصرف برای کارشناس + ثبتِ نشست و لاگ.
/// (بدونِ کنترلِ ریموتِ واقعی — کد/نشست/لاگ برای هماهنگی و ردگیری.)
/// </summary>
public partial class RemoteSupportViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly DiagnosticsCollector _collector;

    [ObservableProperty] private string? _currentCode;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private RemoteSessionDto? _selected;
    [ObservableProperty] private string? _infoMessage;

    public bool HasCode => !string.IsNullOrEmpty(CurrentCode);
    partial void OnCurrentCodeChanged(string? value) => OnPropertyChanged(nameof(HasCode));

    public ObservableCollection<RemoteSessionDto> Sessions { get; } = new();

    public RemoteSupportViewModel(IMediator mediator, DiagnosticsCollector collector,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService) { _mediator = mediator; _collector = collector; }

    public override Task LoadAsync() => RefreshAsync();

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
            var res = await _mediator.Send(new GenerateSupportCodeCommand(string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }

            CurrentCode = res.Value!.Code;
            InfoMessage = $"این کد را به کارشناسِ پشتیبانی بدهید. تا {res.Value.ExpiresAt:HH:mm} معتبر است.";

            // لاگِ نشست (فقط دادهٔ فنی) را روی دیسک ذخیره می‌کنیم.
            try
            {
                var info = await _collector.CollectAsync();
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "پشتیبانی");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"session_{res.Value.Code}.txt");
                await File.WriteAllTextAsync(path,
                    $"نشستِ پشتیبانیِ {res.Value.Code}\nشروع: {DateTime.Now:yyyy-MM-dd HH:mm}\nیادداشت: {Note}\n\n{info.ToReportText()}",
                    new System.Text.UTF8Encoding(true));
            }
            catch { /* لاگ best-effort است */ }

            Note = string.Empty;
            await RefreshAsync();
        }, "در حال تولیدِ کدِ پشتیبانی...");
    }

    [RelayCommand]
    private void CopyCode()
    {
        if (string.IsNullOrEmpty(CurrentCode)) return;
        try { System.Windows.Clipboard.SetText(CurrentCode); } catch { }
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
}
