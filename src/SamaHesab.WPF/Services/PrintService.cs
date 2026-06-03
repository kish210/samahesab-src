using System.Globalization;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SamaHesab.WPF.Services;

public record PrintLine(int Row, string Code, string Name, decimal Qty, decimal UnitPrice, decimal Discount, decimal Net);

public record PrintDocumentData(
    string DocTitle, string Number, string Date,
    string PartyLabel, string PartyName,
    IReadOnlyList<PrintLine> Lines,
    decimal SubTotal, decimal Discount, decimal Tax, decimal Shipping,
    decimal GrandTotal, decimal Paid, decimal Remain, string? Description);

public interface IPrintService
{
    void PrintInvoice(PrintDocumentData data);
    void PrintReceipt(PrintDocumentData data);
    void Preview(PrintDocumentData data);
}

public class PrintService : IPrintService
{
    private static FontFamily Vazir =>
        (FontFamily?)System.Windows.Application.Current.TryFindResource("VazirFont") ?? new FontFamily("Tahoma");

    private static string Money(decimal v) => v.ToString("#,##0", CultureInfo.InvariantCulture);

    // ── public entry points ──
    public void PrintInvoice(PrintDocumentData data)
    {
        var s = AppSettingsStore.GetPrintSettings();
        var doc = Build(data, s, receipt: s.Paper == PaperKind.Receipt80mm);
        Send(doc, s);
    }

    public void PrintReceipt(PrintDocumentData data)
    {
        var s = AppSettingsStore.GetPrintSettings();
        var doc = Build(data, s, receipt: true);
        Send(doc, s);
    }

    public void Preview(PrintDocumentData data)
    {
        var s = AppSettingsStore.GetPrintSettings();
        var doc = Build(data, s, receipt: s.Paper == PaperKind.Receipt80mm);
        var viewer = new FlowDocumentScrollViewer { Document = doc };
        new Window
        {
            Title = $"پیش‌نمایش چاپ — {data.DocTitle} {data.Number}",
            Content = viewer, Width = 820, Height = 900,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            FlowDirection = FlowDirection.RightToLeft
        }.Show();
    }

    // ── send to printer ──
    private static void Send(FlowDocument doc, PrintSettings s)
    {
        var pd = new PrintDialog();
        if (!string.IsNullOrWhiteSpace(s.PrinterName))
        {
            try
            {
                var server = new LocalPrintServer();
                var queue = server.GetPrintQueues().FirstOrDefault(q => q.Name == s.PrinterName);
                if (queue != null) pd.PrintQueue = queue;
            }
            catch { /* fall back to default */ }
        }

        if (s.ShowDialog && pd.ShowDialog() != true) return;

        pd.PrintTicket.CopyCount = Math.Max(1, s.Copies);
        IDocumentPaginatorSource idp = doc;
        for (int i = 0; i < Math.Max(1, s.Copies); i++)
            pd.PrintDocument(idp.DocumentPaginator, doc.Name ?? "SamaHesab");
    }

    // ── document builder (public-ish via internal) ──
    public FlowDocument Build(PrintDocumentData d, PrintSettings s, bool receipt)
    {
        var doc = new FlowDocument
        {
            FlowDirection = FlowDirection.RightToLeft,
            FontFamily = Vazir,
            PagePadding = new Thickness(receipt ? 8 : 36),
            ColumnWidth = double.MaxValue,
            Background = Brushes.White,
            Foreground = Brushes.Black,
            FontSize = receipt ? 11 : 12
        };
        if (receipt) { doc.PageWidth = 300; }   // ~80mm thermal
        else if (s.Paper == PaperKind.A5) { doc.PageWidth = 559; doc.PageHeight = 794; }
        else { doc.PageWidth = 794; doc.PageHeight = 1123; } // A4 @96dpi

        // header
        doc.Blocks.Add(new Paragraph(new Run(s.HeaderTitle))
        { FontSize = receipt ? 15 : 20, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0) });
        if (!string.IsNullOrWhiteSpace(s.HeaderLine2))
            doc.Blocks.Add(Center(s.HeaderLine2!, receipt ? 9 : 11, Brushes.Gray));
        var contact = string.Join("  |  ", new[] { s.Phone, s.Address }.Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(contact)) doc.Blocks.Add(Center(contact, receipt ? 8 : 10, Brushes.Gray));

        doc.Blocks.Add(Rule());

