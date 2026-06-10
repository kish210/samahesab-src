using SamaHesab.Domain.Entities.Settings;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>آیتم‌های اخیر/سنجاق‌شده‌ی کاربر (Favorites/Recent/Pinned) — منطق دامنه.</summary>
public class UserItemRefTests
{
    private static UserItemRef New() => UserItemRef.Create(1, 7, "Customer", 42, "مشتری نمونه");

    [Fact]
    public void Create_Starts_With_UseCount_One()
    {
        var r = New();
        Assert.Equal(1, r.UseCount);
        Assert.False(r.Pinned);
        Assert.Equal("Customer", r.EntityType);
        Assert.Equal(42, r.EntityId);
    }

    [Fact]
    public void Touch_Increments_UseCount_And_Updates_Label()
    {
        var r = New();
        r.Touch();
        r.Touch("نام تازه");
        Assert.Equal(3, r.UseCount);
        Assert.Equal("نام تازه", r.Label);
    }

    [Fact]
    public void SetPinned_Toggles_Pin()
    {
        var r = New();
        r.SetPinned(true);
        Assert.True(r.Pinned);
        r.SetPinned(false);
        Assert.False(r.Pinned);
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Product", 0)]
    public void Create_Validates_Inputs(string type, int id)
        => Assert.Throws<ArgumentException>(() => UserItemRef.Create(1, 7, type, id, "x"));
}
