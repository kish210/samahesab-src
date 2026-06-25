using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Treasury.Commands;
using SamaHesab.Application.Treasury.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Globalization;

namespace SamaHesab.WPF.ViewModels.Treasury;

/// <summary>
/// کار #۲۰ — فهرست دریافتنی‌ها/پرداختنی‌ها با «وصول/پرداخت سریع».
/// گردش‌کار وصول مطالبات: بدهکاران مرتب بر اساس مبلغ؛ یک کلیک = ثبت دریافت کامل،
/// یا مبلغ دلخواه با ورودی. همه از طریق `CreateReceiptCommand`/`CreatePaymentCommand` (سند خودکار).
/// </summary>
public partial class ReceivablesViewModel : BaseViewModel
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;
    private readonly IPersianCalendarService _calendar;
    private readonly IBarcodeService _barcode;

    public ObservableCollection<ReceivableDto> Receivables { get; } = new();
    public ObservableCollection<PayableDto> Payables { get; } = new();

    [ObservableProperty] private decimal _totalReceivable;
    [ObservableProperty] private decimal _totalPayable;
    [ObservableProperty] private ReceivableDto? _selectedReceivable;   // برای میان‌برهای کیبورد
    [ObservableProperty] private PayableDto? _selectedPayable;

    public ReceivablesViewModel(IMediator mediator, ICurrentUserService user,
        IPersianCalendarService calendar, IBarcodeService barcode,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _mediator = mediator; _user = user; _calendar = calendar; _barcode = barcode;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var recv = await _mediator.Send(new GetReceivablesQuery());
            var pay = await _mediator.Send(new GetPayablesQuery());
            Receivables.Clear(); foreach (var r in recv) Receivables.Add(r);
            Payables.Clear(); foreach (var p in pay) Payables.Add(p);
            TotalReceivable = recv.Sum(r => r.Balance);
            TotalPayable = pay.Sum(p => p.Balance);
            SelectedReceivable = Receivables.FirstOrDefault();   // انتخابِ خودکار برای کارِ کیبوردی
            SelectedPayable = Payables.FirstOrDefault();
        }, "در حال بارگیری مطالبات...");
    }

    [RelayCommand] private Task RefreshAsync() => LoadAsync();

    /// <summary>P2 — ارسالِ پیامکیِ یادآورِ بدهیِ معوق + چکِ سررسید به طرف‌حساب‌ها (اقدامِ صریحِ کاربر).
    /// ابتدا کوئریِ مونتاژ صدا زده می‌شود، خلاصه (تعداد/مبلغ) نمایش و تأیید گرفته می‌شود، سپس ارسال.</summary>
    [RelayCommand]
    private async Task SendOverdueRemindersAsync()
    {
        await ExecuteAsync(async () =>
        {
            var company = Services.AppSettingsStore.GetGeneral().CompanyName ?? "";
            var reminders = await _mediator.Send(new SamaHesab.Application.Automation.Queries.GetOverdueRemindersQuery(
                _calendar.GetCurrentPersianDate(), company));

            if (reminders.Count == 0)
            { await _dialogService.ShowInfoAsync("یادآوری برای ارسال نیست (طرف‌حسابی با بدهیِ معوق/چکِ سررسیدِ نزدیک و شمارهٔ موبایل یافت نشد)."); return; }

            var total = reminders.Sum(r => r.Amount);
            if (!await _dialogService.ConfirmAsync(
                $"ارسالِ {reminders.Count:#,##0} پیامکِ یادآور (جمعِ مبلغ: {total:#,##0} ریال) به طرف‌حساب‌ها؟",
                "ارسالِ یادآورِ پیامکی")) return;

            var res = await _mediator.Send(new SamaHesab.Application.Automation.Commands.SendOverdueRemindersCommand(reminders));
            if (res.Failed == 0)
                await _dialogService.ShowSuccessAsync($"{res.Sent:#,##0} پیامکِ یادآور با موفقیت ارسال شد.");
            else
                await _dialogService.ShowWarningAsync($"ارسال انجام شد: {res.Sent:#,##0} موفق، {res.Failed:#,##0} ناموفق (پیکربندیِ سامانهٔ پیامک را بررسی کنید).");
        }, "در حال ارسالِ یادآورها...");
    }

    // میان‌برهای کیبورد روی ردیفِ انتخاب‌شدهٔ هر گرید (Enter=کامل · Ctrl+Enter=مبلغِ دلخواه).
    [RelayCommand] private Task ReceiveFullSelectedAsync() => ReceiveFullAsync(SelectedReceivable);
    [RelayCommand] private Task ReceiveCustomSelectedAsync() => ReceiveCustomAsync(SelectedReceivable);
    [RelayCommand] private Task PayFullSelectedAsync() => PayFullAsync(SelectedPayable);
    [RelayCommand] private Task PayCustomSelectedAsync() => PayCustomAsync(SelectedPayable);

    [RelayCommand]
    private Task ReceiveFullAsync(ReceivableDto? r)
        => ReceiveAsync(r, r?.Balance ?? 0);

    [RelayCommand]
    private async Task ReceiveCustomAsync(ReceivableDto? r)
    {
        if (r is null) return;
        var input = await _dialogService.ShowInputAsync($"مبلغ دریافت از «{r.Name}» (مانده: {r.Balance:#,##0}):", "ثبت دریافت");
        if (TryAmount(input, out var amount)) await ReceiveAsync(r, amount);
    }

    private async Task ReceiveAsync(ReceivableDto? r, decimal amount)
    {
        if (r is null || amount <= 0) return;
        if (!await _dialogService.ConfirmAsync($"ثبت دریافت {amount:#,##0} ریال از «{r.Name}»؟")) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new CreateReceiptCommand(
                _user.BranchId ?? 1, 1, _calendar.GetCurrentPersianDate(), r.CustomerId, amount,
                "نقدی", $"وصول از فهرست دریافتنی‌ها"));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync($"دریافت ثبت شد (سند #{res.Value}).");
            await LoadAsync();
            await OfferReceiptPrintAsync("Receipt", res.Value.ToString(), r.Name, amount, "وصول از مشتری");
        }, "در حال ثبت دریافت...");
    }

    /// <summary>P1/DT-6 — پس از ثبتِ دریافت/پرداخت، چاپِ رسید/پرداختِ خزانه با قالبِ پویا (در صورتِ وجودِ قالب و تأییدِ کاربر).</summary>
    private async Task OfferReceiptPrintAsync(string docType, string docNumber, string partyName, decimal amount, string reason)
    {
        try
        {
            var tpls = await _mediator.Send(new SamaHesab.Application.Documents.GetDocumentTemplatesQuery(docType));
            if (tpls.Count == 0) return;
            if (!await _dialogService.ConfirmAsync("رسید چاپ شود؟", "چاپِ رسید")) return;

            var pick = tpls.FirstOrDefault(t => t.IsDefault) ?? tpls[0];
            var full = await _mediator.Send(new SamaHesab.Application.Documents.GetDocumentTemplateQuery(pick.Id));
            if (full is null) return;

            var g = Services.AppSettingsStore.GetGeneral();
            string N(decimal d) => d.ToString("#,##0");
            var fields = new Dictionary<string, string?>
            {
                ["DocNumber"] = docNumber, ["QrData"] = docNumber, ["QrImage"] = _barcode.QrImageHtml(docNumber),
                ["Date"] = _calendar.GetCurrentPersianDate(),
                ["PartyName"] = partyName, ["Reason"] = reason, ["AccountName"] = "صندوق",
                ["Amount"] = N(amount), ["AmountInWords"] = SafeWords(amount), ["Notes"] = reason,
                ["CompanyName"] = g.CompanyName, ["CompanyAddress"] = g.CompanyAddress, ["CompanyPhone"] = g.CompanyPhone,
                ["EconomicCode"] = g.CompanyEconomicCode, ["NationalId"] = g.CompanyNationalId, ["BranchName"] = "",
                ["PrintDate"] = _calendar.GetCurrentPersianDate(), ["PrintTime"] = DateTime.Now.ToString("HH:mm"),
            };
            var rows = new List<IReadOnlyDictionary<string, string?>>
            {
                new Dictionary<string, string?> { ["Description"] = reason, ["LineAmount"] = N(amount) }
            };
            var data = SamaHesab.Application.Documents.DocumentData.Of(fields, rows);
            var html = SamaHesab.Application.Documents.DocumentTemplateEngine.Render(full.HeaderHtml, data)
                     + SamaHesab.Application.Documents.DocumentTemplateEngine.Render(full.BodyHtml, data)
                     + SamaHesab.Application.Documents.DocumentTemplateEngine.Render(full.FooterHtml, data);

            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "اسناد");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"رسید_{docNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.html");
            System.IO.File.WriteAllText(path, html, new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { /* چاپِ رسید اختیاری است؛ خطا نباید جریانِ ثبت را خراب کند */ }
    }

    private string SafeWords(decimal amount)
    {
        try { return _calendar.NumberToWords(amount); } catch { return ""; }
    }

    [RelayCommand]
    private Task PayFullAsync(PayableDto? p) => PayAsync(p, p?.Balance ?? 0);

    [RelayCommand]
    private async Task PayCustomAsync(PayableDto? p)
    {
        if (p is null) return;
        var input = await _dialogService.ShowInputAsync($"مبلغ پرداخت به «{p.Name}» (مانده: {p.Balance:#,##0}):", "ثبت پرداخت");
        if (TryAmount(input, out var amount)) await PayAsync(p, amount);
    }

    private async Task PayAsync(PayableDto? p, decimal amount)
    {
        if (p is null || amount <= 0) return;
        if (!await _dialogService.ConfirmAsync($"ثبت پرداخت {amount:#,##0} ریال به «{p.Name}»؟")) return;
        await ExecuteAsync(async () =>
        {
            var res = await _mediator.Send(new CreatePaymentCommand(
                _user.BranchId ?? 1, 1, _calendar.GetCurrentPersianDate(), p.SupplierId, amount,
                "نقدی", $"پرداخت از فهرست پرداختنی‌ها"));
            if (!res.Succeeded) { await _dialogService.ShowErrorAsync(res.ErrorMessage); return; }
            await _dialogService.ShowSuccessAsync($"پرداخت ثبت شد (سند #{res.Value}).");
            await LoadAsync();
            await OfferReceiptPrintAsync("Payment", res.Value.ToString(), p.Name, amount, "پرداخت به تأمین‌کننده");
        }, "در حال ثبت پرداخت...");
    }

    private static bool TryAmount(string? s, out decimal amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return decimal.TryParse(s.Replace(",", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out amount) && amount > 0;
    }
}
