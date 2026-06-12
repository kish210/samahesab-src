using SamaHesab.Domain.Entities.Inventory;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>منطق موجودیِ بچ — افزایش/کاهش (INV-1 گام۳).</summary>
public class BatchQuantityTests
{
    private static Batch NewBatch(decimal qty) =>
        Batch.Create(productId: 1, batchNumber: "B-001", quantity: qty);

    [Fact]
    public void Increase_Adds_To_Quantity()
    {
        var b = NewBatch(10);
        b.Increase(5);
        Assert.Equal(15, b.Quantity);
    }

    [Fact]
    public void Decrease_Subtracts_From_Quantity()
    {
        var b = NewBatch(10);
        b.Decrease(4);
        Assert.Equal(6, b.Quantity);
    }

    [Fact]
    public void Decrease_Beyond_Quantity_Throws()
    {
        var b = NewBatch(3);
        Assert.Throws<System.InvalidOperationException>(() => b.Decrease(5));
    }

    [Fact]
    public void Negative_Amounts_Throw()
    {
        var b = NewBatch(10);
        Assert.Throws<System.ArgumentException>(() => b.Increase(-1));
        Assert.Throws<System.ArgumentException>(() => b.Decrease(-1));
    }

    [Fact]
    public void IsExpired_Compares_Shamsi_Dates()
    {
        var expired = Batch.Create(1, "B-X", expiryDate: "1404/01/01", quantity: 1);
        var fresh = Batch.Create(1, "B-Y", expiryDate: "1404/12/29", quantity: 1);
        Assert.True(expired.IsExpired("1404/06/01"));
        Assert.False(fresh.IsExpired("1404/06/01"));
    }
}
