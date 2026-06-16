using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Globalization;

namespace SamaHesab.WPF.ViewModels.Accounting;

public partial class ChequeListViewModel : BaseViewModel
{
    private readonly IChequeRepository _chequeRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;
    private readonly IBarcodeService _barcode;

    // فیلترها (سمتِ کلاینت روی لیستِ بارگذاری‌شده تا کارت/چیپ/جستجو لحظه‌ای باشد)
    [ObservableProperty] private string _statusFilter = "همه";   // کلیدِ کارتِ انتخاب‌شده
    [ObservableProperty] private string _typeFilter = "دریافتی"; // چیپ دریافتنی/پرداختنی
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private ChequeListRow? _selectedCheque; // ردیفِ انتخاب‌شدهٔ گرید

    // جمع‌های نوار پایین
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private decimal _totalAmount;

    // آمارِ کارت‌ها (تعداد/مبلغ به تفکیکِ وضعیت)
    [ObservableProperty] private int _allCount; [ObservableProperty] private decimal _allSum;
    [ObservableProperty] private int _inProcessCount; [ObservableProperty] private decimal _inProcessSum;
    [ObservableProperty] private int _transferredCount; [ObservableProperty] private decimal _transferredSum;
    [ObservableProperty] private int _clearedCount; [ObservableProperty] private decimal _clearedSum;
    [ObservableProperty] private int _returnedCount; [ObservableProperty] private decimal _returnedSum;

    // سررسیدِ هفتهٔ جاری (نوار تولبار)
    [ObservableProperty] private int _dueWeekCount; [ObservableProperty] private decimal _dueWeekSum;

    private readonly List<ChequeListRow> _all = new();
    public ObservableCollection<ChequeListRow> Cheques { get; } = new();

