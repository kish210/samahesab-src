using SamaHesab.Application.Inventory.Commands;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>قفلِ رگرسیونِ باگِ تاریخیِ «انبارِ مقصدِ صفر در انتقالِ سریع» — تأییدشده با تستِ زندهٔ
/// رویِ DBِ واقعیِ چندانباره در @2026-07-16 (todo.rm #8).</summary>
public class TransferStockCommandValidatorTests
{
    private readonly TransferStockCommandValidator _validator = new();

    [Fact]
    public void Rejects_When_ToWarehouseId_Is_Zero()
    {
        var cmd = new TransferStockCommand(FromWarehouseId: 1, ToWarehouseId: 0, ProductId: 1, Quantity: 1, Date: "1405/01/01", Description: null);
        var result = _validator.Validate(cmd);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_When_FromWarehouseId_Is_Zero()
    {
        var cmd = new TransferStockCommand(FromWarehouseId: 0, ToWarehouseId: 2, ProductId: 1, Quantity: 1, Date: "1405/01/01", Description: null);
        var result = _validator.Validate(cmd);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_When_Source_And_Destination_Are_Same()
    {
        var cmd = new TransferStockCommand(FromWarehouseId: 1, ToWarehouseId: 1, ProductId: 1, Quantity: 1, Date: "1405/01/01", Description: null);
        var result = _validator.Validate(cmd);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Accepts_Valid_Transfer()
    {
        var cmd = new TransferStockCommand(FromWarehouseId: 1, ToWarehouseId: 2, ProductId: 1, Quantity: 5, Date: "1405/01/01", Description: "تست");
        var result = _validator.Validate(cmd);
        Assert.True(result.IsValid);
    }
}
