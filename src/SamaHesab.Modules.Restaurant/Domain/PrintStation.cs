using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Restaurant.Domain;

/// <summary>
/// ایستگاهِ چاپِ رستوران = یک فیش‌پرینتر برای یک بخش (آشپزخانه/سالادبار/پنتری/بار).
/// هر آیتمِ منو (کالا) از طریقِ <see cref="ProductStationMap"/> به یک ایستگاه نگاشته می‌شود؛
/// هنگامِ ارسالِ سفارش، تیکتِ هر ایستگاه به پرینترِ همان ایستگاه چاپ می‌شود.
/// ایستگاهِ پیش‌فرض، آیتم‌های بدونِ نگاشت را می‌گیرد.
/// </summary>
public class PrintStation : AuditableEntity
{
    public string Name { get; private set; } = default!;          // مثلِ «آشپزخانه»، «سالادبار»
    public string PrinterName { get; private set; } = default!;   // نامِ پرینترِ ویندوز (خالی = پرینترِ پیش‌فرضِ سیستم)
    public bool IsDefault { get; private set; }                   // ایستگاهِ پیش‌فرض برای آیتمِ بدونِ نگاشت
    public bool Active { get; private set; } = true;

    private PrintStation() { }

    public static PrintStation Create(int companyId, string name, string? printerName, bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ ایستگاه الزامی است.");
        return new PrintStation
        {
            CompanyId = companyId, Name = name.Trim(),
            PrinterName = (printerName ?? "").Trim(), IsDefault = isDefault
        };
    }

    public void Update(string name, string? printerName, bool isDefault, bool active, int? userId = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
        PrinterName = (printerName ?? "").Trim();
        IsDefault = isDefault;
        Active = active;
        SetAudit(userId);
    }

    /// <summary>هنگامِ تعیینِ یک ایستگاهِ پیش‌فرضِ جدید، پیش‌فرضِ بودنِ این یکی برداشته شود.</summary>
    public void ClearDefault() { IsDefault = false; SetAudit(null); }
}
