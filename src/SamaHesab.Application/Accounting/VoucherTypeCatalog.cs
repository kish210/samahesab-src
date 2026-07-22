namespace SamaHesab.Application.Accounting;

/// <summary>منبعِ واحدِ نامِ نوعِ سند — پیش‌تر این دیکشنری فقط داخلِ `GetVouchersQueryHandler` تکرار شده بود.</summary>
public static class VoucherTypeCatalog
{
    public static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string> {
        {1,"افتتاحیه"},{2,"اختتامیه"},{3,"فروش"},{4,"خرید"},{5,"صندوق"},{6,"بانک"},
        {7,"چک"},{9,"عمومی"},{10,"پرداخت"},{11,"دریافت"},{12,"حقوق"} };
}
