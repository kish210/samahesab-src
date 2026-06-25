using SamaHesab.Modules.Tourism.Application;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>SP-1 — تشخیصِ خودکارِ فروشنده در فروشِ گردشگری (پنلِ فروشنده‌محور).</summary>
public class SellerResolverTests
{
    [Fact]
    public void Mapped_NonAdmin_User_Is_Forced_To_Own_Identity()
        // کاربرِ فروشنده (Party 42)، غیرادمین — حتی اگر درخواست فروشنده‌ی دیگری (99) بفرستد، 42 می‌ماند.
        => Assert.Equal(42, SellerResolver.Resolve(isAdmin: false, mappedSellerPartyId: 42, requestedSellerPartyId: 99));

    [Fact]
    public void Admin_Keeps_Requested_Seller()
        // ادمین می‌تواند به‌جای هر فروشنده ثبت کند حتی اگر خودش نگاشت داشته باشد.
        => Assert.Equal(99, SellerResolver.Resolve(isAdmin: true, mappedSellerPartyId: 42, requestedSellerPartyId: 99));

    [Fact]
    public void Unmapped_User_Uses_Requested_Seller()
        // کاربرِ بدونِ نگاشت (مثلِ اپراتورِ دفتر) دستی انتخاب می‌کند.
        => Assert.Equal(7, SellerResolver.Resolve(isAdmin: false, mappedSellerPartyId: null, requestedSellerPartyId: 7));

    [Fact]
    public void Mapped_NonAdmin_Ignores_Empty_Request()
        // پنلِ فروشنده‌محور حتی فروشنده نمی‌فرستد (0) — باز هم هویتِ خودش اعمال می‌شود.
        => Assert.Equal(42, SellerResolver.Resolve(isAdmin: false, mappedSellerPartyId: 42, requestedSellerPartyId: 0));
}
