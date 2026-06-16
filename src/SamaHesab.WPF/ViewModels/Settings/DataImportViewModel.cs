using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Import;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.Generic;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Settings;

/// <summary>
/// فاز ۱۲ G4 — ورودِ دادهٔ مشتری/تأمین‌کننده از اکسل (مهاجرت از سیستمِ قبلی).
/// فایل → پیش‌نمایش → درجِ گروهی با commandهای Application (idempotent بر اساسِ کد).
/// </summary>
public partial class DataImportViewModel : BaseViewModel
{
    private readonly IExcelImportService _excel;
    private readonly IExcelExportService _exporter;
    private readonly IMediator _mediator;
    private IReadOnlyList<IReadOnlyDictionary<string, string>> _rows = new List<IReadOnlyDictionary<string, string>>();

    public List<string> EntityTypes { get; } = new() { "مشتری", "تأمین‌کننده", "کالا" };

    [ObservableProperty] private string _selectedEntityType = "مشتری";
    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private int _rowCount;
    [ObservableProperty] private string _previewText = string.Empty;
    [ObservableProperty] private string _resultText = string.Empty;
    [ObservableProperty] private bool _canImport;
    [ObservableProperty] private bool _resultIsError;   // POLISH-1: رنگِ نتیجه

    public DataImportViewModel(IExcelImportService excel, IExcelExportService exporter, IMediator mediator,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _excel = excel; _exporter = exporter; _mediator = mediator; }

    /// <summary>سرستون‌های نمونهٔ هر نوع (همان نگاشتِ import).</summary>
    private string[] TemplateHeaders => SelectedEntityType switch
    {
        "کالا" => new[] { "کد", "نام", "واحد", "قیمت فروش", "قیمت خرید", "قیمت عمده", "قیمت مصرف‌کننده", "مالیات", "بارکد" },
        "تأمین‌کننده" => new[] { "کد", "نام", "نام خانوادگی", "نام شرکت", "تلفن", "موبایل", "ایمیل", "استان", "شهر", "آدرس" },
        _ => new[] { "کد", "نام", "نام خانوادگی", "نام شرکت", "تلفن", "موبایل", "ایمیل", "استان", "شهر", "آدرس", "کد پستی", "کد ملی", "کد اقتصادی", "توضیحات" },
    };

    private object?[] TemplateSample => SelectedEntityType switch
    {
        "کالا" => new object?[] { "K1001", "خودکار آبی", "عدد", 25000, 18000, 22000, 25000, 9, "6261234567890" },
        "تأمین‌کننده" => new object?[] { "S1001", "علی", "احمدی", "", "02112345678", "09120000000", "a@x.com", "تهران", "تهران", "خ ولیعصر" },
        _ => new object?[] { "C1001", "رضا", "کریمی", "", "02112345678", "09120000000", "r@x.com", "تهران", "تهران", "خ آزادی", "1234567890", "", "0499370899", "" },
    };

    /// <summary>POLISH-1 — دانلودِ فایلِ نمونهٔ اکسل با سرستون‌های درست + یک ردیفِ مثال.</summary>
    [RelayCommand]
    private async System.Threading.Tasks.Task DownloadTemplateAsync()
    {
        try
        {
            var bytes = _exporter.Export("نمونه", TemplateHeaders, new[] { (IReadOnlyList<object?>)TemplateSample });
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "قالب‌ها");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"نمونهٔ ورودِ {SelectedEntityType}.xlsx");
            await System.IO.File.WriteAllBytesAsync(path, bytes);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            await _dialogService.ShowSuccessAsync($"فایلِ نمونه ساخته شد: {path}\nستون‌ها را پر کنید و همین فایل را وارد کنید.");
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync("خطا در ساختِ نمونه: " + ex.Message); }
    }

    public override System.Threading.Tasks.Task LoadAsync() => System.Threading.Tasks.Task.CompletedTask;

    public string ColumnHelp => SelectedEntityType switch
    {
        "کالا" => "ستون‌های قابلِ‌خواندن: کد · نام · واحد · قیمت فروش · قیمت خرید · قیمت عمده · قیمت مصرف‌کننده · مالیات · بارکد  (واحدِ ناشناخته → پیش‌فرضِ «عدد»).",
        "تأمین‌کننده" => "ستون‌های قابلِ‌خواندن: کد · نام · نام خانوادگی · نام شرکت · تلفن · موبایل · ایمیل · استان · شهر · آدرس.",
        _ => "ستون‌های قابلِ‌خواندن: کد · نام · نام خانوادگی · نام شرکت · تلفن · موبایل · ایمیل · استان · شهر · آدرس · کد پستی · کد ملی · کد اقتصادی · توضیحات.",
    };

    partial void OnSelectedEntityTypeChanged(string value) => OnPropertyChanged(nameof(ColumnHelp));

    /// <summary>از code-behind بعد از انتخابِ فایل صدا زده می‌شود.</summary>
    public void LoadFile(string path)
    {
        try
        {
            FilePath = path;
            _rows = _excel.ReadRows(path);
            RowCount = _rows.Count;
            ResultText = string.Empty;
            CanImport = RowCount > 0;

            if (RowCount == 0) { PreviewText = "هیچ سطرِ داده‌ای یافت نشد (سطرِ اول باید سرستون باشد)."; return; }

            var headers = _rows[0].Keys.ToList();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("سرستون‌ها: " + string.Join(" | ", headers));
            sb.AppendLine(new string('—', 40));
            foreach (var r in _rows.Take(5))
                sb.AppendLine(string.Join(" | ", headers.Select(h => r.TryGetValue(h, out var v) ? v : "")));
            if (RowCount > 5) sb.AppendLine($"… و {RowCount - 5} سطرِ دیگر");
            PreviewText = sb.ToString();
        }
        catch (System.Exception ex)
        {
            CanImport = false;
            PreviewText = "خطا در خواندنِ فایل: " + ex.Message;
        }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task ImportAsync()
    {
        if (_rows.Count == 0) { await _dialogService.ShowWarningAsync("ابتدا یک فایلِ اکسل انتخاب کنید."); return; }
        if (!await _dialogService.ConfirmAsync($"{_rows.Count} سطر به‌عنوانِ «{SelectedEntityType}» وارد شود؟ (کدهای تکراری رد می‌شوند)")) return;

        await ExecuteAsync(async () =>
        {
            ImportResult res = SelectedEntityType switch
            {
                "کالا" => await _mediator.Send(new ImportProductsCommand(_rows)),
                "تأمین‌کننده" => await _mediator.Send(new ImportSuppliersCommand(_rows)),
                _ => await _mediator.Send(new ImportCustomersCommand(_rows)),
            };

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"وارد شد: {res.Imported} · از قبل موجود: {res.Skipped} · ناموفق: {res.Failed}");
            foreach (var e in res.Errors) sb.AppendLine("• " + e);
            ResultText = sb.ToString();
            ResultIsError = res.Failed > 0 || res.Errors.Count > 0;
            await _dialogService.ShowSuccessAsync($"ورودِ داده پایان یافت — {res.Imported} مورد وارد شد.");
        }, "در حال ورودِ داده...");
    }
}
