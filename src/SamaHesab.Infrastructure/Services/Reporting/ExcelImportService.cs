using ClosedXML.Excel;
using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.Infrastructure.Services.Reporting;

/// <summary>فاز ۱۲ G4 — خواندنِ .xlsx با ClosedXML (همان کتابخانهٔ Export).</summary>
public class ExcelImportService : IExcelImportService
{
    public IReadOnlyList<IReadOnlyDictionary<string, string>> ReadRows(string filePath)
    {
        var result = new List<IReadOnlyDictionary<string, string>>();
        using var wb = new XLWorkbook(filePath);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws is null) return result;

        var range = ws.RangeUsed();
        if (range is null) return result;

        var rows = range.RowsUsed().ToList();
        if (rows.Count < 2) return result;   // فقط سرستون یا خالی

        // سرستون‌ها (ستون → نامِ trim‌شده)
        var headers = new Dictionary<int, string>();
        var headerRow = rows[0];
        foreach (var cell in headerRow.Cells())
        {
            var name = cell.GetString().Trim();
            if (!string.IsNullOrWhiteSpace(name)) headers[cell.Address.ColumnNumber] = name;
        }
        if (headers.Count == 0) return result;

        foreach (var row in rows.Skip(1))
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool any = false;
            foreach (var (col, name) in headers)
            {
                var val = row.Cell(col).GetString().Trim();
                dict[name] = val;
                if (!string.IsNullOrWhiteSpace(val)) any = true;
            }
            if (any) result.Add(dict);   // سطرِ کاملاً خالی نادیده
        }
        return result;
    }
}