    public ChequeListViewModel(IChequeRepository chequeRepository, ICurrentUserService currentUser,
        IPersianCalendarService calendar, IMediator mediator, IBarcodeService barcode,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _chequeRepository = chequeRepository;
        _currentUser = currentUser;
        _calendar = calendar;
        _mediator = mediator;
        _barcode = barcode;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var companyId = _currentUser.CompanyId!.Value;
            var cheques = await _chequeRepository.FindAsync(c => c.CompanyId == companyId);

            _all.Clear();
            foreach (var c in cheques)
                _all.Add(ChequeListRow.From(c.Id, c.ChequeType, c.ChequeNumber, c.BankName, c.Amount,
                    c.DueDate, c.Status, c.IssuedBy ?? "", c.Description ?? ""));

            RecomputeStats();
            ApplyFilter();
        }, "در حال بارگذاری چک‌ها...");
        await LoadPrintTemplatesAsync();
    }

    /// <summary>P1/DT-6 — قالب‌های چاپِ چک (دریافتی=ChequeReceipt · پرداختی=ChequePayment).</summary>
    public ObservableCollection<Application.Documents.DocumentTemplateListDto> PrintTemplates { get; } = new();

    private async Task LoadPrintTemplatesAsync()
    {
        var docType = TypeFilter == "پرداختی" ? "ChequePayment" : "ChequeReceipt";
        PrintTemplates.Clear();
        try { foreach (var t in await _mediator.Send(new Application.Documents.GetDocumentTemplatesQuery(docType))) PrintTemplates.Add(t); }
        catch { /* قالب اختیاری است */ }
    }

    partial void OnTypeFilterChanged(string value) => _ = LoadPrintTemplatesAsync();

    /// <summary>P1/DT-6 — چاپِ چکِ انتخاب‌شده با قالبِ پویا.</summary>
    [RelayCommand]
    private async Task PrintWithTemplateAsync(Application.Documents.DocumentTemplateListDto? tpl)
    {
        if (tpl is null) return;
        if (SelectedCheque is not { } c) { await _dialogService.ShowErrorAsync("ابتدا یک چک را از لیست انتخاب کنید."); return; }
        try
        {
            var full = await _mediator.Send(new Application.Documents.GetDocumentTemplateQuery(tpl.Id));
            if (full is null) { await _dialogService.ShowErrorAsync("قالب یافت نشد."); return; }

            var g = Services.AppSettingsStore.GetGeneral();
            string N(decimal d) => d.ToString("#,##0");
            var fields = new Dictionary<string, string?>
            {
                ["ChequeNumber"] = c.Number, ["DocNumber"] = c.Number, ["QrData"] = c.Number,
                ["QrImage"] = _barcode.QrImageHtml(c.Number),
                ["Date"] = _calendar.GetCurrentPersianDate(), ["DueDate"] = c.DueDate,
                ["BankName"] = c.Bank, ["PartyName"] = c.IssuedBy, ["AccountName"] = c.Bank,
                ["Amount"] = N(c.Amount), ["Reason"] = c.Reference, ["Notes"] = c.Reference,
                ["CompanyName"] = g.CompanyName, ["CompanyAddress"] = g.CompanyAddress, ["CompanyPhone"] = g.CompanyPhone,
                ["EconomicCode"] = g.CompanyEconomicCode, ["NationalId"] = g.CompanyNationalId, ["BranchName"] = "",
                ["PrintDate"] = _calendar.GetCurrentPersianDate(), ["PrintTime"] = DateTime.Now.ToString("HH:mm"),
            };
            var rows = new List<IReadOnlyDictionary<string, string?>>
            {
                new Dictionary<string, string?> { ["Description"] = c.Reference, ["LineAmount"] = N(c.Amount) }
            };
            var data = Application.Documents.DocumentData.Of(fields, rows);
            var html = Application.Documents.DocumentTemplateEngine.Render(full.HeaderHtml, data)
                     + Application.Documents.DocumentTemplateEngine.Render(full.BodyHtml, data)
                     + Application.Documents.DocumentTemplateEngine.Render(full.FooterHtml, data);

            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "اسناد");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"چک_{c.Number}_{tpl.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            System.IO.File.WriteAllText(path, html, new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }

    private void RecomputeStats()
    {
        // آمار بر اساسِ نوعِ انتخاب‌شده (دریافتنی/پرداختنی) محاسبه می‌شود تا با گریدِ فیلترشده هماهنگ باشد.
        var scope = _all.Where(c => c.TypeKey == TypeFilter).ToList();
        AllCount = scope.Count; AllSum = scope.Sum(c => c.Amount);
        InProcessCount = scope.Count(c => c.StatusKey == "InProcess"); InProcessSum = scope.Where(c => c.StatusKey == "InProcess").Sum(c => c.Amount);
        TransferredCount = scope.Count(c => c.StatusKey == "Transferred"); TransferredSum = scope.Where(c => c.StatusKey == "Transferred").Sum(c => c.Amount);
        ClearedCount = scope.Count(c => c.StatusKey == "Cleared"); ClearedSum = scope.Where(c => c.StatusKey == "Cleared").Sum(c => c.Amount);
        ReturnedCount = scope.Count(c => c.StatusKey == "Returned"); ReturnedSum = scope.Where(c => c.StatusKey == "Returned").Sum(c => c.Amount);
        DueWeekCount = scope.Count(c => c.IsDueSoon); DueWeekSum = scope.Where(c => c.IsDueSoon).Sum(c => c.Amount);
    }

    private void ApplyFilter()
    {
        IEnumerable<ChequeListRow> q = _all.Where(c => c.TypeKey == TypeFilter);
        if (StatusFilter != "همه")
            q = q.Where(c => c.StatusKey == StatusFilter);
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var s = SearchText.Trim();
            q = q.Where(c => c.Number.Contains(s) || c.IssuedBy.Contains(s) || c.Bank.Contains(s));
        }
        Cheques.Clear();
        int i = 1;
        foreach (var c in q.OrderBy(c => c.DueDate)) { c.RowNumber = i++; Cheques.Add(c); }
        TotalCount = Cheques.Count;
        TotalAmount = Cheques.Sum(c => c.Amount);
    }

    partial void OnSearchTextChanged(string? value) => ApplyFilter();

    /// <summary>انتخابِ کارتِ وضعیت (فیلترِ گرید بر اساسِ وضعیت).</summary>
    [RelayCommand]
    private void SelectStatus(string? key) { StatusFilter = key ?? "همه"; ApplyFilter(); }

    /// <summary>تغییرِ چیپ دریافتنی/پرداختنی.</summary>
    [RelayCommand]
    private void SelectType(string? key)
    {
        TypeFilter = key ?? "دریافتی";
        StatusFilter = "همه";
        RecomputeStats();
        ApplyFilter();
    }

    [RelayCommand] private async Task SearchAsync() => await LoadAsync();
    [RelayCommand] private void NewCheque() { }

    /// <summary>وصولِ چکِ انتخاب‌شده (سند حسابداری خودکار در `ChangeChequeStatusCommand`).</summary>
    [RelayCommand]
    private async Task ClearChequeAsync()
    {
        if (SelectedCheque is not { } c) { await _dialogService.ShowErrorAsync("ابتدا یک چک را از لیست انتخاب کنید."); return; }
        if (!await _dialogService.ConfirmAsync($"وصول چک {c.Number} به مبلغ {c.Amount:#,##0} ریال؟", "وصول چک")) return;
        await ChangeStatusAsync(c.Id, ChequeAction.Clear, null, "وصول چک ثبت شد.");
    }

    /// <summary>واگذاریِ چکِ انتخاب‌شده به بانک.</summary>
    [RelayCommand]
    private async Task TransferChequeAsync()
    {
        if (SelectedCheque is not { } c) { await _dialogService.ShowErrorAsync("ابتدا یک چک را از لیست انتخاب کنید."); return; }
        if (!await _dialogService.ConfirmAsync($"چک {c.Number} به بانک واگذار شود؟", "واگذاری به بانک")) return;
        await ChangeStatusAsync(c.Id, ChequeAction.Transfer, null, "واگذاری چک به بانک ثبت شد.");
    }

    /// <summary>برگشتِ چکِ انتخاب‌شده (با علت).</summary>
    [RelayCommand]
    private async Task ReturnChequeAsync()
    {
        if (SelectedCheque is not { } c) { await _dialogService.ShowErrorAsync("ابتدا یک چک را از لیست انتخاب کنید."); return; }
        var reason = await _dialogService.ShowInputAsync($"علت برگشت چک {c.Number}:", "برگشت چک");
        if (string.IsNullOrWhiteSpace(reason)) return;
        await ChangeStatusAsync(c.Id, ChequeAction.Return, reason, "برگشت چک ثبت شد.");
    }

    private async Task ChangeStatusAsync(int id, ChequeAction action, string? reason, string okMsg)
    {
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new ChangeChequeStatusCommand(id, action, _calendar.GetCurrentPersianDate(), reason));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync(okMsg);
            await LoadAsync();   // آمار/گرید پس از تغییر وضعیت تازه‌سازی می‌شود
        }, "در حال ثبت...");
    }
}

