using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Restaurant;

/// <summary>
/// نمایشگر آشپزخانه (Kitchen Display System): رسیدهای فعال را نشان می‌دهد و آشپز
/// وضعیت آن‌ها را پیش می‌برد (در حال آماده‌سازی → آماده → تحویل). رفرش خودکار دارد.
/// </summary>
public partial class KitchenViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    public ObservableCollection<KitchenTicketVM> Tickets { get; } = new();

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private string _currentTime = string.Empty;

    public KitchenViewModel(ApiClient api, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _api = api;
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _timer.Tick += async (_, _) => { CurrentTime = DateTime.Now.ToString("HH:mm:ss"); await RefreshAsync(); };
        _timer.Start();
    }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var board = await _api.GetKitchenBoardAsync();
        Tickets.Clear();
        foreach (var t in board) Tickets.Add(new KitchenTicketVM(t));
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        StatusText = Tickets.Count == 0 ? "سفارش فعالی در آشپزخانه نیست" : $"{Tickets.Count} رسید فعال";
    }

    [RelayCommand]
    private async Task AdvanceAsync(KitchenTicketVM? ticket)
    {
        if (ticket is null || ticket.NextStatus == 0) return;
        var (ok, error) = await _api.AdvanceKitchenTicketAsync(ticket.Id, ticket.NextStatus);
        if (!ok) { await _dialogService.ShowErrorAsync(error ?? "خطا در تغییر وضعیت."); return; }
        await RefreshAsync();
    }
}

public partial class KitchenTicketVM : ObservableObject
{
    public int Id { get; }
    public string TicketNumber { get; }
    public string TableName { get; }
    public string Status { get; }
    public int StatusCode { get; }      // 0=New 1=Preparing 2=Ready 3=Completed
    public DateTime CreatedAt { get; }
    public ObservableCollection<string> Lines { get; } = new();

    public int NextStatus { get; }      // وضعیت بعدی برای دکمه
    public string NextLabel { get; }

    public KitchenTicketVM(ApiKitchenTicket t)
    {
        Id = t.Id;
        TicketNumber = t.TicketNumber;
        TableName = string.IsNullOrWhiteSpace(t.TableName) ? "بیرون‌بر" : t.TableName!;
        Status = t.Status;
        StatusCode = t.StatusCode;
        CreatedAt = t.CreatedAt;
        foreach (var i in t.Items)
            Lines.Add($"{i.Quantity:0.##}× {i.ProductName}" + (string.IsNullOrWhiteSpace(i.Notes) ? "" : $"  ({i.Notes})"));

        (NextStatus, NextLabel) = StatusCode switch
        {
            0 => (1, "شروع آماده‌سازی"),
            1 => (2, "آماده شد"),
            2 => (3, "تحویل داده شد"),
            _ => (0, "—")
        };
    }
}
