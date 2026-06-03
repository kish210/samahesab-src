namespace SamaHesab.Application.Common.Interfaces;

/// <summary>
/// Reusable tabular export to Excel (.xlsx). Lives in the Application layer so the
/// desktop, API and future web client all produce identical exports.
/// </summary>
public interface IExcelExportService
{
    byte[] Export(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows);
}