/// <summary>سطرِ نمایشِ چک با مشتقاتِ UI (کلیدِ وضعیت/نوع، رنگِ چیپ، مانده تا سررسید).</summary>
public partial class ChequeListRow : ObservableObject
{
    [ObservableProperty] private int _rowNumber;
    public int Id { get; init; }
    public string Number { get; init; } = "";
    public string Bank { get; init; } = "";
    public decimal Amount { get; init; }
    public string DueDate { get; init; } = "";
    public string IssuedBy { get; init; } = "";
    public string Reference { get; init; } = "";

    public string StatusKey { get; init; } = "";   // InProcess/Cleared/Returned/Transferred/Cancelled
    public string Status { get; init; } = "";       // برچسبِ فارسی
    public string TypeKey { get; init; } = "";      // دریافتی/پرداختی
    public string ChipKind { get; init; } = "n";    // a/b/g/r/n برای رنگِ چیپِ نقطه‌دار
    public string DaysLeftText { get; init; } = "—";
    public bool IsDueSoon { get; init; }

    public static ChequeListRow From(int id, ChequeType type, string number, string bank, decimal amount,
        string dueDate, ChequeStatus status, string issuedBy, string reference)
    {
        var (faStatus, chip) = status switch
        {
            ChequeStatus.InProcess   => ("در جریان", "a"),
            ChequeStatus.Cleared     => ("وصول شده", "g"),
            ChequeStatus.Returned    => ("برگشتی", "r"),
            ChequeStatus.Transferred => ("واگذار به بانک", "b"),
            ChequeStatus.Cancelled   => ("ابطال شده", "n"),
            _ => ("نامشخص", "n")
        };
        // مانده تا سررسید فقط برای چک‌های بازِ (در جریان/واگذارشده) محاسبه می‌شود.
        var open = status is ChequeStatus.InProcess or ChequeStatus.Transferred;
        var days = open ? DaysUntil(dueDate) : (int?)null;
        var dueSoon = days is >= 0 and <= 7;
        var daysText = days is null ? "—" : days < 0 ? "گذشته" : $"{ToFa(days.Value)} روز";

        return new ChequeListRow
        {
            Id = id, Number = number, Bank = bank, Amount = amount, DueDate = dueDate,
            IssuedBy = issuedBy, Reference = reference,
            StatusKey = status.ToString(), Status = faStatus, ChipKind = chip,
            TypeKey = type == ChequeType.Received ? "دریافتی" : "پرداختی",
            DaysLeftText = daysText, IsDueSoon = dueSoon
        };
    }

    /// <summary>اختلافِ روزِ تاریخِ شمسیِ «yyyy/MM/dd» تا امروز (با تقویمِ شمسی).</summary>
    private static int? DaysUntil(string shamsi)
    {
        var parts = shamsi.Split('/');
        if (parts.Length != 3) return null;
        if (!int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var d))
            return null;
        try
        {
            var pc = new PersianCalendar();
            var due = pc.ToDateTime(y, m, d, 0, 0, 0, 0).Date;
            return (int)(due - DateTime.Today).TotalDays;
        }
        catch { return null; }
    }

    private static string ToFa(int n)
    {
        var map = new[] { '۰','۱','۲','۳','۴','۵','۶','۷','۸','۹' };
        return string.Concat(n.ToString().Select(ch => char.IsDigit(ch) ? map[ch - '0'] : ch));
    }
}
