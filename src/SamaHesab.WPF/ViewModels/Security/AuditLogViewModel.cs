using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
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

    public ObservableCollection<AuditLogDto> Rows { get; } = new();

    [ObservableProperty] private int _daysBack = 30;
    [ObservableProperty] private string _actionFilter = string.Empty;
    [ObservableProperty] private int _rowCount;

    public AuditLogViewModel(IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => RunAsync();

    [RelayCommand]
    private async Task RunAsync()
    {
        await ExecuteAsync(async () =>
        {
            var rows = await _mediator.Send(new GetAuditLogQuery(
                DaysBack: DaysBack <= 0 ? 30 : DaysBack,
                Action: string.IsNullOrWhiteSpace(ActionFilter) ? null : ActionFilter.Trim()));
            Rows.Clear();
            foreach (var r in rows) Rows.Add(r);
            RowCount = Rows.Count;
        }, "در حال بارگیری لاگِ حسابرسی...");
    }

    private ReportTable BuildTable() => new("لاگِ حسابرسی",
        new[] { "تاریخ", "کاربر", "عمل", "جدول", "جزئیات" },
        Rows.Select(r => new[] { r.When, r.User ?? "—", r.Action, r.TableName ?? "—", r.Details ?? "" }).ToList());

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
}
