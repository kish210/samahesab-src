using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Documents;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Settings;

/// <summary>
/// فاز ۱۰ DT-4 — طراح/مدیریتِ قالبِ اسناد (Settings → Document Templates):
/// انتخابِ نوعِ سند → فهرستِ قالب‌ها → ساخت/ویرایش (Header/Body/Footer + کاغذ) + پیش‌نمایش با داده‌ی نمونه.
/// </summary>
public partial class DocumentTemplatesViewModel : BaseViewModel
{
    private readonly IMediator _mediator;

    public List<DocTypeOption> DocumentTypes { get; } = new()
    {
        new("SalesInvoice","فاکتور فروش"), new("PurchaseInvoice","فاکتور خرید"),
        new("SalesReturn","برگشت از فروش"), new("PurchaseReturn","برگشت از خرید"),
        new("Quotation","پیش‌فاکتور"), new("Proforma","پروفرما"),
        new("Receipt","رسید دریافت"), new("Payment","رسید پرداخت"),
        new("WarehouseReceipt","رسید انبار"), new("WarehouseIssue","حوالهٔ انبار"),
        new("InventoryTransfer","انتقالِ انبار"), new("ChequeReceipt","رسید چک"),
        new("ChequePayment","پرداخت چک"), new("Contract","قرارداد"),
        new("TourismVoucher","واچر گردشگری"), new("HotelVoucher","واچر هتل"),
        new("RestaurantReceipt","رسید رستوران"), new("PosReceipt","رسید صندوق"),
    };
    public List<string> PaperSizes { get; } = new() { "A4P", "A4L", "A5", "Thermal80", "Thermal58", "Custom" };

    public ObservableCollection<DocumentTemplateListDto> Templates { get; } = new();

    [ObservableProperty] private string _selectedType = "SalesInvoice";
    [ObservableProperty] private DocumentTemplateListDto? _selectedTemplate;

    // فرمِ ویرایش
    [ObservableProperty] private int? _editId;
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editPaperSize = "A4P";
    [ObservableProperty] private string? _editHeader;
    [ObservableProperty] private string _editBody = string.Empty;
    [ObservableProperty] private string? _editFooter;

    public DocumentTemplatesViewModel(IMediator mediator, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _mediator = mediator; }

    public override Task LoadAsync() => ReloadAsync();

    partial void OnSelectedTypeChanged(string value) => _ = ReloadAsync();

    private async Task ReloadAsync()
    {
        Templates.Clear();
        foreach (var t in await _mediator.Send(new GetDocumentTemplatesQuery(SelectedType))) Templates.Add(t);
    }

    partial void OnSelectedTemplateChanged(DocumentTemplateListDto? value) { if (value != null) _ = LoadIntoEditorAsync(value.Id); }

    private async Task LoadIntoEditorAsync(int id)
    {
        var full = await _mediator.Send(new GetDocumentTemplateQuery(id));
        if (full is null) return;
        EditId = full.Id; EditName = full.Name; EditPaperSize = full.PaperSize;
        EditHeader = full.HeaderHtml; EditBody = full.BodyHtml; EditFooter = full.FooterHtml;
    }

    [RelayCommand]
    private void NewTemplate()
    {
        EditId = null; EditName = string.Empty; EditPaperSize = "A4P";
        EditHeader = null; EditFooter = null;
        EditBody = "<div style=\"font-family:Tahoma;direction:rtl;padding:16px\">\n  <h2 style=\"text-align:center\">{InvoiceNumber}</h2>\n  <p>مشتری: {CustomerName} — تاریخ: {InvoiceDate}</p>\n  [[ROWS]]<div>{#}. {ProductName} × {Quantity} = {LineTotal}</div>[[/ROWS]]\n  <h3>جمع: {TotalAmount}</h3>\n</div>";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditName)) { await _dialogService.ShowWarningAsync("نامِ قالب الزامی است."); return; }
        if (string.IsNullOrWhiteSpace(EditBody)) { await _dialogService.ShowWarningAsync("بدنهٔ قالب الزامی است."); return; }
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new SaveDocumentTemplateCommand(EditId, SelectedType, EditName, EditPaperSize,
                EditHeader, EditBody, EditFooter));
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در ذخیرهٔ قالب."); return; }
            await _dialogService.ShowSuccessAsync($"قالبِ «{EditName}» ذخیره شد.");
            await ReloadAsync();
        }, "در حال ذخیرهٔ قالب...");
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (EditId is not int id) { await _dialogService.ShowWarningAsync("قالبی برای حذف انتخاب نشده."); return; }
        if (!await _dialogService.ConfirmAsync($"قالبِ «{EditName}» حذف شود؟")) return;
        await ExecuteAsync(async () =>
        {
            var r = await _mediator.Send(new DeleteDocumentTemplateCommand(id));
            if (!r.Succeeded) { await _dialogService.ShowErrorAsync(r.ErrorMessage ?? "خطا در حذف."); return; }
            NewTemplate();
            await ReloadAsync();
        }, "در حال حذف...");
    }

    /// <summary>پیش‌نمایشِ قالبِ فعلی با دادهٔ نمونه (HTMLِ قابل‌چاپ).</summary>
    [RelayCommand]
    private async Task PreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(EditBody)) { await _dialogService.ShowWarningAsync("بدنهٔ قالب خالی است."); return; }
        try
        {
            var fields = new Dictionary<string, string?>
            {
                ["InvoiceNumber"] = "F-1001", ["InvoiceDate"] = "1405/03/25", ["CustomerName"] = "مشتریِ نمونه",
                ["CustomerCode"] = "100", ["TotalAmount"] = "12,500,000", ["Tax"] = "1,125,000",
                ["Discount"] = "0", ["BranchName"] = "سما حساب",
            };
            var rows = new List<IReadOnlyDictionary<string, string?>>
            {
                new Dictionary<string, string?> { ["ProductName"]="کالا الف", ["Quantity"]="2", ["UnitPrice"]="1,000,000", ["LineTotal"]="2,000,000" },
                new Dictionary<string, string?> { ["ProductName"]="کالا ب",  ["Quantity"]="3", ["UnitPrice"]="3,500,000", ["LineTotal"]="10,500,000" },
            };
            var data = DocumentData.Of(fields, rows);
            var html = DocumentTemplateEngine.Render(EditHeader, data)
                     + DocumentTemplateEngine.Render(EditBody, data)
                     + DocumentTemplateEngine.Render(EditFooter, data);
            var dir = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "SamaHesab", "اسناد");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"preview_{System.DateTime.Now:yyyyMMdd_HHmmss}.html");
            System.IO.File.WriteAllText(path, html, new System.Text.UTF8Encoding(true));
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync(ex.Message); }
    }
}

public record DocTypeOption(string Id, string Name);