        // doc title + meta
        doc.Blocks.Add(new Paragraph(new Run(d.DocTitle))
        { FontSize = receipt ? 13 : 16, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 2, 0, 4) });
        doc.Blocks.Add(KeyVals(new[]
        {
            ("شماره", d.Number), ("تاریخ", d.Date), (d.PartyLabel, d.PartyName)
        }, receipt));

        // items table
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 6) };
        double[] widths = receipt ? new double[] { 22, 100, 34, 60 } : new double[] { 36, 90, 240, 60, 70, 90 };
        foreach (var w in widths) table.Columns.Add(new TableColumn { Width = new GridLength(w, GridUnitType.Star) });
        var head = new TableRowGroup();
        var hr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)) };
        string[] headers = receipt
            ? new[] { "#", "کالا", "تعداد", "مبلغ" }
            : new[] { "ردیف", "کد", "نام کالا", "مقدار", "قیمت", "مبلغ خالص" };
        foreach (var h in headers) hr.Cells.Add(HeadCell(h));
        head.Rows.Add(hr);
        table.RowGroups.Add(head);

        var body = new TableRowGroup();
        foreach (var ln in d.Lines)
        {
            var r = new TableRow();
            if (receipt)
            {
                r.Cells.Add(Cell(ln.Row.ToString()));
                r.Cells.Add(Cell(ln.Name));
                r.Cells.Add(Cell(ln.Qty.ToString("#,##0.##")));
                r.Cells.Add(Cell(Money(ln.Net), TextAlignment.Left));
            }
            else
            {
                r.Cells.Add(Cell(ln.Row.ToString()));
                r.Cells.Add(Cell(ln.Code));
                r.Cells.Add(Cell(ln.Name));
                r.Cells.Add(Cell(ln.Qty.ToString("#,##0.##")));
                r.Cells.Add(Cell(Money(ln.UnitPrice), TextAlignment.Left));
                r.Cells.Add(Cell(Money(ln.Net), TextAlignment.Left));
            }
            body.Rows.Add(r);
        }
        table.RowGroups.Add(body);
        doc.Blocks.Add(table);

        doc.Blocks.Add(Rule());

        // totals
        doc.Blocks.Add(KeyVals(new[]
        {
            ("جمع کل", Money(d.SubTotal)),
            ("تخفیف", Money(d.Discount)),
            ("مالیات", Money(d.Tax)),
            ("هزینه حمل", Money(d.Shipping)),
        }, receipt));
        doc.Blocks.Add(new Paragraph(new Run($"مبلغ قابل پرداخت: {Money(d.GrandTotal)} ریال"))
        { FontWeight = FontWeights.Bold, FontSize = receipt ? 13 : 15, TextAlignment = TextAlignment.Left, Margin = new Thickness(0, 4, 0, 2) });
        doc.Blocks.Add(KeyVals(new[] { ("پرداختی", Money(d.Paid)), ("مانده", Money(d.Remain)) }, receipt));

        if (!string.IsNullOrWhiteSpace(d.Description))
            doc.Blocks.Add(new Paragraph(new Run("توضیحات: " + d.Description)) { FontSize = receipt ? 9 : 11, Foreground = Brushes.Gray });

        doc.Blocks.Add(Rule());
        if (!string.IsNullOrWhiteSpace(s.FooterText))
            doc.Blocks.Add(Center(s.FooterText, receipt ? 9 : 11, Brushes.Gray));

        return doc;
    }

    // ── helpers ──
    private static Paragraph Center(string text, double size, Brush fg) =>
        new(new Run(text)) { FontSize = size, Foreground = fg, TextAlignment = TextAlignment.Center, Margin = new Thickness(0) };

    private static Block Rule() => new Paragraph { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 2, 0, 2), Padding = new Thickness(0) };

    private static TableCell HeadCell(string text) => new(new Paragraph(new Run(text))
    { Foreground = Brushes.White, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, Margin = new Thickness(0) })
    { Padding = new Thickness(3) };

    private static TableCell Cell(string text, TextAlignment align = TextAlignment.Right) =>
        new(new Paragraph(new Run(text)) { TextAlignment = align, Margin = new Thickness(0) })
        { Padding = new Thickness(3, 2, 3, 2), BorderBrush = Brushes.Gainsboro, BorderThickness = new Thickness(0, 0, 0, 0.5) };

    private static Paragraph KeyVals((string k, string v)[] pairs, bool receipt)
    {
        var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1), FontSize = receipt ? 10 : 12 };
        bool first = true;
        foreach (var (k, v) in pairs)
        {
            if (!first) p.Inlines.Add(new Run("     "));
            first = false;
            p.Inlines.Add(new Run($"{k}: ") { Foreground = Brushes.Gray });
            p.Inlines.Add(new Run(v) { FontWeight = FontWeights.SemiBold });
        }
        return p;
    }
}
