using ClosedXML.Excel;
using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.Infrastructure.Services.Reporting;

public class ExcelExportService : IExcelExportService
{
    public byte[] Export(string sheetName, IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<object?>> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName);
        ws.RightToLeft = true;

        // header row
        for (int c = 0; c < headers.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int r = 2;
        foreach (var row in rows)
        {
            for (int c = 0; c < row.Count; c++)
            {
                var v = row[c];
                var cell = ws.Cell(r, c + 1);
                switch (v)
                {
                    case null: break;
                    case decimal dec: cell.Value = dec; cell.Style.NumberFormat.Format = "#,##0"; break;
                    case double dbl: cell.Value = dbl; cell.Style.NumberFormat.Format = "#,##0"; break;
                    case int i: cell.Value = i; break;
                    case bool b: cell.Value = b ? "بله" : "خیر"; break;
                    case DateTime dt: cell.Value = dt; break;
                    default: cell.Value = v.ToString(); break;
                }
            }
            r++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
