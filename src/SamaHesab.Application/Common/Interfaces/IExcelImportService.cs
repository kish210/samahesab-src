namespace SamaHesab.Application.Common.Interfaces;

/// <summary>
/// فاز ۱۲ G4 — خواندنِ جدولیِ فایلِ اکسل (.xlsx) برای مهاجرتِ داده.
/// سطرِ اولِ غیرخالی = سرستون؛ هر سطر → دیکشنریِ «سرستون → متنِ سلول» (case-insensitive).
/// در لایهٔ Application تعریف می‌شود تا پیاده‌سازی (ClosedXML) در Infrastructure بماند.
/// </summary>
public interface IExcelImportService
{
    IReadOnlyList<IReadOnlyDictionary<string, string>> ReadRows(string filePath);
}
