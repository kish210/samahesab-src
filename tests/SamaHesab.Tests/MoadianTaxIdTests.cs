using System;
using System.Text.RegularExpressions;
using SamaHesab.Application.Common;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>🇮🇷 POS-IR-4 — ساختارِ شمارهٔ منحصربه‌فردِ مالیاتیِ مودیان (۲۲ نویسهٔ Base36).</summary>
public class MoadianTaxIdTests
{
    private static readonly DateTime D = new(2026, 6, 17, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Id_Is_22_Base36_Chars()
    {
        var id = MoadianTaxId.Generate("AB12CD", D, 12345);
        Assert.Equal(22, id.Length);
        Assert.Matches("^[0-9A-Z]{22}$", id);
    }

    [Fact]
    public void Is_Deterministic()
        => Assert.Equal(MoadianTaxId.Generate("AB12CD", D, 7), MoadianTaxId.Generate("ab12cd", D, 7));

    [Fact]
    public void MemoryId_Occupies_First_Six()
        => Assert.StartsWith("AB12CD", MoadianTaxId.Generate("AB12CD", D, 1));

    [Fact]
    public void Short_MemoryId_Is_Left_Padded()
        => Assert.StartsWith("0000X9", MoadianTaxId.Generate("X9", D, 1));   // شناسهٔ ۲نویسه‌ای → چپ‌پُر تا ۶

    [Fact]
    public void Different_Serial_Changes_Tail()
        => Assert.NotEqual(MoadianTaxId.Generate("AB12CD", D, 1), MoadianTaxId.Generate("AB12CD", D, 2));

    [Fact]
    public void Qr_Payload_Is_The_TaxId()
    {
        var id = MoadianTaxId.Generate("AB12CD", D, 1);
        Assert.Equal(id, MoadianTaxId.QrPayload(id));
    }
}
