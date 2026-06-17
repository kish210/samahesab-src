using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Support;
using SamaHesab.Application.Support.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Support;

/// <summary>🆘 HC-5 — دانشنامه (مقاله/FAQ/راهنما/ویدئو) از وردپرس، با جست‌وجو و کَشِ محلی.</summary>
public partial class KnowledgeBaseViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private ArticleDto? _selected;
    [ObservableProperty] private string _statusText = string.Empty;

    public ObservableCollection<ArticleDto> Articles { get; } = new();

    public bool HasUrl => !string.IsNullOrWhiteSpace(Selected?.Url);
    partial void OnSelectedChanged(ArticleDto? value) => OnPropertyChanged(nameof(HasUrl));

    public KnowledgeBaseViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService) { _mediator = mediator; }

    public override Task LoadAsync() => SearchArticlesAsync();

    [RelayCommand]
    private async Task SearchArticlesAsync()
    {
        await ExecuteAsync(async () =>
        {
            var list = await _mediator.Send(new SyncKnowledgeArticlesCommand(string.IsNullOrWhiteSpace(Search) ? null : Search.Trim()));
            Articles.Clear();
            foreach (var a in list) Articles.Add(a);
            Selected = Articles.FirstOrDefault();
            StatusText = Articles.Count == 0
                ? "مقاله‌ای یافت نشد. اگر سرورِ پشتیبانی پیکربندی شود، دانشنامه از kishwifi.com بارگذاری می‌شود."
                : $"{Articles.Count} مقاله";
        }, "در حال جست‌وجوی دانشنامه...");
    }

    [RelayCommand]
    private void OpenUrl()
    {
        if (string.IsNullOrWhiteSpace(Selected?.Url)) return;
        try { Process.Start(new ProcessStartInfo(Selected!.Url!) { UseShellExecute = true }); } catch { }
    }
}
