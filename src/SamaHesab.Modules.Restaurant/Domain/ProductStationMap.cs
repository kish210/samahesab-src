using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Restaurant.Domain;

/// <summary>
/// نگاشتِ یک آیتمِ منو (کالا) به ایستگاهِ چاپ. هر کالا حداکثر یک نگاشت دارد؛ کالای بدونِ نگاشت
/// به ایستگاهِ پیش‌فرض می‌رود. (در schemaی Rst نگه‌داری می‌شود — هسته دست‌نخورده.)
/// </summary>
public class ProductStationMap : AuditableEntity
{
    public int ProductId { get; private set; }
    public int StationId { get; private set; }

    private ProductStationMap() { }

    public static ProductStationMap Create(int companyId, int productId, int stationId)
    {
        if (productId <= 0) throw new ArgumentException("کالا الزامی است.");
        if (stationId <= 0) throw new ArgumentException("ایستگاه الزامی است.");
        return new ProductStationMap { CompanyId = companyId, ProductId = productId, StationId = stationId };
    }

    public void Reassign(int stationId, int? userId = null)
    {
        if (stationId <= 0) throw new ArgumentException("ایستگاه الزامی است.");
        StationId = stationId;
        SetAudit(userId);
    }
}
