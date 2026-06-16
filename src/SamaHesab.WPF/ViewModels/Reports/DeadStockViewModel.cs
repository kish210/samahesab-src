using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Reports;

/// <summary>فاز ۱۲ (پولیش) — گزارشِ کالای راکد/کم‌گردش: سرمایهٔ خوابیده در انبار + خروجیِ اکسل/PDF.</summary>
public partial class DeadStockViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPersianCalendarService _calendar;
    private readonly IPdfService _pdf;

    public ObservableCollection<DeadStockRow> Rows { get; } = new();

    [ObservableProperty] private int _idleDays = 90;
    [ObservableProperty] private int _itemCount;
    [ObservableProperty] private decimal _idleValue;

    public DeadStockViewModel(IMediator mediator, IPersianCalendarService calendar, IPdfService pdf,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _calendar = calendar; _pdf = pdf; }

    public override Task LoadAsync() => RunAsync();

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var rows = await _mediator.Send(new GetDeadStockQuery(IdleDays <= 0 ? 90 : IdleDays, _calendar.GetCurrentPersianDate()));
            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);
            ItemCount = rows.Count;
            IdleValue = rows.Sum(r => r.Value);
        }, "در حال یافتنِ کالاهای راکد...");
    }

    private ReportTable BuildTable()
    {
        string N(decimal d) => d.ToString("N0");
        string Idle(int d) => d < 0 ? "بدونِ حرکت" : N(d);
        var headers = new[] { "کد", "نام کالا", "موجودی", "ارزش (ریال)", "آخرین حرکت", "روزِ رکود" };
        var rows = Rows.Select(r => new[] { r.Code, r.Name, N(r.Quantity), N(r.Value), r.LastMovement, Idle(r.IdleDays) }).ToList();
        rows.Add(new[] { "", "جمعِ سرمایهٔ خوابیده", "", N(IdleValue), "", "" });
        return new ReportTable($"کالاهای راکد (بیش از {IdleDays} روز بدونِ حرکت)", headers, rows);
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("موردی برای خروجی نیست."); return; }
        try { OpenFile(SaveBytes("csv", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToCsv(BuildTable())))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("موردی برای خروجی نیست."); return; }
        try
        {
            var g = AppSettingsStore.GetGeneral();
            var meta = new PdfMeta(g.CompanyName, $"کالاهای راکدِ بیش از {IdleDays} روز", _calendar.GetCurrentPersianDateTime(), Landscape: true);
            OpenFile(SaveBytes("pdf", _pdf.RenderTable(BuildTable(), meta)));
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task PrintAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("موردی برای خروجی نیست."); return; }
        try { OpenFile(SaveBytes("html", new System.Text.UTF8Encoding(true).GetBytes(ReportExporter.ToHtml(BuildTable())))); }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    private static string SaveBytes(string ext, byte[] content)
    {
        var dir = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
        System.IO.Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, $"کالای_راکد_{System.DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
        System.IO.File.WriteAllBytes(path, content);
        return path;
    }

    private static void OpenFile(string path)
        => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
}
