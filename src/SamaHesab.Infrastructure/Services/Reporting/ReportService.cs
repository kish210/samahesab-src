using Microsoft.Extensions.Logging;
using SamaHesab.Application.Common.Interfaces;
using ClosedXML.Excel;

namespace SamaHesab.Infrastructure.Services.Reporting;

public class ReportService : IReportService
{
    private readonly ILogger<ReportService> _logger;
    public ReportService(ILogger<ReportService> logger) => _logger = logger;

    public Task<byte[]> GeneratePdfAsync(string reportName, object data, CancellationToken ct = default)
    {
        // Simple HTML-based PDF placeholder — in production use SSRS or FastReport
        var html = BuildHtmlReport(reportName, data);
        var bytes = System.Text.Encoding.UTF8.GetBytes(html);
        _logger.LogInformation("PDF generated for report: {ReportName}", reportName);
        return Task.FromResult(bytes);
    }

    public Task<byte[]> GenerateExcelAsync(string reportName, object data, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook();
        var sheetName = reportName.Length > 31 ? reportName[..31] : reportName;
        var ws = workbook.Worksheets.Add(sheetName);

        // Title row
        ws.Cell(1, 1).Value = reportName;
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 5).Merge();

        // Headers
        var headers = new[] { "کد", "شرح", "بدهکار", "بستانکار", "مانده" };
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(2, c + 1).Value = headers[c];
            ws.Cell(2, c + 1).Style.Font.Bold = true;
            ws.Cell(2, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#2563EB");
            ws.Cell(2, c + 1).Style.Font.FontColor = XLColor.White;
        }

        // Data rows from IEnumerable
        if (data is System.Collections.IEnumerable rows)
        {
            int row = 3;
            foreach (var item in rows)
            {
                var props = item.GetType().GetProperties();
                for (int c = 0; c < Math.Min(props.Length, 5); c++)
                    ws.Cell(row, c + 1).Value = props[c].GetValue(item)?.ToString();
                row++;
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> GenerateWordAsync(string reportName, object data, CancellationToken ct = default)
    {
        var sb = new System.Text.StringBuilder();

        // RTF document — opens natively in Microsoft Word
        sb.Append(@"{\rtf1\ansi\ansicpg1256\deff0 ");
        sb.Append(@"{\fonttbl{\f0\fnil\fcharset178 Tahoma;}}");
        sb.Append(@"{\colortbl ;\red37\green99\blue235;}");
        sb.Append(@"\deflang1065\widowctrl ");

        // Title
        sb.Append(@"\pard\rtlpar\qc\sb200\sa200\b\f0\fs28 ");
        sb.Append(ToRtf(reportName));
        sb.Append(@"\b0\par ");

        // Column headers
        sb.Append(@"\pard\rtlpar\sb100\sa100\b\cf1 ");
        foreach (var header in new[] { "کد", "شرح", "بدهکار", "بستانکار", "مانده" })
            sb.Append(ToRtf(header) + @"\tab ");
        sb.Append(@"\b0\par ");

        // Data rows
        if (data is System.Collections.IEnumerable rows)
        {
            foreach (var item in rows)
            {
                var props = item.GetType().GetProperties();
                sb.Append(@"\pard\rtlpar ");
                foreach (var p in props.Take(5))
                    sb.Append(ToRtf(p.GetValue(item)?.ToString() ?? string.Empty) + @"\tab ");
                sb.Append(@"\par ");
            }
        }

        sb.Append('}');

        var bytes = System.Text.Encoding.ASCII.GetBytes(sb.ToString());
        _logger.LogInformation("Word document generated for report: {ReportName}", reportName);
        return Task.FromResult(bytes);
    }

    private static string ToRtf(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char c in text)
        {
            if (c == '\\') sb.Append(@"\\");
            else if (c == '{') sb.Append(@"\{");
            else if (c == '}') sb.Append(@"\}");
            else if (c > 127) sb.Append($@"\u{(int)c}?");
            else sb.Append(c);
        }
        return sb.ToString();
    }

    public async Task PrintAsync(string reportName, object data, string? printerName = null, CancellationToken ct = default)
    {
        var bytes = await GeneratePdfAsync(reportName, data, ct);
        var tmpFile = Path.Combine(Path.GetTempPath(), $"SamaHesab_{Guid.NewGuid():N}.html");
        await File.WriteAllBytesAsync(tmpFile, bytes, ct);
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = tmpFile,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
    }

    private static string BuildHtmlReport(string title, object data)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<html dir='rtl'><head><meta charset='utf-8'/>");
        sb.AppendLine("<style>body{font-family:Tahoma;direction:rtl;}table{width:100%;border-collapse:collapse;}");
        sb.AppendLine("th{background:#2563EB;color:white;padding:8px;}td{border:1px solid #ddd;padding:6px;}</style></head><body>");
        sb.AppendLine($"<h2>{title}</h2>");
        sb.AppendLine("<table><tr><th>کد</th><th>شرح</th><th>بدهکار</th><th>بستانکار</th><th>مانده</th></tr>");

        if (data is System.Collections.IEnumerable rows)
        {
            foreach (var item in rows)
            {
                var props = item.GetType().GetProperties();
                sb.Append("<tr>");
                foreach (var p in props.Take(5))
                    sb.Append($"<td>{p.GetValue(item)}</td>");
                sb.AppendLine("</tr>");
            }
        }

        sb.AppendLine("</table></body></html>");
        return sb.ToString();
    }
}
