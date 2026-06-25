namespace SamaHesab.Modules.Tourism.Application;

/// <summary>
/// SP-1 — قاعدهٔ خالصِ تشخیصِ فروشنده در فروشِ گردشگری (پنلِ فروشنده‌محور).
/// </summary>
public static class SellerResolver
{
    /// <summary>
    /// اگر کاربر به یک «فروشنده» نگاشته شده و ADMIN نیست → همان فروشنده (قفل؛ مقدارِ ارسالی نادیده).
    /// ادمین/مدیر یا کاربرِ بدونِ نگاشت → فروشنده‌ی درخواست‌شده (انتخابِ دستی).
    /// </summary>
    public static int Resolve(bool isAdmin, int? mappedSellerPartyId, int requestedSellerPartyId)
        => (!isAdmin && mappedSellerPartyId is > 0) ? mappedSellerPartyId.Value : requestedSellerPartyId;
}
