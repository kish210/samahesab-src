using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Automation;
using SamaHesab.Application.Automation.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Automation;

/// <summary>
/// کار #۲۵ — مرکزِ اعلان‌های عملیاتی: مصرف‌کنندهٔ <see cref="GetAlertsQuery"/>/AlertEngine
/// (چکِ سررسید · کسریِ موجودی · بدهیِ معوق · انقضای موجودی). فهرستِ مرتب‌شده بر اساسِ شدت.
/// </summary>
public partial class AlertsViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;

    public ObservableCollection<AlertRow> Alerts { get; } = new();

    [ObservableProperty] private int _criticalCount;
    [ObservableProperty] private int _warningCount;
    [ObservableProperty] private int _infoCount;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private bool _isEmpty;

    public AlertsViewModel(IMediator mediator, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; }

    public override Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await ExecuteAsync(async () =>
        {
            var today = _calendar.GetCurrentPersianDate();
            var alerts = await _mediator.Send(new GetAlertsQuery(today));

            Alerts.Clear();
            foreach (var a in alerts) Alerts.Add(AlertRow.From(a));

            CriticalCount = alerts.Count(a => a.Severity == AlertSeverity.Critical);
            WarningCount  = alerts.Count(a => a.Severity == AlertSeverity.Warning);
            InfoCount     = alerts.Count(a => a.Severity == AlertSeverity.Info);
            TotalCount    = alerts.Count;
            IsEmpty       = TotalCount == 0;
        }, "در حال بارگیریِ اعلان‌ها...");
    }
}

/// <summary>ردیفِ نمایشیِ اعلان (آیکون/رنگ/برچسبِ نوع بر اساسِ شدت و Kind).</summary>
public class AlertRow
{
    public string Icon { get; init; } = "";
    public System.Windows.Media.Brush Accent { get; init; } = System.Windows.Media.Brushes.SteelBlue;
    public string SeverityText { get; init; } = "";
    public string KindLabel { get; init; } = "";
    public string Title { get; init; } = "";
    public string AmountText { get; init; } = "";

    public static AlertRow From(Alert a)
    {
        var (icon, hex, sev) = a.Severity switch
        {
            AlertSeverity.Critical => ("🔴", "#DC2626", "بحرانی"),
            AlertSeverity.Warning  => ("🟡", "#D97706", "هشدار"),
            _                       => ("🔵", "#2563EB", "اطلاع"),
        };
        return new AlertRow
        {
            Icon = icon,
            Accent = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex)!,
            SeverityText = sev,
            KindLabel = KindFa(a.Kind),
            Title = a.Title,
            AmountText = a.Amount > 0 ? Fa(a.Amount.ToString("#,##0", CultureInfo.InvariantCulture)) + " ریال" : "",
        };
    }

    private static string KindFa(string kind) => kind switch
    {
        "ChequeOverdue"      => "چکِ سررسیدگذشته",
        "ChequeDueToday"     => "چکِ سررسیدِ امروز",
        "OutOfStock"         => "اتمامِ موجودی",
        "LowStock"           => "کسریِ موجودی",
        "OverdueReceivable"  => "بدهیِ معوق",
        "ReceivableDueToday" => "سررسیدِ امروزِ دریافت",
        "Expired"            => "کالای منقضی",
        "ExpiringSoon"       => "نزدیکِ انقضا",
        _                     => kind,
    };

    private static string Fa(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) sb.Append(c >= '0' && c <= '9' ? (char)('۰' + (c - '0')) : c);
        return sb.ToString();
    }
}
