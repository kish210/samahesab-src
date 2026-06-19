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
    private readonly SamaHesab.Application.Common.Interfaces.IPersianCalendarService? _calendar;
    public PrintService(SamaHesab.Application.Common.Interfaces.IPersianCalendarService? calendar = null)
        => _calendar = calendar;

    private static FontFamily Vazir =>
        (FontFamily?)System.Windows.Application.Current.TryFindResource("VazirFont") ?? new FontFamily("Tahoma");

    /// <summary>ارقامِ لاتین → فارسی (برای چاپ/پیش‌نمایشِ سند).</summary>
    private static string Fa(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) sb.Append(c >= '0' && c <= '9' ? (char)('۰' + (c - '0')) : c);
        return sb.ToString();
    }

    private static string Money(decimal v) => Fa(v.ToString("#,##0", CultureInfo.InvariantCulture));

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

        var win = new Window
        {
            Title = $"پیش‌نمایش چاپ — {data.DocTitle} {data.Number}",
            Width = 820, Height = 900,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            FlowDirection = FlowDirection.RightToLeft,
            FontFamily = Vazir
        };

        // نوارِ ابزار: چاپ + بستن (تا کاربر بتواند پنجره را ببندد)
        var bar = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Background = System.Windows.Media.Brushes.WhiteSmoke,
            Margin = new Thickness(0)
        };
        var printBtn = new System.Windows.Controls.Button { Content = "🖨 چاپ", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(8, 6, 4, 6), Cursor = System.Windows.Input.Cursors.Hand };
        printBtn.Click += (_, _) => { try { Send(Build(data, s, receipt: s.Paper == PaperKind.Receipt80mm), s); } catch { } };
        var closeBtn = new System.Windows.Controls.Button { Content = "✕ بستن", Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(4, 6, 8, 6), Cursor = System.Windows.Input.Cursors.Hand };
        closeBtn.Click += (_, _) => win.Close();
        bar.Children.Add(printBtn);
        bar.Children.Add(closeBtn);

        var dock = new System.Windows.Controls.DockPanel();
        System.Windows.Controls.DockPanel.SetDock(bar, System.Windows.Controls.Dock.Top);
        dock.Children.Add(bar);
        dock.Children.Add(viewer);
        win.Content = dock;

        // Esc هم ببندد
        win.InputBindings.Add(new System.Windows.Input.KeyBinding(System.Windows.Input.ApplicationCommands.Close, System.Windows.Input.Key.Escape, System.Windows.Input.ModifierKeys.None));
        win.CommandBindings.Add(new System.Windows.Input.CommandBinding(System.Windows.Input.ApplicationCommands.Close, (_, _) => win.Close()));
        win.Show();
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
            Background = Brushes.White,
            Foreground = Brushes.Black,
            FontSize = receipt ? 11 : 12
        };
        if (receipt) { doc.PageWidth = 300; }   // ~80mm thermal
        else if (s.Paper == PaperKind.A5) { doc.PageWidth = 559; doc.PageHeight = 794; }
        else { doc.PageWidth = 794; doc.PageHeight = 1123; } // A4 @96dpi

        // عرضِ ستونِ متن = عرضِ مفیدِ صفحه (تک‌ستونی). نبودِ این مقدار → جدولِ Star به‌هم می‌ریزد.
        double pad = receipt ? 8 : 36;
        double contentWidth = doc.PageWidth - 2 * pad;
        doc.ColumnWidth = contentWidth;

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
            ("شماره", Fa(d.Number)), ("تاریخ", Fa(d.Date)), (d.PartyLabel, d.PartyName)
        }, receipt));

        // items table
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0, 6, 0, 6) };
        double[] widths = receipt ? new double[] { 22, 100, 34, 60 } : new double[] { 30, 70, 200, 48, 80, 66, 86 };
        double wsum = widths.Sum();
        // پیکسلِ قطعی (نسبت‌ها روی عرضِ مفیدِ صفحه) — قابل‌اعتمادتر از Star در FlowDocument.
        foreach (var w in widths) table.Columns.Add(new TableColumn { Width = new GridLength(w / wsum * contentWidth, GridUnitType.Pixel) });
        var head = new TableRowGroup();
        var hr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x5F)) };
        string[] headers = receipt
            ? new[] { "#", "کالا", "تعداد", "مبلغ" }
            : new[] { "ردیف", "کد", "نام کالا", "تعداد", "قیمت واحد", "تخفیف", "مبلغ" };
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
                r.Cells.Add(Cell(Fa(ln.Qty.ToString("#,##0.##"))));
                r.Cells.Add(Cell(Money(ln.Net), TextAlignment.Left));
            }
            else
            {
                r.Cells.Add(Cell(Fa(ln.Row.ToString())));
                r.Cells.Add(Cell(ln.Code));
                r.Cells.Add(Cell(ln.Name));
                r.Cells.Add(Cell(Fa(ln.Qty.ToString("#,##0.##"))));
                r.Cells.Add(Cell(Money(ln.UnitPrice), TextAlignment.Left));
                r.Cells.Add(Cell(Money(ln.Discount), TextAlignment.Left));
                r.Cells.Add(Cell(Money(ln.Net), TextAlignment.Left));
            }
            body.Rows.Add(r);
        }
        table.RowGroups.Add(body);

        // ردیفِ جمعِ جدول (تعدادِ کل، تخفیفِ کل، مبلغِ کل) — فقط A4
        if (!receipt && d.Lines.Count > 0)
        {
            TableCell B(string t, TextAlignment a = TextAlignment.Right) { var c = Cell(t, a); c.FontWeight = FontWeights.Bold; return c; }
            var foot = new TableRowGroup();
            var fr = new TableRow { Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF7)) };
            var lbl = B($"جمع ({Fa(d.Lines.Count.ToString())} ردیف)", TextAlignment.Center);
            lbl.ColumnSpan = 3;
            fr.Cells.Add(lbl);
            fr.Cells.Add(B(Fa(d.Lines.Sum(x => x.Qty).ToString("#,##0.##"))));
            fr.Cells.Add(B("", TextAlignment.Left));
            fr.Cells.Add(B(Money(d.Lines.Sum(x => x.Discount)), TextAlignment.Left));
            fr.Cells.Add(B(Money(d.Lines.Sum(x => x.Net)), TextAlignment.Left));
            foot.Rows.Add(fr);
            table.RowGroups.Add(foot);
        }

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

        // مبلغ به حروف (در صورتِ دسترسی به سرویسِ تبدیل)
        var words = _calendar?.NumberToWords(decimal.Truncate(d.GrandTotal));
        if (!string.IsNullOrWhiteSpace(words))
            doc.Blocks.Add(new Paragraph(new Run($"به حروف: {words} ریال"))
            { FontSize = receipt ? 9 : 11, Foreground = Brushes.DimGray, Margin = new Thickness(0, 0, 0, 2) });

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
