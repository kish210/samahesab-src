using SamaHesab.Modules.Tourism.Application.Commands;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>TUR-C2-6 — اعتبارسنجیِ ساختِ محصولِ گردشگری.</summary>
public class SaveTourismProductValidatorTests
{
    private readonly SaveTourismProductCommandValidator _v = new();

    private static SaveTourismProductCommand Make(string name, int supplierId, decimal buy = 0, decimal sell = 0)
        => new(0, name, supplierId, buy, sell, null, false, true);

    [Fact] public void Rejects_Empty_Name() => Assert.False(_v.Validate(Make("", 5)).IsValid);
    [Fact] public void Rejects_Short_Name() => Assert.False(_v.Validate(Make("ا", 5)).IsValid);
    [Fact] public void Rejects_No_Supplier() => Assert.False(_v.Validate(Make("گشتِ جزیره", 0)).IsValid);
    [Fact] public void Accepts_Valid() => Assert.True(_v.Validate(Make("گشتِ جزیره", 5, 7_000_000, 10_000_000)).IsValid);
}
