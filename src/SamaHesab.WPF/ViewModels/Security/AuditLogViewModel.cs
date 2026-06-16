using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;
using SamaHesab.Application.Security.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Security;

/// <summary>T20 — مشاهدهٔ لاگِ حسابرسی (audit trail): فیلترِ بازه/عمل + خروجی اکسل.</summary>
public partial class AuditLogViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly IPdfService _pdf;
    private readonly List<AuditLogDto> _all = new();   // P-G10 — backing list (خروجی همه را شامل می‌شود؛ گرید صفحه‌بندی می‌شود)

    public ObservableCollection<AuditLogDto> Rows { get; } = new();

    [ObservableProperty] private int _daysBack = 30;
    [ObservableProperty] private string _actionFilter = string.Empty;
    [ObservableProperty] private int _rowCount;

    // P-G10 — صفحه‌بندیِ سمتِ کلاینت
    private const int PageSizeConst = 50;
    [ObservableProperty] private int _pageNumber = 1;
    [ObservableProperty] private int _totalPages = 1;
    public bool CanPrevPage => PageNumber > 1;
    public bool CanNextPage => PageNumber < TotalPages;
    partial void OnPageNumberChanged(int value) => RaisePaging();
    partial void OnTotalPagesChanged(int value) => RaisePaging();
    private void RaisePaging() { OnPropertyChanged(nameof(CanPrevPage)); OnPropertyChanged(nameof(CanNextPage)); }

    [RelayCommand] private void FirstPage() { if (PageNumber != 1) { PageNumber = 1; ApplyPage(); } }
    [RelayCommand] private void PrevPage()  { if (CanPrevPage) { PageNumber--; ApplyPage(); } }
    [RelayCommand] private void NextPage()  { if (CanNextPage) { PageNumber++; ApplyPage(); } }
    [RelayCommand] private void LastPage()  { if (PageNumber != TotalPages) { PageNumber = TotalPages; ApplyPage(); } }

    public AuditLogViewModel(IMediator mediator, IPdfService pdf,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; _pdf = pdf; }

    public override Task LoadAsync() => RunAsync();

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var rows = await _mediator.Send(new GetAuditLogQuery(
                DaysBack: DaysBack <= 0 ? 30 : DaysBack,
                Action: string.IsNullOrWhiteSpace(ActionFilter) ? null : ActionFilter.Trim()));
            _all.Clear();
            _all.AddRange(rows);
            RowCount = _all.Count;
            PageNumber = 1;
            ApplyPage();
        }, "در حال بارگیری لاگِ حسابرسی...");
    }

    private void ApplyPage()
    {
        TotalPages = System.Math.Max(1, (int)System.Math.Ceiling(_all.Count / (double)PageSizeConst));
        if (PageNumber > TotalPages) PageNumber = TotalPages;
        if (PageNumber < 1) PageNumber = 1;
        Rows.Clear();
        foreach (var r in _all.Skip((PageNumber - 1) * PageSizeConst).Take(PageSizeConst)) Rows.Add(r);
        RaisePaging();
    }

    // خروجی (CSV/PDF) همهٔ ردیف‌های بارگذاری‌شده را شامل می‌شود، نه فقط صفحهٔ جاری.
    private ReportTable BuildTable() => new("لاگِ حسابرسی",
        new[] { "تاریخ", "کاربر", "عمل", "جدول", "جزئیات" },
        _all.Select(r => new[] { r.When, r.User ?? "—", r.Action, r.TableName ?? "—", r.Details ?? "" }).ToList());

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try
        {
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"حسابرسی_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            System.IO.File.WriteAllText(path, ReportExporter.ToCsv(BuildTable()), new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (Rows.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا گزارش را تهیه کنید."); return; }
        try
        {
            var g = AppSettingsStore.GetGeneral();
            var meta = new PdfMeta(g.CompanyName, $"{DaysBack} روزِ اخیر", System.DateTime.Now.ToString("yyyy/MM/dd HH:mm"), Landscape: true);
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "گزارش‌ها");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"حسابرسی_{System.DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            System.IO.File.WriteAllBytes(path, _pdf.RenderTable(BuildTable(), meta));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}
